<template>
  <div class="page">
    <van-nav-bar
      :title="navTitle"
      left-arrow
      @click-left="onBack"
    >
      <template #right>
        <div class="nav-actions">
          <van-icon
            :name="form.isPinned ? 'star' : 'star-o'"
            size="20"
            :color="form.isPinned ? '#ff976a' : undefined"
            @click="form.isPinned = !form.isPinned"
          />
          <van-icon name="edit" size="20" class="color-icon" @click="showColorPicker = true" />
          <van-button size="mini" type="primary" :loading="saving" @click="onSave">保存</van-button>
        </div>
      </template>
    </van-nav-bar>

    <div class="editor" :style="editorStyle">
      <van-field
        v-model="form.title"
        placeholder="标题"
        class="title-field"
        :style="{ background: 'transparent' }"
        maxlength="200"
      />

      <div class="meta-row" :style="{ borderTopColor: metaBorderColor, borderColor: metaBorderColor }">
        <van-cell
          title="分类"
          is-link
          :value="selectedCategoryName || '未选择'"
          @click="showCategoryPicker = true"
        />
        <van-cell
          title="标签"
          is-link
          :value="selectedTagNames || '未选择'"
          @click="showTagPicker = true"
        />
      </div>

      <div v-if="!isDesktop" class="toolbar-wrap" :style="{ borderTopColor: metaBorderColor }">
        <van-tabs v-model:active="mode" shrink>
          <van-tab title="编辑" name="edit" />
          <van-tab title="预览" name="preview" />
        </van-tabs>
      </div>

      <div class="editor-body">
        <div
          v-show="isDesktop || mode === 'edit'"
          class="edit-area"
          :class="{ 'drag-over': dragOver }"
          :style="areaStyle"
          @dragover.prevent.stop="onDragOver"
          @dragleave.prevent.stop="onDragLeave"
          @drop.prevent.stop="onDrop"
        >
          <!-- 桌面端：工具栏位于 textarea 上方 -->
          <div v-if="isDesktop" class="toolbar desktop-toolbar">
            <template v-for="(b, bi) in toolbarButtons" :key="'dt-'+bi">
              <van-button
                v-if="!b.divider"
                size="small"
                plain
                :type="b.primary ? 'primary' : undefined"
                @click="b.action"
                :title="b.title"
              >
                <span v-html="b.label" />
              </van-button>
              <div v-else class="toolbar-divider" />
            </template>
            <input
              ref="fileInput"
              type="file"
              accept="image/*"
              style="display: none"
              @change="onFileChange"
            />
          </div>

          <div class="textarea-wrap">
            <textarea
              ref="textareaRef"
              v-model="form.content"
              class="content-area"
              :style="textareaDynamicStyle"
              placeholder="开始记录... 支持 Markdown 语法"
              @select="onTextSelect"
              @mouseup="onTextSelect"
              @touchend="onTouchEnd"
              @focus="onTextareaFocus"
              @scroll="hideFloatingBar"
              @blur="onTextareaBlur"
              @paste.capture="onPaste"
            ></textarea>

            <!-- 字数统计 -->
            <div class="word-count" :style="{ color: textColor }">
              {{ charCount }} 字 · {{ lineCount }} 行
            </div>

            <!-- =============== P1-5 选中文本浮动工具栏 =============== -->
            <transition name="float-fade">
              <div
                v-show="floatingBarVisible"
                class="floating-toolbar"
                :style="floatingBarStyle"
                @mousedown.prevent
                @touchstart.stop.passive
              >
                <button class="ft-btn" type="button" title="粗体 (Ctrl+B)" @click="applyFloat('bold')">
                  <b>B</b>
                </button>
                <button class="ft-btn" type="button" title="斜体 (Ctrl+I)" @click="applyFloat('italic')">
                  <i>I</i>
                </button>
                <button class="ft-btn" type="button" title="删除线" @click="applyFloat('strike')">
                  <s>S</s>
                </button>
                <button class="ft-btn" type="button" title="行内代码" @click="applyFloat('code')">
                  <span style="font-family:monospace;font-size:13px">&lt;/&gt;</span>
                </button>
                <button class="ft-btn" type="button" title="链接 (Ctrl+K)" @click="applyFloat('link')">🔗</button>
              </div>
            </transition>
          </div>
        </div>

        <div v-show="isDesktop || mode === 'preview'" class="preview-area" :style="areaStyle">
          <div v-if="isDesktop" class="preview-label" :style="{ color: subTextColor, borderColor: metaBorderColor }">预览</div>
          <markdown-body :content="form.content || '*暂无内容*'" :style="{ color: textColor }" />
        </div>
      </div>

      <!-- 移动端：工具栏吸底常驻（动态 bottom: 跟随软键盘高度） -->
      <div
        v-if="!isDesktop"
        class="toolbar mobile-toolbar"
        :style="mobileToolbarStyle"
      >
        <div class="mobile-toolbar-scroll">
          <template v-for="(b, bi) in toolbarButtons" :key="'mt-'+bi">
            <van-button
              v-if="!b.divider"
              size="small"
              plain
              :type="b.primary ? 'primary' : undefined"
              @click="b.action"
            >
              <span v-html="b.label" />
            </van-button>
          </template>
          <input
            ref="mobileFileInput"
            type="file"
            accept="image/*"
            style="display: none"
            @change="onFileChange"
          />
        </div>
      </div>
    </div>

    <!-- 分类选择 -->
    <van-popup v-model:show="showCategoryPicker" position="bottom" round>
      <van-picker
        :columns="categoryColumns"
        @confirm="onCategoryConfirm"
        @cancel="showCategoryPicker = false"
      />
    </van-popup>

    <!-- 标签选择 -->
    <van-popup v-model:show="showTagPicker" position="bottom" round style="height: 60%">
      <div class="tag-picker">
        <van-nav-bar title="选择标签">
          <template #right>
            <van-button size="mini" type="primary" @click="showTagPicker = false">完成</van-button>
          </template>
        </van-nav-bar>
        <div class="tag-list">
          <van-checkbox-group v-model="form.tagIds">
            <van-cell
              v-for="t in tags"
              :key="t.id"
              :title="t.name"
              :label="`包含 ${t.noteCount} 篇笔记`"
              clickable
              @click="toggleTag(t.id)"
            >
              <template #right-icon>
                <van-checkbox shape="square" :name="t.id" />
              </template>
            </van-cell>
          </van-checkbox-group>
          <div v-if="tags.length === 0" class="empty-tags">
            <van-empty description="还没有标签" image-size="80">
              <van-button size="small" type="primary" plain @click="$router.push('/tags')">去创建</van-button>
            </van-empty>
          </div>
        </div>
      </div>
    </van-popup>

    <!-- 颜色选择 -->
    <ColorPicker
      v-model:show="showColorPicker"
      v-model="form.backgroundColor"
    />
  </div>
</template>

<script setup>
import { ref, reactive, computed, onMounted, onUnmounted, nextTick, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { showToast, showConfirmDialog } from 'vant'
import http from '../api/http'
import MarkdownBody from '../components/MarkdownBody.vue'
import ColorPicker from '../components/ColorPicker.vue'
import { useResponsive } from '../composables/useResponsive'
import { useAuthStore } from '../stores/auth'
import { resolveNoteColor, getContrastColor, isDarkColor } from '../utils/color'

const route = useRoute()
const router = useRouter()
const { isDesktop } = useResponsive()
const auth = useAuthStore()

const noteId = computed(() => route.params.id)
const isEdit = computed(() => !!noteId.value)

// =============== P1-8: 临时 noteId（新建模式下，上传图片前先建空壳，把 id 暂存在这） ===============
const temporaryNoteId = ref(null)
/** 上传附件 / 正式保存时的"真实 note id"，优先使用临时值（路由还没跳转前） */
const effectiveNoteId = computed(() => temporaryNoteId.value || noteId.value || null)
const isReallyEdit = computed(() => !!effectiveNoteId.value)

const form = reactive({
  title: '',
  content: '',
  categoryId: null,
  tagIds: [],
  backgroundColor: null,
  isPinned: false
})
const categories = ref([])
const tags = ref([])
const mode = ref('edit')
const saving = ref(false)
const showCategoryPicker = ref(false)
const showTagPicker = ref(false)
const showColorPicker = ref(false)
const textareaRef = ref(null)
const fileInput = ref(null)
const mobileFileInput = ref(null)

// ========== P0-1 草稿自动保存 ==========
const draftKey = computed(() => `note_draft/${noteId.value || 'new'}`)
const hasUnsavedChanges = ref(false)

function debounce(fn, wait = 300) {
  let timer = null
  return function (...args) {
    if (timer) clearTimeout(timer)
    timer = setTimeout(() => fn.apply(this, args), wait)
  }
}

function saveDraft() {
  if (!form.title.trim() && !form.content.trim()) {
    try { localStorage.removeItem(draftKey.value) } catch {}
    hasUnsavedChanges.value = false
    updateDocumentTitle()
    return
  }
  const snapshot = JSON.stringify({
    title: form.title,
    content: form.content,
    categoryId: form.categoryId,
    tagIds: [...form.tagIds],
    backgroundColor: form.backgroundColor,
    isPinned: form.isPinned,
    savedAt: Date.now()
  })
  try {
    localStorage.setItem(draftKey.value, snapshot)
    hasUnsavedChanges.value = true
    updateDocumentTitle()
  } catch (e) { /* ignore */ }
}

function clearDraft() {
  try { localStorage.removeItem(draftKey.value) } catch {}
  hasUnsavedChanges.value = false
  updateDocumentTitle()
}

function readDraft() {
  try {
    const raw = localStorage.getItem(draftKey.value)
    if (!raw) return null
    return JSON.parse(raw)
  } catch { return null }
}

function updateDocumentTitle() {
  const base = isEdit.value ? '编辑笔记' : '新建笔记'
  document.title = hasUnsavedChanges.value ? `● ${base}` : base
}

const scheduleSaveDraft = debounce(saveDraft, 2500)

watch(
  [() => form.title, () => form.content, () => form.categoryId, () => form.tagIds, () => form.backgroundColor, () => form.isPinned],
  () => { scheduleSaveDraft() },
  { deep: true }
)

// ========== P0-4 字数统计 ==========
const charCount = computed(() => form.content ? form.content.length : 0)
const lineCount = computed(() => {
  if (!form.content) return 0
  return form.content.split('\n').length
})

// ========== 工具栏按钮 ==========
const toolbarButtons = computed(() => [
  { label: 'H', title: '一级标题', action: () => insert('# ', '', '标题') },
  { label: 'H2', title: '二级标题', action: () => insert('## ', '', '二级标题') },
  { label: 'H3', title: '三级标题', action: () => insert('### ', '', '三级标题') },
  { divider: true },
  { label: '<b>B</b>', title: '粗体', action: () => insert('**', '**', '粗体') },
  { label: '<i>I</i>', title: '斜体', action: () => insert('*', '*', '斜体') },
  { label: '<s>S</s>', title: '删除线', action: () => insert('~~', '~~', '删除线') },
  { label: '<code>&lt;/&gt;</code>', title: '行内代码', action: () => insert('`', '`', 'code') },
  { divider: true },
  { label: '•', title: '无序列表', action: () => insert('- ', '', '列表项') },
  { label: '1.', title: '有序列表', action: () => insert('1. ', '', '列表项') },
  { label: '&quot;', title: '引用', action: () => insert('> ', '', '引用文字') },
  { label: '---', title: '分割线', action: () => insert('\n---\n', '', '') },
  { divider: true },
  { label: '{ }', title: '代码块', action: () => insert('\n```\n', '\n```\n', '代码') },
  { label: '$', title: '行内公式', action: () => insert('$', '$', '公式') },
  { label: '$$', title: '块级公式', action: () => insert('\n$$\n', '\n$$\n', '公式') },
  { label: '链接', title: '链接', action: () => insert('[', '](https://)', '链接文本') },
  { label: '图片', title: '插入图片', primary: true, action: triggerUpload },
  { divider: true },
  { label: '格式化', title: '格式化公式', action: formatFormulas }
])

const categoryColumns = computed(() => [
  { text: '无分类', value: null },
  ...categories.value.map((c) => ({ text: c.name, value: c.id }))
])

const selectedCategoryName = computed(() => {
  const c = categories.value.find((x) => x.id === form.categoryId)
  return c?.name || ''
})

const selectedTagNames = computed(() => {
  const names = tags.value
    .filter((t) => form.tagIds.includes(t.id))
    .map((t) => t.name)
  return names.join('、')
})

const effectiveBg = computed(() => resolveNoteColor(form.backgroundColor, auth.user?.defaultNoteColor))
const textColor = computed(() => getContrastColor(effectiveBg.value))
const subTextColor = computed(() => isDarkColor(effectiveBg.value) ? 'rgba(255,255,255,0.6)' : '#969799')
const metaBorderColor = computed(() => isDarkColor(effectiveBg.value) ? 'rgba(255,255,255,0.15)' : '#ebedf0')

const navTitle = computed(() => (isEdit.value ? '编辑笔记' : '新建笔记'))

const editorStyle = computed(() => ({
  background: effectiveBg.value,
  color: textColor.value
}))

const areaStyle = computed(() => ({
  background: 'transparent',
  color: textColor.value
}))

// =============== P1-6: 软键盘高度 & viewport 适配（移动端） ===============
const keyboardHeight = ref(0)
const dragOver = ref(false)

function onViewportChange() {
  if (!window.visualViewport) return
  // 视口可见高度 vs 真实窗口高度之差 ≈ 键盘高度（额外排除一些安全区误差）
  const kh = Math.max(0, window.innerHeight - window.visualViewport.height - window.visualViewport.offsetTop)
  keyboardHeight.value = kh > 30 ? kh : 0 // 忽略 <30px 的微小差异（例如地址栏收起）
}

// =============== P1-5: 选中文本浮动工具栏 ===============
const floatingBarVisible = ref(false)
const floatingX = ref(0)
const floatingY = ref(0)

function getSelectionRange() {
  const ta = textareaRef.value
  if (!ta) return null
  const s = ta.selectionStart
  const e = ta.selectionEnd
  if (s === e) return null
  return { start: s, end: e }
}

/**
 * 计算 textarea 中某个字符 offset 对应的（相对 textarea-wrap 的）像素位置
 * 用"镜像 div 克隆 + span 插入"法：将 textarea 的样式复刻到一个脱离文档流的 div，
 * 把 [0, offset) 文本放进去，最后塞一个零宽 <span id="marker">，测量其位置。
 */
let _mirror = null
function getCaretPixel(offset) {
  const ta = textareaRef.value
  if (!ta) return { top: 0, left: 0 }
  const wrap = ta.parentElement // .textarea-wrap
  if (!_mirror) {
    _mirror = document.createElement('div')
    const cs = window.getComputedStyle(ta)
    // 复制所有影响排版的样式
    const copyProps = [
      'boxSizing', 'width', 'height', 'overflowX', 'overflowY',
      'borderTopWidth', 'borderRightWidth', 'borderBottomWidth', 'borderLeftWidth',
      'paddingTop', 'paddingRight', 'paddingBottom', 'paddingLeft',
      'fontStyle', 'fontVariant', 'fontWeight', 'fontStretch', 'fontSize',
      'fontSizeAdjust', 'lineHeight', 'fontFamily',
      'textAlign', 'textTransform', 'textIndent', 'textDecoration',
      'letterSpacing', 'wordSpacing', 'tabSize', 'MozTabSize', 'whiteSpace',
      'wordWrap', 'wordBreak'
    ]
    copyProps.forEach(p => { _mirror.style[p] = cs[p] })
    _mirror.style.position = 'absolute'
    _mirror.style.top = '0'
    _mirror.style.left = '-9999px'
    _mirror.style.zIndex = '-1'
    _mirror.style.visibility = 'hidden'
    _mirror.style.whiteSpace = 'pre-wrap'
    document.body.appendChild(_mirror)
  }
  const text = ta.value.substring(0, offset)
  _mirror.textContent = ''
  const pre = document.createTextNode(text)
  const marker = document.createElement('span')
  marker.textContent = '\u200b'
  _mirror.appendChild(pre)
  _mirror.appendChild(marker)
  // 同步滚动，避免测量位置错位
  _mirror.scrollTop = ta.scrollTop
  _mirror.scrollLeft = ta.scrollLeft
  const taRect = ta.getBoundingClientRect()
  const mRect = marker.getBoundingClientRect()
  const wrapRect = wrap.getBoundingClientRect()
  // 转换为相对 textarea-wrap 的坐标
  return {
    top: mRect.top - wrapRect.top,
    left: mRect.left - wrapRect.left,
    taTopInWrap: taRect.top - wrapRect.top,
    taLeftInWrap: taRect.left - wrapRect.left
  }
}

function updateFloatingBarPosition() {
  const range = getSelectionRange()
  if (!range) { hideFloatingBar(); return }
  const pos = getCaretPixel(Math.min(range.start, range.end))
  const endPos = getCaretPixel(Math.max(range.start, range.end))
  const wrap = textareaRef.value?.parentElement
  if (!wrap) return
  // 水平居中于选区中间；垂直在选区上方
  const barWidth = 220 // 5 个按钮预估宽度
  let left = (pos.left + endPos.left) / 2 - barWidth / 2
  const wrapWidth = wrap.clientWidth
  left = Math.max(8, Math.min(left, wrapWidth - barWidth - 8))
  // 在选区上方，bar 高度约 40px
  let top = Math.min(pos.top, endPos.top) - 48
  if (top < 8) top = Math.max(pos.top, endPos.top) + 24 // 上方不够 → 放选区下方
  floatingX.value = left
  floatingY.value = top
  floatingBarVisible.value = true
}

function hideFloatingBar() {
  floatingBarVisible.value = false
}

let hideFloatTimer = null
function onTextSelect() {
  if (hideFloatTimer) clearTimeout(hideFloatTimer)
  hideFloatTimer = setTimeout(() => {
    const range = getSelectionRange()
    if (range) updateFloatingBarPosition()
    else hideFloatingBar()
  }, 30)
}
function onTouchEnd() {
  // 移动端 touchend 后稍等片刻（等 selection 稳定）再计算
  if (hideFloatTimer) clearTimeout(hideFloatTimer)
  hideFloatTimer = setTimeout(onTextSelect, 80)
}
function onTextareaBlur() {
  // blur 时可能是点击浮动工具栏（mousedown preventDefault 阻止了 blur）
  // 所以延迟隐藏，给工具栏 click 留时间
  setTimeout(hideFloatingBar, 120)
}
function onTextareaFocus() {
  // =============== P1-6: 聚焦时把光标滚动到可视区中央，避免被软键盘遮挡 ===============
  nextTick(() => {
    const ta = textareaRef.value
    if (!ta) return
    try {
      ta.scrollIntoView({ block: 'center', behavior: 'smooth' })
    } catch {}
  })
}

const floatingBarStyle = computed(() => ({
  left: `${floatingX.value}px`,
  top: `${floatingY.value}px`
}))

function applyFloat(type) {
  switch (type) {
    case 'bold': insert('**', '**', '粗体文字'); break
    case 'italic': insert('*', '*', '斜体文字'); break
    case 'strike': insert('~~', '~~', '删除线文字'); break
    case 'code': insert('`', '`', 'code'); break
    case 'link': insert('[', '](https://)', '链接文本'); break
  }
  nextTick(() => {
    // 插入后重新定位（如果还有选区）
    const r = getSelectionRange()
    if (r) updateFloatingBarPosition()
    else hideFloatingBar()
  })
}

// =============== P1-6 动态样式（软键盘高度影响 textarea padding 与吸底工具栏位置） ===============
const textareaDynamicStyle = computed(() => {
  if (isDesktop.value) return {}
  return {
    paddingBottom: `${90 + keyboardHeight.value}px`
  }
})

const mobileToolbarStyle = computed(() => {
  if (isDesktop.value) return {}
  const safeBottom = typeof window !== 'undefined'
    ? (parseFloat(getComputedStyle(document.documentElement).getPropertyValue('--safe-area-bottom')) || 0)
    : 0
  return {
    bottom: `${keyboardHeight.value}px`,
    paddingBottom: `calc(8px + env(safe-area-inset-bottom, ${safeBottom}px) + ${keyboardHeight.value > 0 ? 4 : 0}px)`
  }
})

// =============== P1-7 键盘快捷键（桌面端） ===============
function onKeyDown(e) {
  const ta = textareaRef.value
  if (!ta) return
  const mod = e.metaKey || e.ctrlKey
  if (!mod) return
  const k = e.key.toLowerCase()
  if (k === 's' && !e.shiftKey) {
    e.preventDefault()
    onSave()
    return
  }
  if (k === 'b') { e.preventDefault(); applyFloat('bold'); return }
  if (k === 'i') { e.preventDefault(); applyFloat('italic'); return }
  if (k === 'k') { e.preventDefault(); applyFloat('link'); return }
  if (k === 's' && e.shiftKey) { e.preventDefault(); applyFloat('strike'); return }
}

async function loadData() {
  const [cats, tgs] = await Promise.all([http.get('/categories'), http.get('/tags')])
  categories.value = cats
  tags.value = tgs

  if (!auth.user) {
    try { await auth.fetchUser() } catch { /* ignore */ }
  }

  const draft = readDraft()
  let serverUpdatedAt = 0

  if (isEdit.value) {
    const note = await http.get(`/notes/${noteId.value}`)
    form.title = note.title
    form.content = note.content
    form.categoryId = note.categoryId
    form.isPinned = note.isPinned || false
    form.tagIds = (note.tags || [])
      .map((name) => tags.value.find((t) => t.name === name)?.id)
      .filter(Boolean)
    form.backgroundColor = note.backgroundColor || null
    serverUpdatedAt = note.updatedAt ? new Date(note.updatedAt).getTime() : 0
  }

  if (draft && (draft.title || draft.content)) {
    const needAsk = !isEdit.value
      ? true
      : new Date(draft.savedAt || 0).getTime() > serverUpdatedAt
    if (needAsk) {
      try {
        await showConfirmDialog({
          title: '发现未保存的草稿',
          message: `保存时间：${formatDraftTime(draft.savedAt)}\n是否恢复到上次编辑的内容？`,
          confirmButtonText: '恢复草稿',
          cancelButtonText: '丢弃草稿'
        })
        form.title = draft.title || ''
        form.content = draft.content || ''
        form.categoryId = draft.categoryId ?? null
        form.tagIds = Array.isArray(draft.tagIds) ? draft.tagIds : []
        form.backgroundColor = draft.backgroundColor ?? null
        form.isPinned = draft.isPinned || false
        hasUnsavedChanges.value = true
        updateDocumentTitle()
        showToast('草稿已恢复')
      } catch {
        clearDraft()
      }
    }
  }
}

function formatDraftTime(ts) {
  if (!ts) return '未知时间'
  try {
    const d = new Date(ts)
    const pad = (n) => n.toString().padStart(2, '0')
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`
  } catch { return '未知时间' }
}

function onCategoryConfirm({ selectedValues }) {
  form.categoryId = selectedValues[0] ?? null
  showCategoryPicker.value = false
}

function toggleTag(id) {
  const i = form.tagIds.indexOf(id)
  if (i >= 0) form.tagIds.splice(i, 1)
  else form.tagIds.push(id)
}

function insert(before, after = '', placeholder = '') {
  const ta = textareaRef.value
  if (!ta) return
  const start = ta.selectionStart
  const end = ta.selectionEnd
  const sel = form.content.substring(start, end) || placeholder
  form.content =
    form.content.substring(0, start) + before + sel + after + form.content.substring(end)
  nextTick(() => {
    ta.focus()
    const pos = start + before.length + sel.length
    ta.setSelectionRange(pos, pos)
  })
}

function formatFormulas() {
  let text = form.content
  if (!text || !text.trim()) {
    showToast('没有内容可格式化')
    return
  }
  const original = text
  text = text.replace(/\$\$([\s\S]*?)\$\$/g, (match, inner, offset, full) => {
    const formula = inner.trim()
    const beforeChar = offset > 0 ? full[offset - 1] : ''
    const afterChar = offset + match.length < full.length ? full[offset + match.length] : ''
    const needNewlineBefore = beforeChar && beforeChar !== '\n'
    const needNewlineAfter = afterChar && afterChar !== '\n'
    return `${needNewlineBefore ? '\n' : ''}$$\n${formula}\n$$${needNewlineAfter ? '\n' : ''}`
  })
  text = text.replace(/(?<![\$\\])\$(?!\$)([^\n]+?)(?<!\$)\$(?!\$)/g, (match, _content, offset, full) => {
    const beforeChar = offset > 0 ? full[offset - 1] : ''
    const afterChar = offset + match.length < full.length ? full[offset + match.length] : ''
    const needPrefix = beforeChar && !/\s/.test(beforeChar)
    const needSuffix = afterChar && !/\s/.test(afterChar)
    return `${needPrefix ? ' ' : ''}${match}${needSuffix ? ' ' : ''}`
  })
  if (text !== original) {
    form.content = text
    showToast('已格式化公式')
  } else {
    showToast('无需格式化')
  }
}

// =============== P1-8: 确保当前笔记有 id（新建模式下，上传图片前先 POST 建空壳） ===============
async function ensureNoteId() {
  if (isReallyEdit.value) return true
  // 新建模式 → 先建空壳笔记
  if (saving.value) return false
  saving.value = true
  try {
    const payload = {
      title: form.title.trim(),
      content: form.content || ' ',
      categoryId: form.categoryId,
      tagIds: form.tagIds,
      backgroundColor: form.backgroundColor || '',
      isPinned: form.isPinned
    }
    const created = await http.post('/notes', payload)
    temporaryNoteId.value = created.id
    // 清除"新建"草稿 key
    try { localStorage.removeItem('note_draft/new') } catch {}
    // URL 同步跳转到真正的编辑页（不改变内存中的表单）
    await router.replace(`/notes/${created.id}/edit`)
    showToast('已自动创建草稿笔记')
    return true
  } catch (err) {
    showToast('创建草稿失败')
    return false
  } finally {
    saving.value = false
  }
}

async function triggerUpload() {
  if (!isReallyEdit.value) {
    const ok = await ensureNoteId()
    if (!ok) return
  }
  const target = isDesktop.value ? fileInput.value : mobileFileInput.value
  target?.click()
}

async function handleImageFile(file) {
  if (!file || !file.type?.startsWith('image/')) return
  if (!isReallyEdit.value) {
    const ok = await ensureNoteId()
    if (!ok) return
  }
  try {
    const fd = new FormData()
    fd.append('file', file)
    const res = await http.post(`/notes/${effectiveNoteId.value}/attachments`, fd)
    insert(`\n![${res.fileName || '图片'}](/api/attachments/${res.id})\n`)
    showToast('图片已插入')
  } catch {
    // 拦截器提示
  }
}

async function onFileChange(e) {
  const file = e.target.files?.[0]
  if (!file) return
  await handleImageFile(file)
  e.target.value = ''
}

// =============== P1-8 粘贴上传图片 ===============
function onPaste(e) {
  if (!e.clipboardData) return
  const items = e.clipboardData.items
  if (!items || !items.length) return
  for (const it of items) {
    if (it.type?.startsWith('image/')) {
      const file = it.getAsFile?.()
      if (file) {
        e.preventDefault()
        handleImageFile(file)
        return
      }
    }
  }
}

// =============== P1-8 拖拽上传图片 ===============
function onDragOver(e) {
  if (!e.dataTransfer?.types?.includes('Files')) return
  dragOver.value = true
}
function onDragLeave(e) {
  dragOver.value = false
}
function onDrop(e) {
  dragOver.value = false
  const files = e.dataTransfer?.files
  if (!files || !files.length) return
  for (const f of files) {
    if (f.type?.startsWith('image/')) {
      handleImageFile(f)
      break
    }
  }
}

async function onSave() {
  if (!form.title.trim() && !form.content.trim()) {
    showToast('标题和内容不能同时为空')
    return
  }
  saving.value = true
  try {
    const payload = {
      title: form.title.trim(),
      content: form.content,
      categoryId: form.categoryId,
      tagIds: form.tagIds,
      backgroundColor: form.backgroundColor || '',
      isPinned: form.isPinned
    }
    if (isReallyEdit.value) {
      await http.put(`/notes/${effectiveNoteId.value}`, payload)
      showToast('已保存')
    } else {
      const created = await http.post('/notes', payload)
      showToast('已创建')
      clearDraft()
      router.replace(`/notes/${created.id}/edit`)
      return
    }
    clearDraft()
  } finally {
    saving.value = false
  }
}

async function onBack() {
  if (hasUnsavedChanges.value || form.title.trim() || form.content.trim()) {
    try {
      await showConfirmDialog({
        title: hasUnsavedChanges.value ? '内容尚未保存' : '提示',
        message: hasUnsavedChanges.value
          ? '有未保存的内容，离开后会保留为草稿，下次进入可恢复。确定离开吗？'
          : '尚未保存，确定离开吗？',
        confirmButtonText: '离开',
        cancelButtonText: '继续编辑'
      })
    } catch {
      return
    }
  }
  router.back()
}

// =============== 生命周期 & 全局事件 ===============
let _keydownHandler = null

onMounted(() => {
  updateDocumentTitle()
  loadData()

  // P1-7 桌面端快捷键（挂在 textarea 上，避免全局冲突）
  nextTick(() => {
    const ta = textareaRef.value
    if (ta) {
      _keydownHandler = onKeyDown
      ta.addEventListener('keydown', _keydownHandler)
    }
  })

  // P1-6 软键盘高度监听
  if (window.visualViewport) {
    window.visualViewport.addEventListener('resize', onViewportChange)
    window.visualViewport.addEventListener('scroll', onViewportChange)
  }
  window.addEventListener('resize', onViewportChange)

  // 点击外部关闭浮动工具栏
  document.addEventListener('mousedown', (e) => {
    const ta = textareaRef.value
    const wrap = ta?.parentElement
    if (!wrap) return
    if (!wrap.contains(e.target)) hideFloatingBar()
  })
})

onUnmounted(() => {
  saveDraft()
  document.title = '笔记'

  // 清理浮动栏的镜像 div
  if (_mirror) {
    try { document.body.removeChild(_mirror) } catch {}
    _mirror = null
  }
  // 清理快捷键
  if (_keydownHandler && textareaRef.value) {
    textareaRef.value.removeEventListener('keydown', _keydownHandler)
    _keydownHandler = null
  }
  // 清理键盘高度监听
  if (window.visualViewport) {
    window.visualViewport.removeEventListener('resize', onViewportChange)
    window.visualViewport.removeEventListener('scroll', onViewportChange)
  }
  window.removeEventListener('resize', onViewportChange)
  dragOver.value = false
})
</script>

<style scoped>
.page {
  min-height: 100vh;
  display: flex;
  flex-direction: column;
}
.nav-actions {
  display: flex;
  align-items: center;
  gap: 12px;
}
.color-icon {
  cursor: pointer;
}
.editor {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  padding-bottom: 0;
}
.title-field :deep(.van-field__control) {
  font-size: 18px;
  font-weight: 600;
}
.meta-row {
  border-top: 1px solid;
}
.toolbar-wrap {
  border-top: 1px solid;
  background: rgba(255, 255, 255, 0.5);
}

/* ============ 工具栏（通用） ============ */
.toolbar {
  display: flex;
  gap: 6px;
  padding: 8px 12px;
  overflow-x: auto;
  background: rgba(255, 255, 255, 0.4);
  border-bottom: 1px solid #ebedf0;
  flex-shrink: 0;
}
.toolbar .van-button {
  flex-shrink: 0;
}
.toolbar-divider {
  width: 1px;
  align-self: stretch;
  margin: 4px 2px;
  background: rgba(0, 0, 0, 0.08);
  flex-shrink: 0;
}

/* ============ 桌面端：工具栏在编辑区顶部 ============ */
.desktop-toolbar {
  border-bottom: 1px solid #ebedf0;
}

/* ============ 移动端：工具栏吸底常驻 ============ */
.mobile-toolbar {
  position: sticky;
  left: 0;
  right: 0;
  z-index: 20;
  border-top: 1px solid rgba(0, 0, 0, 0.08);
  border-bottom: none;
  padding: 8px 8px calc(8px + env(safe-area-inset-bottom, 0px));
  background: var(--mobile-toolbar-bg, rgba(255, 255, 255, 0.94));
  backdrop-filter: saturate(180%) blur(10px);
  -webkit-backdrop-filter: saturate(180%) blur(10px);
  box-shadow: 0 -2px 10px rgba(0, 0, 0, 0.06);
  /* 吸底工具栏通过 inline style 动态设置 bottom 值（跟随软键盘高度） */
}
.mobile-toolbar-scroll {
  display: flex;
  gap: 6px;
  overflow-x: auto;
  width: 100%;
  -webkit-overflow-scrolling: touch;
}
.mobile-toolbar-scroll::-webkit-scrollbar { display: none; }

/* textarea 包装 + 字数统计定位容器 */
.edit-area {
  display: flex;
  flex-direction: column;
  flex: 1;
  overflow: hidden;
  position: relative;
  transition: box-shadow 0.15s;
}
/* 拖拽图片进入时的高亮提示 */
.edit-area.drag-over {
  box-shadow: inset 0 0 0 2px #1989fa;
  background: rgba(25, 137, 250, 0.05);
}
.textarea-wrap {
  position: relative;
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.content-area {
  flex: 1;
  width: 100%;
  border: none;
  outline: none;
  resize: none;
  padding: 14px 16px;
  font-size: 15px;
  line-height: 1.7;
  min-height: 50vh;
  font-family: inherit;
  background: transparent;
  color: inherit;
  box-sizing: border-box;
  overflow-y: auto;
}
.content-area::placeholder {
  color: inherit;
  opacity: 0.5;
}

/* 字数统计悬浮 */
.word-count {
  position: absolute;
  right: 18px;
  bottom: 10px;
  font-size: 12px;
  opacity: 0.45;
  pointer-events: none;
  user-select: none;
  letter-spacing: 0.3px;
  padding: 2px 8px;
  background: rgba(128, 128, 128, 0.08);
  border-radius: 10px;
  backdrop-filter: blur(2px);
}

/* 移动端默认（无键盘）的底部预留 */
@media (max-width: 1023px) {
  .content-area {
    padding-bottom: 90px;
  }
}

.editor-body {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}
.preview-area {
  padding: 14px 16px;
  min-height: 50vh;
  overflow-y: auto;
}
.preview-label {
  font-size: 12px;
  padding: 0 4px 8px;
  border-bottom: 1px solid;
  margin-bottom: 8px;
  letter-spacing: 1px;
}
.tag-picker {
  height: 100%;
  display: flex;
  flex-direction: column;
}
.tag-list {
  flex: 1;
  overflow-y: auto;
}
.empty-tags {
  padding-top: 20px;
}

/* ============ P1-5: 选中文本浮动工具栏 ============ */
.floating-toolbar {
  position: absolute;
  z-index: 30;
  display: flex;
  gap: 2px;
  padding: 4px;
  background: #1f2329;
  border-radius: 10px;
  box-shadow: 0 4px 16px rgba(0, 0, 0, 0.2);
}
.floating-toolbar::after {
  /* 小箭头指向选区 */
  content: '';
  position: absolute;
  left: 50%;
  bottom: -6px;
  transform: translateX(-50%);
  border-left: 6px solid transparent;
  border-right: 6px solid transparent;
  border-top: 6px solid #1f2329;
}
.ft-btn {
  min-width: 36px;
  height: 32px;
  padding: 0 8px;
  border: none;
  background: transparent;
  color: #fff;
  border-radius: 6px;
  cursor: pointer;
  font-size: 14px;
  line-height: 1;
  transition: background 0.12s;
  display: inline-flex;
  align-items: center;
  justify-content: center;
}
.ft-btn:hover,
.ft-btn:active {
  background: rgba(255, 255, 255, 0.15);
}
.ft-btn b { font-weight: 700; }
.ft-btn i { font-style: italic; }
.ft-btn s { text-decoration: line-through; }

.float-fade-enter-active,
.float-fade-leave-active {
  transition: opacity 0.12s ease, transform 0.12s ease;
}
.float-fade-enter-from,
.float-fade-leave-to {
  opacity: 0;
  transform: translateY(4px) scale(0.96);
}

:root[data-widget-theme="dark"] .floating-toolbar,
.floating-toolbar { /* 深色模式下保持反色毛玻璃可读性 */
  background: #1f2329;
}

/* 深色背景下：移动端吸底工具栏保持浅色毛玻璃 */
:deep(.page) .mobile-toolbar {
  background: rgba(255, 255, 255, 0.92);
}

/* 桌面端：编辑/预览并排显示 */
@media (min-width: 1024px) {
  .page {
    max-width: 1200px;
    margin: 0 auto;
    min-height: calc(100vh - 40px);
    border-left: 1px solid #ebedf0;
    border-right: 1px solid #ebedf0;
  }
  .editor {
    padding: 0 24px 24px;
  }
  .editor-body {
    flex-direction: row;
    gap: 16px;
    border: 1px solid #ebedf0;
    border-radius: 8px;
    overflow: hidden;
    min-height: 60vh;
  }
  .edit-area {
    flex: 1 1 50%;
    border-right: 1px solid #ebedf0;
  }
  .preview-area {
    flex: 1 1 50%;
    min-height: 60vh;
    padding: 14px 20px;
  }
  .content-area {
    min-height: 60vh;
    padding-bottom: 40px;
  }
  .desktop-toolbar {
    padding: 10px 16px;
    flex-wrap: wrap;
  }
  .mobile-toolbar {
    display: none;
  }
}
</style>
