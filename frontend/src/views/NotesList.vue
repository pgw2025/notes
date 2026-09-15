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
      <!-- 桌面端：三栏骨架（中栏列表 + 右栏预览/快速编辑），组织维度在全局左栏 -->
      <div class="notes-desktop-layout">
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
                <p class="empty-title">{{ emptyTitle }}</p>
                <p class="empty-sub">{{ emptySub }}</p>
                <van-button type="primary" round icon="plus" size="small" @click="$router.push('/notes/new')">
                  新建笔记
                </van-button>
              </div>

              <div v-if="notes.length > 0" class="notes-grid">
                <van-swipe-cell
                  v-for="n in notes"
                  :key="n.id"
                  class="notes-grid-item"
                  :class="{ 'is-pinned': n.isPinned, 'is-selected': n.id === selectedNoteId }"
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

        <!-- 桌面端：右栏预览 / 快速编辑面板 -->
        <aside class="preview-pane" v-if="isDesktop">
          <template v-if="detailLoading">
            <div class="pane-loading">
              <van-loading size="24" color="var(--color-primary)" />
            </div>
          </template>

          <template v-else-if="detailNote">
            <!-- 预览态 -->
            <div v-if="!editMode" class="pane-preview">
              <div class="pane-toolbar">
                <div class="pane-title">{{ detailNote.title || '无标题' }}</div>
                <div class="pane-actions">
                  <button class="pane-btn" title="编辑" @click="enterEdit">
                    <van-icon name="edit" size="16" />
                  </button>
                  <button class="pane-btn" title="全屏阅读" @click="router.push(`/notes/${detailNote.id}`)">
                    <van-icon name="expand-o" size="16" />
                  </button>
                </div>
              </div>
              <div class="pane-meta">
                <span v-if="detailNote.categoryName" class="pane-cat">📁 {{ detailNote.categoryName }}</span>
                <span class="pane-time">{{ formatTime(detailNote.updatedAt) }}</span>
              </div>
              <div class="pane-body">
                <MarkdownBody :content="detailNote.content" :collapsible="true" />
              </div>
            </div>

            <!-- 快速编辑态 -->
            <div v-else class="pane-edit">
              <div class="pane-toolbar">
                <div class="pane-title">快速编辑</div>
                <div class="pane-actions">
                  <button class="pane-btn" title="取消" @click="cancelEdit">
                    <van-icon name="cross" size="16" />
                  </button>
                </div>
              </div>
              <div class="pane-edit-body">
                <input
                  v-model="editTitle"
                  class="pane-edit-title"
                  placeholder="标题"
                />
                <textarea
                  v-model="editContent"
                  class="pane-edit-content"
                  placeholder="使用 Markdown 书写正文..."
                ></textarea>
              </div>
              <div class="pane-edit-footer">
                <van-button size="small" plain @click="cancelEdit">取消</van-button>
                <van-button
                  size="small"
                  type="primary"
                  :loading="saving"
                  @click="saveQuickEdit"
                >保存</van-button>
              </div>
            </div>
          </template>

          <div v-else class="pane-empty">
            <div class="pane-empty-icon">📄</div>
            <p>从左侧选择一篇笔记查看详情</p>
          </div>
        </aside>
      </div>
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
import { ref, onActivated, onDeactivated, onMounted, computed, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { showConfirmDialog, showToast } from 'vant'
import http from '../api/http'
import { formatTime } from '../utils/format'
import { resolveNoteColor, getContrastColor, isDarkColor } from '../utils/color'
import { findCategoryById } from '../utils/categoryTree'
import { useAuthStore } from '../stores/auth'
import { useThemeStore } from '../stores/theme'
import { useResponsive } from '../composables/useResponsive'
import CategoryNav from '../components/CategoryNav.vue'
import MarkdownBody from '../components/MarkdownBody.vue'

defineOptions({ name: 'NotesList' })

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const theme = useThemeStore()
const { isDesktop } = useResponsive()

// 使用 computed：auto 模式下当系统从 light→dark（或用户手动切 mode）时，
// 卡片背景色也会实时切换，不需要重新拉列表。
const isDarkEffective = computed(() => theme.isDarkEffective)

const notes = ref([])
const categories = ref([])
const tags = ref([])
// null = 首页（入口「首页」）
const activeCategoryId = ref(null)
// null = 不按标签筛选
const activeTagId = ref(null)

// 标记：当前属性变更是否正由「路由 query」驱动
let applyingFromRoute = false
// 路由 query 驱动的加载自增序号，用于 onActivated 判断本轮是否已被路由加载过
let routeLoadSeq = 0
// 记录组件上次激活/初始化时 routeLoadSeq 的基线值
let lastRouteLoadSeqAtActivation = 0

// 视图模式：由分类/标签取值派生
// 'home'     = 首页（categoryId 与 tagId 均为 null）：未分类 + 置顶
// 'category' = 按分类
// 'tag'      = 按标签
const viewMode = computed(() => {
  if (activeTagId.value != null) return 'tag'
  if (activeCategoryId.value != null) return 'category'
  return 'home'
})
const showDrawer = ref(false)
const refreshing = ref(false)
const loading = ref(false)
const listLoading = ref(false)
const listFinished = ref(false)
const currentPage = ref(1)
const pageSize = 20

// 导航栏标题：当前选中分类/标签名，首页时显示「首页」
const currentCategoryName = computed(() => {
  if (activeTagId.value != null) {
    const foundTag = tags.value.find((t) => t.id === activeTagId.value)
    return foundTag ? `#${foundTag.name}` : '标签笔记'
  }
  if (activeCategoryId.value == null) return '首页'
  return findCategoryById(categories.value, activeCategoryId.value)?.name ?? '笔记'
})

// 空态文案：按视图区分
const emptyTitle = computed(() => {
  if (viewMode.value === 'home') return '还没有笔记'
  if (viewMode.value === 'tag') return '该标签下暂无笔记'
  return '该分类下暂无笔记'
})
const emptySub = computed(() => {
  if (viewMode.value === 'home') return '置顶的笔记和未分类的笔记会显示在这里'
  return '记录灵感、待办、会议纪要或学习心得'
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
    // 常用分类（笔记数多）排前面，便于快速定位（仅对顶层排序，保留树结构）
    const cats = await http.get('/categories')
    categories.value = (cats || []).sort((a, b) => b.noteCount - a.noteCount)
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
  // 切换视图时清空右栏选中态，待新列表加载后由 fetchPage 重新默认选中
  selectedNoteId.value = null
  detailNote.value = null
  editMode.value = false
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
  const params = { page: currentPage.value, pageSize }
  if (viewMode.value === 'category') params.categoryId = activeCategoryId.value
  if (viewMode.value === 'tag') params.tagId = activeTagId.value
  if (viewMode.value === 'home') params.homeOnly = true
  const res = await http.get('/notes', { params })
  notes.value.push(...res.items)
  if (!res.hasMore) listFinished.value = true
  // 桌面端：首次加载（当前页=1）后默认选中第一篇，右栏立即有内容
  if (isDesktop.value && currentPage.value === 1 && !selectedNoteId.value && notes.value.length > 0) {
    selectNote(notes.value[0].id)
  }
}

// 侧栏 / 抽屉选择分类：v-model 更新后触发加载
// flush:'sync' 使本 watch 在 activeCategoryId 赋值后立即执行，确保 applyingFromRoute 守卫可靠
watch(
  activeCategoryId,
  (val) => {
    // 由路由 query 驱动时，加载统一在 route.query watch 里完成，这里只做状态同步
    if (applyingFromRoute) return
    if (val != null) {
      activeTagId.value = null
    }
    showDrawer.value = false // 移动端选中后自动收起抽屉
    loadNotes()
    // 同步到 URL，刷新 / 分享时保持所选分类
    syncUrl()
    routeLoadSeq++
  },
  { flush: 'sync' }
)

// 侧栏 / 抽屉选择标签：v-model 更新后触发加载
watch(
  activeTagId,
  (val) => {
    if (applyingFromRoute) return
    if (val != null) {
      activeCategoryId.value = null
    }
    showDrawer.value = false
    loadNotes()
    syncUrl()
    routeLoadSeq++
  },
  { flush: 'sync' }
)

function syncUrl() {
  const query = {}
  if (activeCategoryId.value) query.categoryId = activeCategoryId.value
  if (activeTagId.value) query.tagId = activeTagId.value
  router.replace({ query })
}

// 将路由 query 同步到当前筛选状态；若发生筛选变化则由路由统一加载。
// 返回 true 表示筛选被路由驱动变化并已发起加载（routeLoadSeq 也已递增）。
function applyRouteQuery() {
  const qCat = route.query.categoryId ? Number(route.query.categoryId) : null
  const qTag = route.query.tagId ? Number(route.query.tagId) : null
  if (qCat === activeCategoryId.value && qTag === activeTagId.value) return false

  applyingFromRoute = true
  activeCategoryId.value = qCat
  activeTagId.value = qTag
  applyingFromRoute = false
  // 由路由驱动的筛选变化在这里统一加载
  loadNotes()
  routeLoadSeq++
  return true
}

// 从左侧主导航栏或外部点击跳转（携带分类/标签 query）时，query 变化统一在这里处理，
// 避免与 keep-alive 重新激活时的 onActivated 重复触发 loadNotes。
watch(() => route.query, applyRouteQuery)

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
  // 桌面端：右栏就地预览；移动端：跳转详情页
  if (isDesktop.value) {
    selectNote(id)
    return
  }
  router.push(`/notes/${id}`)
}

function goEdit(n) {
  const id = typeof n === 'object' ? n.id : n
  // 桌面端：右栏就地切换为快速编辑
  if (isDesktop.value) {
    selectNote(id, { edit: true })
    return
  }
  router.push(`/notes/${id}/edit`)
}

// ========== 右栏：预览 / 快速编辑双态 ==========
const selectedNoteId = ref(null)
const detailNote = ref(null)
const detailLoading = ref(false)
const editMode = ref(false)
const editTitle = ref('')
const editContent = ref('')
const saving = ref(false)

// 选中并加载笔记详情（可指定是否直接进入编辑态）
async function selectNote(id, opts = {}) {
  if (selectedNoteId.value === id && !opts.edit) {
    // 已选中同一篇且非编辑请求，仅退出编辑态回到预览
    editMode.value = false
    return
  }
  selectedNoteId.value = id
  editMode.value = !!opts.edit
  detailLoading.value = true
  try {
    const d = await http.get(`/notes/${id}`)
    detailNote.value = d
    editTitle.value = d.title || ''
    editContent.value = d.content || ''
    if (opts.edit) {
      // 进入编辑态时，若已删除/加载失败则回退
    }
  } catch {
    detailNote.value = null
  } finally {
    detailLoading.value = false
  }
}

// 进入编辑态（从预览态点「编辑」）
function enterEdit() {
  if (!detailNote.value) return
  editTitle.value = detailNote.value.title || ''
  editContent.value = detailNote.value.content || ''
  editMode.value = true
}

// 取消编辑，回到预览态（丢弃未保存修改）
function cancelEdit() {
  editMode.value = false
  editTitle.value = detailNote.value?.title || ''
  editContent.value = detailNote.value?.content || ''
}

// 保存快速编辑（仅标题 + 正文；分类/标签/颜色原样回传，避免被清空）
async function saveQuickEdit() {
  if (!detailNote.value || saving.value) return
  const d = detailNote.value
  saving.value = true
  try {
    // 标签 name → id 映射（与 NoteEdit 一致），未匹配到的忽略
    const tagIds = (d.tags || [])
      .map((name) => tags.value.find((t) => t.name === name)?.id)
      .filter(Boolean)
    await http.put(`/notes/${d.id}`, {
      title: editTitle.value,
      content: editContent.value,
      categoryId: d.categoryId ?? null,
      tagIds,
      backgroundColor: d.backgroundColor ?? null,
      isPinned: d.isPinned ?? false
    })
    // PUT 返回 204 NoContent，本地同步更新详情与列表项
    const now = new Date().toISOString()
    const preview = makePreview(editContent.value)
    detailNote.value = { ...d, title: editTitle.value, content: editContent.value, updatedAt: now }
    const listItem = notes.value.find((n) => n.id === d.id)
    if (listItem) {
      listItem.title = editTitle.value
      listItem.contentPreview = preview
      listItem.updatedAt = now
    }
    editMode.value = false
    showToast('已保存')
  } catch {
    // 错误由拦截器提示
  } finally {
    saving.value = false
  }
}

// 生成与后端 contentPreview 一致的 120 字预览（去除空白与 Markdown 标记）
function makePreview(content) {
  const text = (content || '')
    .replace(/```[\s\S]*?```/g, ' ')   // 去代码块
    .replace(/[#>*`~\-\[\]()!|]/g, ' ') // 去 Markdown 标记
    .replace(/\s+/g, ' ')
    .trim()
  return text.length > 120 ? text.slice(0, 120) + '…' : text
}

async function onDelete(note) {
  try {
    await showConfirmDialog({ title: '删除笔记', message: `确定删除「${note.title || '无标题'}」吗？` })
    await http.delete(`/notes/${note.id}`)
    notes.value = notes.value.filter((n) => n.id !== note.id)
    // 若删除的是右栏当前选中笔记，清空右栏
    if (selectedNoteId.value === note.id) {
      selectedNoteId.value = null
      detailNote.value = null
      editMode.value = false
    }
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

  if (qCatId && findCategoryById(categories.value, qCatId)) {
    activeCategoryId.value = qCatId
  } else if (qTagId && tags.value.some((t) => t.id === qTagId)) {
    activeTagId.value = qTagId
  } else {
    await loadNotes()
  }
  initialized = true
  lastRouteLoadSeqAtActivation = routeLoadSeq
})

// 离开页面（被 keep-alive 停用）时记录路由加载基线，供 onActivated 判断
// 本轮重新激活是否已被路由 query 驱动加载过，避免重复加载。
onDeactivated(() => {
  lastRouteLoadSeqAtActivation = routeLoadSeq
})

onActivated(() => {
  if (!initialized) return
  loadCategories()
  loadTags()
  // 先同步路由 query（若发生变化则已由 applyRouteQuery 加载，routeLoadSeq 已递增）
  applyRouteQuery()
  // 本轮没有发生路由驱动的筛选变化，说明是“返回同一页”（如从编辑页返回），需要刷新
  if (routeLoadSeq === lastRouteLoadSeqAtActivation) {
    loadNotes()
  }
  lastRouteLoadSeqAtActivation = routeLoadSeq
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
    max-width: none;
    margin: 0 auto;
    padding-bottom: 32px;
  }
  .layout {
    padding: 16px 24px;
  }
  /* 三栏骨架：中栏列表 + 右栏预览/编辑 */
  .notes-desktop-layout {
    display: flex;
    gap: 20px;
    align-items: flex-start;
  }
  .content {
    flex: 1;
    min-width: 0;
    max-width: none;
    margin: 0;
  }
  .preview-pane {
    width: 480px;
    flex-shrink: 0;
    position: sticky;
    top: 72px;
    max-height: calc(100vh - 90px);
    overflow-y: auto;
    background: var(--surface);
    border: 1px solid var(--border);
    border-radius: 14px;
    box-shadow: var(--shadow-sm);
    display: flex;
    flex-direction: column;
  }
  .notes-grid {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(240px, 1fr));
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
  .notes-grid-item.is-selected {
    border-color: var(--color-primary);
    box-shadow: 0 0 0 2px var(--color-primary);
  }
  .note-card {
    cursor: pointer;
    height: 100%;
  }
}

/* ========== 右栏预览 / 快速编辑面板 ========== */
.pane-loading {
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 60px 0;
}

.pane-preview,
.pane-edit {
  display: flex;
  flex-direction: column;
  min-height: 0;
  height: 100%;
}

.pane-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  padding: 14px 16px;
  border-bottom: 1px solid var(--border);
}

.pane-title {
  flex: 1;
  min-width: 0;
  font-size: 16px;
  font-weight: 700;
  color: var(--text-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.pane-actions {
  display: flex;
  align-items: center;
  gap: 6px;
  flex-shrink: 0;
}

.pane-btn {
  width: 30px;
  height: 30px;
  border-radius: 8px;
  border: 1px solid var(--border);
  background: var(--surface);
  color: var(--text-secondary);
  display: inline-flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  transition: all 0.15s ease;
}

.pane-btn:hover {
  color: var(--color-primary);
  border-color: var(--color-primary);
  background: rgba(59, 130, 246, 0.08);
}

.pane-meta {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 16px 0;
  font-size: 12px;
  color: var(--text-tertiary);
}

.pane-cat {
  color: var(--color-primary);
  font-weight: 600;
}

.pane-body {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  padding: 16px;
}

.pane-empty {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 10px;
  padding: 80px 20px;
  color: var(--text-tertiary);
  font-size: 13px;
  text-align: center;
}

.pane-empty-icon {
  font-size: 40px;
  opacity: 0.5;
}

/* 快速编辑态 */
.pane-edit-body {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
  padding: 14px 16px;
  gap: 10px;
}

.pane-edit-title {
  width: 100%;
  padding: 10px 12px;
  border: 1px solid var(--border);
  border-radius: 10px;
  background: var(--surface);
  color: var(--text-primary);
  font-size: 15px;
  font-weight: 600;
  outline: none;
  transition: border-color 0.15s ease;
}

.pane-edit-title:focus {
  border-color: var(--color-primary);
}

.pane-edit-content {
  flex: 1;
  min-height: 240px;
  width: 100%;
  padding: 12px;
  border: 1px solid var(--border);
  border-radius: 10px;
  background: var(--surface);
  color: var(--text-primary);
  font-size: 14px;
  line-height: 1.7;
  font-family: inherit;
  resize: none;
  outline: none;
  transition: border-color 0.15s ease;
}

.pane-edit-content:focus {
  border-color: var(--color-primary);
}

.pane-edit-footer {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  padding: 12px 16px;
  border-top: 1px solid var(--border);
}
</style>
