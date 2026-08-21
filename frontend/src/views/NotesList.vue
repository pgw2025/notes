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
      <van-list
        v-model:loading="listLoading"
        :finished="listFinished"
        finished-text=""
        @load="onLoadMore"
      >
      <div v-if="!loading && notes.length === 0 && listFinished" class="empty">
        <van-empty description="还没有笔记，点击右上角创建" />
      </div>

      <van-cell-group v-if="notes.length > 0" inset class="notes-grid" style="margin-top: 8px">
    <van-swipe-cell
      v-for="n in notes"
      :key="n.id"
      class="notes-grid-item"
      :class="{ 'is-pinned': n.isPinned }"
    >
      <div
        class="note-card"
        :style="cardStyle(n)"
        @click="goDetail(n.id)"
      >
        <div class="note-title-row">
          <van-icon v-if="n.isPinned" name="star" color="#ff976a" size="16" class="pin-icon" />
          <div class="note-title">{{ n.title || '无标题' }}</div>
          <van-icon
            :name="n.isPinned ? 'star' : 'star-o'"
            :color="n.isPinned ? '#ff976a' : undefined"
            size="16"
            class="edit-icon pin-action-icon"
            :title="n.isPinned ? '取消置顶' : '置顶'"
            @click.stop="onTogglePin(n)"
          />
          <van-icon
            name="delete-o"
            size="16"
            class="edit-icon del-action-icon"
            title="删除"
            @click.stop="onDelete(n)"
          />
          <van-icon
            name="edit"
            size="16"
            class="edit-icon"
            title="编辑"
            @click.stop="goEdit(n.id)"
          />
        </div>
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
        <van-button
          square
          type="primary"
          text="编辑"
          class="edit-btn"
          @click.stop="goEdit(n)"
        />
        <van-button
          square
          type="warning"
          :text="n.isPinned ? '取消置顶' : '置顶'"
          class="pin-btn"
          @click.stop="onTogglePin(n)"
        />
        <van-button square type="danger" text="删除" class="del-btn" @click.stop="onDelete(n)" />
      </template>
    </van-swipe-cell>
  </van-cell-group>
      </van-list>
    </van-pull-refresh>
  </div>
</template>

<script setup>
import { ref, onActivated, onMounted, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { showConfirmDialog, showToast } from 'vant'
import http from '../api/http'
import { formatTime } from '../utils/format'
import { resolveNoteColor, getContrastColor, isDarkColor } from '../utils/color'
import { useAuthStore } from '../stores/auth'
import { useThemeStore } from '../stores/theme'

defineOptions({ name: 'NotesList' })

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const theme = useThemeStore()

// 使用 computed：auto 模式下当系统从 light→dark（或用户手动切 mode）时，
// 卡片背景色也会实时切换，不需要重新拉列表。
const isDarkEffective = computed(() => theme.isDarkEffective)

const notes = ref([])
const categories = ref([])
const activeTab = ref(0)
const refreshing = ref(false)
const loading = ref(false)
const listLoading = ref(false)
const listFinished = ref(false)
const currentPage = ref(1)
const pageSize = 20

// 笔记卡片样式：背景色 + 文字色
function cardStyle(n) {
  const bg = resolveNoteColor(n.backgroundColor, auth.user?.defaultNoteColor, isDarkEffective.value)
  const color = getContrastColor(bg)
  return {
    background: bg,
    color
  }
}

// Tag 样式：浅色背景 → 默认蓝/灰；深色背景 → 半透明白底白字
function tagStyle(n) {
  const bg = resolveNoteColor(n.backgroundColor, auth.user?.defaultNoteColor, isDarkEffective.value)
  if (isDarkColor(bg)) {
    return {
      background: 'rgba(255, 255, 255, 0.2)',
      color: '#FFFFFF',
      borderColor: 'rgba(255, 255, 255, 0.3)'
    }
  }
  return {} // 浅色背景用 Vant 默认样式
}

// 时间文字色：深色背景 → 半透明白；浅色背景 → 中灰（保证可读性）
function timeColor(n) {
  const bg = resolveNoteColor(n.backgroundColor, auth.user?.defaultNoteColor, isDarkEffective.value)
  return isDarkColor(bg) ? 'rgba(255, 255, 255, 0.75)' : 'var(--text-tertiary)'
}

async function onTogglePin(n) {
  try {
    if (n.isPinned) {
      await http.post(`/notes/${n.id}/unpin`)
      n.isPinned = false
      n.pinnedAt = null
      showToast('已取消置顶')
    } else {
      await http.post(`/notes/${n.id}/pin`)
      n.isPinned = true
      n.pinnedAt = new Date().toISOString()
      showToast('已置顶')
    }
    // 置顶状态变化后需要重新排序（后端会按置顶先排，但前端显示顺序没变）
    // 所以这里直接在前端按相同规则重排
    sortNotesInPlace()
  } catch {
    /* 错误由拦截器提示 */
  }
}

function sortNotesInPlace() {
  notes.value.sort((a, b) => {
    if (a.isPinned !== b.isPinned) return a.isPinned ? -1 : 1
    const pa = a.pinnedAt ? new Date(a.pinnedAt).getTime() : 0
    const pb = b.pinnedAt ? new Date(b.pinnedAt).getTime() : 0
    if (pa !== pb) return pb - pa
    return new Date(b.updatedAt) - new Date(a.updatedAt)
  })
}

async function loadCategories() {
  try {
    categories.value = await http.get('/categories')
  } catch {
    // 忽略
  }
}

function resetList() {
  notes.value = []
  currentPage.value = 1
  listFinished.value = false
}

async function loadNotes() {
  // 首次加载 / 刷新：重置后加载第一页
  loading.value = true
  resetList()
  try {
    await fetchPage()
  } finally {
    loading.value = false
  }
}

async function fetchPage() {
  const categoryId = activeTab.value > 0 ? categories.value[activeTab.value - 1]?.id : undefined
  const res = await http.get('/notes', { params: { categoryId, page: currentPage.value, pageSize } })
  notes.value.push(...res.items)
  if (!res.hasMore) listFinished.value = true
}

async function onLoadMore() {
  try {
    currentPage.value++
    await fetchPage()
  } catch {
    // 失败回退页码
    currentPage.value--
  } finally {
    listLoading.value = false
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

function goEdit(n) {
  const id = typeof n === 'object' ? n.id : n
  router.push(`/notes/${id}/edit`)
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
  background: var(--app-bg);
}
.empty {
  padding-top: 40px;
}
.note-card {
  padding: 14px 16px;
}
.note-title-row {
  display: flex;
  align-items: center;
  gap: 6px;
  margin-bottom: 6px;
}
.pin-icon {
  flex-shrink: 0;
}
.edit-icon {
  flex-shrink: 0;
  width: 28px;
  height: 28px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border-radius: 6px;
  opacity: 0.75;
  transition: background 0.15s, opacity 0.15s, transform 0.15s;
  color: inherit; /* 跟随父级文字色 */
}
.edit-icon:hover {
  background: rgba(128, 128, 128, 0.18);
  opacity: 1;
  transform: translateY(-1px);
}
:global(body.dark) .edit-icon:hover {
  background: rgba(255, 255, 255, 0.12);
}
.edit-icon:active {
  background: rgba(128, 128, 128, 0.32);
  transform: translateY(0);
}
:global(body.dark) .edit-icon:active {
  background: rgba(255, 255, 255, 0.22);
}
/* 卡片顶部：置顶 / 删除 按钮修饰（复用 edit-icon 基础尺寸/hover） */
.pin-action-icon {
  opacity: 0.9;
}
.del-action-icon {
  opacity: 0.8;
  /* 删除按钮：红色系，作为警示语义 —— 亮/暗模式取 color-danger CSS 变量，
     没变量时回退到 ee0a24，避免继承到与背景接近的颜色 */
  color: var(--color-danger, #ee0a24);
}
.del-action-icon:hover {
  /* 亮模式：danger 红淡底；暗模式：叠加下面 dark 覆盖 */
  background: rgba(238, 10, 36, 0.12) !important;
}
:global(body.dark) .del-action-icon:hover {
  background: rgba(255, 59, 71, 0.18) !important;
  color: var(--color-danger, #ff3b47);
}
.del-action-icon:active {
  background: rgba(238, 10, 36, 0.24) !important;
}
:global(body.dark) .del-action-icon:active {
  background: rgba(255, 59, 71, 0.3) !important;
}
.note-title {
  flex: 1;
  font-size: 16px;
  font-weight: 600;
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
.pin-btn {
  height: 100%;
}
.edit-btn {
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
    border: 1px solid var(--border);
    border-radius: 8px;
    overflow: hidden;
    transition: box-shadow 0.2s, transform 0.2s;
  }
  .notes-grid-item:hover {
    box-shadow: var(--shadow-sm);
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
