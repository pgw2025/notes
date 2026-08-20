<template>
  <div class="page" :class="{ 'page--fullscreen': isFullscreen }">
    <van-nav-bar
      :title="navTitle"
      left-arrow
      @click-left="onBack"
    >
      <template #right>
        <!-- 桌面端单行布局：按钮在标题右侧 -->
        <div class="nav-actions nav-actions--desktop">
          <van-icon
            :name="form.isPinned ? 'star' : 'star-o'"
            size="20"
            :color="form.isPinned ? '#ff976a' : undefined"
            @click="form.isPinned = !form.isPinned"
          />
          <!-- P2-1 大纲按钮 -->
          <span class="nav-icon nav-char-btn" title="大纲 (Ctrl+Shift+O)" @click="showOutline = !showOutline">☰</span>
          <!-- P2-2 搜索按钮 -->
          <span class="nav-icon nav-char-btn" title="搜索替换 (Ctrl+F)" @click="openSearch">🔍</span>
          <!-- P2-3 全屏按钮 -->
          <span class="nav-icon nav-char-btn" :title="isFullscreen ? '退出全屏 (Esc)' : '全屏编辑'" @click="toggleFullscreen">{{ isFullscreen ? '⤢' : '⛶' }}</span>
          <van-icon name="edit" size="20" class="color-icon" @click="showColorPicker = true" />
          <van-button size="mini" type="primary" :loading="saving" @click="onSave">保存</van-button>
        </div>
      </template>
    </van-nav-bar>

    <!-- 移动端第二行工具栏：按钮独占一行，和标题完全不重叠 -->
    <div class="nav-actions nav-actions--mobile">
      <van-icon
        :name="form.isPinned ? 'star' : 'star-o'"
        size="20"
        :color="form.isPinned ? '#ff976a' : undefined"
        title="置顶"
        @click="form.isPinned = !form.isPinned"
      />
      <span class="nav-icon nav-char-btn" title="大纲" @click="showOutline = !showOutline">☰</span>
      <span class="nav-icon nav-char-btn" title="搜索替换" @click="openSearch">🔍</span>
      <span class="nav-icon nav-char-btn" :title="isFullscreen ? '退出全屏' : '全屏'" @click="toggleFullscreen">{{ isFullscreen ? '⤢' : '⛶' }}</span>
      <van-icon name="edit" size="20" class="color-icon" title="背景色" @click="showColorPicker = true" />
      <van-button size="mini" type="primary" :loading="saving" @click="onSave">保存</van-button>
    </div>

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
              @scroll="onTextareaScroll"
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

            <!-- =============== P2-2 搜索 & 替换面板 =============== -->
            <transition name="float-fade">
              <div v-show="showSearch" class="search-panel" @mousedown.stop>
                <div class="search-row">
                  <input
                    v-model="searchKeyword"
                    class="search-input"
                    type="text"
                    placeholder="查找 (支持正则)"
                    @input="ensureMatchesComputed"
                    @keydown.enter.prevent="searchNext"
                    @keydown.esc.prevent="closeSearch"
                  />
                  <span class="search-count" :title="`共 ${matches.length} 处`">
                    {{ matches.length === 0 ? '0/0' : `${currentMatchIdx + 1}/${matches.length}` }}
                  </span>
                  <button type="button" class="sp-btn" title="上一个 (Shift+Enter)" @click="searchPrev">▲</button>
                  <button type="button" class="sp-btn" title="下一个 (Enter)" @click="searchNext">▼</button>
                  <button type="button" class="sp-btn sp-close" title="关闭 (Esc)" @click="closeSearch">✕</button>
                </div>
                <div class="search-row search-row-2">
                  <button
                    type="button"
                    class="sp-toggle"
                    :class="{ active: useRegex }"
                    title="正则模式"
                    @click="useRegex = !useRegex"
                  >.*</button>
                  <button
                    type="button"
                    class="sp-toggle"
                    :class="{ active: matchCase }"
                    title="区分大小写"
                    @click="matchCase = !matchCase"
                  >Aa</button>
                  <input
                    v-model="replaceText"
                    class="search-input replace-input"
                    type="text"
                    placeholder="替换为"
                    @keydown.enter.prevent="doReplace"
                  />
                  <button
                    type="button"
                    class="sp-btn sp-primary"
                    :disabled="matches.length === 0"
                    @click="doReplace"
                  >替换</button>
                  <button
                    type="button"
                    class="sp-btn sp-primary"
                    :disabled="matches.length === 0"
                    @click="doReplaceAll"
                  >全部</button>
                </div>
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

    <!-- =============== P2-1 大纲面板（右侧抽屉） =============== -->
    <transition name="slide-right">
      <div v-show="showOutline" class="outline-mask" @click.self="showOutline = false">
        <aside class="outline-panel" :style="{ background: effectiveBg, color: textColor }" @mousedown.stop>
          <div class="outline-header" :style="{ borderColor: metaBorderColor }">
            <span class="outline-title">📑 大纲</span>
            <button type="button" class="sp-btn sp-close" @click="showOutline = false">✕</button>
          </div>
          <div v-if="outlineList.length === 0" class="outline-empty" :style="{ color: subTextColor }">
            暂无标题，使用 <b># / ## / ###</b> 来组织内容
          </div>
          <ul v-else class="outline-list">
            <li
              v-for="(h, i) in outlineList"
              :key="'h-'+i"
              class="outline-item"
              :class="[
                'outline-level-'+h.level,
                { active: i === activeHeadingIdx }
              ]"
              :style="(i === activeHeadingIdx ? { borderColor: '#1989fa', color: '#1989fa' } : {})"
              @click="scrollToHeading(i)"
            >
              <span class="outline-dot" :style="(i === activeHeadingIdx ? { background: '#1989fa' } : { background: subTextColor })"></span>
              <span class="outline-text">{{ h.text || '(空标题)' }}</span>
            </li>
          </ul>
        </aside>
      </div>
    </transition>

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
// 服务器端保存快照：保存成功后写入，返回时以此为准判断是否有未保存改动
const lastSavedSnapshot = ref('')

function snapshotForm() {
  return JSON.stringify({
    title: form.title,
    content: form.content,
    categoryId: form.categoryId,
    tagIds: form.tagIds,
    backgroundColor: form.backgroundColor,
    isPinned: form.isPinned
  })
}
function isDirty() {
  if (hasUnsavedChanges.value) return true
  const cur = snapshotForm()
  // 新建笔记：既没有保存过，也没有任何内容 → 不算脏
  if (!lastSavedSnapshot.value && !form.title.trim() && !form.content.trim()) return false
  // 新建笔记但有内容 → 算脏（需要提醒保存/丢弃）
  if (!lastSavedSnapshot.value) return !!(form.title.trim() || form.content.trim())
  return cur !== lastSavedSnapshot.value
}

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

// =============== P2-1 大纲导航 ===============
const showOutline = ref(false)
const activeHeadingIdx = ref(0)

/**
 * 解析 Markdown 标题（ATX 风格：在行首匹配 1~6 个 # 后接空格）
 * 排除出现在代码块或引用块中的 #。实现：逐行扫描 + inFence/inBlockquote 状态机，
 * 只接受"真正的行首"（之前是换行/文本起始）、非 ``` 代码块内、且非 > 引用行首的标题。
 */
const outlineList = computed(() => {
  const text = form.content || ''
  const result = []
  let inFence = false
  const lines = text.split(/\r?\n/)
  let offset = 0 // 累积字符 offset（包含换行），用于后续 scrollToHeading 精确定位
  for (let i = 0; i < lines.length; i++) {
    const line = lines[i]
    const lineStartOffset = offset
    // 代码块围栏检测（``` 或 ~~~）
    const fenceMatch = line.match(/^\s*(```|~~~)/)
    if (fenceMatch && !line.slice(fenceMatch[0].length).includes(fenceMatch[1])) {
      inFence = !inFence
    }
    if (!inFence) {
      const m = line.match(/^(#{1,6})(?:\s+)(.*)$/)
      if (m) {
        const level = m[1].length
        const rawText = m[2].replace(/\s*#+\s*$/, '').trim() // 去掉闭合 #
        result.push({
          level,
          text: rawText,
          offset: lineStartOffset,
          line: i
        })
      }
    }
    offset += line.length + 1 // +1 表示换行符
  }
  return result
})

function scrollToHeading(idx) {
  const ta = textareaRef.value
  if (!ta) return
  const h = outlineList.value[idx]
  if (!h) return
  activeHeadingIdx.value = idx
  // 用镜像 div 算出该标题行首的像素位置 → 滚动到顶部再留一点上边距
  const px = getCaretPixel(h.offset)
  const targetTop = (px.taTopInWrap || 0) + px.top - 24
  ta.scrollTo({ top: Math.max(0, targetTop), behavior: 'smooth' })
  ta.focus()
  ta.setSelectionRange(h.offset, h.offset)
}

function updateActiveHeading() {
  const ta = textareaRef.value
  if (!ta || outlineList.value.length === 0) return
  const viewTop = ta.scrollTop
  // 找最后一个「标题像素位置 <= viewport 顶部 + 40」的标题作为当前
  let bestIdx = 0
  for (let i = 0; i < outlineList.value.length; i++) {
    const px = getCaretPixel(outlineList.value[i].offset)
    const rel = (px.taTopInWrap || 0) + px.top
    // 相对 textarea 的内容可视区顶部：px.top 是相对 wrap，但 textarea 的 scroll 把上方内容挤出可视区
    // 简化做法：offset 对应字符 offset，scrollTop 近似正比；用镜像 + scrollTop 同步后取 marker.top - ta.top
    const taRect = ta.getBoundingClientRect()
    const wrapRect = ta.parentElement.getBoundingClientRect()
    const absTop = (px.taTopInWrap || 0) + px.top - (taRect.top - wrapRect.top) + ta.scrollTop
    if (absTop <= viewTop + 36) bestIdx = i
    else break
  }
  activeHeadingIdx.value = bestIdx
}

function onTextareaScroll() {
  hideFloatingBar()
  // P2-1: 滚动时更新大纲高亮（节流 30ms）
  if (_scrollTimer) clearTimeout(_scrollTimer)
  _scrollTimer = setTimeout(updateActiveHeading, 30)
}
let _scrollTimer = null

// =============== P2-2 搜索 & 替换 ===============
const showSearch = ref(false)
const searchKeyword = ref('')
const replaceText = ref('')
const useRegex = ref(false)
const matchCase = ref(false)
const currentMatchIdx = ref(0)

/** 把 matches 作为副作用触发计算（Vue 计算属性在模板中访问时会懒求值，
 *  这里把"匹配结果数组"和"当前 index"拆包出来，以便搜索框输入时实时高亮定位） */
const matches = computed(() => {
  const kw = searchKeyword.value
  const text = form.content || ''
  if (!kw) return []
  let re
  try {
    const flags = (matchCase.value ? '' : 'i') + 'g'
    if (useRegex.value) re = new RegExp(kw, flags)
    else {
      // 非正则模式：转义特殊字符
      const escaped = kw.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
      re = new RegExp(escaped, flags)
    }
  } catch { return [] }
  const res = []
  let m
  while ((m = re.exec(text)) !== null) {
    res.push({ start: m.index, end: m.index + m[0].length, text: m[0] })
    if (m[0].length === 0) re.lastIndex++ // 避免零宽死循环
  }
  return res
})

function ensureMatchesComputed() {
  // 仅为了触发 matches computed 在输入时立即重算（不依赖模板访问时机）
  void matches.value
  if (matches.value.length > 0) {
    // 若当前索引超出，重置到最后一个
    if (currentMatchIdx.value >= matches.value.length) {
      currentMatchIdx.value = matches.value.length - 1
    }
    scrollToMatch(currentMatchIdx.value)
  }
}

function openSearch() {
  showSearch.value = true
  nextTick(() => {
    const el = document.querySelector('.search-input')
    if (el) { el.focus(); (el).select() }
  })
}
function closeSearch() {
  showSearch.value = false
  currentMatchIdx.value = 0
  searchKeyword.value = ''
  replaceText.value = ''
}

function scrollToMatch(idx) {
  const ta = textareaRef.value
  if (!ta || !matches.value[idx]) return
  const { start, end } = matches.value[idx]
  // 把当前 match 滚到可视区中上位置
  const px = getCaretPixel(start)
  const taRect = ta.getBoundingClientRect()
  const wrapRect = ta.parentElement.getBoundingClientRect()
  const contentY = (px.taTopInWrap || 0) + px.top - (taRect.top - wrapRect.top) + ta.scrollTop
  ta.scrollTo({ top: Math.max(0, contentY - ta.clientHeight / 3), behavior: 'smooth' })
  ta.focus()
  nextTick(() => ta.setSelectionRange(start, end))
}
function searchNext() {
  if (matches.value.length === 0) return
  currentMatchIdx.value = (currentMatchIdx.value + 1) % matches.value.length
  scrollToMatch(currentMatchIdx.value)
}
function searchPrev() {
  if (matches.value.length === 0) return
  currentMatchIdx.value = (currentMatchIdx.value - 1 + matches.value.length) % matches.value.length
  scrollToMatch(currentMatchIdx.value)
}

function doReplace() {
  if (matches.value.length === 0) return
  const m = matches.value[currentMatchIdx.value]
  if (!m) return
  const before = form.content.substring(0, m.start)
  const after = form.content.substring(m.end)
  form.content = before + replaceText.value + after
  nextTick(() => {
    ensureMatchesComputed()
    if (matches.value.length > 0) {
      // 跳到下一个（若当前已经是末尾则重置到第 0 个）
      if (currentMatchIdx.value >= matches.value.length) currentMatchIdx.value = 0
      scrollToMatch(currentMatchIdx.value)
    }
  })
  showToast('已替换 1 处')
}

function doReplaceAll() {
  const total = matches.value.length
  if (total === 0) return
  const kw = searchKeyword.value
  try {
    const flags = (matchCase.value ? '' : 'i') + 'g'
    const re = useRegex.value ? new RegExp(kw, flags) : new RegExp(kw.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'), flags)
    form.content = form.content.replace(re, replaceText.value)
    showToast(`已替换 ${total} 处`)
    currentMatchIdx.value = 0
  } catch {
    showToast('正则语法错误')
  }
}

// =============== P2-3 全屏沉浸式编辑 ===============
const isFullscreen = ref(false)

function toggleFullscreen() {
  const doc = document
  if (!doc.fullscreenElement) {
    const target = doc.documentElement
    const req = target.requestFullscreen || (target).webkitRequestFullscreen || (target).msRequestFullscreen
    if (req) req.call(target).catch(() => showToast('当前浏览器不支持全屏'))
    else showToast('当前浏览器不支持全屏')
  } else {
    const exit = doc.exitFullscreen || (doc).webkitExitFullscreen || (doc).msExitFullscreen
    if (exit) exit.call(doc)
  }
}
function onFullscreenChange() {
  isFullscreen.value = !!document.fullscreenElement
}

// =============== P1-7 键盘快捷键（桌面端） — 扩展 P2 的快捷键 ===============
function onKeyDown(e) {
  const ta = textareaRef.value
  if (!ta) return
  const mod = e.metaKey || e.ctrlKey
  const k = e.key.toLowerCase()

  // Esc 优先处理：关闭搜索/大纲或退出全屏
  if (e.key === 'Escape') {
    if (showSearch.value) { closeSearch(); e.preventDefault(); return }
    if (showOutline.value) { showOutline.value = false; e.preventDefault(); return }
    if (isFullscreen.value) {
      const doc = document
      if (doc.fullscreenElement) {
        const exit = doc.exitFullscreen || (doc).webkitExitFullscreen || (doc).msExitFullscreen
        if (exit) exit.call(doc)
      }
      e.preventDefault()
      return
    }
  }

  if (mod) {
    if (k === 'f') {
      // 打开搜索（Ctrl/Cmd+F 拦截浏览器默认页内搜索）
      e.preventDefault()
      openSearch()
      return
    }
    if (k === 's' && !e.shiftKey) {
      e.preventDefault()
      onSave()
      return
    }
    if (e.shiftKey && k === 'o') {
      // Ctrl+Shift+O 切换大纲
      e.preventDefault()
      showOutline.value = !showOutline.value
      return
    }
    if (k === 'b') { e.preventDefault(); applyFloat('bold'); return }
    if (k === 'i') { e.preventDefault(); applyFloat('italic'); return }
    if (k === 'k') { e.preventDefault(); applyFloat('link'); return }
    if (k === 's' && e.shiftKey) { e.preventDefault(); applyFloat('strike'); return }
  }

  // 搜索面板内部：Shift+Enter → 上一个；Enter → 下一个
  if (showSearch.value) {
    if (e.key === 'Enter' && e.shiftKey) {
      e.preventDefault()
      searchPrev()
      return
    }
  }
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
      // 记录服务器端初始快照：用于判断后续是否真正有改动
      lastSavedSnapshot.value = snapshotForm()
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
      // 新笔记创建成功也记录快照 + 跳转后不再触发脏判断
      lastSavedSnapshot.value = snapshotForm()
      router.replace(`/notes/${created.id}/edit`)
      return
    }
    clearDraft()
    // 保存成功：更新快照，下次返回直接放行不再弹窗
    lastSavedSnapshot.value = snapshotForm()
  } finally {
    saving.value = false
  }
}

async function onBack() {
  // 完全没内容：直接离开，不调用保存（接口会拒绝标题+内容同时为空）
  const blank = !form.title.trim() && !form.content.trim()
  if (blank || !isDirty()) {
    router.back()
    return
  }
  // 有内容/有改动：自动保存后再返回，不再弹窗询问
  try {
    saving.value = true
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
    } else {
      const created = await http.post('/notes', payload)
      clearDraft()
      lastSavedSnapshot.value = snapshotForm()
      // 新建笔记：先 replace 到 edit 路径，对齐编辑态；之后统一 router.back
      // 但新建笔记直接返回列表更符合预期，这里不 replace，改完快照直接返回
      form.id = created.id
    }
    clearDraft()
    lastSavedSnapshot.value = snapshotForm()
  } catch (e) {
    // 保存失败：提示并中止返回，避免丢内容
    showToast('保存失败，未离开')
    saving.value = false
    return
  } finally {
    saving.value = false
  }
  router.back()
}

// =============== 生命周期 & 全局事件 ===============
let _keydownHandler = null
let _docKeydownHandler = null

onMounted(() => {
  updateDocumentTitle()
  loadData()

  // P1-7 + P2：textarea 范围内快捷键（处理修饰键组合等）
  nextTick(() => {
    const ta = textareaRef.value
    if (ta) {
      _keydownHandler = onKeyDown
      ta.addEventListener('keydown', _keydownHandler)
    }
  })

  // P2：文档级快捷键拦截（Ctrl+F / Esc / Ctrl+Shift+O 可以不在 textarea focus 时触发）
  _docKeydownHandler = (e) => {
    const mod = e.metaKey || e.ctrlKey
    const k = e.key.toLowerCase()
    if (e.key === 'Escape') {
      // 交给 onKeyDown，但需要手动调用一次来处理面板关闭/退出全屏（当焦点不在 textarea）
      const ta = textareaRef.value
      if (document.activeElement !== ta) onKeyDown(e)
    }
    if (mod && k === 'f') {
      // 只有当焦点不在浏览器原生搜索输入时拦截
      const tag = (document.activeElement?.tagName || '').toLowerCase()
      if (tag === 'input' || tag === 'textarea') return // 由元素上监听器处理
      e.preventDefault()
      openSearch()
    }
    if (mod && e.shiftKey && k === 'o') {
      e.preventDefault()
      showOutline.value = !showOutline.value
    }
  }
  document.addEventListener('keydown', _docKeydownHandler)

  // P1-6 软键盘高度监听
  if (window.visualViewport) {
    window.visualViewport.addEventListener('resize', onViewportChange)
    window.visualViewport.addEventListener('scroll', onViewportChange)
  }
  window.addEventListener('resize', onViewportChange)

  // 点击外部关闭浮动工具栏 / 大纲面板 / 搜索面板
  document.addEventListener('mousedown', (e) => {
    const ta = textareaRef.value
    const wrap = ta?.parentElement
    if (wrap && !wrap.contains(e.target)) hideFloatingBar()

    const outline = document.querySelector('.outline-panel')
    if (showOutline.value && outline && !outline.contains(e.target) && !(e.target).closest?.('.nav-icon')) {
      // 不在这里直接关，避免和按钮的 click 冲突：交给各自 click self mask 处理
    }
  })

  // P2-3 全屏事件
  document.addEventListener('fullscreenchange', onFullscreenChange)
  document.addEventListener('webkitfullscreenchange', onFullscreenChange)
  document.addEventListener('msfullscreenchange', onFullscreenChange)
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
  if (_docKeydownHandler) {
    document.removeEventListener('keydown', _docKeydownHandler)
    _docKeydownHandler = null
  }
  // 清理键盘高度监听
  if (window.visualViewport) {
    window.visualViewport.removeEventListener('resize', onViewportChange)
    window.visualViewport.removeEventListener('scroll', onViewportChange)
  }
  window.removeEventListener('resize', onViewportChange)
  // 清理全屏监听
  document.removeEventListener('fullscreenchange', onFullscreenChange)
  document.removeEventListener('webkitfullscreenchange', onFullscreenChange)
  document.removeEventListener('msfullscreenchange', onFullscreenChange)
  dragOver.value = false
  if (_scrollTimer) clearTimeout(_scrollTimer)
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
.nav-icon {
  cursor: pointer;
  opacity: 0.75;
  transition: opacity 0.15s, color 0.15s;
}
.nav-icon:hover { opacity: 1; color: #1989fa; }
.nav-char-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 22px;
  height: 22px;
  font-size: 17px;
  line-height: 1;
  user-select: none;
  color: inherit;
}
.nav-char-btn:hover { color: #1989fa; }
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

/* 移动端默认（无键盘）的底部预留 & 第二行工具栏布局 */
@media (max-width: 1023px) {
  .content-area {
    padding-bottom: 90px;
  }
  /* 移动端：van-nav-bar 标题独占第一行，右侧按钮在导航栏slot里隐藏 */
  :deep(.van-nav-bar__title) {
    max-width: 70%;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    font-size: 16px;
  }
  .nav-actions--desktop {
    display: none !important;
  }
  /* 移动端第二行工具栏：全宽独占一行 */
  .nav-actions--mobile {
    display: flex;
    align-items: center;
    justify-content: space-around;
    padding: 8px 10px 10px;
    gap: 4px;
    background: inherit;
    border-bottom: 1px solid rgba(128, 128, 128, 0.1);
  }
  .nav-actions--mobile .nav-char-btn {
    width: 28px;
    height: 28px;
    font-size: 18px;
  }
  .nav-actions--mobile .van-icon { font-size: 20px; }
  .nav-actions--mobile .color-icon { font-size: 20px; }
  .nav-actions--mobile .van-button {
    padding: 0 14px;
    height: 30px;
    line-height: 30px;
    font-size: 13px;
    flex: 0 0 auto;
  }
  /* 全屏态下移动端：工具栏也沉浸 */
  .page--fullscreen .nav-actions--mobile {
    background: transparent;
    backdrop-filter: saturate(1.3) blur(6px);
    border-bottom: 1px solid rgba(128, 128, 128, 0.08);
  }
}

/* 桌面端：保持单行布局，隐藏移动端第二行工具栏 */
@media (min-width: 1024px) {
  .nav-actions--mobile {
    display: none !important;
  }
  .nav-actions--desktop {
    display: flex;
    align-items: center;
    gap: 12px;
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

/* ============ P2-2 搜索 & 替换面板 ============ */
.search-panel {
  position: absolute;
  top: 8px;
  right: 16px;
  z-index: 40;
  min-width: 320px;
  display: flex;
  flex-direction: column;
  gap: 6px;
  padding: 8px;
  background: #fff;
  border: 1px solid #ebedf0;
  border-radius: 10px;
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.12);
}
@media (prefers-color-scheme: dark) {
  .search-panel { background: #2c2f36; border-color: rgba(255,255,255,0.1); }
  .search-input { background: #1f2329; color: #eee; border-color: rgba(255,255,255,0.15); }
  .search-count { color: #bbb; }
}
.search-row {
  display: flex;
  align-items: center;
  gap: 6px;
}
.search-row-2 {
  padding-top: 2px;
  border-top: 1px dashed #ebedf0;
}
.search-input {
  flex: 1;
  height: 32px;
  padding: 0 10px;
  border: 1px solid #dcdee0;
  border-radius: 6px;
  font-size: 13px;
  outline: none;
  background: #fff;
  transition: border-color 0.15s;
  min-width: 0;
}
.search-input:focus { border-color: #1989fa; }
.replace-input { flex: 1 1 50%; }
.search-count {
  min-width: 48px;
  text-align: center;
  font-size: 12px;
  color: #969799;
  letter-spacing: 0.3px;
}
.sp-btn {
  min-width: 28px;
  height: 30px;
  padding: 0 8px;
  border: 1px solid #ebedf0;
  background: #f7f8fa;
  border-radius: 6px;
  cursor: pointer;
  font-size: 13px;
  line-height: 1;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  transition: background 0.12s, border-color 0.12s;
}
.sp-btn:hover:not(:disabled) { background: #eef0f3; border-color: #dcdee0; }
.sp-btn:disabled { opacity: 0.45; cursor: not-allowed; }
.sp-btn.sp-close { color: #c8c9cc; background: transparent; border-color: transparent; }
.sp-btn.sp-close:hover { color: #ee0a24; background: rgba(238, 10, 36, 0.06); }
.sp-btn.sp-primary { background: #1989fa; color: #fff; border-color: #1989fa; }
.sp-btn.sp-primary:hover:not(:disabled) { background: #0f7ae5; border-color: #0f7ae5; }
.sp-toggle {
  height: 28px;
  padding: 0 10px;
  border: 1px solid #ebedf0;
  background: #f7f8fa;
  border-radius: 6px;
  cursor: pointer;
  font-size: 12px;
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  transition: all 0.12s;
}
.sp-toggle.active {
  background: #e8f3ff;
  border-color: #1989fa;
  color: #1989fa;
}

/* ============ P2-1 大纲面板 ============ */
.outline-mask {
  position: fixed;
  inset: 0;
  z-index: 80;
  background: rgba(0, 0, 0, 0.2);
  display: flex;
  justify-content: flex-end;
}
.outline-panel {
  width: min(320px, 86vw);
  max-width: 100%;
  height: 100%;
  box-shadow: -6px 0 24px rgba(0, 0, 0, 0.1);
  display: flex;
  flex-direction: column;
  overflow: hidden;
}
.outline-header {
  padding: 12px 16px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  border-bottom: 1px solid;
}
.outline-title {
  font-size: 15px;
  font-weight: 600;
  letter-spacing: 0.5px;
}
.outline-empty {
  padding: 40px 20px;
  text-align: center;
  font-size: 13px;
  line-height: 1.7;
}
.outline-list {
  list-style: none;
  margin: 0;
  padding: 8px 0 24px;
  overflow-y: auto;
  flex: 1;
}
.outline-item {
  position: relative;
  display: flex;
  align-items: flex-start;
  gap: 8px;
  padding: 8px 16px 8px 20px;
  cursor: pointer;
  border-left: 2px solid transparent;
  font-size: 14px;
  line-height: 1.5;
  transition: background 0.12s, border-color 0.12s, color 0.12s;
  user-select: none;
}
.outline-item:hover { background: rgba(25, 137, 250, 0.06); }
.outline-item.active { background: rgba(25, 137, 250, 0.08); border-left: 3px solid #1989fa; padding-left: 19px; }
.outline-level-1 { padding-left: 16px; font-weight: 600; font-size: 14.5px; }
.outline-level-2 { padding-left: 32px; }
.outline-level-3 { padding-left: 48px; font-size: 13.5px; opacity: 0.9; }
.outline-level-4 { padding-left: 64px; font-size: 13px; opacity: 0.85; }
.outline-level-5 { padding-left: 80px; font-size: 13px; opacity: 0.8; }
.outline-level-6 { padding-left: 96px; font-size: 13px; opacity: 0.75; }
.outline-item.active.outline-level-1,
.outline-item.active.outline-level-2,
.outline-item.active.outline-level-3,
.outline-item.active.outline-level-4,
.outline-item.active.outline-level-5,
.outline-item.active.outline-level-6 { padding-left: calc(16px + 16px * (attr(class) - 1) - 1px); }
.outline-dot {
  flex-shrink: 0;
  width: 6px;
  height: 6px;
  border-radius: 50%;
  margin-top: 8px;
}
.outline-text {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

/* 进入：从右滑入 + 背景渐显 */
.slide-right-enter-active,
.slide-right-leave-active {
  transition: opacity 0.22s ease;
}
.slide-right-enter-active .outline-panel,
.slide-right-leave-active .outline-panel {
  transition: transform 0.22s cubic-bezier(0.22, 1, 0.36, 1);
}
.slide-right-enter-from,
.slide-right-leave-to { opacity: 0; }
.slide-right-enter-from .outline-panel,
.slide-right-leave-to .outline-panel { transform: translateX(100%); }

/* ============ P2-3 全屏模式 ============ */
.page.page--fullscreen {
  max-width: none !important;
  min-height: 100vh !important;
  border-left: none !important;
  border-right: none !important;
  margin: 0 !important;
}
.page--fullscreen :deep(.van-nav-bar) {
  background: transparent;
  box-shadow: none;
  position: sticky;
  top: 0;
  z-index: 10;
  backdrop-filter: saturate(1.3) blur(6px);
  border-bottom: 1px solid rgba(128, 128, 128, 0.1);
}
.page--fullscreen :deep(.van-nav-bar__title) { font-weight: 500; font-size: 14px; opacity: 0.6; }
.page--fullscreen :deep(.van-nav-bar__text:active) { background: transparent; }
.page--fullscreen .editor {
  padding: 0 !important;
  min-height: 100vh;
}
.page--fullscreen .meta-row,
.page--fullscreen .title-field {
  padding-left: 16px;
  padding-right: 16px;
}
.page--fullscreen .editor-body {
  min-height: calc(100vh - 88px);
  border-radius: 0 !important;
  border-left: none !important;
  border-right: none !important;
}
.page--fullscreen .preview-label { display: none; }
.page--fullscreen .content-area {
  font-size: 16px;
  line-height: 1.8;
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
