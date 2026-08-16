<template>
  <div class="page">
    <van-nav-bar
      :title="isEdit ? '编辑笔记' : '新建笔记'"
      left-arrow
      @click-left="onBack"
    >
      <template #right>
        <van-button size="mini" type="primary" :loading="saving" @click="onSave">保存</van-button>
      </template>
    </van-nav-bar>

    <div class="editor">
      <van-field
        v-model="form.title"
        placeholder="标题"
        class="title-field"
        maxlength="200"
      />

      <div class="meta-row">
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

      <div class="toolbar-wrap">
        <van-tabs v-model:active="mode" shrink>
          <van-tab title="编辑" name="edit" />
          <van-tab title="预览" name="preview" />
        </van-tabs>
      </div>

      <div v-show="mode === 'edit'" class="edit-area">
        <div class="toolbar">
          <van-button size="small" plain @click="insert('# ', '', '标题')">H</van-button>
          <van-button size="small" plain @click="insert('**', '**', '粗体')"><b>B</b></van-button>
          <van-button size="small" plain @click="insert('*', '*', '斜体')"><i>I</i></van-button>
          <van-button size="small" plain @click="insert('- ', '', '列表项')">•</van-button>
          <van-button size="small" plain @click="insert('\n```\n', '\n```\n', '代码')">{ }</van-button>
          <van-button size="small" plain @click="insert('[', '](https://)', '链接')">链接</van-button>
          <van-button size="small" plain type="primary" @click="triggerUpload">图片</van-button>
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

      <div v-show="mode === 'preview'" class="preview-area">
        <markdown-body :content="form.content || '*暂无内容*'" />
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
  </div>
</template>

<script setup>
import { ref, reactive, computed, onMounted, nextTick } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { showToast, showConfirmDialog } from 'vant'
import http from '../api/http'
import MarkdownBody from '../components/MarkdownBody.vue'

const route = useRoute()
const router = useRouter()

const noteId = computed(() => route.params.id)
const isEdit = computed(() => !!noteId.value)

const form = reactive({
  title: '',
  content: '',
  categoryId: null,
  tagIds: []
})
const categories = ref([])
const tags = ref([])
const mode = ref('edit')
const saving = ref(false)
const showCategoryPicker = ref(false)
const showTagPicker = ref(false)
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

async function loadData() {
  const [cats, tgs] = await Promise.all([http.get('/categories'), http.get('/tags')])
  categories.value = cats
  tags.value = tgs

  if (isEdit.value) {
    const note = await http.get(`/notes/${noteId.value}`)
    form.title = note.title
    form.content = note.content
    form.categoryId = note.categoryId
    // 把标签名映射为 id
    form.tagIds = (note.tags || [])
      .map((name) => tags.value.find((t) => t.name === name)?.id)
      .filter(Boolean)
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
      tagIds: form.tagIds
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
  background: #fff;
}
.toolbar {
  display: flex;
  gap: 6px;
  padding: 8px 12px;
  overflow-x: auto;
  background: #fff;
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
  background: #fff;
}
.preview-area {
  padding: 14px 16px;
  background: #fff;
  min-height: 50vh;
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
</style>
