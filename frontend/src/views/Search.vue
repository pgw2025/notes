<template>
  <div class="page" :class="{ 'page--desktop': isDesktop }">
    <van-nav-bar title="搜索笔记" />

    <!-- ==================== 桌面端：双栏即时检索 + Markdown 预览 ==================== -->
    <div v-if="isDesktop" class="search-desktop">
      <!-- 宽屏顶栏：宽幅搜索框 + 热门标签快速检索气泡 -->
      <div class="search-desktop-topbar">
        <van-search
          v-model="keyword"
          placeholder="搜索笔记标题、内容或标签..."
          shape="round"
          clearable
          class="desktop-search-input"
          @search="onSearch"
          @clear="onClear"
          @keyup.enter="onSearch"
        />
        <div v-if="popularTags.length" class="hot-tag-chips">
          <button
            v-for="t in popularTags"
            :key="t.id"
            class="chip-btn"
            @click="quickSearch(t.name)"
          >
            <span class="chip-hash">#</span> {{ t.name }}
          </button>
        </div>
      </div>

      <div class="search-desktop-body">
        <!-- 左栏：结果流 (440px) -->
        <aside class="results-pane">
          <div v-if="searched" class="results-header">
            <span class="results-count">找到 {{ results.length }} 篇相关笔记</span>
            <span class="results-kw">「{{ currentKeyword }}」</span>
          </div>

          <div v-if="searched && results.length === 0" class="empty-state">
            <div class="empty-icon">🔍</div>
            <p class="empty-title">未找到相关笔记</p>
            <p class="empty-desc">尝试搜索其他关键词或缩短搜索词</p>
          </div>

          <van-list
            v-else-if="searched"
            v-model:loading="loading"
            :finished="finished"
            finished-text="没有更多了"
            class="results-list"
            @load="onLoadMore"
          >
            <div
              v-for="n in results"
              :key="n.id"
              class="result-card"
              :class="{ 'is-selected': selectedId === n.id }"
              @click="selectNote(n)"
            >
              <div class="card-header">
                <h3 class="result-title">{{ n.title || '无标题笔记' }}</h3>
                <span class="result-time">{{ formatTime(n.updatedAt) }}</span>
              </div>
              <p class="result-preview">{{ n.contentPreview || '暂无文字内容' }}</p>
              <div class="card-footer" v-if="n.categoryName || (n.tags && n.tags.length)">
                <span v-if="n.categoryName" class="cat-badge">📁 {{ n.categoryName }}</span>
                <div class="tag-badges" v-if="n.tags && n.tags.length">
                  <span v-for="t in n.tags" :key="t.id || t" class="tag-badge">#{{ t.name || t }}</span>
                </div>
              </div>
            </div>
          </van-list>

          <div v-else class="desktop-empty-guide">
            <div class="empty-icon">💡</div>
            <p class="prompt-title">即时全文检索</p>
            <p class="prompt-desc">输入关键词或点击上方标签，实时检索标题、正文与标签</p>
          </div>
        </aside>

        <!-- 右栏：即时阅读工作台 -->
        <main class="preview-pane">
          <template v-if="selectedId">
            <div class="preview-toolbar">
              <span class="preview-title" v-if="selectedNote">{{ selectedNote.title || '无标题笔记' }}</span>
              <div class="preview-actions">
                <van-button size="small" round plain icon="expand-o" @click="goFullscreen">全屏阅读</van-button>
                <van-button size="small" round type="primary" icon="edit" @click="goEdit">编辑笔记</van-button>
              </div>
            </div>
            <div v-if="previewLoading" class="preview-loading">
              <van-loading type="spinner" color="var(--color-primary)">加载正文中…</van-loading>
            </div>
            <div v-else-if="selectedNote" class="preview-scroll">
              <markdown-body :content="selectedNote.content" :collapsible="true" />
            </div>
          </template>
          <div v-else class="preview-empty">
            <div class="preview-empty-icon">📄</div>
            <p>从左侧选择一篇笔记，即可在此即时预览正文</p>
          </div>
        </main>
      </div>
    </div>

    <!-- ==================== 移动端：保持单列结果流 ==================== -->
    <div v-else>
      <div class="search-box-wrap">
        <van-search
          v-model="keyword"
          placeholder="搜索笔记标题、内容或标签..."
          show-action
          shape="round"
          clearable
          @search="onSearch"
          @clear="onClear"
          @keyup.enter="onSearch"
        >
          <template #action>
            <div class="search-btn" @click="onSearch">搜索</div>
          </template>
        </van-search>
      </div>

      <div v-if="searched" class="search-content">
        <div class="results-header">
          <span class="results-count">找到 {{ results.length }} 篇相关笔记</span>
          <span class="results-kw">关键词: 「{{ currentKeyword }}」</span>
        </div>

        <div v-if="results.length === 0" class="empty-state">
          <div class="empty-icon">🔍</div>
          <p class="empty-title">未找到相关笔记</p>
          <p class="empty-desc">尝试搜索其他关键词或缩短搜索词</p>
        </div>

        <van-list
          v-else
          v-model:loading="loading"
          :finished="finished"
          finished-text="没有更多了"
          class="results-list"
          @load="onLoadMore"
        >
          <div
            v-for="n in results"
            :key="n.id"
            class="result-card"
            @click="$router.push(`/notes/${n.id}`)"
          >
            <div class="card-header">
              <h3 class="result-title">{{ n.title || '无标题笔记' }}</h3>
              <span class="result-time">{{ formatTime(n.updatedAt) }}</span>
            </div>

            <p class="result-preview">{{ n.contentPreview || '暂无文字内容' }}</p>

            <div class="card-footer" v-if="n.categoryName || n.category || (n.tags && n.tags.length)">
              <span v-if="n.categoryName || n.category" class="cat-badge">📁 {{ n.categoryName || n.category?.name }}</span>
              <div class="tag-badges" v-if="n.tags && n.tags.length">
                <span v-for="t in n.tags" :key="t.id || t" class="tag-badge">#{{ t.name || t }}</span>
              </div>
            </div>
          </div>
        </van-list>
      </div>

      <div v-else class="search-content">
        <div v-if="popularTags.length" class="quick-section">
          <div class="quick-title">热门标签</div>
          <div class="quick-chips">
            <button
              v-for="t in popularTags"
              :key="t.id"
              class="chip-btn"
              @click="quickSearch(t.name)"
            >
              <span class="chip-hash">#</span> {{ t.name }}
            </button>
          </div>
        </div>

        <div class="empty-prompt">
          <div class="empty-icon">💡</div>
          <p class="prompt-title">即时全文检索</p>
          <p class="prompt-desc">支持实时检索笔记标题、正文文本以及关联的标签</p>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import http from '../api/http'
import { formatTime } from '../utils/format'
import { useResponsive } from '../composables/useResponsive'
import MarkdownBody from '../components/MarkdownBody.vue'

const route = useRoute()
const router = useRouter()
const { isDesktop } = useResponsive()
const keyword = ref('')
const currentKeyword = ref('')
const results = ref([])
const searched = ref(false)
const loading = ref(false)
const finished = ref(false)
const currentPage = ref(1)
const pageSize = 20
const popularTags = ref([])

// 桌面端：选中笔记即时预览
const selectedId = ref(null)
const selectedNote = ref(null)
const previewLoading = ref(false)

async function selectNote(n) {
  if (selectedId.value === n.id) return
  selectedId.value = n.id
  previewLoading.value = true
  try {
    selectedNote.value = await http.get(`/notes/${n.id}`)
  } catch {
    selectedNote.value = null
  } finally {
    previewLoading.value = false
  }
}

function goFullscreen() {
  if (selectedId.value) router.push(`/notes/${selectedId.value}`)
}

function goEdit() {
  if (selectedId.value) router.push(`/notes/${selectedId.value}/edit`)
}

async function loadPopularTags() {
  try {
    const res = await http.get('/tags')
    popularTags.value = (res || []).slice(0, 12)
  } catch {}
}

async function onSearch() {
  const q = keyword.value.trim()
  if (!q) {
    searched.value = false
    results.value = []
    return
  }
  currentKeyword.value = q
  results.value = []
  currentPage.value = 1
  finished.value = false
  loading.value = true
  try {
    const res = await http.get('/notes/search', { params: { q, page: currentPage.value, pageSize } })
    results.value = res.items || []
    if (!res.hasMore) finished.value = true
    searched.value = true
  } finally {
    loading.value = false
  }
}

function quickSearch(tag) {
  keyword.value = tag
  onSearch()
}

async function onLoadMore() {
  try {
    currentPage.value++
    const q = currentKeyword.value
    const res = await http.get('/notes/search', { params: { q, page: currentPage.value, pageSize } })
    results.value.push(...(res.items || []))
    if (!res.hasMore) finished.value = true
  } catch {
    currentPage.value--
  } finally {
    loading.value = false
  }
}

function onClear() {
  searched.value = false
  results.value = []
  finished.value = false
}

onMounted(() => {
  loadPopularTags()
  if (route.query.q) {
    keyword.value = String(route.query.q)
    onSearch()
  }
})
</script>

<style scoped>
.page {
  min-height: 100vh;
  padding-bottom: 80px;
  background: var(--app-bg);
}

.search-box-wrap {
  position: sticky;
  top: 0;
  z-index: 10;
  background: var(--surface);
  border-bottom: 1px solid var(--border);
}

:deep(.van-search) {
  background: transparent;
  padding: 10px 14px;
}

:deep(.van-search__content) {
  background: var(--surface-2);
}

.search-btn {
  color: var(--color-primary);
  font-weight: 600;
  font-size: 14px;
  cursor: pointer;
  padding: 0 4px;
}

.search-content {
  padding: 16px;
}

.results-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 4px 4px 12px;
  border-bottom: 1px solid var(--border);
  margin-bottom: 14px;
}

.results-count {
  font-size: 13.5px;
  font-weight: 600;
  color: var(--text-primary);
}

.results-kw {
  font-size: 12px;
  color: var(--text-tertiary);
}

.results-list {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.result-card {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 12px;
  padding: 14px 16px;
  box-shadow: var(--shadow-xs);
  cursor: pointer;
  transition: all 0.18s ease;
}

.result-card:hover {
  transform: translateY(-2px);
  box-shadow: var(--shadow-sm);
  border-color: var(--color-primary);
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 12px;
  margin-bottom: 6px;
}

.result-title {
  font-size: 15px;
  font-weight: 600;
  color: var(--text-primary);
  margin: 0;
  flex: 1;
}

.result-time {
  font-size: 11.5px;
  color: var(--text-tertiary);
  white-space: nowrap;
}

.result-preview {
  font-size: 13px;
  color: var(--text-secondary);
  line-height: 1.5;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
  margin: 0 0 10px;
}

.card-footer {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 6px;
  padding-top: 8px;
  border-top: 1px dashed var(--divider);
}

.cat-badge {
  font-size: 11px;
  color: var(--color-primary);
  background: rgba(59, 130, 246, 0.08);
  padding: 2px 8px;
  border-radius: 6px;
}

.tag-badges {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
}

.tag-badge {
  font-size: 11px;
  color: var(--text-tertiary);
  background: var(--surface-2);
  padding: 2px 6px;
  border-radius: 4px;
}

/* 热门标签推荐 */
.quick-section {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 12px;
  padding: 16px;
  margin-bottom: 20px;
}

.quick-title {
  font-size: 13px;
  font-weight: 600;
  color: var(--text-tertiary);
  text-transform: uppercase;
  letter-spacing: 0.5px;
  margin-bottom: 12px;
}

.quick-chips {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.chip-btn {
  background: var(--surface-2);
  border: 1px solid var(--border);
  border-radius: 16px;
  padding: 5px 12px;
  font-size: 13px;
  color: var(--text-primary);
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  gap: 4px;
  transition: all 0.15s ease;
}

.chip-btn:hover {
  background: var(--surface-3);
  border-color: var(--color-primary);
  color: var(--color-primary);
}

.chip-hash {
  color: var(--color-primary);
  font-weight: 700;
}

.empty-state, .empty-prompt {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 60px 20px;
  text-align: center;
}

.empty-icon {
  font-size: 44px;
  margin-bottom: 12px;
}

.empty-title, .prompt-title {
  font-size: 16px;
  font-weight: 600;
  color: var(--text-primary);
  margin: 0 0 6px;
}

.empty-desc, .prompt-desc {
  font-size: 13px;
  color: var(--text-tertiary);
  margin: 0;
  max-width: 280px;
}

/* 桌面端：紧凑居中宽度 */
@media (min-width: 1024px) {
  .page {
    max-width: none;
    margin: 0 auto;
    padding-bottom: 32px;
  }
}

/* ==================== 桌面端双栏工作台 ==================== */
.page--desktop {
  height: 100vh;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

.search-desktop {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-height: 0;
  background: var(--app-bg);
}

.search-desktop-topbar {
  padding: 12px 20px;
  background: var(--surface);
  border-bottom: 1px solid var(--border);
  flex-shrink: 0;
}

.desktop-search-input {
  padding: 0;
  background: transparent;
}

:deep(.desktop-search-input .van-search__content) {
  background: var(--surface-2);
}

.hot-tag-chips {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  padding-top: 8px;
}

.search-desktop-body {
  flex: 1;
  display: flex;
  min-height: 0;
}

/* 左栏：结果流 */
.results-pane {
  width: 440px;
  min-width: 360px;
  max-width: 460px;
  flex-shrink: 0;
  border-right: 1px solid var(--border);
  background: var(--surface);
  overflow-y: auto;
  padding: 16px;
  box-sizing: border-box;
}

.result-card.is-selected {
  border-color: var(--color-primary);
  background: rgba(59, 130, 246, 0.06);
  box-shadow: 0 0 0 1px var(--color-primary);
}

:global(body.dark) .result-card.is-selected {
  background: rgba(56, 189, 248, 0.12);
}

.desktop-empty-guide {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 60px 20px;
  text-align: center;
  color: var(--text-tertiary);
}

/* 右栏：即时阅读工作台 */
.preview-pane {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  background: var(--app-bg);
}

.preview-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 12px 20px;
  border-bottom: 1px solid var(--border);
  background: var(--surface);
  flex-shrink: 0;
}

.preview-title {
  font-size: 15px;
  font-weight: 600;
  color: var(--text-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  flex: 1;
  min-width: 0;
}

.preview-actions {
  display: flex;
  gap: 8px;
  flex-shrink: 0;
}

.preview-loading {
  display: flex;
  justify-content: center;
  align-items: center;
  padding: 60px 0;
}

.preview-scroll {
  flex: 1;
  overflow-y: auto;
  padding: 24px 32px 40px;
}

.preview-empty {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 12px;
  color: var(--text-tertiary);
  font-size: 14px;
}

.preview-empty-icon {
  font-size: 48px;
  opacity: 0.6;
}
</style>
