<template>
  <div class="page">
    <van-nav-bar title="笔记">
      <template #right>
        <van-icon name="plus" size="22" @click="$router.push('/notes/new')" />
      </template>
    </van-nav-bar>

    <van-tabs v-model:active="activeTab" sticky @change="onTabChange">
      <van-tab title="全部" />
      <van-tab v-for="c in categories" :key="c.id" :title="c.name" />
    </van-tabs>

    <van-pull-refresh v-model="refreshing" @refresh="onRefresh">
      <div v-if="!loading && notes.length === 0" class="empty">
        <van-empty description="还没有笔记，点击右上角创建" />
      </div>

      <van-cell-group v-else inset class="notes-grid" style="margin-top: 8px">
        <van-swipe-cell
          v-for="n in notes"
          :key="n.id"
          class="notes-grid-item"
        >
          <div
            class="note-card"
            :style="cardStyle(n)"
            @click="goDetail(n.id)"
          >
            <div class="note-title">{{ n.title || '无标题' }}</div>
            <div class="note-preview">{{ n.contentPreview || '暂无内容' }}</div>
            <div class="note-meta">
              <van-tag v-if="n.categoryName" plain type="primary" size="medium" :style="tagStyle(n)"> {{ n.categoryName }}</van-tag>
              <van-tag
                v-for="t in n.tags"
                :key="t"
                plain
                size="medium"
                :style="tagStyle(n)"
              >{{ t }}</van-tag>
              <span class="note-time" :style="{ color: timeColor(n) }">{{ formatTime(n.updatedAt) }}</span>
            </div>
          </div>
          <template #right>
            <van-button square type="danger" text="删除" class="del-btn" @click.stop="onDelete(n)" />
          </template>
        </van-swipe-cell>
      </van-cell-group>
    </van-pull-refresh>
  </div>
</template>

<script setup>
import { ref, onActivated, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { showConfirmDialog, showToast } from 'vant'
import http from '../api/http'
import { formatTime } from '../utils/format'
import { resolveNoteColor, getContrastColor, isDarkColor } from '../utils/color'
import { useAuthStore } from '../stores/auth'

defineOptions({ name: 'NotesList' })

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()

const notes = ref([])
const categories = ref([])
const activeTab = ref(0)
const refreshing = ref(false)
const loading = ref(false)

// 笔记卡片样式：背景色 + 文字色
function cardStyle(n) {
  const bg = resolveNoteColor(n.backgroundColor, auth.user?.defaultNoteColor)
  const color = getContrastColor(bg)
  return {
    background: bg,
    color
  }
}

// Tag 样式：浅色背景 → 默认蓝/灰；深色背景 → 半透明白底白字
function tagStyle(n) {
  const bg = resolveNoteColor(n.backgroundColor, auth.user?.defaultNoteColor)
  if (isDarkColor(bg)) {
    return {
      background: 'rgba(255, 255, 255, 0.2)',
      color: '#FFFFFF',
      borderColor: 'rgba(255, 255, 255, 0.3)'
    }
  }
  return {} // 浅色背景用 Vant 默认样式
}

// 时间文字色：深色背景 → 半透明白；浅色背景 → 浅灰
function timeColor(n) {
  const bg = resolveNoteColor(n.backgroundColor, auth.user?.defaultNoteColor)
  return isDarkColor(bg) ? 'rgba(255, 255, 255, 0.6)' : '#c8c9cc'
}

async function loadCategories() {
  try {
    categories.value = await http.get('/categories')
  } catch {
    // 忽略
  }
}

async function loadNotes() {
  loading.value = true
  try {
    const categoryId = activeTab.value > 0 ? categories.value[activeTab.value - 1]?.id : undefined
    notes.value = await http.get('/notes', { params: { categoryId } })
  } finally {
    loading.value = false
  }
}

function onTabChange() {
  loadNotes()
}

function onRefresh() {
  Promise.all([loadCategories(), loadNotes()]).finally(() => {
    refreshing.value = false
  })
}

function goDetail(id) {
  router.push(`/notes/${id}`)
}

async function onDelete(note) {
  try {
    await showConfirmDialog({ title: '删除笔记', message: `确定删除「${note.title || '无标题'}」吗？` })
    await http.delete(`/notes/${note.id}`)
    notes.value = notes.value.filter((n) => n.id !== note.id)
    showToast('已删除')
  } catch {
    // 取消
  }
}

let initialized = false

onMounted(async () => {
  // 确保用户信息已加载（含 defaultNoteColor），用于卡片背景色 fallback
  if (!auth.user) {
    try { await auth.fetchUser() } catch { /* 未登录时忽略 */ }
  }
  await loadCategories()
  const qid = Number(route.query.categoryId)
  if (qid) {
    const idx = categories.value.findIndex((c) => c.id === qid)
    if (idx >= 0) activeTab.value = idx + 1
  }
  await loadNotes()
  initialized = true
})

onActivated(() => {
  if (!initialized) return
  // 从编辑页返回时刷新
  loadCategories()
  loadNotes()
})
</script>

<style scoped>
.page {
  min-height: 100vh;
  padding-bottom: 80px;
}
.empty {
  padding-top: 40px;
}
.note-card {
  padding: 14px 16px;
}
.note-title {
  font-size: 16px;
  font-weight: 600;
  margin-bottom: 6px;
  /* color 由内联样式控制，浅色背景默认深色 */
  color: inherit;
}
.note-preview {
  font-size: 13px;
  line-height: 1.5;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
  margin-bottom: 8px;
  opacity: 0.7; /* 跟随父级文字色，稍微弱化 */
}
.note-meta {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 6px;
}
.note-time {
  margin-left: auto;
  font-size: 12px;
  /* color 由内联样式控制 */
}
.del-btn {
  height: 100%;
}

/* 移动端：给每条笔记之间加间距，间距透明（让卡片色连成一片） */
.notes-grid-item {
  margin-bottom: 8px;
  border-radius: 8px;
  overflow: hidden;
  /* 不设 background，让 .note-card 的内联背景色直接显示 */
}
.notes-grid-item:last-child {
  margin-bottom: 0;
}

/* 桌面端：多列网格 + 居中阅读宽度 */
@media (min-width: 1024px) {
  .page {
    max-width: 1100px;
    margin: 0 auto;
    padding-bottom: 32px;
  }
  .notes-grid {
    display: grid;
    grid-template-columns: repeat(2, 1fr);
    gap: 14px;
    margin: 16px;
    border-radius: 0;
    background: transparent;
    overflow: visible;
  }
  .notes-grid-item {
    margin-bottom: 0; /* 桌面端用 grid gap 控制间距 */
    border: 1px solid rgba(0, 0, 0, 0.08);
    border-radius: 8px;
    overflow: hidden;
    transition: box-shadow 0.2s, transform 0.2s;
  }
  .notes-grid-item:hover {
    box-shadow: 0 4px 12px rgba(0, 0, 0, 0.08);
    transform: translateY(-2px);
  }
  .note-card {
    cursor: pointer;
    height: 100%;
  }
}

@media (min-width: 1440px) {
  .notes-grid {
    grid-template-columns: repeat(3, 1fr);
  }
}
</style>
