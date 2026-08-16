<template>
  <div class="page">
    <van-nav-bar title="分类">
      <template #right>
        <van-icon name="plus" size="22" @click="onAdd" />
      </template>
    </van-nav-bar>

    <van-cell-group v-if="list.length" inset style="margin-top: 8px">
      <van-swipe-cell v-for="c in list" :key="c.id">
        <van-cell
          :title="c.name"
          :value="`${c.noteCount} 篇`"
          is-link
          @click="goNotes(c.id)"
        />
        <template #right>
          <van-button square text="编辑" type="primary" class="action-btn" @click="onEdit(c)" />
          <van-button square text="删除" type="danger" class="action-btn" @click="onDelete(c)" />
        </template>
      </van-swipe-cell>
    </van-cell-group>

    <van-empty v-else description="还没有分类，点击右上角添加" />

    <van-popup v-model:show="showForm" round position="bottom">
      <div class="form">
        <van-nav-bar :title="editing ? '编辑分类' : '新建分类'" />
        <van-field
          v-model="formName"
          placeholder="请输入分类名称"
          maxlength="50"
          autofocus
        />
        <div class="form-actions">
          <van-button block type="primary" :loading="submitting" @click="onSubmitForm">确定</van-button>
        </div>
      </div>
    </van-popup>
  </div>
</template>

<script setup>
import { ref, onActivated, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { showConfirmDialog, showToast } from 'vant'
import http from '../api/http'

const router = useRouter()
const list = ref([])
const showForm = ref(false)
const formName = ref('')
const editing = ref(null)
const submitting = ref(false)

async function load() {
  list.value = await http.get('/categories')
}

function onAdd() {
  editing.value = null
  formName.value = ''
  showForm.value = true
}

function onEdit(c) {
  editing.value = c
  formName.value = c.name
  showForm.value = true
}

async function onSubmitForm() {
  const name = formName.value.trim()
  if (!name) {
    showToast('请输入分类名称')
    return
  }
  submitting.value = true
  try {
    if (editing.value) {
      await http.put(`/categories/${editing.value.id}`, { name })
      showToast('已更新')
    } else {
      await http.post('/categories', { name })
      showToast('已创建')
    }
    showForm.value = false
    await load()
  } finally {
    submitting.value = false
  }
}

async function onDelete(c) {
  try {
    await showConfirmDialog({
      title: '删除分类',
      message: `确定删除「${c.name}」吗？分类下笔记不会被删除。`
    })
    await http.delete(`/categories/${c.id}`)
    list.value = list.value.filter((x) => x.id !== c.id)
    showToast('已删除')
  } catch {
    // 取消
  }
}

function goNotes(categoryId) {
  router.push({ path: '/notes', query: { categoryId } })
}

onMounted(load)
onActivated(load)
</script>

<style scoped>
.page {
  min-height: 100vh;
  padding-bottom: 80px;
}
.action-btn {
  height: 100%;
}
.form {
  padding-bottom: 16px;
}
.form-actions {
  padding: 12px 16px;
}

/* 桌面端：居中阅读宽度 */
@media (min-width: 1024px) {
  .page {
    max-width: 720px;
    margin: 0 auto;
    padding-bottom: 32px;
  }
}
</style>
