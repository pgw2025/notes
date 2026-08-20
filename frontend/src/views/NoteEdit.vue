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
        <div v-show="isDesktop || mode === 'edit'" class="edit-area" :style="areaStyle">
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
                <component :is="b.icon" v-if="b.iconComponent" />
                <span v-else v-html="b.label" />
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
              placeholder="开始记录... 支持 Markdown 语法"
            ></textarea>

            <!-- 字数统计 -->
            <div class="word-count" :style="{ color: textColor }">
              {{ charCount }} 字 · {{ lineCount }} 行
            </div>
          </div>
        </div>

        <div v-show="isDesktop || mode === 'preview'" class="preview-area" :style="areaStyle">
          <div v-if="isDesktop" class="preview-label" :style="{ color: subTextColor, borderColor: metaBorderColor }">预览</div>
          <markdown-body :content="form.content || '*暂无内容*'" :style="{ color: textColor }" />
        </div>
      </div>

      <!-- 移动端：工具栏吸底常驻 -->
      <div v-if="!isDesktop" class="toolbar mobile-toolbar">
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

// 简单防抖工具
function debounce(fn, wait = 300) {
  let timer = null
  return function (...args) {
    if (timer) clearTimeout(timer)
    timer = setTimeout(() => fn.apply(this, args), wait)
  }
}

function saveDraft() {
  if (!form.title.trim() && !form.content.trim()) {
    // 空内容不保存草稿，但清除可能存在的旧草稿
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
  } catch (e) {
    // 忽略 localStorage 异常（例如隐私模式）
  }
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

// 监听内容变化 → 延迟写草稿
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

// ========== 工具栏按钮（统一配置，桌面端和移动端复用） ==========
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

// 笔记的有效背景色：自身色 → 用户默认色 → 系统白色
const effectiveBg = computed(() => resolveNoteColor(form.backgroundColor, auth.user?.defaultNoteColor))
const textColor = computed(() => getContrastColor(effectiveBg.value))
const subTextColor = computed(() => isDarkColor(effectiveBg.value) ? 'rgba(255,255,255,0.6)' : '#969799')
const metaBorderColor = computed(() => isDarkColor(effectiveBg.value) ? 'rgba(255,255,255,0.15)' : '#ebedf0')

// 页面标题（带未保存圆点）
const navTitle = computed(() => (isEdit.value ? '编辑笔记' : '新建笔记'))

// 整个编辑器容器的背景色 + 文字色
const editorStyle = computed(() => ({
  background: effectiveBg.value,
  color: textColor.value
}))

// 编辑区/预览区的背景色（透明，让父容器的背景色透出）
const areaStyle = computed(() => ({
  background: 'transparent',
  color: textColor.value
}))

async function loadData() {
  const [cats, tgs] = await Promise.all([http.get('/categories'), http.get('/tags')])
  categories.value = cats
  tags.value = tgs

  // 确保用户信息已加载（含 defaultNoteColor）
  if (!auth.user) {
    try { await auth.fetchUser() } catch { /* ignore */ }
  }

  // =============== P0-1: 草稿恢复检测 ===============
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

  // 如果存在草稿，且是新建模式 / 草稿比服务器更新 → 询问恢复
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
        // 用户点恢复 → 应用草稿
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
        // 用户点丢弃 → 清除草稿
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

/**
 * 格式化 KaTeX 公式，重点处理 $ 两侧的空格问题：
 * 1. 块级公式 $$...$$：确保定界符单独成行，内容首尾去空白
 * 2. 行内公式 $...$：在 $ 外侧补空格（文字$x$文字 → 文字 $x$ 文字）
 */
function formatFormulas() {
  let text = form.content
  if (!text || !text.trim()) {
    showToast('没有内容可格式化')
    return
  }

  const original = text

  // 1. 块级公式 $$...$$：定界符单独成行，内容首尾去空白
  text = text.replace(/\$\$([\s\S]*?)\$\$/g, (match, inner, offset, full) => {
    const formula = inner.trim()
    const beforeChar = offset > 0 ? full[offset - 1] : ''
    const afterChar = offset + match.length < full.length ? full[offset + match.length] : ''
    const needNewlineBefore = beforeChar && beforeChar !== '\n'
    const needNewlineAfter = afterChar && afterChar !== '\n'
    return `${needNewlineBefore ? '\n' : ''}$$\n${formula}\n$$${needNewlineAfter ? '\n' : ''}`
  })

  // 2. 行内公式 $...$：在 $ 外侧补空格
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

function triggerUpload() {
  if (!isEdit.value) {
    showToast('请先保存笔记后再插入图片')
    return
  }
  // 桌面端/移动端分别有两个 input ref，按当前端触发
  const target = isDesktop.value ? fileInput.value : mobileFileInput.value
  target?.click()
}

async function onFileChange(e) {
  const file = e.target.files?.[0]
  if (!file) return
  try {
    const fd = new FormData()
    fd.append('file', file)
    const res = await http.post(`/notes/${noteId.value}/attachments`, fd)
    insert(`\n![${res.fileName}](/api/attachments/${res.id})\n`)
    showToast('图片已插入')
  } catch {
    // 错误已由拦截器提示
  } finally {
    e.target.value = ''
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
    if (isEdit.value) {
      await http.put(`/notes/${noteId.value}`, payload)
      showToast('已保存')
    } else {
      const created = await http.post('/notes', payload)
      showToast('已创建')
      // 创建成功后清除"新建草稿"，跳转到编辑页时会走新 key 的草稿逻辑
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
  // 有未保存草稿 → 询问
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

onMounted(() => {
  updateDocumentTitle()
  loadData()
})

onUnmounted(() => {
  // 离开页面时立即把当前内存中的表单存一次草稿（不再等防抖）
  saveDraft()
  // 恢复默认 title
  document.title = '笔记'
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
  /* 移动端吸底工具栏占位 */
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
  bottom: 0;
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
}
.mobile-toolbar-scroll {
  display: flex;
  gap: 6px;
  overflow-x: auto;
  width: 100%;
  /* iOS 滚动弹性 */
  -webkit-overflow-scrolling: touch;
}
.mobile-toolbar-scroll::-webkit-scrollbar { display: none; }

/* textarea 包装 + 字数统计定位容器 */
.edit-area {
  display: flex;
  flex-direction: column;
  flex: 1;
  overflow: hidden;
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

/* 移动端 textarea 底部预留吸底工具栏高度，避免被遮挡 */
@media (max-width: 1023px) {
  .content-area {
    /* 吸底工具栏约 60px + 安全区 + 字数统计区 */
    padding-bottom: 90px;
  }
  .editor {
    /* 工具栏是 sticky，不占位，这里给整个 editor-body 留底部空间防止预览被挡 */
    padding-bottom: 0;
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

/* 深色背景下：移动端吸底工具栏调整半透明底色的颜色偏向 */
:deep(.page) .mobile-toolbar {
  /* 由父容器背景色通过 JS 设置？这里保持一个合理的默认浅色毛玻璃；
     具体主题色通过 CSS 变量注入太复杂，保留 0.94 白+模糊，在大多数笔记颜色上都能看清 */
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
    /* 桌面端不贴吸底工具栏，底部 padding 给字数统计留空间即可 */
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
