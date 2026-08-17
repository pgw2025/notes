<template>
  <div class="page">
    <van-nav-bar
      :title="isEdit ? '编辑笔记' : '新建笔记'"
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

      <div class="meta-row" :style="{ borderColor: metaBorderColor }">
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

      <div v-if="!isDesktop" class="toolbar-wrap">
        <van-tabs v-model:active="mode" shrink>
          <van-tab title="编辑" name="edit" />
          <van-tab title="预览" name="preview" />
        </van-tabs>
      </div>

      <div class="editor-body">
        <div v-show="isDesktop || mode === 'edit'" class="edit-area" :style="areaStyle">
          <div class="toolbar">
            <van-button size="small" plain @click="insert('# ', '', '标题')">H</van-button>
            <van-button size="small" plain @click="insert('**', '**', '粗体')"><b>B</b></van-button>
            <van-button size="small" plain @click="insert('*', '*', '斜体')"><i>I</i></van-button>
            <van-button size="small" plain @click="insert('- ', '', '列表项')">•</van-button>
            <van-button size="small" plain @click="insert('\n```\n', '\n```\n', '代码')">{ }</van-button>
            <van-button size="small" plain @click="insert('[', '](https://)', '链接')">链接</van-button>
            <van-button size="small" plain type="primary" @click="triggerUpload">图片</van-button>
            <van-button size="small" plain @click="formatFormulas">格式化</van-button>
            <input
              ref="fileInput"
              type="file"
              accept="image/*"
              style="display: none"
              @change="onFileChange"
            />
          </div>
          <textarea
            ref="textareaRef"
            v-model="form.content"
            class="content-area"
            placeholder="开始记录... 支持 Markdown 语法"
          ></textarea>
        </div>

        <div v-show="isDesktop || mode === 'preview'" class="preview-area" :style="areaStyle">
          <div v-if="isDesktop" class="preview-label" :style="{ color: subTextColor, borderColor: metaBorderColor }">预览</div>
          <markdown-body :content="form.content || '*暂无内容*'" :style="{ color: textColor }" />
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
import { ref, reactive, computed, onMounted, nextTick } from 'vue'
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

  if (isEdit.value) {
    const note = await http.get(`/notes/${noteId.value}`)
    form.title = note.title
    form.content = note.content
    form.categoryId = note.categoryId
    form.isPinned = note.isPinned || false
    // 把标签名映射为 id
    form.tagIds = (note.tags || [])
      .map((name) => tags.value.find((t) => t.name === name)?.id)
      .filter(Boolean)
    // 笔记自身色（API 返回的是 effective color，直接用）
    form.backgroundColor = note.backgroundColor || null
  }
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
  //    (?<![\$\\])  开始 $ 前面不是 $ 或 \（排除 $$ 和转义 \$）
  //    (?!\$)       开始/结束 $ 后面不是 $（排除 $$）
  //    (?<!\$)      结束 $ 前面不是 $（排除 $$）
  //    [^\n]        不跨行，避免误匹配块级公式内部
  text = text.replace(/(?<![\$\\])\$(?!\$)([^\n]+?)(?<!\$)\$(?!\$)/g, (match, _content, offset, full) => {
    // 检查 $ 外侧是否需要补空格
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
  fileInput.value?.click()
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
      backgroundColor: form.backgroundColor || '',  // null/空串表示清除为跟随默认色
      isPinned: form.isPinned
    }
    if (isEdit.value) {
      await http.put(`/notes/${noteId.value}`, payload)
      showToast('已保存')
    } else {
      const created = await http.post('/notes', payload)
      showToast('已创建')
      router.replace(`/notes/${created.id}/edit`)
    }
  } finally {
    saving.value = false
  }
}

async function onBack() {
  if (form.title.trim() || form.content.trim()) {
    try {
      await showConfirmDialog({
        title: '提示',
        message: '尚未保存，确定离开吗？',
        confirmButtonText: '离开',
        cancelButtonText: '继续编辑'
      })
    } catch {
      return
    }
  }
  router.back()
}

onMounted(loadData)
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
}
.title-field :deep(.van-field__control) {
  font-size: 18px;
  font-weight: 600;
}
.meta-row {
  border-top: 1px solid #ebedf0;
}
.toolbar-wrap {
  border-top: 1px solid #ebedf0;
  background: rgba(255, 255, 255, 0.5);
}
.toolbar {
  display: flex;
  gap: 6px;
  padding: 8px 12px;
  overflow-x: auto;
  background: rgba(255, 255, 255, 0.3);
  border-bottom: 1px solid #ebedf0;
}
.toolbar .van-button {
  flex-shrink: 0;
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
}
.content-area::placeholder {
  color: inherit;
  opacity: 0.5;
}
.editor-body {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}
.edit-area {
  display: flex;
  flex-direction: column;
  flex: 1;
  overflow: hidden;
}
.preview-area {
  padding: 14px 16px;
  min-height: 50vh;
}
.preview-label {
  font-size: 12px;
  color: #969799;
  padding: 0 4px 8px;
  border-bottom: 1px solid #ebedf0;
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
    overflow-y: auto;
    min-height: 60vh;
    padding: 14px 20px;
  }
  .content-area {
    min-height: 60vh;
  }
  .toolbar {
    padding: 10px 16px;
    flex-wrap: wrap;
  }
}
</style>
