<template>
  <div class="page" :class="{ 'has-desktop-toc': headings.length > 0 && showDesktopToc }" :style="pageStyle">
    <van-nav-bar title="笔记详情" left-arrow @click-left="$router.back()">
      <template #right>
        <!-- 桌面端单行布局：按钮在标题右侧 -->
        <div class="nav-right nav-actions--desktop">
          <van-icon
            name="search"
            size="20"
            :style="{ color: showSearchBar ? '#1989fa' : navIconColor }"
            title="正文搜索 (Ctrl+F)"
            @click="toggleSearch"
          />
          <van-icon
            v-if="headings.length"
            name="bars"
            size="20"
            :style="{ color: showDesktopToc ? '#1989fa' : navIconColor }"
            title="切换大纲侧栏"
            @click="showDesktopToc = !showDesktopToc"
          />
          <van-icon
            :name="note?.isPinned ? 'star' : 'star-o'"
            size="20"
            :color="note?.isPinned ? '#ff976a' : navIconColor"
            @click="onTogglePin"
          />
          <van-icon name="clock-o" size="20" :style="{ color: navIconColor }" @click="openVersions" />
          <van-icon name="down" size="20" :style="{ color: navIconColor }" @click="showExportSheet = true" />
          <van-icon name="edit" size="20" :style="{ color: navIconColor }" @click="$router.push(`/notes/${note.id}/edit`)" />
        </div>
      </template>
    </van-nav-bar>

    <!-- 移动端第二行：按钮独占一行，与标题完全不重叠 -->
    <div class="nav-actions nav-actions--mobile">
      <van-icon
        name="search"
        size="20"
        :style="{ color: showSearchBar ? '#1989fa' : navIconColor }"
        title="正文搜索"
        @click="toggleSearch"
      />
      <van-icon
        v-if="headings.length"
        name="bars"
        size="20"
        :style="{ color: navIconColor }"
        title="文章大纲"
        @click="showMobileToc = true"
      />
      <van-icon
        :name="note?.isPinned ? 'star' : 'star-o'"
        size="20"
        :color="note?.isPinned ? '#ff976a' : navIconColor"
        @click="onTogglePin"
      />
      <van-icon name="clock-o" size="20" :style="{ color: navIconColor }" @click="openVersions" />
      <van-icon name="down" size="20" :style="{ color: navIconColor }" @click="showExportSheet = true" />
      <van-icon name="edit" size="20" :style="{ color: navIconColor }" @click="note && $router.push(`/notes/${note.id}/edit`)" />
    </div>

    <!-- 浮动正文搜索栏（支持快捷键 Ctrl/Cmd+F，与章节折叠联动） -->
    <transition name="search-slide">
      <div v-if="showSearchBar" class="floating-search-bar" :style="{ color: textColor }">
        <div class="search-bar-inner">
          <van-icon name="search" class="search-bar-icon" />
          <input
            ref="searchInputRef"
            v-model="searchKeyword"
            type="text"
            class="search-bar-input"
            placeholder="在正文中查找 (Enter 下一个)..."
            @input="onSearchInput"
            @keydown.enter.exact.prevent="onNextSearch"
            @keydown.shift.enter.prevent="onPrevSearch"
            @keydown.esc.prevent="closeSearch"
          />
          <div v-if="searchKeyword.trim()" class="search-bar-badge" :class="{ 'is-zero': searchTotal === 0 }">
            <span v-if="searchTotal > 0">{{ currentSearchIndex + 1 }}/{{ searchTotal }}</span>
            <span v-else>无匹配</span>
          </div>
          <div class="search-bar-controls">
            <button
              type="button"
              class="search-bar-btn"
              :disabled="searchTotal === 0"
              title="上一个 (Shift+Enter)"
              @click="onPrevSearch"
            >
              <van-icon name="arrow-up" size="13" />
            </button>
            <button
              type="button"
              class="search-bar-btn"
              :disabled="searchTotal === 0"
              title="下一个 (Enter)"
              @click="onNextSearch"
            >
              <van-icon name="arrow-down" size="13" />
            </button>
            <div class="search-bar-sep"></div>
            <button
              type="button"
              class="search-bar-btn search-bar-close"
              title="关闭 (Esc)"
              @click="closeSearch"
            >
              <van-icon name="cross" size="14" />
            </button>
          </div>
        </div>
      </div>
    </transition>

    <div v-if="note" class="detail-container">
      <!-- 核心正文阅读区 -->
      <div class="detail-main" :style="{ color: textColor }">
        <h1 class="detail-title">{{ note.title || '无标题' }}</h1>

        <div class="detail-meta">
          <van-tag v-if="note.categoryName" type="primary" size="medium" :style="tagStyle">{{ note.categoryName }}</van-tag>
          <van-tag
            v-for="t in note.tags"
            :key="t"
            plain
            size="medium"
            :style="tagStyle"
          >{{ t }}</van-tag>
        </div>

        <div class="detail-time" :style="{ color: subTextColor }">
          创建于 {{ formatDateTime(note.createdAt) }} · 更新于 {{ formatDateTime(note.updatedAt) }}
        </div>

        <markdown-body
          ref="markdownBodyRef"
          :content="note.content"
          :style="{ color: textColor }"
          @outline-change="onOutlineChange"
          @collapse-change="onCollapseChange"
        />

        <div v-if="note.attachments?.length" class="attachments">
          <div class="section-title" :style="{ color: subTextColor }">附件</div>
          <van-cell-group inset>
            <van-cell
              v-for="a in note.attachments"
              :key="a.id"
              :title="a.fileName"
              :value="formatSize(a.size)"
              is-link
              :url="attachmentUrl(a.id, a.fileName)"
            >
              <template #icon>
                <van-icon :name="isImage(a.contentType) ? 'photo-o' : 'description'" class="att-icon" />
              </template>
            </van-cell>
          </van-cell-group>
        </div>
      </div>

      <!-- 桌面端侧边悬浮大纲栏 (Sticky TOC) -->
      <aside
        v-if="headings.length && showDesktopToc"
        class="desktop-toc-aside"
        :style="{ color: textColor }"
      >
        <div class="toc-card">
          <div class="toc-header">
            <div class="toc-header-title">
              <van-icon name="bars" class="toc-header-icon" />
              <span>大纲</span>
              <span class="toc-badge">{{ headings.length }}</span>
            </div>
            <div class="toc-header-actions">
              <button type="button" class="toc-action-btn" @click="foldAll" title="折叠全部章节">折叠全部</button>
              <span class="toc-action-sep">·</span>
              <button type="button" class="toc-action-btn" @click="unfoldAll" title="展开全部章节">展开全部</button>
            </div>
          </div>
          <nav class="toc-nav-list" ref="tocNavRef">
            <button
              v-for="h in headings"
              :key="h.id"
              type="button"
              class="toc-nav-item"
              :class="[
                'toc-level-' + h.level,
                { 'is-active': activeHeadingId === h.id },
                { 'is-folded': collapsedSet.has(h.id) }
              ]"
              @click="onTocClick(h.id)"
            >
              <span class="toc-indicator"></span>
              <span class="toc-text">{{ h.text }}</span>
              <span v-if="collapsedSet.has(h.id)" class="toc-fold-flag">已折叠</span>
            </button>
          </nav>
        </div>
      </aside>
    </div>

    <van-loading v-else class="loading" type="spinner" />

    <!-- 移动端：悬浮大纲胶囊（随屏滚动提示进度，点击呼出半屏抽屉） -->
    <transition name="van-fade">
      <div
        v-if="headings.length && !showMobileToc"
        class="mobile-toc-capsule"
        @click="showMobileToc = true"
      >
        <span class="mtc-icon">☰</span>
        <span class="mtc-text">大纲</span>
        <span class="mtc-count">{{ activeHeadingIndex >= 0 ? (activeHeadingIndex + 1) + '/' + headings.length : headings.length }}</span>
      </div>
    </transition>

    <!-- 移动端：大纲抽屉 Popup -->
    <van-popup
      v-model:show="showMobileToc"
      position="bottom"
      round
      class="mobile-toc-popup"
      :style="{ maxHeight: '76vh' }"
    >
      <div class="mobile-toc-header">
        <div class="mth-left">
          <span class="mth-title">文章大纲</span>
          <span class="mth-count">{{ headings.length }} 节</span>
        </div>
        <div class="mth-actions">
          <button type="button" class="mth-btn" @click="foldAll">折叠全部</button>
          <span class="mth-btn-sep">·</span>
          <button type="button" class="mth-btn" @click="unfoldAll">展开全部</button>
          <van-icon name="cross" size="20" class="mth-close" @click="showMobileToc = false" />
        </div>
      </div>
      <div class="mobile-toc-list">
        <div
          v-for="h in headings"
          :key="h.id"
          class="mobile-toc-item"
          :class="[
            'mti-level-' + h.level,
            { 'is-active': activeHeadingId === h.id },
            { 'is-folded': collapsedSet.has(h.id) }
          ]"
          @click="onMobileTocSelect(h.id)"
        >
          <span class="mti-indicator"></span>
          <span class="mti-text">{{ h.text }}</span>
          <span v-if="collapsedSet.has(h.id)" class="mti-fold-flag">已折叠</span>
        </div>
      </div>
    </van-popup>

    <!-- =============== 历史版本 Popup =============== -->
    <van-popup
      v-model:show="showVersionsPopup"
      position="bottom"
      round
      :style="{ height: '75%' }"
    >
      <van-nav-bar title="历史版本">
        <template #right>
          <van-icon name="cross" size="20" @click="showVersionsPopup = false" />
        </template>
      </van-nav-bar>

      <div v-if="versionsLoading" class="versions-loading">
        <van-loading color="#1989fa">加载版本中…</van-loading>
      </div>

      <div v-else-if="!versions.length" class="versions-empty">
        <van-empty description="还没有版本记录" />
      </div>

      <van-cell-group v-else inset class="versions-list">
        <div
          v-for="v in versions"
          :key="v.id"
          class="version-item"
          :class="{ 'is-current': v.isCurrent }"
        >
          <div class="version-row">
            <div class="version-title-row">
              <div class="version-time">{{ formatDateTime(v.createdAt) }}</div>
              <van-tag v-if="v.isCurrent" size="mini" type="success" plain>当前版本</van-tag>
            </div>
            <div class="version-preview-title">{{ v.titlePreview }}</div>
            <div class="version-preview-body">{{ v.contentPreview }}</div>
            <div class="version-meta">
              <van-tag v-if="v.categoryName" size="mini" plain type="primary">{{ v.categoryName }}</van-tag>
              <van-tag v-for="t in v.tags" :key="t" size="mini" plain color="#969799" text-color="#969799">{{ t }}</van-tag>
            </div>
            <div class="version-actions">
              <van-button size="mini" plain @click="viewVersion(v)">查看内容</van-button>
              <van-button
                v-if="!v.isCurrent"
                size="mini"
                type="warning"
                :loading="restoringId === v.id"
                @click="restoreVersion(v)"
              >恢复到此版本</van-button>
            </div>
          </div>
        </div>
      </van-cell-group>
    </van-popup>

    <!-- =============== 查看旧版本 Modal =============== -->
    <van-popup
      v-model:show="showViewModal"
      position="bottom"
      round
      :style="{ height: '85%' }"
    >
      <van-nav-bar>
        <template #left>
          <van-icon name="cross" size="20" @click="showViewModal = false" />
        </template>
        <template #title>
          <span>旧版本 · {{ activeVersion ? formatDateTime(activeVersion.createdAt) : '' }}</span>
        </template>
        <template #right>
          <van-button
            v-if="activeVersion && !activeVersion.isCurrent"
            size="mini"
            type="warning"
            :loading="restoringId === activeVersion.id"
            @click="restoreById(activeVersion.id)"
          >恢复</van-button>
        </template>
      </van-nav-bar>

      <div v-if="viewLoading" class="versions-loading">
        <van-loading color="#1989fa">加载中…</van-loading>
      </div>

      <div v-else-if="activeVersion" class="version-detail">
        <div class="vd-meta">
          <van-tag v-if="activeVersion.categoryName" size="medium" plain type="primary">{{ activeVersion.categoryName }}</van-tag>
          <van-tag
            v-for="t in activeVersion.tagNames"
            :key="t"
            size="medium"
            plain
            color="#969799"
            text-color="#969799"
          >{{ t }}</van-tag>
        </div>
        <h2 class="vd-title">{{ activeVersion.title || '（无标题）' }}</h2>
        <markdown-body :content="activeVersion.content || '*无内容*'" />
      </div>
    </van-popup>

    <!-- 导出格式选择 -->
    <van-action-sheet
      v-model:show="showExportSheet"
      title="导出此笔记"
      :actions="exportActions"
      @select="onExportSelect"
      cancel-text="取消"
      close-on-click-action
    />
  </div>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted, nextTick } from 'vue'
import { useRoute } from 'vue-router'
import { showToast, showConfirmDialog, showSuccessToast } from 'vant'
import http from '../api/http'
import MarkdownBody from '../components/MarkdownBody.vue'
import { formatDateTime } from '../utils/format'
import { resolveNoteColor, getContrastColor, isDarkColor } from '../utils/color'
import { exportSingleNote } from '../utils/exportImport'
import { useAuthStore } from '../stores/auth'
import { useThemeStore } from '../stores/theme'

const route = useRoute()
const auth = useAuthStore()
const theme = useThemeStore()
const note = ref(null)

// ================= 大纲（TOC）与章节折叠 =================
const markdownBodyRef = ref(null)
const headings = ref([])
const collapsedSet = ref(new Set())
const activeHeadingId = ref('')
const showDesktopToc = ref(true)
const showMobileToc = ref(false)
let headingObserver = null

const activeHeadingIndex = computed(() =>
  headings.value.findIndex(h => h.id === activeHeadingId.value)
)

function onOutlineChange(list) {
  headings.value = list || []
  if (headings.value.length && !activeHeadingId.value) {
    activeHeadingId.value = headings.value[0].id
  }
  setupHeadingObserver()
}

function onCollapseChange(ids) {
  collapsedSet.value = new Set(ids)
}

function foldAll() {
  markdownBodyRef.value?.foldAll()
}

function unfoldAll() {
  markdownBodyRef.value?.unfoldAll()
}

function onTocClick(headingId) {
  markdownBodyRef.value?.expandHeading(headingId)
  activeHeadingId.value = headingId
  nextTick(() => {
    const el = document.getElementById(headingId)
    if (el) {
      el.scrollIntoView({ behavior: 'smooth', block: 'start' })
    }
  })
}

function onMobileTocSelect(headingId) {
  showMobileToc.value = false
  markdownBodyRef.value?.expandHeading(headingId)
  activeHeadingId.value = headingId
  nextTick(() => {
    const el = document.getElementById(headingId)
    if (el) {
      el.scrollIntoView({ behavior: 'smooth', block: 'start' })
    }
  })
}

function setupHeadingObserver() {
  if (headingObserver) {
    headingObserver.disconnect()
    headingObserver = null
  }
  if (!headings.value.length) return

  nextTick(() => {
    try {
      headingObserver = new IntersectionObserver(
        (entries) => {
          for (const entry of entries) {
            if (entry.isIntersecting) {
              activeHeadingId.value = entry.target.id
            }
          }
        },
        {
          rootMargin: '-70px 0px -65% 0px'
        }
      )
      for (const h of headings.value) {
        const el = document.getElementById(h.id)
        if (el) headingObserver.observe(el)
      }
    } catch {
      // 容错处理
    }
  })
}

onUnmounted(() => {
  if (headingObserver) {
    headingObserver.disconnect()
    headingObserver = null
  }
  window.removeEventListener('keydown', handleGlobalKeyDown)
  clearTimeout(searchDebounceTimer)
})

// ================= 正文搜索与联动 =================
const showSearchBar = ref(false)
const searchKeyword = ref('')
const searchTotal = ref(0)
const currentSearchIndex = ref(-1)
const searchInputRef = ref(null)
let searchDebounceTimer = null

function toggleSearch() {
  if (showSearchBar.value) {
    closeSearch()
  } else {
    openSearch()
  }
}

function openSearch() {
  showSearchBar.value = true
  nextTick(() => {
    searchInputRef.value?.focus()
    if (searchKeyword.value) {
      searchInputRef.value?.select()
      executeSearch()
    }
  })
}

function closeSearch() {
  showSearchBar.value = false
  searchKeyword.value = ''
  searchTotal.value = 0
  currentSearchIndex.value = -1
  markdownBodyRef.value?.clearSearch()
}

function onSearchInput() {
  clearTimeout(searchDebounceTimer)
  searchDebounceTimer = setTimeout(() => {
    executeSearch()
  }, 120)
}

function executeSearch() {
  if (!markdownBodyRef.value) return
  const res = markdownBodyRef.value.highlightSearch(searchKeyword.value)
  searchTotal.value = res.total
  currentSearchIndex.value = res.current
}

function onNextSearch() {
  if (!markdownBodyRef.value || searchTotal.value === 0) return
  const res = markdownBodyRef.value.nextSearchMatch()
  searchTotal.value = res.total
  currentSearchIndex.value = res.current
}

function onPrevSearch() {
  if (!markdownBodyRef.value || searchTotal.value === 0) return
  const res = markdownBodyRef.value.prevSearchMatch()
  searchTotal.value = res.total
  currentSearchIndex.value = res.current
}

function handleGlobalKeyDown(e) {
  // Ctrl+F / Cmd+F 快捷唤起正文搜索
  if ((e.ctrlKey || e.metaKey) && (e.key === 'f' || e.key === 'F')) {
    e.preventDefault()
    openSearch()
    return
  }
  // Esc 快捷关闭搜索
  if (showSearchBar.value && e.key === 'Escape') {
    e.preventDefault()
    closeSearch()
  }
}

// 导出
const showExportSheet = ref(false)
const exportActions = [
  { name: 'JSON 格式（完整备份）', value: 'json' },
  { name: 'Markdown（纯正文）', value: 'markdown' },
  { name: 'Markdown + 元数据', value: 'markdown-meta' }
]

function onExportSelect({ value }) {
  showExportSheet.value = false
  if (!note.value) return
  try {
    exportSingleNote(note.value, value)
    showSuccessToast('已导出')
  } catch (e) {
    showToast('导出失败：' + (e.message || '未知错误'))
  }
}

const showVersionsPopup = ref(false)
const showViewModal = ref(false)
const versionsLoading = ref(false)
const viewLoading = ref(false)
const versions = ref([])
const activeVersion = ref(null)
const restoringId = ref(null)

// 笔记有效背景色（笔记色 → 用户默认色 → 系统默认白/深蓝灰）
const effectiveBg = computed(() =>
  note.value
    ? resolveNoteColor(note.value.backgroundColor, auth.user?.defaultNoteColor, theme.isDarkEffective)
    : (theme.isDarkEffective ? '#20242c' : '#FFFFFF')
)
const textColor = computed(() => getContrastColor(effectiveBg.value))
const subTextColor = computed(() => isDarkColor(effectiveBg.value) ? 'rgba(255,255,255,0.6)' : 'var(--text-tertiary)')
const navIconColor = computed(() => isDarkColor(effectiveBg.value) ? '#FFFFFF' : '#323233')

// 整页背景色
const pageStyle = computed(() => ({
  background: effectiveBg.value
}))

// Tag 样式：深色背景 → 半透明白底白字
const tagStyle = computed(() => {
  if (isDarkColor(effectiveBg.value)) {
    return {
      background: 'rgba(255, 255, 255, 0.2)',
      color: '#FFFFFF',
      borderColor: 'rgba(255, 255, 255, 0.3)'
    }
  }
  return {}
})

async function loadNote() {
  try {
    // 确保用户信息已加载（含 defaultNoteColor）
    if (!auth.user) {
      try { await auth.fetchUser() } catch { /* ignore */ }
    }
    note.value = await http.get(`/notes/${route.params.id}`)
  } catch {
    showToast('加载失败')
  }
}

async function onTogglePin() {
  if (!note.value) return
  try {
    if (note.value.isPinned) {
      await http.post(`/notes/${note.value.id}/unpin`)
      note.value.isPinned = false
      note.value.pinnedAt = null
      showToast('已取消置顶')
    } else {
      await http.post(`/notes/${note.value.id}/pin`)
      note.value.isPinned = true
      note.value.pinnedAt = new Date().toISOString()
      showToast('已置顶')
    }
  } catch {
    /* 拦截器处理 */
  }
}

function isImage(contentType) {
  return contentType?.startsWith('image/')
}

function formatSize(bytes) {
  if (!bytes) return ''
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`
}

function attachmentUrl(id, fileName) {
  const token = localStorage.getItem('token')
  return `/api/attachments/${id}?access_token=${encodeURIComponent(token)}`
}

// ================= 版本列表 =================
async function openVersions() {
  showVersionsPopup.value = true
  versionsLoading.value = true
  try {
    versions.value = await http.get(`/notes/${route.params.id}/versions`)
  } finally {
    versionsLoading.value = false
  }
}

async function viewVersion(v) {
  showViewModal.value = true
  viewLoading.value = true
  try {
    const detail = await http.get(`/notes/${route.params.id}/versions/${v.id}`)
    activeVersion.value = { ...detail, isCurrent: v.isCurrent }
  } finally {
    viewLoading.value = false
  }
}

async function restoreVersion(v) {
  try {
    await showConfirmDialog({
      title: '恢复版本',
      message: `确定要将笔记恢复为「${formatDateTime(v.createdAt)}」的版本吗？\n当前版本会作为历史保留。`
    })
    await restoreById(v.id)
  } catch {
    // 取消
  }
}

async function restoreById(vid) {
  restoringId.value = vid
  try {
    await http.post(`/notes/${route.params.id}/versions/${vid}/restore`)
    showToast('已恢复')
    showVersionsPopup.value = false
    showViewModal.value = false
    // 重新加载详情
    await loadNote()
    // 重新打开版本列表（多了一个新版本）
    versionsLoading.value = true
    try {
      versions.value = await http.get(`/notes/${route.params.id}/versions`)
    } finally {
      versionsLoading.value = false
    }
  } finally {
    restoringId.value = null
  }
}

onMounted(() => {
  loadNote()
  window.addEventListener('keydown', handleGlobalKeyDown)
})
</script>

<style scoped>
.page {
  min-height: 100vh;
  padding-bottom: 40px;
}
.nav-right {
  display: flex;
  gap: 18px;
  align-items: center;
  padding-right: 10px;
}
.detail-container {
  display: flex;
  justify-content: center;
  align-items: flex-start;
  gap: 28px;
  padding: 16px;
  position: relative;
}

.detail-main {
  flex: 1;
  min-width: 0;
  max-width: 820px;
}

.desktop-toc-aside {
  display: none;
}

.detail-title {
  font-size: 22px;
  font-weight: 700;
  margin: 0 0 12px;
  line-height: 1.4;
  color: inherit; /* 跟随父级文字色 */
}
.detail-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  margin-bottom: 8px;
}
.detail-time {
  font-size: 12px;
  margin-bottom: 20px;
}
.section-title {
  font-size: 14px;
  font-weight: 600;
  color: var(--text-secondary);
  margin: 24px 0 8px;
  padding: 0 4px;
}
.att-icon {
  margin-right: 8px;
  font-size: 18px;
  color: var(--color-primary);
}

/* ========== 桌面端大纲侧栏 ========== */
.toc-card {
  padding: 14px 16px;
  border-radius: 12px;
  background: var(--surface);
  border: 1px solid var(--border);
  box-shadow: 0 4px 16px rgba(0, 0, 0, 0.04);
}

:global(body.dark) .toc-card {
  background: #242830;
  border-color: rgba(255, 255, 255, 0.08);
  box-shadow: 0 4px 16px rgba(0, 0, 0, 0.24);
}

.toc-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding-bottom: 10px;
  margin-bottom: 10px;
  border-bottom: 1px solid var(--border);
}

.toc-header-title {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 13px;
  font-weight: 600;
}

.toc-header-icon {
  font-size: 14px;
  color: var(--color-primary);
}

.toc-badge {
  font-size: 11px;
  padding: 1px 6px;
  border-radius: 10px;
  background: rgba(25, 137, 250, 0.1);
  color: var(--color-primary);
  font-weight: 500;
}

.toc-header-actions {
  display: flex;
  align-items: center;
  gap: 4px;
}

.toc-action-btn {
  background: none;
  border: none;
  font-size: 11px;
  color: var(--text-secondary);
  cursor: pointer;
  padding: 2px 4px;
  border-radius: 4px;
  transition: color 0.15s, background 0.15s;
}

.toc-action-btn:hover {
  color: var(--color-primary);
  background: rgba(25, 137, 250, 0.08);
}

.toc-action-sep {
  font-size: 11px;
  opacity: 0.4;
}

.toc-nav-list {
  display: flex;
  flex-direction: column;
  gap: 2px;
  max-height: calc(100vh - 180px);
  overflow-y: auto;
}

.toc-nav-item {
  display: flex;
  align-items: center;
  gap: 8px;
  width: 100%;
  text-align: left;
  border: none;
  background: transparent;
  padding: 6px 8px;
  border-radius: 6px;
  font-size: 13px;
  color: var(--text-secondary);
  cursor: pointer;
  line-height: 1.4;
  transition: all 0.15s ease;
  box-sizing: border-box;
}

.toc-nav-item:hover {
  background: rgba(128, 128, 128, 0.08);
  color: var(--text-primary);
}

.toc-nav-item.is-active {
  color: var(--color-primary);
  font-weight: 600;
  background: rgba(25, 137, 250, 0.08);
}

.toc-indicator {
  width: 4px;
  height: 4px;
  border-radius: 50%;
  background: currentColor;
  opacity: 0.4;
  flex-shrink: 0;
  transition: transform 0.15s, opacity 0.15s;
}

.toc-nav-item.is-active .toc-indicator {
  opacity: 1;
  transform: scale(1.5);
  background: var(--color-primary);
}

.toc-level-1 {
  font-weight: 500;
  padding-left: 8px;
}
.toc-level-2 {
  padding-left: 18px;
  font-size: 12.5px;
}
.toc-level-3 {
  padding-left: 28px;
  font-size: 12px;
  opacity: 0.9;
}
.toc-level-4, .toc-level-5, .toc-level-6 {
  padding-left: 36px;
  font-size: 11.5px;
  opacity: 0.8;
}

.toc-text {
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.toc-fold-flag {
  font-size: 10px;
  padding: 1px 4px;
  border-radius: 4px;
  background: rgba(128, 128, 128, 0.15);
  color: var(--text-secondary);
  flex-shrink: 0;
}

/* ========== 移动端悬浮大纲胶囊 ========== */
.mobile-toc-capsule {
  position: fixed;
  right: 18px;
  bottom: calc(24px + env(safe-area-inset-bottom, 0px));
  z-index: 80;
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 8px 14px;
  border-radius: 24px;
  background: rgba(255, 255, 255, 0.94);
  backdrop-filter: blur(12px);
  -webkit-backdrop-filter: blur(12px);
  border: 1px solid rgba(0, 0, 0, 0.1);
  box-shadow: 0 4px 20px rgba(0, 0, 0, 0.12);
  color: var(--text-primary);
  font-size: 13px;
  font-weight: 500;
  cursor: pointer;
  user-select: none;
  transition: transform 0.15s ease, box-shadow 0.15s ease;
}

.mobile-toc-capsule:active {
  transform: scale(0.95);
}

:global(body.dark) .mobile-toc-capsule {
  background: rgba(36, 40, 48, 0.94);
  border-color: rgba(255, 255, 255, 0.12);
  box-shadow: 0 4px 20px rgba(0, 0, 0, 0.35);
  color: #fff;
}

.mtc-icon {
  font-size: 13px;
  color: var(--color-primary);
}

.mtc-text {
  font-size: 13px;
}

.mtc-count {
  font-size: 11px;
  font-weight: 600;
  padding: 1px 6px;
  border-radius: 10px;
  background: rgba(25, 137, 250, 0.1);
  color: var(--color-primary);
}

/* ========== 移动端底部大纲抽屉 ========== */
.mobile-toc-popup {
  display: flex;
  flex-direction: column;
}

.mobile-toc-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 14px 18px 12px;
  border-bottom: 1px solid var(--border);
  position: sticky;
  top: 0;
  background: inherit;
  z-index: 2;
}

.mth-left {
  display: flex;
  align-items: center;
  gap: 8px;
}

.mth-title {
  font-size: 16px;
  font-weight: 600;
}

.mth-count {
  font-size: 12px;
  padding: 1px 6px;
  border-radius: 10px;
  background: rgba(25, 137, 250, 0.1);
  color: var(--color-primary);
}

.mth-actions {
  display: flex;
  align-items: center;
  gap: 8px;
}

.mth-btn {
  background: rgba(128, 128, 128, 0.08);
  border: none;
  font-size: 12px;
  padding: 4px 8px;
  border-radius: 4px;
  color: var(--text-primary);
  cursor: pointer;
}

.mth-btn:active {
  background: rgba(25, 137, 250, 0.15);
  color: var(--color-primary);
}

.mth-btn-sep {
  font-size: 12px;
  opacity: 0.3;
}

.mth-close {
  margin-left: 4px;
  color: var(--text-secondary);
  cursor: pointer;
}

.mobile-toc-list {
  padding: 10px 14px 28px;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.mobile-toc-item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 12px;
  border-radius: 8px;
  font-size: 14px;
  color: var(--text-primary);
  cursor: pointer;
  transition: background 0.15s ease;
}

.mobile-toc-item:active {
  background: rgba(25, 137, 250, 0.08);
}

.mobile-toc-item.is-active {
  background: rgba(25, 137, 250, 0.1);
  color: var(--color-primary);
  font-weight: 600;
}

.mti-indicator {
  width: 5px;
  height: 5px;
  border-radius: 50%;
  background: currentColor;
  opacity: 0.4;
  flex-shrink: 0;
}

.mobile-toc-item.is-active .mti-indicator {
  opacity: 1;
  transform: scale(1.4);
  background: var(--color-primary);
}

.mti-level-1 {
  font-weight: 600;
  padding-left: 10px;
}
.mti-level-2 {
  padding-left: 22px;
  font-size: 13.5px;
}
.mti-level-3 {
  padding-left: 34px;
  font-size: 13px;
  opacity: 0.9;
}
.mti-level-4, .mti-level-5, .mti-level-6 {
  padding-left: 44px;
  font-size: 12.5px;
  opacity: 0.8;
}

.mti-text {
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.mti-fold-flag {
  font-size: 11px;
  padding: 1px 6px;
  border-radius: 4px;
  background: rgba(128, 128, 128, 0.12);
  color: var(--text-secondary);
  flex-shrink: 0;
}
.loading {
  display: flex;
  justify-content: center;
  padding: 60px 0;
}

/* ========== 版本列表 ========== */
.versions-loading {
  padding: 40px 0;
  text-align: center;
}
.versions-empty {
  padding-top: 40px;
}
.versions-list {
  margin: 12px 16px;
  background: transparent;
}
.version-item {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 8px;
  padding: 12px 14px;
  margin-bottom: 10px;
}
.version-item.is-current {
  border-color: var(--color-success);
  background: rgba(7, 193, 96, 0.08);
}
:global(body.dark) .version-item.is-current {
  background: rgba(32, 206, 120, 0.12);
}
.version-title-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 6px;
}
.version-time {
  font-size: 13px;
  font-weight: 600;
  color: var(--text-primary);
}
.version-preview-title {
  font-size: 14px;
  font-weight: 600;
  margin-bottom: 4px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.version-preview-body {
  font-size: 12px;
  color: var(--text-secondary);
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
  margin-bottom: 8px;
}
.version-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  margin-bottom: 8px;
}
.version-actions {
  display: flex;
  gap: 8px;
  justify-content: flex-end;
  border-top: 1px dashed var(--border);
  padding-top: 8px;
}

/* ========== 版本详情查看 Modal ========== */
.version-detail {
  padding: 16px 18px 40px;
  overflow-y: auto;
}
.vd-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  margin-bottom: 10px;
}
.vd-title {
  font-size: 18px;
  font-weight: 700;
  margin: 0 0 16px;
  padding-bottom: 10px;
  border-bottom: 1px solid var(--border);
}

/* 移动端默认：第二行工具栏布局 */
@media (max-width: 1023px) {
  :deep(.van-nav-bar__title) {
    max-width: 70%;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    font-size: 16px;
  }
  .nav-actions--desktop {
    display: none !important;
  }
  /* 移动端第二行工具栏：全宽独占一行 */
  .nav-actions--mobile {
    display: flex;
    align-items: center;
    justify-content: space-around;
    padding: 8px 10px 10px;
    gap: 4px;
    background: inherit;
    border-bottom: 1px solid rgba(128, 128, 128, 0.1);
  }
  .nav-actions--mobile .van-icon {
    font-size: 20px;
  }
}

/* 桌面端：保持单行布局，隐藏移动端第二行工具栏 */
@media (min-width: 1024px) {
  .nav-actions--mobile {
    display: none !important;
  }
  .nav-actions--desktop {
    display: flex;
    align-items: center;
    gap: 12px;
  }
}

/* 桌面端：居中阅读宽度 + 版本模态改为居中 Dialog 样式 */
@media (min-width: 1024px) {
  .page {
    max-width: var(--page-width-compact, 860px);
    margin: 0 auto;
    padding-bottom: 48px;
    transition: max-width 0.2s ease;
  }
  .page.has-desktop-toc {
    max-width: var(--page-width-wide, 1180px);
  }
  .detail-container {
    padding: 24px 32px;
  }
  .desktop-toc-aside {
    display: block;
    width: 240px;
    flex-shrink: 0;
    position: sticky;
    top: 64px;
    z-index: 10;
  }
  .detail-title {
    font-size: 26px;
  }
  .markdown-body {
    font-size: 16px;
    line-height: 1.8;
  }

  /* 桌面端把版本 Popup 改为居中 Modal 风格 */
  :deep(.van-popup--bottom) {
    max-width: 720px;
    margin: 3vh auto;
    left: 50%;
    transform: translateX(-50%);
    border-radius: 12px;
    box-shadow: 0 8px 30px rgba(0,0,0,0.12);
  }
}

/* ========== 浮动正文搜索栏 ========== */
.floating-search-bar {
  position: fixed;
  top: 54px;
  right: 24px;
  z-index: 95;
  width: 360px;
  max-width: calc(100vw - 32px);
  border-radius: 12px;
  background: rgba(255, 255, 255, 0.95);
  backdrop-filter: blur(16px);
  -webkit-backdrop-filter: blur(16px);
  border: 1px solid var(--border);
  box-shadow: 0 8px 26px rgba(0, 0, 0, 0.12);
  padding: 6px 10px;
  box-sizing: border-box;
}

:global(body.dark) .floating-search-bar {
  background: rgba(36, 40, 48, 0.95);
  border-color: rgba(255, 255, 255, 0.12);
  box-shadow: 0 8px 30px rgba(0, 0, 0, 0.4);
}

@media (max-width: 1023px) {
  .floating-search-bar {
    top: 94px;
    left: 12px;
    right: 12px;
    width: auto;
  }
}

.search-bar-inner {
  display: flex;
  align-items: center;
  gap: 8px;
}

.search-bar-icon {
  font-size: 16px;
  color: #1989fa;
  flex-shrink: 0;
  opacity: 0.85;
}

.search-bar-input {
  flex: 1;
  min-width: 0;
  border: none;
  background: transparent;
  font-size: 13.5px;
  color: inherit;
  outline: none;
  padding: 4px 0;
}

.search-bar-input::placeholder {
  color: var(--text-secondary);
  opacity: 0.65;
  font-size: 12.5px;
}

.search-bar-badge {
  font-size: 11px;
  padding: 2px 7px;
  border-radius: 10px;
  background: rgba(25, 137, 250, 0.12);
  color: #1989fa;
  font-weight: 600;
  flex-shrink: 0;
  white-space: nowrap;
}

.search-bar-badge.is-zero {
  background: rgba(238, 10, 36, 0.12);
  color: #ee0a24;
}

.search-bar-controls {
  display: flex;
  align-items: center;
  gap: 3px;
  flex-shrink: 0;
}

.search-bar-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 26px;
  height: 26px;
  border-radius: 6px;
  border: none;
  background: transparent;
  color: var(--text-secondary);
  cursor: pointer;
  padding: 0;
  transition: all 0.15s ease;
}

.search-bar-btn:hover:not(:disabled) {
  background: rgba(128, 128, 128, 0.12);
  color: var(--text-primary);
}

.search-bar-btn:disabled {
  opacity: 0.3;
  cursor: not-allowed;
}

.search-bar-close:hover {
  background: rgba(238, 10, 36, 0.12) !important;
  color: #ee0a24 !important;
}

.search-bar-sep {
  width: 1px;
  height: 14px;
  background: var(--border);
  margin: 0 2px;
}

/* 展开与收起平滑过渡 */
.search-slide-enter-active,
.search-slide-leave-active {
  transition: opacity 0.18s ease, transform 0.18s ease;
}

.search-slide-enter-from,
.search-slide-leave-to {
  opacity: 0;
  transform: translateY(-8px) scale(0.98);
}
</style>
