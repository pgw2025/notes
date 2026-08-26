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

      <div v-if="list.length" class="category-grid">
        <div
          v-for="c in list"
          :key="c.id"
          class="cat-card"
          @click="goNotes(c.id)"
        >
          <div class="cat-card-header">
            <div class="cat-card-icon">📁</div>
            <div class="cat-card-actions" @click.stop>
              <button class="cat-btn" title="编辑" @click="onEdit(c)">
                <van-icon name="edit" size="14" />
              </button>
              <button class="cat-btn btn-del" title="删除" @click="onDelete(c)">
                <van-icon name="delete-o" size="14" />
              </button>
            </div>
          </div>

          <div class="cat-card-body">
            <div class="cat-card-name">{{ c.name }}</div>
            <div class="cat-card-count">{{ c.noteCount }} 篇笔记</div>
          </div>

          <div class="cat-card-footer">
            <span>点击查看笔记</span>
            <van-icon name="arrow" size="12" />
          </div>
        </div>
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
      message: `确定删除分类「${c.name}」吗？分类下的笔记将被移至未分类。`
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

.category-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(160px, 1fr));
  gap: 12px;
}

.cat-card {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 12px;
  padding: 14px;
  box-shadow: var(--shadow-xs);
  cursor: pointer;
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  min-height: 120px;
  transition: all 0.2s ease;
}

.cat-card:hover {
  transform: translateY(-2px);
  box-shadow: var(--shadow-sm);
  border-color: var(--color-primary);
}

.cat-card-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 10px;
}

.cat-card-icon {
  font-size: 24px;
}

.cat-card-actions {
  display: flex;
  gap: 4px;
}

.cat-btn {
  width: 26px;
  height: 26px;
  border-radius: 6px;
  border: none;
  background: var(--surface-2);
  color: var(--text-secondary);
  display: inline-flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  transition: all 0.15s ease;
}

.cat-btn:hover {
  background: var(--surface-3);
  color: var(--text-primary);
}

.cat-btn.btn-del:hover {
  background: rgba(239, 68, 68, 0.15);
  color: var(--color-danger);
}

.cat-card-name {
  font-size: 15px;
  font-weight: 600;
  color: var(--text-primary);
  margin-bottom: 4px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.cat-card-count {
  font-size: 12px;
  color: var(--text-tertiary);
}

.cat-card-footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-top: 12px;
  padding-top: 8px;
  border-top: 1px dashed var(--border);
  font-size: 11.5px;
  color: var(--color-primary);
  font-weight: 500;
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
  margin-bottom: 16px;
}

.form-actions {
  padding-top: 4px;
}

/* 桌面端：居中阅读宽度 */
@media (min-width: 1024px) {
  .page {
    max-width: 900px;
    margin: 0 auto;
    padding-bottom: 32px;
  }
  .category-grid {
    grid-template-columns: repeat(3, 1fr);
    gap: 16px;
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
