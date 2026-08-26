<template>
  <div class="page">
    <van-nav-bar :title="currentCategoryName" class="top-nav-bar">
      <template #left>
        <!-- 移动端：菜单按钮唤起抽屉 -->
        <van-icon name="bars" size="20" class="mobile-only menu-btn" @click="showDrawer = true" />
      </template>
      <template #right>
        <div class="nav-right-actions">
          <van-icon name="search" size="20" class="nav-search-btn" @click="$router.push('/search')" title="搜索" />
          <van-icon name="plus" size="22" class="nav-plus-btn" @click="$router.push('/notes/new')" title="新建笔记" />
        </div>
      </template>
    </van-nav-bar>

    <div class="layout">
      <!-- 桌面端：常驻侧边栏（分类与标签） -->
      <aside class="sidebar desktop-only">
        <CategoryNav
          v-model="activeCategoryId"
          v-model:tagModelValue="activeTagId"
          :categories="categories"
          :tags="tags"
        />
      </aside>

      <main class="content">
        <!-- 顶部信息摘要条 -->
        <div class="content-header">
          <div class="header-left">
            <h2 class="view-title">{{ currentCategoryName }}</h2>
            <span class="view-count">{{ notes.length }} 篇笔记</span>
          </div>
          <div class="header-right">
            <router-link to="/notes/new" class="quick-new-link">
              <van-icon name="plus" size="14" />
              <span>写笔记</span>
            </router-link>
          </div>
        </div>

        <van-pull-refresh v-model="refreshing" @refresh="onRefresh">
          <van-list
            v-model:loading="listLoading"
            :finished="listFinished"
            finished-text="没有更多了"
            @load="onLoadMore"
          >
            <!-- 空状态 -->
            <div v-if="!loading && notes.length === 0 && listFinished" class="empty-wrap">
              <div class="empty-icon-box">📝</div>
              <p class="empty-title">还没有笔记</p>
              <p class="empty-sub">记录灵感、待办、会议纪要或学习心得</p>
              <van-button type="primary" round icon="plus" size="small" @click="$router.push('/notes/new')">
                新建第一篇笔记
              </van-button>
            </div>

            <div v-if="notes.length > 0" class="notes-grid">
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
                  <!-- 置顶标签 -->
                  <div v-if="n.isPinned" class="pinned-badge">
                    <van-icon name="star" size="12" />
                    <span>置顶</span>
                  </div>

                  <div class="note-title-row">
                    <div class="note-title">{{ n.title || '无标题' }}</div>
                    
                    <!-- 桌面端悬浮操作按钮组 -->
                    <div class="card-hover-actions">
                      <button
                        class="card-action-btn"
                        :class="{ 'is-active': n.isPinned }"
                        :title="n.isPinned ? '取消置顶' : '置顶'"
                        @click.stop="onTogglePin(n)"
                      >
                        <van-icon :name="n.isPinned ? 'star' : 'star-o'" size="15" />
                      </button>
                      <button
                        class="card-action-btn"
                        title="编辑"
                        @click.stop="goEdit(n.id)"
                      >
                        <van-icon name="edit" size="15" />
                      </button>
                      <button
                        class="card-action-btn btn-danger"
                        title="删除"
                        @click.stop="onDelete(n)"
                      >
                        <van-icon name="delete-o" size="15" />
                      </button>
                    </div>
                  </div>

                  <div class="note-preview">{{ n.contentPreview || '暂无内容' }}</div>

                  <div class="note-meta">
                    <div class="tags-row">
                      <span v-if="n.categoryName" class="meta-tag cat-tag" :style="tagStyle(n)">
                        📁 {{ n.categoryName }}
                      </span>
                      <span
                        v-for="t in n.tags"
                        :key="t"
                        class="meta-tag"
                        :style="tagStyle(n)"
                      >
                        #{{ t }}
                      </span>
                    </div>
                    <span class="note-time" :style="{ color: timeColor(n) }">{{ formatTime(n.updatedAt) }}</span>
                  </div>
                </div>

                <template #right>
                  <van-button
                    square
                    type="primary"
                    text="编辑"
                    class="swipe-action-btn edit-btn"
                    @click.stop="goEdit(n)"
                  />
                  <van-button
                    square
                    type="warning"
                    :text="n.isPinned ? '取消置顶' : '置顶'"
                    class="swipe-action-btn pin-btn"
                    @click.stop="onTogglePin(n)"
                  />
                  <van-button
                    square
                    type="danger"
                    text="删除"
                    class="swipe-action-btn del-btn"
                    @click.stop="onDelete(n)"
                  />
                </template>
              </van-swipe-cell>
            </div>
          </van-list>
        </van-pull-refresh>
      </main>
    </div>

    <!-- 移动端：抽屉（包含分类与标签） -->
    <van-popup
      v-model:show="showDrawer"
      position="left"
      :style="{ width: '75%', maxWidth: '320px', height: '100%' }"
    >
      <div class="drawer-inner">
        <div class="drawer-header">
          <span class="drawer-title">分类与标签</span>
          <van-icon name="cross" size="18" @click="showDrawer = false" />
        </div>
        <CategoryNav
          v-model="activeCategoryId"
          v-model:tagModelValue="activeTagId"
          :categories="categories"
          :tags="tags"
        />
      </div>
    </van-popup>
  </div>
</template>

<script setup>
import { ref, onActivated, onMounted, computed, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { showConfirmDialog, showToast } from 'vant'
import http from '../api/http'
import { formatTime } from '../utils/format'
import { resolveNoteColor, getContrastColor, isDarkColor } from '../utils/color'
import { useAuthStore } from '../stores/auth'
import { useThemeStore } from '../stores/theme'
import CategoryNav from '../components/CategoryNav.vue'

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
const tags = ref([])
// null = 全部分类
const activeCategoryId = ref(null)
// null = 不按标签筛选
const activeTagId = ref(null)
const showDrawer = ref(false)
const refreshing = ref(false)
const loading = ref(false)
const listLoading = ref(false)
const listFinished = ref(false)
const currentPage = ref(1)
const pageSize = 20

// 导航栏标题：当前选中分类/标签名，全部时显示「笔记」
const currentCategoryName = computed(() => {
  if (activeTagId.value != null) {
    const foundTag = tags.value.find((t) => t.id === activeTagId.value)
    return foundTag ? `#${foundTag.name}` : '标签笔记'
  }
  if (activeCategoryId.value == null) return '笔记'
  return categories.value.find((c) => c.id === activeCategoryId.value)?.name ?? '笔记'
})

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
    // 常用分类（笔记数多）排前面，便于快速定位
    categories.value = (await http.get('/categories')).sort((a, b) => b.noteCount - a.noteCount)
  } catch {
    // 忽略
  }
}

async function loadTags() {
  try {
    tags.value = (await http.get('/tags')).sort((a, b) => (b.noteCount || 0) - (a.noteCount || 0))
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
  const categoryId = activeCategoryId.value ?? undefined
  const tagId = activeTagId.value ?? undefined
  const res = await http.get('/notes', { params: { categoryId, tagId, page: currentPage.value, pageSize } })
  notes.value.push(...res.items)
  if (!res.hasMore) listFinished.value = true
}

// 侧栏 / 抽屉选择分类：v-model 更新后触发加载
watch(activeCategoryId, (val) => {
  if (val != null) {
    activeTagId.value = null
  }
  showDrawer.value = false // 移动端选中后自动收起抽屉
  loadNotes()
  // 同步到 URL，刷新 / 分享时保持所选分类
  syncUrl()
})

// 侧栏 / 抽屉选择标签：v-model 更新后触发加载
watch(activeTagId, (val) => {
  if (val != null) {
    activeCategoryId.value = null
  }
  showDrawer.value = false
  loadNotes()
  syncUrl()
})

function syncUrl() {
  const query = {}
  if (activeCategoryId.value) query.categoryId = activeCategoryId.value
  if (activeTagId.value) query.tagId = activeTagId.value
  router.replace({ query })
}

// 监听路由 query 变化（从左侧主导航栏或外部点击跳转时）
watch(
  () => route.query,
  (newQ) => {
    const qCat = newQ.categoryId ? Number(newQ.categoryId) : null
    const qTag = newQ.tagId ? Number(newQ.tagId) : null
    if (qCat !== activeCategoryId.value || qTag !== activeTagId.value) {
      activeCategoryId.value = qCat
      activeTagId.value = qTag
    }
  }
)

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

function onRefresh() {
  Promise.all([loadCategories(), loadTags(), loadNotes()]).finally(() => {
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
  await Promise.all([loadCategories(), loadTags()])
  const qCatId = Number(route.query.categoryId)
  const qTagId = Number(route.query.tagId)

  if (qCatId && categories.value.some((c) => c.id === qCatId)) {
    activeCategoryId.value = qCatId
  } else if (qTagId && tags.value.some((t) => t.id === qTagId)) {
    activeTagId.value = qTagId
  } else {
    await loadNotes()
  }
  initialized = true
})

onActivated(() => {
  if (!initialized) return
  // 从编辑页返回时刷新
  loadCategories()
  loadTags()
  loadNotes()
})
</script>

<style scoped>
.page {
  min-height: 100vh;
  padding-bottom: 80px;
  background: var(--app-bg);
}

.top-nav-bar {
  background: var(--surface);
}

.mobile-only {
  display: inline-flex;
}
.desktop-only {
  display: none;
}

.menu-btn {
  color: var(--text-primary);
  cursor: pointer;
}

.nav-right-actions {
  display: flex;
  align-items: center;
  gap: 14px;
}

.nav-search-btn, .nav-plus-btn {
  color: var(--text-primary);
  cursor: pointer;
  transition: transform 0.15s ease, color 0.15s ease;
}

.nav-search-btn:hover, .nav-plus-btn:hover {
  color: var(--color-primary);
  transform: scale(1.1);
}

/* 顶部信息摘要条 */
.content-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 16px 16px 8px;
}

.header-left {
  display: flex;
  align-items: baseline;
  gap: 10px;
}

.view-title {
  font-size: 18px;
  font-weight: 700;
  color: var(--text-primary);
  margin: 0;
}

.view-count {
  font-size: 12px;
  color: var(--text-tertiary);
  font-weight: 500;
}

.quick-new-link {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 5px 12px;
  border-radius: 20px;
  background: rgba(59, 130, 246, 0.1);
  color: var(--color-primary);
  font-size: 12.5px;
  font-weight: 600;
  text-decoration: none;
  transition: all 0.15s ease;
}

.quick-new-link:hover {
  background: var(--color-primary);
  color: #fff;
}

/* 空状态 */
.empty-wrap {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 60px 20px;
  text-align: center;
}

.empty-icon-box {
  font-size: 48px;
  margin-bottom: 12px;
  filter: drop-shadow(0 4px 12px rgba(0,0,0,0.06));
}

.empty-title {
  font-size: 16px;
  font-weight: 600;
  color: var(--text-primary);
  margin: 0 0 6px;
}

.empty-sub {
  font-size: 13px;
  color: var(--text-tertiary);
  margin: 0 0 20px;
  max-width: 280px;
}

/* 笔记卡片容器与网格 */
.notes-grid {
  padding: 8px 16px 16px;
}

.notes-grid-item {
  margin-bottom: 12px;
  border-radius: 12px;
  overflow: hidden;
  box-shadow: var(--shadow-xs);
  border: 1px solid var(--border);
  position: relative;
  transition: all 0.2s cubic-bezier(0.4, 0, 0.2, 1);
}

.notes-grid-item.is-pinned {
  border-color: rgba(251, 191, 36, 0.5);
  box-shadow: 0 2px 10px rgba(251, 191, 36, 0.12);
}

.note-card {
  padding: 16px;
  position: relative;
  min-height: 110px;
  display: flex;
  flex-direction: column;
  justify-content: space-between;
}

/* 置顶标签 */
.pinned-badge {
  display: inline-flex;
  align-items: center;
  gap: 3px;
  position: absolute;
  top: 12px;
  right: 12px;
  background: linear-gradient(135deg, #fbbf24, #f59e0b);
  color: #78350f;
  font-size: 10.5px;
  font-weight: 700;
  padding: 2px 7px;
  border-radius: 12px;
  box-shadow: 0 2px 6px rgba(245, 158, 11, 0.25);
  z-index: 2;
}

.note-title-row {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 8px;
  margin-bottom: 8px;
}

.note-title {
  flex: 1;
  font-size: 16px;
  font-weight: 700;
  color: inherit;
  line-height: 1.4;
  word-break: break-word;
}

/* 桌面端悬浮操作按钮 */
.card-hover-actions {
  display: none;
  align-items: center;
  gap: 4px;
  opacity: 0;
  transition: opacity 0.15s ease;
}

.card-action-btn {
  width: 26px;
  height: 26px;
  border-radius: 6px;
  border: none;
  background: rgba(0, 0, 0, 0.08);
  color: inherit;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  transition: all 0.15s ease;
}

:global(body.dark) .card-action-btn {
  background: rgba(255, 255, 255, 0.12);
}

.card-action-btn:hover {
  background: rgba(0, 0, 0, 0.18);
  transform: scale(1.08);
}

:global(body.dark) .card-action-btn:hover {
  background: rgba(255, 255, 255, 0.24);
}

.card-action-btn.is-active {
  color: #f59e0b;
}

.card-action-btn.btn-danger:hover {
  background: rgba(239, 68, 68, 0.2);
  color: #ef4444;
}

.note-preview {
  font-size: 13.5px;
  line-height: 1.55;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
  margin-bottom: 12px;
  opacity: 0.78;
}

.note-meta {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 6px;
}

.tags-row {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 5px;
}

.meta-tag {
  font-size: 11px;
  padding: 2px 7px;
  border-radius: 6px;
  background: rgba(0, 0, 0, 0.05);
  color: inherit;
  font-weight: 500;
}

:global(body.dark) .meta-tag {
  background: rgba(255, 255, 255, 0.1);
}

.meta-tag.cat-tag {
  background: rgba(59, 130, 246, 0.12);
  color: var(--color-primary);
  font-weight: 600;
}

.note-time {
  font-size: 11.5px;
  opacity: 0.7;
  font-variant-numeric: tabular-nums;
}

.swipe-action-btn {
  height: 100%;
  border: none;
}

/* 抽屉样式 */
.drawer-inner {
  padding: 16px;
  height: 100%;
  display: flex;
  flex-direction: column;
}

.drawer-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 4px 14px;
  border-bottom: 1px solid var(--border);
  margin-bottom: 12px;
}

.drawer-title {
  font-size: 16px;
  font-weight: 700;
  color: var(--text-primary);
}

/* 桌面端：多列网格 + 居中阅读宽度 + 侧边栏双栏布局 */
@media (min-width: 1024px) {
  .mobile-only {
    display: none;
  }
  .desktop-only {
    display: block;
  }
  .page {
    max-width: 1320px;
    margin: 0 auto;
    padding-bottom: 32px;
  }
  .layout {
    display: flex;
    gap: 20px;
    padding: 16px 24px;
    align-items: flex-start;
  }
  .sidebar {
    width: 240px;
    flex-shrink: 0;
    position: sticky;
    top: 72px;
    max-height: calc(100vh - 90px);
    overflow-y: auto;
  }
  .content {
    flex: 1;
    min-width: 0;
  }
  .notes-grid {
    display: grid;
    grid-template-columns: repeat(2, 1fr);
    gap: 16px;
    padding: 8px 0 24px;
  }
  .notes-grid-item {
    margin-bottom: 0;
    cursor: pointer;
  }
  .notes-grid-item:hover {
    box-shadow: var(--shadow-md);
    transform: translateY(-2px);
    border-color: var(--border-strong);
  }
  .notes-grid-item:hover .card-hover-actions {
    display: inline-flex;
    opacity: 1;
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
