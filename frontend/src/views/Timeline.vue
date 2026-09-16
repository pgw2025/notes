<template>
  <div class="page timeline-page">
    <van-nav-bar title="时间线" fixed placeholder />

    <!-- ============ 移动端筛选栏（<1024px）============ -->
    <div v-if="!isDesktop" class="filter-bar">
      <div class="filter-row">
        <van-button
          size="small"
          plain
          :icon="filterOpen.cats ? 'checked' : 'apps-o'"
          :type="activeCategoryIds.length ? 'primary' : 'default'"
          @click="filterOpen.cats = !filterOpen.cats"
        >
          {{ activeCategoryIds.length ? `分类(${activeCategoryIds.length})` : '分类' }}
        </van-button>
        <van-button
          size="small"
          plain
          :icon="filterOpen.tags ? 'checked' : 'bookmark-o'"
          :type="activeTagIds.length ? 'primary' : 'default'"
          @click="filterOpen.tags = !filterOpen.tags"
        >
          {{ activeTagIds.length ? `标签(${activeTagIds.length})` : '标签' }}
        </van-button>
        <van-button
          size="small"
          plain
          icon="calendar-o"
          :type="(filters.fromDate || filters.toDate) ? 'primary' : 'default'"
          @click="filterOpen.dates = !filterOpen.dates"
        >
          日期
        </van-button>
      </div>

      <van-search
        v-model="filters.keyword"
        placeholder="搜索标题或正文"
        shape="round"
        :clearable="true"
        @search="reload"
        @clear="reload"
      />

      <!-- 分类多选 -->
      <div v-if="filterOpen.cats" class="filter-panel">
        <div class="filter-title">按分类筛选</div>
        <van-checkbox-group v-model="activeCategoryIds" direction="horizontal">
          <van-checkbox
            v-for="c in flatCategories"
            :key="c.id"
            :name="c.id"
            shape="square"
            class="filter-chip"
          >{{ c.name }}</van-checkbox>
        </van-checkbox-group>
        <div class="filter-actions">
          <van-button size="mini" plain @click="activeCategoryIds = []; reload()">清空</van-button>
          <van-button size="mini" type="primary" @click="filterOpen.cats = false; reload()">确定</van-button>
        </div>
      </div>

      <!-- 标签多选 -->
      <div v-if="filterOpen.tags" class="filter-panel">
        <div class="filter-title">按标签筛选（任意匹配）</div>
        <van-checkbox-group v-model="activeTagIds" direction="horizontal">
          <van-checkbox
            v-for="t in tags"
            :key="t.id"
            :name="t.id"
            shape="square"
            class="filter-chip"
          >{{ t.name }}</van-checkbox>
        </van-checkbox-group>
        <div class="filter-actions">
          <van-button size="mini" plain @click="activeTagIds = []; reload()">清空</van-button>
          <van-button size="mini" type="primary" @click="filterOpen.tags = false; reload()">确定</van-button>
        </div>
      </div>

      <!-- 日期范围 -->
      <div v-if="filterOpen.dates" class="filter-panel">
        <div class="filter-title">按修改时间范围</div>
        <div class="date-row">
          <van-field
            v-model="filters.fromDate"
            label="起始"
            is-link
            readonly
            placeholder="点击选择"
            @click="showFromPicker = true"
          />
          <van-field
            v-model="filters.toDate"
            label="截止"
            is-link
            readonly
            placeholder="点击选择"
            @click="showToPicker = true"
          />
        </div>
        <div class="filter-actions">
          <van-button size="mini" plain @click="filters.fromDate=''; filters.toDate=''; reload()">清空</van-button>
          <van-button size="mini" type="primary" @click="filterOpen.dates = false; reload()">确定</van-button>
        </div>
      </div>
    </div>

    <van-picker
      v-if="showFromPicker"
      title="选择起始日期"
      :columns="dateColumns"
      :model-value="datePickerValue('from')"
      @confirm="(v) => onDateConfirm('from', v)"
      @cancel="showFromPicker = false"
    />
    <van-picker
      v-if="showToPicker"
      title="选择截止日期"
      :columns="dateColumns"
      :model-value="datePickerValue('to')"
      @confirm="(v) => onDateConfirm('to', v)"
      @cancel="showToPicker = false"
    />

    <!-- 时间线主布局：筛选侧栏 + 中间结果 + 详情预览，侧栏/预览常驻，仅中间栏做三态切换 -->
    <div class="timeline-layout">
      <!-- ============ 桌面端左侧筛选侧栏（>=1024px）============ -->
      <aside v-if="isDesktop" class="filter-sidebar">
        <div class="sidebar-scroll">
          <van-search
            v-model="filters.keyword"
            placeholder="搜索标题或正文"
            shape="round"
            :clearable="true"
            @search="reload"
            @clear="reload"
          />

          <!-- 已选条件胶囊条 -->
          <div v-if="activeFilters.length" class="active-filters">
            <span
              v-for="(f, i) in activeFilters"
              :key="i"
              class="active-chip"
              @click="removeFilter(f)"
            >
              {{ f.label }} <van-icon name="cross" size="10" />
            </span>
            <span class="active-clear" @click="clearAllFilters">清空</span>
          </div>

          <!-- 分类 -->
          <div class="filter-group">
            <div class="group-title">
              <span>分类</span>
              <van-tag v-if="activeCategoryIds.length" plain type="primary" size="mini">{{ activeCategoryIds.length }}</van-tag>
            </div>
            <div class="group-body category-list">
              <div
                v-for="c in flatCategories"
                :key="c.id"
                class="group-option"
                :class="{ active: activeCategoryIds.includes(c.id) }"
                @click="toggleCategory(c.id)"
              >
                <van-icon :name="activeCategoryIds.includes(c.id) ? 'checked' : 'circle'" size="15" />
                <span class="option-label">{{ c.name }}</span>
                <span class="option-count">{{ categoryCount[c.id] || 0 }}</span>
              </div>
            </div>
          </div>

          <!-- 标签 -->
          <div class="filter-group">
            <div class="group-title">
              <span>标签</span>
              <van-tag v-if="activeTagIds.length" plain type="primary" size="mini">{{ activeTagIds.length }}</van-tag>
            </div>
            <van-search
              v-if="tags.length > 8"
              v-model="tagFilter"
              placeholder="筛选标签"
              shape="round"
              size="small"
            />
            <div class="group-body tag-list">
              <div
                v-for="t in filteredTags"
                :key="t.id"
                class="group-option"
                :class="{ active: activeTagIds.includes(t.id) }"
                @click="toggleTag(t.id)"
              >
                <van-icon :name="activeTagIds.includes(t.id) ? 'checked' : 'circle'" size="15" />
                <span class="option-label"># {{ t.name }}</span>
              </div>
              <div v-if="!filteredTags.length" class="group-empty">无匹配标签</div>
            </div>
          </div>

          <!-- 日期 -->
          <div class="filter-group">
            <div class="group-title">
              <span>日期范围</span>
              <van-tag v-if="filters.fromDate || filters.toDate" plain type="primary" size="mini">已选</van-tag>
            </div>
            <div class="group-body">
              <div class="quick-ranges">
                <span
                  v-for="r in quickRanges"
                  :key="r.key"
                  class="quick-chip"
                  :class="{ active: quickActive === r.key }"
                  @click="applyQuickRange(r.key)"
                >{{ r.label }}</span>
              </div>
              <div class="date-field" @click="showDateRange = true">
                <van-icon name="calendar-o" size="15" />
                <span class="date-value">{{ dateRangeText }}</span>
              </div>
            </div>
          </div>

          <!-- 结果计数 -->
          <div class="result-count">命中 <b>{{ timeline.totalNotes }}</b> 篇</div>
        </div>
      </aside>

      <van-calendar
        v-if="isDesktop"
        v-model:show="showDateRange"
        type="range"
        :min-date="minDate"
        :max-date="maxDate"
        :show-confirm="false"
        @confirm="onDateRangeConfirm"
      />

      <div class="timeline">
        <!-- 加载态 -->
        <van-loading v-if="loading" class="loading" color="#1989fa" size="24px">加载中…</van-loading>

        <!-- 空态 -->
        <div v-else-if="!timeline?.years?.length" class="empty">
          <van-empty :description="emptyText" />
        </div>

        <!-- 数据态 -->
        <template v-else>
        <template v-for="year in timeline.years" :key="year.year">
        <div class="year-group">
          <div class="year-header" @click="toggleYear(year.year)">
            <van-icon :name="yearExpanded[year.year] ? 'arrow-down' : 'arrow'" size="14" />
            <span class="year-title">{{ year.year }} 年</span>
            <van-tag plain color="#969799" class="year-count">{{ year.noteCount }} 篇</van-tag>
            <van-tag v-if="year.isCurrentYear" size="medium" type="primary" plain class="year-current">今年</van-tag>
          </div>

          <div v-show="yearExpanded[year.year]" class="year-body">
            <template v-for="month in year.months" :key="`${year.year}-${month.month}`">
              <div class="month-group">
                <div class="month-header" @click="toggleMonth(year.year, month.month)">
                  <van-icon :name="monthExpanded[keyOfMonth(year.year, month.month)] ? 'arrow-down' : 'arrow'" size="12" />
                  <span class="month-title">{{ month.monthLabel }}</span>
                  <span class="month-count">{{ month.noteCount }} 篇</span>
                  <van-tag v-if="month.isCurrentMonth" size="medium" type="warning" plain class="month-current">本月</van-tag>
                </div>

                <div v-show="monthExpanded[keyOfMonth(year.year, month.month)]" class="month-body">
                  <template v-for="day in month.days" :key="`${year.year}-${month.month}-${day.date}`">
                    <div class="day-group">
                      <div class="day-dot"></div>
                      <div class="day-header">
                        <span class="day-title">{{ formatDate(day.date) }} · {{ day.weekdayLabel }}</span>
                        <span class="day-count">{{ day.noteCount }} 篇</span>
                      </div>
                      <div class="notes-list">
                        <div
                          v-for="n in day.notes"
                          :key="n.id"
                          class="note-card"
                          :class="{ selected: n.id === selectedNoteId }"
                          @click="openNote(n.id)"
                        >
                          <div class="note-time">{{ formatTime(n.updatedAt) }}</div>
                          <div class="note-title">{{ n.title || '无标题' }}</div>
                          <div class="note-preview">{{ n.contentPreview || '暂无内容' }}</div>
                          <div class="note-meta">
                            <van-tag v-if="n.categoryName" plain type="primary" size="mini">{{ n.categoryName }}</van-tag>
                            <van-tag v-for="t in n.tags" :key="t" plain size="mini" color="#969799" text-color="#969799">{{ t }}</van-tag>
                          </div>
                        </div>
                      </div>
                    </div>
                  </template>
                </div>
              </div>
            </template>
          </div>
        </div>
      </template>
        </template>
      </div>

      <!-- 桌面端右栏：详情预览工作台 -->
      <aside v-if="isDesktop" class="timeline-preview">
        <div v-if="previewLoading" class="preview-loading">
          <van-loading color="var(--color-primary)">加载中…</van-loading>
        </div>

        <div v-else-if="selectedNote" class="preview-note">
          <div class="preview-toolbar">
            <span class="preview-crumb">预览</span>
            <div class="preview-toolbar-actions">
              <button class="preview-action-btn" @click="$router.push(`/notes/${selectedNote.id}`)" title="全屏阅读">
                <van-icon name="expand-o" size="14" />
                <span>阅读</span>
              </button>
              <button class="preview-action-btn" @click="$router.push(`/notes/${selectedNote.id}/edit`)" title="编辑笔记">
                <van-icon name="edit" size="14" />
                <span>编辑</span>
              </button>
            </div>
          </div>

          <h2 class="preview-title">{{ selectedNote.title || '无标题' }}</h2>
          <div class="preview-meta">
            <span v-if="selectedNote.categoryName" class="preview-cat">📁 {{ selectedNote.categoryName }}</span>
            <span v-for="t in (selectedNote.tags || [])" :key="t" class="preview-tag">#{{ t }}</span>
          </div>
          <div class="preview-body">
            <markdown-body :content="selectedNote.content" :collapsible="true" />
          </div>
        </div>

        <div v-else class="preview-empty">
          <div class="preview-empty-icon">🕐</div>
          <p>点击左侧时间线中的笔记</p>
          <span>即可在此即时预览正文</span>
        </div>
      </aside>
    </div>
  </div>
</template>

<script setup>
import { reactive, ref, onMounted, nextTick, watch, computed } from 'vue'
import { useRouter } from 'vue-router'
import http from '../api/http'
import { flattenCategories } from '../utils/categoryTree'
import { useResponsive } from '../composables/useResponsive'
import MarkdownBody from '../components/MarkdownBody.vue'
import { formatDateTime } from '../utils/format'

const router = useRouter()
const { isDesktop } = useResponsive()

const loading = ref(false)
const timeline = ref({ totalNotes: 0, years: [] })

// ============ 桌面端右栏预览 ============
const selectedNoteId = ref(null)
const selectedNote = ref(null)
const previewLoading = ref(false)

async function selectNote(n) {
  selectedNoteId.value = n.id
  previewLoading.value = true
  try {
    selectedNote.value = await http.get(`/notes/${n.id}`)
  } finally {
    previewLoading.value = false
  }
}

// ============ 过滤器 ============
const filters = reactive({
  keyword: '',
  fromDate: '',
  toDate: ''
})
const activeCategoryIds = ref([])
const activeTagIds = ref([])
const categories = ref([])
const tags = ref([])

// 扁平化分类（树形结构 → 一维，用于筛选 checkbox）
const flatCategories = computed(() => flattenCategories(categories.value).map((c) => ({ id: c.id, name: c.name })))

const filterOpen = reactive({ cats: false, tags: false, dates: false })
const showFromPicker = ref(false)
const showToPicker = ref(false)

// ============ 桌面端筛选侧栏 ============
const tagFilter = ref('')
const showDateRange = ref(false)

// 日期范围日历边界
const minDate = new Date()
minDate.setFullYear(minDate.getFullYear() - 10)
const maxDate = new Date()

// 快捷区间
const quickRanges = [
  { key: 'today', label: '今天' },
  { key: 'week', label: '本周' },
  { key: 'month', label: '本月' },
  { key: 'year', label: '今年' },
  { key: 'all', label: '全部' }
]
const quickActive = ref('all')

// 标签过滤（>8 个时显示搜索）
const filteredTags = computed(() => {
  const kw = tagFilter.value.trim().toLowerCase()
  if (!kw) return tags.value
  return tags.value.filter((t) => t.name.toLowerCase().includes(kw))
})

// 已选条件胶囊（用于顶部汇总条）
const activeFilters = computed(() => {
  const list = []
  const catMap = new Map(flatCategories.value.map((c) => [c.id, c.name]))
  activeCategoryIds.value.forEach((id) => {
    list.push({ type: 'category', label: `分类:${catMap.get(id) || id}`, value: id })
  })
  const tagMap = new Map(tags.value.map((t) => [t.id, t.name]))
  activeTagIds.value.forEach((id) => {
    list.push({ type: 'tag', label: `标签:${tagMap.get(id) || id}`, value: id })
  })
  if (filters.fromDate || filters.toDate) {
    list.push({ type: 'date', label: `日期:${dateRangeText.value}`, value: null })
  }
  return list
})

const dateRangeText = computed(() => {
  const f = filters.fromDate, t = filters.toDate
  if (f && t) return `${f} ~ ${t}`
  if (f) return `${f} 起`
  if (t) return `至 ${t}`
  return '选择日期范围'
})

// 空态文案：区分「无任何笔记」与「当前筛选条件下无匹配」
const hasActiveFilter = computed(() =>
  activeCategoryIds.value.length > 0 ||
  activeTagIds.value.length > 0 ||
  !!filters.keyword ||
  !!filters.fromDate ||
  !!filters.toDate
)
const emptyText = computed(() =>
  hasActiveFilter.value ? '当前筛选条件下没有匹配的笔记' : '还没有笔记，去写第一篇吧～'
)

// 分类计数（用于侧栏显示每个分类下的笔记数，按需返回 0，避免额外请求）
const categoryCount = reactive({})

function toggleCategory(id) {
  const i = activeCategoryIds.value.indexOf(id)
  if (i >= 0) activeCategoryIds.value.splice(i, 1)
  else activeCategoryIds.value.push(id)
  reload()
}

function toggleTag(id) {
  const i = activeTagIds.value.indexOf(id)
  if (i >= 0) activeTagIds.value.splice(i, 1)
  else activeTagIds.value.push(id)
  reload()
}

function removeFilter(f) {
  if (f.type === 'category') {
    activeCategoryIds.value = activeCategoryIds.value.filter((id) => id !== f.value)
    reload()
  } else if (f.type === 'tag') {
    activeTagIds.value = activeTagIds.value.filter((id) => id !== f.value)
    reload()
  } else if (f.type === 'date') {
    filters.fromDate = ''
    filters.toDate = ''
    quickActive.value = 'all'
    reload()
  }
}

function clearAllFilters() {
  activeCategoryIds.value = []
  activeTagIds.value = []
  filters.fromDate = ''
  filters.toDate = ''
  filters.keyword = ''
  tagFilter.value = ''
  quickActive.value = 'all'
  reload()
}

function applyQuickRange(key) {
  quickActive.value = key
  const now = new Date()
  const fmt = (d) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
  if (key === 'all') {
    filters.fromDate = ''
    filters.toDate = ''
  } else if (key === 'today') {
    const s = fmt(now)
    filters.fromDate = s
    filters.toDate = s
  } else if (key === 'week') {
    const day = now.getDay() || 7
    const monday = new Date(now)
    monday.setDate(now.getDate() - day + 1)
    filters.fromDate = fmt(monday)
    filters.toDate = fmt(now)
  } else if (key === 'month') {
    filters.fromDate = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-01`
    filters.toDate = fmt(now)
  } else if (key === 'year') {
    filters.fromDate = `${now.getFullYear()}-01-01`
    filters.toDate = fmt(now)
  }
  reload()
}

function onDateRangeConfirm(values) {
  const [start, end] = values
  const fmt = (d) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
  filters.fromDate = fmt(start)
  filters.toDate = fmt(end)
  quickActive.value = 'all'
  showDateRange.value = false
  reload()
}

// 折叠/展开状态
const yearExpanded = reactive({})
const monthExpanded = reactive({})

const keyOfMonth = (y, m) => `${y}-${m}`

function toggleYear(y) { yearExpanded[y] = !yearExpanded[y] }
function toggleMonth(y, m) { monthExpanded[keyOfMonth(y, m)] = !monthExpanded[keyOfMonth(y, m)] }

// ============ 日期选择器列 ============
const now = new Date()
const years = []
for (let y = now.getFullYear(); y >= now.getFullYear() - 10; y--) years.push(y)
const months = Array.from({ length: 12 }, (_, i) => i + 1)
const days = Array.from({ length: 31 }, (_, i) => i + 1)
const dateColumns = [
  { values: years.map(String) },
  { values: months.map(m => String(m).padStart(2, '0')) },
  { values: days.map(d => String(d).padStart(2, '0')) }
]

function datePickerValue(kind) {
  const s = kind === 'from' ? filters.fromDate : filters.toDate
  if (!s) return [String(now.getFullYear()), String(now.getMonth() + 1).padStart(2, '0'), String(now.getDate()).padStart(2, '0')]
  const d = new Date(s)
  if (isNaN(d)) return [String(now.getFullYear()), '01', '01']
  return [String(d.getFullYear()), String(d.getMonth() + 1).padStart(2, '0'), String(d.getDate()).padStart(2, '0')]
}

function onDateConfirm(kind, values) {
  const s = `${values[0]}-${values[1]}-${values[2]}`
  if (kind === 'from') filters.fromDate = s
  else filters.toDate = s
  showFromPicker.value = false
  showToPicker.value = false
}

// ============ 业务方法 ============
async function loadMeta() {
  try {
    const [cats, t] = await Promise.all([
      http.get('/categories'),
      http.get('/tags')
    ])
    categories.value = cats || []
    tags.value = t || []
  } catch { /* ignore, user already logged out or toast shown */ }
}

async function reload() {
  loading.value = true
  try {
    const params = {}
    activeCategoryIds.value.forEach((id, i) => { params[`categoryIds[${i}]`] = id })
    activeTagIds.value.forEach((id, i) => { params[`tagIds[${i}]`] = id })
    if (filters.keyword) params.keyword = filters.keyword
    if (filters.fromDate) params.fromDate = filters.fromDate
    if (filters.toDate) params.toDate = filters.toDate
    const data = await http.get('/notes/timeline', { params })
    timeline.value = data

    // 默认展开：如果有数据，默认展开今年和各月份（或所有非空月份），方便用户一目了然看到笔记
    nextTick(() => {
      if (!data?.years) return
      for (const y of data.years) {
        yearExpanded[y.year] = true
        for (const m of y.months) {
          monthExpanded[keyOfMonth(y.year, m.month)] = true
        }
      }
    })
  } finally {
    loading.value = false
  }
}

function openNote(id) {
  if (isDesktop.value) {
    selectNote({ id })
  } else {
    router.push(`/notes/${id}`)
  }
}

// ============ 格式化 ============
function pad(n) { return String(n).padStart(2, '0') }
function formatDate(d) {
  const dt = new Date(d)
  return `${dt.getMonth() + 1}月${dt.getDate()}日`
}
function formatTime(d) {
  const dt = new Date(d)
  return `${pad(dt.getHours())}:${pad(dt.getMinutes())}`
}

// ============ 生命周期 ============
onMounted(async () => {
  await loadMeta()
  await reload()
})

// 关键词防抖刷新
let kwTimer = null
watch(() => filters.keyword, () => {
  clearTimeout(kwTimer)
  kwTimer = setTimeout(reload, 400)
})
</script>

<style scoped>
.page {
  min-height: 100vh;
  padding-bottom: 80px;
  background: var(--app-bg);
}
.filter-bar {
  background: var(--surface);
  padding: 8px 12px 12px;
  border-bottom: 1px solid var(--border);
  position: sticky;
  top: 46px; /* 避让 van-nav-bar fixed */
  z-index: 20;
}
.filter-row {
  display: flex;
  gap: 8px;
  margin-bottom: 8px;
  flex-wrap: wrap;
}
.filter-panel {
  padding: 10px 4px 6px;
  border-top: 1px dashed var(--border);
}
.filter-title {
  font-size: 13px;
  color: var(--text-tertiary);
  margin-bottom: 8px;
}
.filter-chip {
  margin-right: 14px;
  margin-bottom: 8px;
}
.filter-actions {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  margin-top: 8px;
}
.date-row {
  display: flex;
  gap: 8px;
}
.date-row :deep(.van-field) {
  flex: 1;
}

.loading {
  padding: 40px 0;
  text-align: center;
}
.empty {
  padding-top: 40px;
}

/* ========== 时间线主体 ========== */
.timeline {
  padding: 12px 12px 24px;
}
.year-group {
  margin-bottom: 14px;
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 14px;
  overflow: hidden;
  box-shadow: var(--shadow-xs);
}
.year-header {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 14px 16px;
  background: var(--surface-2);
  cursor: pointer;
  user-select: none;
  transition: background 0.15s ease;
}
.year-header:hover {
  background: var(--surface-3);
}
.year-title {
  font-weight: 700;
  font-size: 16px;
  color: var(--text-primary);
  flex: 1;
}
.year-count { margin-left: auto; }
.year-current { margin-left: 4px; }

.year-body { padding: 4px 6px 8px; }

.month-group { border-top: 1px solid var(--divider); }
.month-group:first-child { border-top: none; }

.month-header {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 12px 14px;
  cursor: pointer;
  background: var(--surface);
  user-select: none;
  border-radius: 8px;
  margin: 4px 0;
  transition: background 0.15s ease;
}
.month-header:hover {
  background: var(--surface-2);
}
.month-title {
  font-weight: 600;
  font-size: 14.5px;
  color: var(--text-primary);
  flex: 1;
}
.month-count { font-size: 12px; color: var(--text-tertiary); }
.month-current { margin-left: 4px; }

.month-body { padding: 4px 12px 12px; }

.day-group {
  position: relative;
  padding-left: 24px;
  padding-bottom: 12px;
}
.day-dot {
  position: absolute;
  left: 7px;
  top: 8px;
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: var(--color-primary);
  box-shadow: 0 0 0 4px rgba(59, 130, 246, 0.2);
}
:global(body.dark) .day-dot {
  box-shadow: 0 0 0 4px rgba(56, 189, 248, 0.25);
}
.day-group::before {
  content: '';
  position: absolute;
  left: 10px;
  top: 18px;
  bottom: -4px;
  width: 2px;
  background: var(--border);
}
.day-group:last-child::before {
  display: none;
}
.day-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 2px 0 8px;
}
.day-title {
  font-size: 13.5px;
  color: var(--text-secondary);
  font-weight: 600;
}
.day-count {
  font-size: 12px;
  color: var(--text-disabled);
}

.notes-list {
  display: flex;
  flex-direction: column;
  gap: 10px;
}
.note-card {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 10px;
  padding: 12px 14px;
  transition: all 0.18s cubic-bezier(0.4, 0, 0.2, 1);
  box-shadow: var(--shadow-xs);
  cursor: pointer;
}
.note-card:hover {
  box-shadow: var(--shadow-sm);
  border-color: var(--color-primary);
  transform: translateY(-2px);
}
.note-time {
  font-size: 11.5px;
  color: var(--color-primary);
  font-weight: 600;
  margin-bottom: 4px;
}
.note-title {
  font-weight: 600;
  font-size: 15px;
  color: var(--text-primary);
  margin-bottom: 4px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.note-preview {
  font-size: 13px;
  color: var(--text-secondary);
  line-height: 1.5;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
  margin-bottom: 8px;
}
.note-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}

/* ========== 桌面端适配 ========== */
@media (min-width: 1024px) {
  .page {
    max-width: none;
    margin: 0 auto;
    padding-bottom: 48px;
  }
  /* 桌面端 NavBar 归入文档流，避免全屏 fixed 跨出，与内容区容器完全同宽居中 */
  :deep(.van-nav-bar--fixed) {
    position: static !important;
    width: auto !important;
    left: auto !important;
    right: auto !important;
  }
  :deep(.van-nav-bar__placeholder) {
    display: none !important;
  }
  /* 桌面端吸顶筛选栏直接贴顶 */
  .filter-bar {
    top: 0 !important;
    z-index: 10;
    border-radius: 0 0 8px 8px;
    margin: 0 0 12px;
    padding: 12px 16px;
  }
  .notes-list {
    display: grid;
    grid-template-columns: repeat(2, 1fr);
    gap: 10px;
  }
  .timeline-layout {
    display: flex;
    gap: 20px;
    padding: 16px 16px 32px;
    align-items: flex-start;
  }
  .timeline {
    flex: 1;
    min-width: 0;
    padding: 0;
  }
  .timeline-preview {
    width: 380px;
    flex-shrink: 0;
    position: sticky;
    top: 16px;
    max-height: calc(100vh - 32px);
    overflow-y: auto;
    background: var(--surface);
    border: 1px solid var(--border);
    border-radius: 14px;
    box-shadow: var(--shadow-sm);
  }

  /* ========== 桌面端筛选侧栏 ========== */
  .filter-sidebar {
    width: 240px;
    flex-shrink: 0;
    position: sticky;
    top: 16px;
    max-height: calc(100vh - 32px);
    overflow: hidden;
    background: var(--surface);
    border: 1px solid var(--border);
    border-radius: 14px;
    box-shadow: var(--shadow-sm);
    display: flex;
    flex-direction: column;
  }
  .sidebar-scroll {
    overflow-y: auto;
    padding: 12px;
    display: flex;
    flex-direction: column;
    gap: 14px;
  }
  .sidebar-scroll :deep(.van-search) {
    padding: 0;
    background: transparent;
  }
  .sidebar-scroll :deep(.van-search__content) {
    background: var(--surface-2);
  }

  .active-filters {
    display: flex;
    flex-wrap: wrap;
    gap: 6px;
    align-items: center;
  }
  .active-chip {
    display: inline-flex;
    align-items: center;
    gap: 3px;
    padding: 3px 8px;
    border-radius: 12px;
    background: rgba(59, 130, 246, 0.12);
    color: var(--color-primary);
    font-size: 12px;
    cursor: pointer;
    transition: background 0.15s ease;
  }
  .active-chip:hover {
    background: rgba(59, 130, 246, 0.2);
  }
  .active-clear {
    font-size: 12px;
    color: var(--text-tertiary);
    cursor: pointer;
    margin-left: auto;
  }
  .active-clear:hover {
    color: var(--color-primary);
  }

  .filter-group {
    border-top: 1px solid var(--divider);
    padding-top: 12px;
  }
  .group-title {
    display: flex;
    align-items: center;
    gap: 6px;
    font-size: 13px;
    font-weight: 600;
    color: var(--text-secondary);
    margin-bottom: 8px;
  }
  .group-body {
    display: flex;
    flex-direction: column;
    gap: 2px;
  }
  .category-list,
  .tag-list {
    max-height: 220px;
    overflow-y: auto;
  }
  .group-option {
    display: flex;
    align-items: center;
    gap: 8px;
    padding: 6px 8px;
    border-radius: 8px;
    cursor: pointer;
    color: var(--text-primary);
    font-size: 13px;
    transition: background 0.15s ease;
  }
  .group-option:hover {
    background: var(--surface-2);
  }
  .group-option.active {
    background: rgba(59, 130, 246, 0.1);
    color: var(--color-primary);
    font-weight: 600;
  }
  .group-option .option-label {
    flex: 1;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }
  .group-option .option-count {
    font-size: 11px;
    color: var(--text-disabled);
  }
  .group-empty {
    font-size: 12px;
    color: var(--text-tertiary);
    padding: 8px;
    text-align: center;
  }

  .quick-ranges {
    display: flex;
    flex-wrap: wrap;
    gap: 6px;
    margin-bottom: 10px;
  }
  .quick-chip {
    padding: 3px 10px;
    border-radius: 12px;
    border: 1px solid var(--border);
    font-size: 12px;
    color: var(--text-secondary);
    cursor: pointer;
    transition: all 0.15s ease;
  }
  .quick-chip:hover {
    border-color: var(--color-primary);
    color: var(--color-primary);
  }
  .quick-chip.active {
    background: var(--color-primary);
    border-color: var(--color-primary);
    color: #fff;
  }
  .date-field {
    display: flex;
    align-items: center;
    gap: 8px;
    padding: 8px 10px;
    border: 1px solid var(--border);
    border-radius: 8px;
    font-size: 13px;
    color: var(--text-secondary);
    cursor: pointer;
    transition: border-color 0.15s ease;
  }
  .date-field:hover {
    border-color: var(--color-primary);
  }
  .date-value {
    flex: 1;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .result-count {
    border-top: 1px solid var(--divider);
    padding-top: 12px;
    font-size: 13px;
    color: var(--text-secondary);
  }
  .result-count b {
    color: var(--color-primary);
    font-size: 15px;
  }

  .preview-loading {
    display: flex;
    align-items: center;
    justify-content: center;
    padding: 60px 0;
  }
  .preview-note {
    padding: 16px 18px 24px;
  }
  .preview-toolbar {
    display: flex;
    align-items: center;
    justify-content: space-between;
    margin-bottom: 14px;
    padding-bottom: 12px;
    border-bottom: 1px solid var(--border);
  }
  .preview-crumb {
    font-size: 12px;
    font-weight: 600;
    color: var(--text-tertiary);
    text-transform: uppercase;
    letter-spacing: 0.5px;
  }
  .preview-toolbar-actions {
    display: flex;
    gap: 6px;
  }
  .preview-action-btn {
    display: inline-flex;
    align-items: center;
    gap: 4px;
    padding: 4px 9px;
    border-radius: 6px;
    border: 1px solid var(--border);
    background: var(--surface-2);
    color: var(--text-secondary);
    font-size: 12px;
    cursor: pointer;
    transition: all 0.15s ease;
  }
  .preview-action-btn:hover {
    background: var(--surface-3);
    color: var(--text-primary);
    border-color: var(--border-strong);
  }
  .preview-title {
    font-size: 18px;
    font-weight: 700;
    color: var(--text-primary);
    margin: 0 0 8px;
    line-height: 1.4;
    word-break: break-word;
  }
  .preview-meta {
    display: flex;
    flex-wrap: wrap;
    gap: 6px;
    margin-bottom: 16px;
  }
  .preview-cat {
    font-size: 12px;
    padding: 2px 8px;
    border-radius: 6px;
    background: rgba(59, 130, 246, 0.12);
    color: var(--color-primary);
    font-weight: 600;
  }
  .preview-tag {
    font-size: 12px;
    padding: 2px 8px;
    border-radius: 6px;
    background: var(--surface-2);
    color: var(--text-secondary);
  }
  .preview-body {
    font-size: 14px;
    line-height: 1.7;
    color: var(--text-primary);
  }
  .preview-empty {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    padding: 60px 20px;
    text-align: center;
    color: var(--text-tertiary);
  }
  .preview-empty-icon {
    font-size: 40px;
    margin-bottom: 12px;
    opacity: 0.6;
  }
  .preview-empty p {
    margin: 0 0 4px;
    font-size: 14px;
    color: var(--text-secondary);
  }
  .preview-empty span {
    font-size: 12px;
  }
  .note-card.selected {
    border-color: var(--color-primary);
    box-shadow: 0 0 0 2px rgba(59, 130, 246, 0.2);
  }
  .year-group {
    margin-bottom: 16px;
  }
}
@media (min-width: 1440px) {
  .notes-list {
    grid-template-columns: repeat(2, 1fr);
  }
  .timeline-preview {
    width: 400px;
  }
}
@media (min-width: 1680px) {
  .notes-list {
    grid-template-columns: repeat(3, 1fr);
  }
}
</style>
