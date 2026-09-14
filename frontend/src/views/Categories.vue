<template>
  <div class="page">
    <van-nav-bar title="分类管理" left-arrow @click-left="$router.back()">
      <template #right>
        <van-icon name="plus" size="20" class="nav-plus" @click="onAdd" title="新建分类" />
      </template>
    </van-nav-bar>

    <div class="cat-content">
      <div class="cat-intro">
        <div class="intro-text">
          <h2>笔记分类</h2>
          <p>合理组织和归档你的知识与笔记</p>
        </div>
        <van-button size="small" type="primary" round icon="plus" @click="onAdd">
          新建分类
        </van-button>
      </div>

      <div v-if="list.length" class="category-tree">
        <CategoryNode
          v-for="c in list"
          :key="c.id"
          :node="c"
          :depth="0"
          :expanded="expandedIds"
          @toggle="toggleExpand"
          @add-child="onAddChild"
          @edit="onEdit"
          @delete="onDelete"
          @open="goNotes"
        />
      </div>

      <div v-else class="empty-state">
        <div class="empty-icon">📁</div>
        <p class="empty-title">暂无分类</p>
        <p class="empty-desc">创建一个分类来整理你的笔记吧</p>
        <van-button type="primary" round icon="plus" size="small" @click="onAdd">
          创建第一个分类
        </van-button>
      </div>
    </div>

    <van-popup v-model:show="showForm" round position="bottom" class="form-popup">
      <div class="form-wrapper">
        <div class="form-header">
          <span class="form-title">{{ editing ? '编辑分类' : '新建分类' }}</span>
          <van-icon name="cross" size="18" @click="showForm = false" />
        </div>
        <van-field
          v-model="formName"
          placeholder="请输入分类名称（例如：工作、生活、读书）"
          maxlength="50"
          autofocus
          class="form-field"
          clearable
        />
        <div v-if="formParentId !== null && formParentId !== undefined" class="form-parent-label">
          <span class="parent-label-text">所属父分类</span>
          <span class="parent-label-value">{{ parentName || '无（顶级分类）' }}</span>
        </div>
        <div class="form-actions">
          <van-button block round type="primary" :loading="submitting" @click="onSubmitForm">
            保存
          </van-button>
        </div>
      </div>
    </van-popup>
  </div>
</template>

<script setup>
import { ref, onActivated, onMounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import { showConfirmDialog, showToast } from 'vant'
import http from '../api/http'
import { findCategoryById } from '../utils/categoryTree'
import CategoryNode from '../components/CategoryNode.vue'

const router = useRouter()
const list = ref([])
const showForm = ref(false)
const formName = ref('')
const formParentId = ref(null)
const editing = ref(null)
const submitting = ref(false)

// 展开的子分类 id 集合（用于折叠/展开）
const expandedIds = ref(new Set())

async function load() {
  list.value = await http.get('/categories')
}

function onAdd() {
  editing.value = null
  formName.value = ''
  formParentId.value = null
  showForm.value = true
}

function onAddChild(parent) {
  editing.value = null
  formName.value = ''
  formParentId.value = parent.id
  showForm.value = true
}

function onEdit(c) {
  editing.value = c
  formName.value = c.name
  formParentId.value = c.parentId ?? null
  showForm.value = true
}

const parentName = computed(() => {
  if (formParentId.value === null || formParentId.value === undefined) return ''
  const p = findCategoryById(list.value, formParentId.value)
  return p ? p.name : ''
})

function toggleExpand(id) {
  const next = new Set(expandedIds.value)
  if (next.has(id)) next.delete(id)
  else next.add(id)
  expandedIds.value = next
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
      await http.put(`/categories/${editing.value.id}`, { name, parentId: formParentId.value })
      showToast('已更新')
    } else {
      await http.post('/categories', { name, parentId: formParentId.value })
      showToast('已创建')
    }
    showForm.value = false
    // 创建/更新后自动展开父分类
    if (formParentId.value != null) {
      const next = new Set(expandedIds.value)
      next.add(formParentId.value)
      expandedIds.value = next
    }
    await load()
  } catch (err) {
    showToast(err?.response?.data?.message || '操作失败')
  } finally {
    submitting.value = false
  }
}

async function onDelete(c) {
  try {
    await showConfirmDialog({
      title: '删除分类',
      message: `确定删除分类「${c.name}」吗？分类下的笔记将被移至未分类。`
    })
    await http.delete(`/categories/${c.id}`)
    showToast('已删除')
    await load()
  } catch (err) {
    // 后端返回的「有子分类」错误提示
    const msg = err?.response?.data?.message
    if (msg) showToast(msg)
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
  background: var(--app-bg);
}

.nav-plus {
  color: var(--text-primary);
  cursor: pointer;
  transition: transform 0.15s ease;
}
.nav-plus:hover {
  transform: scale(1.1);
  color: var(--color-primary);
}

.cat-content {
  padding: 16px;
}

.cat-intro {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 16px;
  padding: 4px 4px 12px;
  border-bottom: 1px solid var(--border);
}

.intro-text h2 {
  font-size: 18px;
  font-weight: 700;
  color: var(--text-primary);
  margin: 0 0 4px;
}

.intro-text p {
  font-size: 13px;
  color: var(--text-tertiary);
  margin: 0;
}

.category-tree {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 60px 20px;
  text-align: center;
}

.empty-icon {
  font-size: 48px;
  margin-bottom: 12px;
}

.empty-title {
  font-size: 16px;
  font-weight: 600;
  color: var(--text-primary);
  margin: 0 0 6px;
}

.empty-desc {
  font-size: 13px;
  color: var(--text-tertiary);
  margin: 0 0 20px;
}

.form-wrapper {
  padding: 16px 20px 24px;
}

.form-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding-bottom: 14px;
  border-bottom: 1px solid var(--border);
  margin-bottom: 16px;
}

.form-title {
  font-size: 16px;
  font-weight: 700;
  color: var(--text-primary);
}

.form-field {
  background: var(--surface-2);
  border-radius: 8px;
  padding: 10px 14px;
  margin-bottom: 12px;
}

.form-parent-label {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 10px 14px;
  background: var(--surface-2);
  border-radius: 8px;
  margin-bottom: 16px;
  font-size: 13px;
}

.parent-label-text {
  color: var(--text-secondary);
}

.parent-label-value {
  color: var(--color-primary);
  font-weight: 600;
}

.form-actions {
  padding-top: 4px;
}

/* 桌面端：紧凑居中宽度 */
@media (min-width: 1024px) {
  .page {
    max-width: none;
    margin: 0 auto;
    padding-bottom: 32px;
  }
  .cat-content {
    max-width: 720px;
    margin: 0 auto;
  }
  :deep(.form-popup) {
    max-width: 480px;
    margin: 0 auto;
    left: 50%;
    transform: translateX(-50%);
    border-radius: 16px 16px 0 0;
  }
}
</style>
