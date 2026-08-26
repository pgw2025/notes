<template>
  <div class="page">
    <van-nav-bar title="标签管理" left-arrow @click-left="$router.back()">
      <template #right>
        <van-icon name="plus" size="20" class="nav-plus" @click="onAdd" title="新建标签" />
      </template>
    </van-nav-bar>

    <div class="tag-content">
      <div class="tag-intro">
        <div class="intro-text">
          <h2>标签库</h2>
          <p>共 {{ list.length }} 个标签，点击标签可查看相关笔记</p>
        </div>
        <van-button size="small" type="primary" round icon="plus" @click="onAdd">
          新建标签
        </van-button>
      </div>

      <div v-if="list.length" class="tag-cloud">
        <div
          v-for="t in list"
          :key="t.id"
          class="tag-pill"
          @click="goSearchTag(t.name)"
        >
          <span class="tag-hash">#</span>
          <span class="tag-name">{{ t.name }}</span>
          <span class="tag-count">{{ t.noteCount }}</span>
          <button
            class="tag-del-btn"
            title="删除标签"
            @click.stop="onDelete(t)"
          >
            <van-icon name="cross" size="12" />
          </button>
        </div>
      </div>

      <div v-else class="empty-state">
        <div class="empty-icon">🏷️</div>
        <p class="empty-title">暂无标签</p>
        <p class="empty-desc">添加标签可以帮你更自由地检索笔记</p>
        <van-button type="primary" round icon="plus" size="small" @click="onAdd">
          创建第一个标签
        </van-button>
      </div>
    </div>

    <van-dialog
      v-model:show="showForm"
      title="新建标签"
      show-cancel-button
      :before-close="onBeforeClose"
    >
      <div class="dialog-field-box">
        <van-field
          v-model="formName"
          placeholder="请输入标签名称（例如：读书笔记、算法）"
          maxlength="30"
          clearable
          autofocus
        />
      </div>
    </van-dialog>
  </div>
</template>

<script setup>
import { ref, onActivated, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { showConfirmDialog, showToast } from 'vant'
import http from '../api/http'
import { useTagStore } from '../stores/tag'

const router = useRouter()
const tagStore = useTagStore()
const list = ref([])
const showForm = ref(false)
const formName = ref('')

async function load() {
  list.value = await http.get('/tags')
  tagStore.fetchTags()
}

function onAdd() {
  formName.value = ''
  showForm.value = true
}

function goSearchTag(tagName) {
  router.push({ path: '/notes', query: { tagId: list.value.find((t) => t.name === tagName)?.id } })
}

async function onBeforeClose(action) {
  if (action !== 'confirm') return true
  const name = formName.value.trim()
  if (!name) {
    showToast('请输入标签名称')
    return false
  }
  try {
    const created = await http.post('/tags', { name })
    showToast('已创建')
    await load()
    tagStore.addTag(created)
    return true
  } catch {
    return false
  }
}

async function onDelete(t) {
  try {
    await showConfirmDialog({
      title: '删除标签',
      message: `确定删除标签「#${t.name}」吗？已使用此标签的笔记将移除该标签。`
    })
    await http.delete(`/tags/${t.id}`)
    list.value = list.value.filter((x) => x.id !== t.id)
    tagStore.removeTag(t.id)
    showToast('已删除')
  } catch {
    // 取消
  }
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

.tag-content {
  padding: 16px;
}

.tag-intro {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 20px;
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

.tag-cloud {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
}

.tag-pill {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 7px 12px;
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 20px;
  font-size: 14px;
  color: var(--text-primary);
  cursor: pointer;
  box-shadow: var(--shadow-xs);
  transition: all 0.2s ease;
}

.tag-pill:hover {
  border-color: var(--color-primary);
  transform: translateY(-2px);
  box-shadow: var(--shadow-sm);
  background: var(--surface-2);
}

.tag-hash {
  color: var(--color-primary);
  font-weight: 700;
}

.tag-name {
  font-weight: 500;
}

.tag-count {
  font-size: 11px;
  background: var(--surface-3);
  color: var(--text-secondary);
  border-radius: 10px;
  padding: 0 6px;
  line-height: 18px;
  font-weight: 600;
}

.tag-del-btn {
  background: transparent;
  border: none;
  padding: 0;
  margin-left: 2px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  color: var(--text-tertiary);
  cursor: pointer;
  border-radius: 50%;
  width: 16px;
  height: 16px;
  transition: all 0.15s ease;
}

.tag-del-btn:hover {
  background: rgba(239, 68, 68, 0.15);
  color: var(--color-danger);
}

.dialog-field-box {
  padding: 12px 16px 20px;
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

/* 桌面端：居中阅读宽度 */
@media (min-width: 1024px) {
  .page {
    max-width: 820px;
    margin: 0 auto;
    padding-bottom: 32px;
  }
}
</style>
