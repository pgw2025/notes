<template>
  <div class="page" :class="{ 'page--desktop': isDesktop }">
    <!-- 顶部导航栏（移动端标准展示；桌面端作为精炼顶栏） -->
    <van-nav-bar
      :title="isDesktop ? '分类工作台' : '分类管理'"
      :left-arrow="!isDesktop"
      class="cat-top-bar"
      @click-left="$router.back()"
    >
      <template #right>
        <div class="top-bar-actions">
          <van-button
            v-if="isDesktop"
            size="small"
            type="primary"
            round
            icon="plus"
            class="top-add-btn"
            @click="onAdd"
          >
            新建顶级分类
          </van-button>
          <van-icon
            v-else
            name="plus"
            size="20"
            class="nav-plus"
            @click="onAdd"
            title="新建分类"
          />
        </div>
      </template>
    </van-nav-bar>

    <!-- ==================== 桌面端：双栏协同工作台 (Master-Detail) ==================== -->
    <div v-if="isDesktop" class="desktop-workspace">
      <!-- 左栏：层级分类导航树 -->
      <aside class="workspace-sidebar">
        <!-- 侧栏搜索与工具栏 -->
        <div class="sidebar-header">
          <div class="sidebar-search-box">
            <van-icon name="search" class="search-icon" />
            <input
              v-model="searchQuery"
              type="text"
              placeholder="搜索分类名称..."
              class="sidebar-search-input"
            />
            <van-icon
              v-if="searchQuery"
              name="cross"
              class="search-clear-icon"
              @click="searchQuery = ''"
            />
          </div>

          <div class="sidebar-tools">
            <span class="tree-count-text">
              共 {{ totalCategoryCount }} 个分类
            </span>
            <button
              class="tool-text-btn"
              @click="toggleAllExpand"
              :title="isAllExpanded ? '全部折叠' : '全部展开'"
            >
              {{ isAllExpanded ? '全部折叠' : '全部展开' }}
            </button>
          </div>
        </div>

        <!-- 侧栏分类树列表 -->
        <div class="sidebar-tree-scroll">
          <div v-if="displayTree.length" class="desktop-tree">
            <CategoryNode
              v-for="c in displayTree"
              :key="c.id"
              :node="c"
              :depth="0"
              :expanded="expandedIds"
              :active-id="selectedCategory?.id"
              :is-desktop="true"
              @toggle="toggleExpand"
              @add-child="onAddChild"
              @edit="onEdit"
              @delete="onDelete"
              @select="selectCategory"
            />
          </div>

          <div v-else class="sidebar-empty">
            <span class="empty-icon-sm">🔍</span>
            <p>{{ searchQuery ? '未找到匹配分类' : '暂无分类' }}</p>
          </div>
        </div>

        <!-- 侧栏底栏操作 -->
        <div class="sidebar-footer">
          <button class="add-root-btn" @click="onAdd">
            <van-icon name="plus" />
            <span>新建顶级分类</span>
          </button>
        </div>
      </aside>

      <!-- 右栏：选中分类的实时工作台 -->
      <main class="workspace-main">
        <template v-if="selectedCategory">
          <!-- 分类头部概览卡片 -->
          <div class="cat-header-card">
            <!-- 面包屑导航 -->
            <div class="cat-breadcrumbs">
              <span class="breadcrumb-item" @click="goAllNotes">
                <van-icon name="notes-o" /> 全部笔记
              </span>
              <template v-for="(crumb, idx) in breadcrumbs" :key="crumb.id">
                <span class="breadcrumb-sep">/</span>
                <span
                  class="breadcrumb-item"
                  :class="{ 'is-current': idx === breadcrumbs.length - 1 }"
                  @click="selectCategory(crumb)"
                >
                  {{ crumb.name }}
                </span>
              </template>
            </div>

            <!-- 分类标题与操作行 -->
            <div class="cat-meta-row">
              <div class="cat-title-wrap">
                <span class="cat-big-icon">📂</span>
                <div>
                  <h1 class="cat-main-title">{{ selectedCategory.name }}</h1>
                  <div class="cat-sub-info">
                    <span class="meta-pill">
                      {{ selectedCategory.noteCount || categoryNotes.length }} 篇笔记
                    </span>
                    <span v-if="selectedCategory.createdAt" class="meta-date">
                      创建于 {{ formatTime(selectedCategory.createdAt) }}
                    </span>
                  </div>
                </div>
              </div>

              <div class="cat-actions-wrap">
                <van-button
                  type="primary"
                  size="small"
                  round
                  icon="plus"
                  @click="onCreateNoteHere"
                >
                  写笔记
                </van-button>
                <van-button
                  size="small"
                  round
                  plain
                  icon="plus"
                  @click="onAddChild(selectedCategory)"
                  title="在此分类下建子分类"
                >
                  加子分类
                </van-button>
                <van-button
                  size="small"
                  round
                  plain
                  icon="edit"
                  @click="onEdit(selectedCategory)"
                  title="重命名或修改分类"
                >
                  编辑
                </van-button>
                <van-button
                  size="small"
                  round
                  plain
                  type="danger"
                  icon="delete-o"
                  @click="onDelete(selectedCategory)"
                  title="删除该分类"
                >
                  删除
                </van-button>
                <van-button
                  size="small"
                  round
                  plain
                  icon="arrow"
                  @click="goNotes(selectedCategory.id)"
                  title="跳转至主笔记列表查看"
                >
                  全屏列表
                </van-button>
              </div>
            </div>
          </div>

          <!-- 分类下笔记列表区域 -->
          <section class="cat-notes-section">
            <div class="notes-section-header">
              <div class="notes-header-left">
                <h3 class="section-title">
                  分类笔记清单
                  <span class="count-tag">({{ filteredNotes.length }})</span>
                </h3>
              </div>

              <div class="notes-header-right">
                <div class="note-filter-box">
                  <van-icon name="search" size="14" class="search-mini-icon" />
                  <input
                    v-model="noteKeyword"
                    type="text"
                    placeholder="按标题/内容筛选..."
                    class="note-filter-input"
                  />
                  <van-icon
                    v-if="noteKeyword"
                    name="cross"
                    size="12"
                    class="clear-mini-icon"
                    @click="noteKeyword = ''"
                  />
                </div>
              </div>
            </div>

            <!-- 加载状态 -->
            <div v-if="loadingNotes" class="notes-loading-state">
              <van-loading type="spinner" color="var(--color-primary)">加载笔记中...</van-loading>
            </div>

            <!-- 笔记卡片网格 -->
            <div v-else-if="filteredNotes.length > 0" class="desktop-notes-grid">
              <div
                v-for="n in filteredNotes"
                :key="n.id"
                class="desktop-note-card"
                :style="cardStyle(n)"
                @click="goDetail(n.id)"
              >
                <!-- 置顶标记 -->
                <div v-if="n.isPinned" class="card-pinned-tag">
                  <van-icon name="star" size="11" />
                  <span>置顶</span>
                </div>

                <div class="card-top-row">
                  <div class="card-title" :title="n.title || '无标题'">
                    {{ n.title || '无标题' }}
                  </div>

                  <!-- 悬停快捷操作 -->
                  <div class="card-hover-actions" @click.stop>
                    <button
                      class="card-icon-btn"
                      :class="{ 'is-pinned': n.isPinned }"
                      :title="n.isPinned ? '取消置顶' : '置顶笔记'"
                      @click="onTogglePin(n)"
                    >
                      <van-icon :name="n.isPinned ? 'star' : 'star-o'" size="14" />
                    </button>
                    <button
                      class="card-icon-btn"
                      title="编辑笔记"
                      @click="goEdit(n.id)"
                    >
                      <van-icon name="edit" size="14" />
                    </button>
                    <button
                      class="card-icon-btn btn-danger"
                      title="删除笔记"
                      @click="onDeleteNote(n)"
                    >
                      <van-icon name="delete-o" size="14" />
                    </button>
                  </div>
                </div>

                <p class="card-preview">{{ n.contentPreview || '（无预览内容）' }}</p>

                <div class="card-bottom-row">
                  <div class="card-tags-list">
                    <span
                      v-for="t in (n.tags || []).slice(0, 3)"
                      :key="t"
                      class="card-tag"
                      :style="tagStyle(n)"
                    >
                      #{{ t }}
                    </span>
                    <span v-if="(n.tags || []).length > 3" class="card-tag-more">
                      +{{ n.tags.length - 3 }}
                    </span>
                  </div>
                  <span class="card-updated-time">{{ formatTime(n.updatedAt) }}</span>
                </div>
              </div>
            </div>

            <!-- 笔记空状态 -->
            <div v-else class="notes-empty-wrap">
              <div class="empty-art">📝</div>
              <h4>{{ noteKeyword ? '未搜索到相关笔记' : '该分类下暂无笔记' }}</h4>
              <p>{{ noteKeyword ? '换个关键词试一下' : '点击下方按钮，开始记录这一分类的第一条思路吧' }}</p>
              <van-button
                v-if="!noteKeyword"
                type="primary"
                round
                size="small"
                icon="plus"
                @click="onCreateNoteHere"
              >
                在此分类下写笔记
              </van-button>
            </div>
          </section>
        </template>

        <!-- 未选择任何分类时的引导面板 -->
        <div v-else class="workspace-empty-guide">
          <div class="guide-icon">📁</div>
          <h2>请从左侧选择一个分类</h2>
          <p>在左侧导航树中点击分类，可在右侧集中管理该分类下的笔记与层级</p>
          <van-button type="primary" round icon="plus" @click="onAdd">
            新建顶级分类
          </van-button>
        </div>
      </main>
    </div>

    <!-- ==================== 移动端：保持轻便单列纵向树 ==================== -->
    <div v-else class="cat-content">
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
          :active-id="null"
          :is-desktop="false"
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

    <!-- ==================== 创建/编辑分类弹窗 ==================== -->
    <!-- 桌面端居中弹窗，移动端底栏滑出弹窗 -->
    <van-popup
      v-model:show="showForm"
      :position="isDesktop ? 'center' : 'bottom'"
      round
      :class="isDesktop ? 'form-dialog-desktop' : 'form-popup-mobile'"
      :style="isDesktop ? { width: '440px', maxWidth: '90vw' } : {}"
    >
      <div class="form-wrapper">
        <div class="form-header">
          <span class="form-title">{{ editing ? '编辑分类' : '新建分类' }}</span>
          <van-icon name="cross" size="18" class="dialog-close-btn" @click="showForm = false" />
        </div>

        <van-field
          v-model="formName"
          placeholder="请输入分类名称（例如：工作规划、读书札记）"
          maxlength="50"
          autofocus
          class="form-field"
          clearable
          @keyup.enter="onSubmitForm"
        />

        <div v-if="formParentId !== null && formParentId !== undefined" class="form-parent-label">
          <span class="parent-label-text">所属父分类</span>
          <span class="parent-label-value">{{ parentName || '无（顶级分类）' }}</span>
        </div>

        <div class="form-actions">
          <van-button
            v-if="isDesktop"
            plain
            round
            class="dialog-cancel-btn"
            @click="showForm = false"
          >
            取消
          </van-button>
          <van-button
            block
            round
            type="primary"
            :loading="submitting"
            @click="onSubmitForm"
          >
            保存
          </van-button>
        </div>
      </div>
    </van-popup>
  </div>
</template>

<script setup>
import { ref, onActivated, onMounted, computed, watch } from 'vue'
import { useRouter } from 'vue-router'
import { showConfirmDialog, showToast } from 'vant'
import http from '../api/http'
import { useResponsive } from '../composables/useResponsive'
import { useAuthStore } from '../stores/auth'
import { useThemeStore } from '../stores/theme'
import {
  findCategoryById,
  flattenCategories,
  getCategoryBreadcrumbs
} from '../utils/categoryTree'
import { formatTime } from '../utils/format'
import { resolveNoteColor, getContrastColor, isDarkColor } from '../utils/color'
import CategoryNode from '../components/CategoryNode.vue'

const router = useRouter()
const { isDesktop } = useResponsive()
const auth = useAuthStore()
const theme = useThemeStore()

// 当前生效主题是否深色（与 NotesList.vue 保持同构）
// 注：必须用 computed 包一层，才能让 cardStyle / tagStyle 在渲染期正确订阅主题变化
const isDarkEffective = computed(() => theme.isDarkEffective)

const list = ref([])
const showForm = ref(false)
const formName = ref('')
const formParentId = ref(null)
const editing = ref(null)
const submitting = ref(false)

// 展开的子分类 id 集合
const expandedIds = ref(new Set())

// 侧栏搜索过滤词
const searchQuery = ref('')

// 当前选中的分类（桌面双栏联动）
const selectedCategory = ref(null)

// 选中分类下的笔记列表与加载状态
const categoryNotes = ref([])
const loadingNotes = ref(false)
const noteKeyword = ref('')

// 计算分类总数
const totalCategoryCount = computed(() => {
  return flattenCategories(list.value).length
})

// 是否全部已展开
const isAllExpanded = computed(() => {
  const allIds = flattenCategories(list.value).map((c) => c.id)
  return allIds.length > 0 && allIds.every((id) => expandedIds.value.has(id))
})

// 根据搜索框过滤分类树
const displayTree = computed(() => {
  if (!searchQuery.value.trim()) return list.value
  const q = searchQuery.value.trim().toLowerCase()

  function filterNodes(nodes) {
    const res = []
    for (const node of nodes) {
      const selfMatch = node.name.toLowerCase().includes(q)
      const childrenMatch = node.children && node.children.length > 0 ? filterNodes(node.children) : []
      if (selfMatch || childrenMatch.length > 0) {
        res.push({
          ...node,
          children: childrenMatch
        })
      }
    }
    return res
  }

  return filterNodes(list.value)
})

// 搜索变动时，若有搜索词自动展开所有命中分支
watch(searchQuery, (newVal) => {
  if (newVal.trim()) {
    const all = flattenCategories(displayTree.value)
    const next = new Set(expandedIds.value)
    all.forEach((n) => next.add(n.id))
    expandedIds.value = next
  }
})

// 面包屑链路
const breadcrumbs = computed(() => {
  if (!selectedCategory.value) return []
  return getCategoryBreadcrumbs(list.value, selectedCategory.value.id)
})

// 过滤右侧笔记
const filteredNotes = computed(() => {
  let notes = [...categoryNotes.value]
  if (noteKeyword.value.trim()) {
    const k = noteKeyword.value.trim().toLowerCase()
    notes = notes.filter(
      (n) =>
        (n.title && n.title.toLowerCase().includes(k)) ||
        (n.contentPreview && n.contentPreview.toLowerCase().includes(k))
    )
  }
  // 置顶排前，更新时间倒序
  return notes.sort((a, b) => {
    if (a.isPinned && !b.isPinned) return -1
    if (!a.isPinned && b.isPinned) return 1
    return new Date(b.updatedAt) - new Date(a.updatedAt)
  })
})

const parentName = computed(() => {
  if (formParentId.value === null || formParentId.value === undefined) return ''
  const p = findCategoryById(list.value, formParentId.value)
  return p ? p.name : ''
})

async function load() {
  const data = await http.get('/categories')
  list.value = data || []

  // 桌面模式下，若尚未选中或原有分类已被删，默认选中第一个
  if (isDesktop.value && list.value.length > 0) {
    const flat = flattenCategories(list.value)
    if (!selectedCategory.value || !flat.some((c) => c.id === selectedCategory.value.id)) {
      selectCategory(flat[0])
    } else {
      // 刷新当前选中的分类信息
      const fresh = flat.find((c) => c.id === selectedCategory.value.id)
      if (fresh) {
        selectedCategory.value = fresh
        await loadCategoryNotes()
      }
    }
  }
}

// 选中某个分类
async function selectCategory(cat) {
  if (!cat) return
  selectedCategory.value = cat
  // 展开其祖先节点
  const ancestors = getCategoryBreadcrumbs(list.value, cat.id)
  const next = new Set(expandedIds.value)
  ancestors.forEach((a) => next.add(a.id))
  expandedIds.value = next

  await loadCategoryNotes()
}

// 加载当前分类下的笔记
async function loadCategoryNotes() {
  if (!selectedCategory.value) {
    categoryNotes.value = []
    return
  }
  loadingNotes.value = true
  try {
    const res = await http.get('/notes', {
      params: { categoryId: selectedCategory.value.id, pageSize: 60 }
    })
    categoryNotes.value = res.items || []
  } catch (err) {
    console.error('Failed to load category notes:', err)
  } finally {
    loadingNotes.value = false
  }
}

function toggleExpand(id) {
  const next = new Set(expandedIds.value)
  if (next.has(id)) next.delete(id)
  else next.add(id)
  expandedIds.value = next
}

function toggleAllExpand() {
  if (isAllExpanded.value) {
    expandedIds.value = new Set()
  } else {
    const all = flattenCategories(list.value).map((c) => c.id)
    expandedIds.value = new Set(all)
  }
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

async function onSubmitForm() {
  const name = formName.value.trim()
  if (!name) {
    showToast('请输入分类名称')
    return
  }
  submitting.value = true
  try {
    let savedCat = null
    if (editing.value) {
      savedCat = await http.put(`/categories/${editing.value.id}`, {
        name,
        parentId: formParentId.value
      })
      showToast('已更新')
    } else {
      savedCat = await http.post('/categories', {
        name,
        parentId: formParentId.value
      })
      showToast('已创建')
    }
    showForm.value = false

    if (formParentId.value != null) {
      const next = new Set(expandedIds.value)
      next.add(formParentId.value)
      expandedIds.value = next
    }

    await load()

    // 若新建或编辑的是当前分类，自动同步聚焦
    if (savedCat && savedCat.id) {
      const flat = flattenCategories(list.value)
      const target = flat.find((x) => x.id === savedCat.id)
      if (target) selectCategory(target)
    }
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

    // 若删除的是当前选中的分类，重置为第一个
    if (selectedCategory.value?.id === c.id) {
      selectedCategory.value = null
    }
    await load()
  } catch (err) {
    const msg = err?.response?.data?.message
    if (msg) showToast(msg)
  }
}

// 在当前分类下新建笔记
function onCreateNoteHere() {
  if (!selectedCategory.value) {
    router.push('/notes/new')
    return
  }
  router.push({
    path: '/notes/new',
    query: { categoryId: selectedCategory.value.id }
  })
}

// 快速查看笔记详情
function goDetail(noteId) {
  router.push(`/notes/${noteId}`)
}

// 快速编辑笔记
function goEdit(noteId) {
  router.push(`/notes/${noteId}/edit`)
}

// 跳转到主列表
function goNotes(categoryId) {
  router.push({ path: '/notes', query: { categoryId } })
}

function goAllNotes() {
  router.push('/notes')
}

// 快速置顶笔记
async function onTogglePin(note) {
  try {
    const res = await http.post(note.isPinned ? `/notes/${note.id}/unpin` : `/notes/${note.id}/pin`)
    note.isPinned = res.isPinned
    showToast(note.isPinned ? '已置顶' : '已取消置顶')
  } catch {
    showToast('置顶操作失败')
  }
}

// 快速删除笔记
async function onDeleteNote(note) {
  try {
    await showConfirmDialog({
      title: '删除笔记',
      message: `确定删除笔记「${note.title || '无标题'}」吗？`
    })
    await http.delete(`/notes/${note.id}`)
    showToast('已删除')
    await loadCategoryNotes()
    await load() // 刷新分类笔记计数
  } catch (err) {
    if (err !== 'cancel') {
      showToast('删除失败')
    }
  }
}

// 笔记卡片样式
function cardStyle(n) {
  const bg = resolveNoteColor(n.backgroundColor, auth.user?.defaultNoteColor, isDarkEffective.value)
  const fg = getContrastColor(bg)
  return {
    backgroundColor: bg,
    color: fg,
    borderColor: isDarkColor(bg) ? 'rgba(255, 255, 255, 0.12)' : 'var(--border)'
  }
}

function tagStyle(n) {
  const bg = resolveNoteColor(n.backgroundColor, auth.user?.defaultNoteColor, isDarkEffective.value)
  const isDarkBg = isDarkColor(bg)
  return {
    background: isDarkBg ? 'rgba(255, 255, 255, 0.16)' : 'rgba(0, 0, 0, 0.06)',
    color: isDarkBg ? '#FFFFFF' : 'var(--text-secondary)'
  }
}

onMounted(load)
onActivated(load)
</script>

<style scoped>
.page {
  min-height: 100vh;
  background: var(--app-bg);
  display: flex;
  flex-direction: column;
}

.cat-top-bar {
  background: var(--surface);
  border-bottom: 1px solid var(--border);
  flex-shrink: 0;
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

.top-add-btn {
  font-weight: 500;
}

/* ==================== 桌面端：双栏协同工作台 ==================== */
.page--desktop {
  height: 100vh;
  overflow: hidden;
}

.desktop-workspace {
  display: flex;
  flex: 1;
  height: calc(100vh - 46px);
  overflow: hidden;
  background: var(--app-bg);
}

/* 左侧栏：分类导航树面板 */
.workspace-sidebar {
  width: 320px;
  min-width: 280px;
  max-width: 360px;
  display: flex;
  flex-direction: column;
  background: var(--surface);
  border-right: 1px solid var(--border);
  flex-shrink: 0;
}

.sidebar-header {
  padding: 14px 14px 10px;
  border-bottom: 1px solid var(--border);
}

.sidebar-search-box {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 6px 10px;
  background: var(--surface-2);
  border: 1px solid var(--border);
  border-radius: 8px;
  transition: border-color 0.15s ease;
}
.sidebar-search-box:focus-within {
  border-color: var(--color-primary);
}

.search-icon {
  color: var(--text-tertiary);
  font-size: 14px;
}

.sidebar-search-input {
  flex: 1;
  border: none;
  background: transparent;
  outline: none;
  font-size: 13px;
  color: var(--text-primary);
}

.search-clear-icon {
  cursor: pointer;
  color: var(--text-tertiary);
  font-size: 12px;
}
.search-clear-icon:hover {
  color: var(--text-primary);
}

.sidebar-tools {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-top: 10px;
  padding: 0 2px;
}

.tree-count-text {
  font-size: 12px;
  color: var(--text-tertiary);
}

.tool-text-btn {
  background: none;
  border: none;
  font-size: 12px;
  color: var(--color-primary);
  cursor: pointer;
  padding: 2px 6px;
  border-radius: 4px;
  transition: background 0.15s ease;
}
.tool-text-btn:hover {
  background: rgba(15, 169, 140, 0.08);
}

.sidebar-tree-scroll {
  flex: 1;
  overflow-y: auto;
  padding: 12px;
}

.desktop-tree {
  display: flex;
  flex-direction: column;
  gap: 5px;
}

.sidebar-empty {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 40px 16px;
  color: var(--text-tertiary);
  font-size: 13px;
}

.empty-icon-sm {
  font-size: 28px;
  margin-bottom: 8px;
  opacity: 0.7;
}

.sidebar-footer {
  padding: 10px 14px;
  border-top: 1px solid var(--border);
  background: var(--surface);
}

.add-root-btn {
  width: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  padding: 8px 12px;
  background: var(--surface-2);
  border: 1px dashed var(--border);
  border-radius: 8px;
  font-size: 13px;
  font-weight: 500;
  color: var(--text-primary);
  cursor: pointer;
  transition: all 0.15s ease;
}
.add-root-btn:hover {
  background: rgba(15, 169, 140, 0.08);
  border-color: var(--color-primary);
  color: var(--color-primary);
}

/* 右侧主区域：分类工作台 */
.workspace-main {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow-y: auto;
  padding: 24px 32px 40px;
}

/* 分类头部概览卡片 */
.cat-header-card {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 12px;
  padding: 18px 24px;
  margin-bottom: 24px;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.03);
}

.cat-breadcrumbs {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 6px;
  margin-bottom: 12px;
  font-size: 12px;
  color: var(--text-tertiary);
}

.breadcrumb-item {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  cursor: pointer;
  transition: color 0.15s ease;
}
.breadcrumb-item:hover {
  color: var(--color-primary);
}

.breadcrumb-item.is-current {
  color: var(--text-secondary);
  font-weight: 600;
  cursor: default;
}

.breadcrumb-sep {
  opacity: 0.4;
}

.cat-meta-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 16px;
}

.cat-title-wrap {
  display: flex;
  align-items: center;
  gap: 14px;
}

.cat-big-icon {
  font-size: 32px;
  line-height: 1;
}

.cat-main-title {
  margin: 0 0 6px;
  font-size: 22px;
  font-weight: 700;
  color: var(--text-primary);
  letter-spacing: -0.02em;
}

.cat-sub-info {
  display: flex;
  align-items: center;
  gap: 10px;
  font-size: 12px;
  color: var(--text-tertiary);
}

.meta-pill {
  background: rgba(15, 169, 140, 0.1);
  color: var(--color-primary);
  font-weight: 600;
  padding: 2px 8px;
  border-radius: 999px;
}

.cat-actions-wrap {
  display: flex;
  align-items: center;
  gap: 8px;
}

/* 分类下笔记列表区域 */
.cat-notes-section {
  flex: 1;
}

.notes-section-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 16px;
}

.section-title {
  font-size: 16px;
  font-weight: 700;
  color: var(--text-primary);
  margin: 0;
  display: flex;
  align-items: center;
  gap: 6px;
}

.count-tag {
  font-size: 13px;
  font-weight: normal;
  color: var(--text-tertiary);
}

.note-filter-box {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 6px 10px;
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 8px;
}
.note-filter-box:focus-within {
  border-color: var(--color-primary);
}

.search-mini-icon {
  color: var(--text-tertiary);
}

.note-filter-input {
  border: none;
  background: transparent;
  outline: none;
  font-size: 12px;
  color: var(--text-primary);
  width: 160px;
}

.clear-mini-icon {
  cursor: pointer;
  color: var(--text-tertiary);
}

.notes-loading-state {
  display: flex;
  justify-content: center;
  padding: 60px 0;
}

/* 桌面端笔记卡片网格 */
.desktop-notes-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 16px;
}

.desktop-note-card {
  position: relative;
  display: flex;
  flex-direction: column;
  padding: 16px 18px;
  border-radius: 12px;
  border: 1px solid var(--border);
  cursor: pointer;
  transition: transform 0.16s ease, box-shadow 0.16s ease, border-color 0.16s ease;
  min-height: 140px;
}

.desktop-note-card:hover {
  transform: translateY(-2px);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.06);
  border-color: var(--color-primary) !important;
}

.card-pinned-tag {
  position: absolute;
  top: 10px;
  right: 12px;
  display: inline-flex;
  align-items: center;
  gap: 2px;
  font-size: 10px;
  color: #e65100;
  background: rgba(255, 152, 0, 0.15);
  padding: 1px 6px;
  border-radius: 4px;
  font-weight: 600;
}

.card-top-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 8px;
  padding-right: 36px;
}

.card-title {
  font-size: 15px;
  font-weight: 700;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.card-hover-actions {
  position: absolute;
  top: 8px;
  right: 8px;
  display: flex;
  gap: 3px;
  opacity: 0;
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 6px;
  padding: 2px;
  box-shadow: 0 2px 6px rgba(0, 0, 0, 0.08);
  transition: opacity 0.15s ease;
  z-index: 2;
}

.desktop-note-card:hover .card-hover-actions {
  opacity: 1;
}

.card-icon-btn {
  width: 24px;
  height: 24px;
  border-radius: 4px;
  border: none;
  background: transparent;
  color: var(--text-secondary);
  display: inline-flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  transition: all 0.15s ease;
}
.card-icon-btn:hover {
  background: var(--surface-2);
  color: var(--text-primary);
}
.card-icon-btn.is-pinned {
  color: #ff976a;
}
.card-icon-btn.btn-danger:hover {
  color: var(--color-danger);
  background: rgba(239, 68, 68, 0.1);
}

.card-preview {
  flex: 1;
  font-size: 13px;
  line-height: 1.5;
  opacity: 0.8;
  margin: 0 0 14px;
  overflow: hidden;
  display: -webkit-box;
  -webkit-line-clamp: 3;
  -webkit-box-orient: vertical;
}

.card-bottom-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  font-size: 11px;
  gap: 8px;
}

.card-tags-list {
  display: flex;
  align-items: center;
  gap: 4px;
  overflow: hidden;
}

.card-tag {
  font-size: 11px;
  padding: 1px 6px;
  border-radius: 4px;
  white-space: nowrap;
}

.card-tag-more {
  font-size: 10px;
  opacity: 0.6;
}

.card-updated-time {
  opacity: 0.65;
  white-space: nowrap;
  flex-shrink: 0;
}

.notes-empty-wrap {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 60px 20px;
  background: var(--surface);
  border: 1px dashed var(--border);
  border-radius: 12px;
  text-align: center;
}

.empty-art {
  font-size: 40px;
  margin-bottom: 12px;
}

.notes-empty-wrap h4 {
  margin: 0 0 6px;
  font-size: 15px;
  color: var(--text-primary);
}

.notes-empty-wrap p {
  margin: 0 0 16px;
  font-size: 13px;
  color: var(--text-tertiary);
}

.workspace-empty-guide {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  height: 100%;
  text-align: center;
  color: var(--text-tertiary);
}

.guide-icon {
  font-size: 56px;
  margin-bottom: 16px;
}

.workspace-empty-guide h2 {
  margin: 0 0 8px;
  font-size: 18px;
  color: var(--text-primary);
}

.workspace-empty-guide p {
  margin: 0 0 20px;
  font-size: 13px;
  max-width: 320px;
}

/* ==================== 移动端原有样式保留 ==================== */
.cat-content {
  padding: 16px;
  padding-bottom: 80px;
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

/* ==================== 弹窗样式（桌面居中 / 移动底栏） ==================== */
.form-wrapper {
  padding: 20px 24px 24px;
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

.dialog-close-btn {
  cursor: pointer;
  color: var(--text-tertiary);
  transition: color 0.15s ease;
}
.dialog-close-btn:hover {
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
  display: flex;
  gap: 12px;
  padding-top: 6px;
}

.dialog-cancel-btn {
  flex: 1;
}

.form-dialog-desktop {
  box-shadow: 0 8px 30px rgba(0, 0, 0, 0.12);
  border: 1px solid var(--border);
}
</style>
