<template>
  <div class="page" :class="{ 'page--desktop': isDesktop }">
    <van-nav-bar
      title="回收站"
      left-text="返回"
      left-arrow
      @click-left="goBack"
    />

    <!-- 空状态 -->
    <van-empty v-if="!loading && notes.length === 0" description="回收站是空的" />

    <!-- 列表 -->
    <div v-else class="trash-list">
      <div
        v-for="n in notes"
        :key="n.id"
        class="trash-card"
        :style="{ background: n.backgroundColor || undefined }"
      >
        <div class="trash-card__header">
          <span class="trash-card__title">{{ n.title || '无标题' }}</span>
          <span class="trash-card__time">{{ formatDeletedAt(n.deletedAt) }}</span>
        </div>
        <div v-if="n.contentPreview" class="trash-card__preview">
          {{ n.contentPreview }}
        </div>
        <div class="trash-card__meta">
          <span v-if="n.categoryName" class="trash-card__cat">
            <van-icon name="apps-o" size="12" /> {{ n.categoryName }}
          </span>
          <span v-for="t in n.tags" :key="t" class="trash-card__tag">{{ t }}</span>
        </div>
        <div class="trash-card__actions">
          <van-button size="small" plain type="primary" :loading="restoringId === n.id" @click="onRestore(n)">
            恢复
          </van-button>
          <van-button size="small" plain type="danger" :loading="deletingId === n.id" @click="onPermanentDelete(n)">
            彻底删除
          </van-button>
        </div>
      </div>

      <!-- 分页 -->
      <div v-if="hasMore" class="trash-load-more">
        <van-button size="small" plain :loading="loading" @click="loadMore">加载更多</van-button>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { showConfirmDialog, showToast } from 'vant'
import http from '../api/http'
import { useResponsive } from '../composables/useResponsive'

const router = useRouter()
const { isDesktop } = useResponsive()

const notes = ref([])
const loading = ref(false)
const page = ref(1)
const pageSize = 20
const hasMore = ref(false)
const restoringId = ref(null)
const deletingId = ref(null)

function goBack() {
  if (window.history.length > 1) {
    router.back()
  } else {
    router.replace('/notes')
  }
}

async function fetchTrash(reset = false) {
  loading.value = true
  try {
    const res = await http.get('/notes/trash', { params: { page: page.value, pageSize } })
    if (reset) {
      notes.value = res.items || []
    } else {
      notes.value = notes.value.concat(res.items || [])
    }
    hasMore.value = !!res.hasMore
  } catch {
    // 错误已由 http 拦截器统一提示
  } finally {
    loading.value = false
  }
}

function loadMore() {
  page.value += 1
  fetchTrash(false)
}

async function onRestore(n) {
  try {
    await showConfirmDialog({
      title: '恢复笔记',
      message: `确定恢复「${n.title || '无标题'}」吗？`
    })
    restoringId.value = n.id
    await http.post(`/notes/${n.id}/restore`)
    notes.value = notes.value.filter((x) => x.id !== n.id)
    showToast('已恢复')
  } catch {
    // 取消或失败
  } finally {
    restoringId.value = null
  }
}

async function onPermanentDelete(n) {
  try {
    await showConfirmDialog({
      title: '彻底删除',
      message: `「${n.title || '无标题'}」将被永久删除且无法恢复，确定吗？`
    })
    deletingId.value = n.id
    await http.delete(`/notes/${n.id}/permanent`)
    notes.value = notes.value.filter((x) => x.id !== n.id)
    showToast('已彻底删除')
  } catch {
    // 取消或失败
  } finally {
    deletingId.value = null
  }
}

function formatDeletedAt(dt) {
  if (!dt) return ''
  const d = new Date(dt)
  const now = new Date()
  const diff = now - d
  const minute = 60 * 1000
  const hour = 60 * minute
  const day = 24 * hour
  if (diff < minute) return '刚刚'
  if (diff < hour) return `${Math.floor(diff / minute)} 分钟前`
  if (diff < day) return `${Math.floor(diff / hour)} 小时前`
  if (diff < 30 * day) return `${Math.floor(diff / day)} 天前`
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

onMounted(() => {
  page.value = 1
  fetchTrash(true)
})
</script>

<style scoped>
.page {
  min-height: 100vh;
  padding-bottom: 90px;
  background: var(--app-bg);
}

.trash-list {
  padding: 12px;
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.trash-card {
  border: 1px solid var(--border);
  border-radius: 12px;
  padding: 14px;
  box-shadow: var(--shadow-xs);
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.trash-card__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
}

.trash-card__title {
  font-size: 15px;
  font-weight: 700;
  color: var(--text-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.trash-card__time {
  font-size: 12px;
  color: var(--text-tertiary);
  flex-shrink: 0;
}

.trash-card__preview {
  font-size: 13px;
  color: var(--text-secondary);
  line-height: 1.5;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.trash-card__meta {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 6px;
}

.trash-card__cat {
  display: inline-flex;
  align-items: center;
  gap: 3px;
  font-size: 12px;
  color: var(--text-tertiary);
}

.trash-card__tag {
  font-size: 11px;
  color: var(--text-tertiary);
  background: var(--surface-2);
  padding: 2px 8px;
  border-radius: 999px;
}

.trash-card__actions {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
  margin-top: 4px;
}

.trash-load-more {
  display: flex;
  justify-content: center;
  padding: 12px 0;
}

/* 桌面端：居中限宽 */
@media (min-width: 1024px) {
  .page--desktop {
    padding-bottom: 40px;
  }
  .trash-list {
    max-width: 720px;
    margin: 0 auto;
  }
}
</style>
