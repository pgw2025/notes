<template>
  <div class="page timeline-page">
    <van-nav-bar title="时间线" fixed placeholder />

    <div class="filter-bar">
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
            v-for="c in categories"
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

    <!-- 加载 -->
    <van-loading v-if="loading" class="loading" color="#1989fa" size="24px">加载中…</van-loading>

    <!-- 空态 -->
    <div v-else-if="!timeline?.years?.length" class="empty">
      <van-empty description="还没有笔记，去写第一篇吧～" />
    </div>

    <!-- 时间线 -->
    <div v-else class="timeline">
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
    </div>
  </div>
</template>

<script setup>
import { reactive, ref, onMounted, nextTick, watch } from 'vue'
import { useRouter } from 'vue-router'
import http from '../api/http'

const router = useRouter()

const loading = ref(false)
const timeline = ref({ totalNotes: 0, years: [] })

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

const filterOpen = reactive({ cats: false, tags: false, dates: false })
const showFromPicker = ref(false)
const showToPicker = ref(false)

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

    // 默认展开：今年 + 当前月
    nextTick(() => {
      if (!data?.years) return
      for (const y of data.years) {
        if (y.isCurrentYear) {
          yearExpanded[y.year] = true
          for (const m of y.months) {
            if (m.isCurrentMonth) monthExpanded[keyOfMonth(y.year, m.month)] = true
          }
        } else {
          if (yearExpanded[y.year] === undefined) yearExpanded[y.year] = false
          for (const m of y.months) {
            const k = keyOfMonth(y.year, m.month)
            if (monthExpanded[k] === undefined) monthExpanded[k] = false
          }
        }
      }
    })
  } finally {
    loading.value = false
  }
}

function openNote(id) { router.push(`/notes/${id}`) }

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
}
.filter-bar {
  background: #fff;
  padding: 8px 12px 12px;
  border-bottom: 1px solid #ebedf0;
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
  border-top: 1px dashed #ebedf0;
}
.filter-title {
  font-size: 13px;
  color: #969799;
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
  margin-bottom: 10px;
  background: #fff;
  border: 1px solid #ebedf0;
  border-radius: 8px;
  overflow: hidden;
}
.year-header {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 12px 14px;
  background: #f7f8fa;
  cursor: pointer;
  user-select: none;
}
.year-title {
  font-weight: 600;
  font-size: 15px;
  flex: 1;
}
.year-count { margin-left: auto; }
.year-current { margin-left: 4px; }

.year-body { padding: 0 4px 4px; }

.month-group { border-top: 1px solid #f2f3f5; }
.month-group:first-child { border-top: none; }

.month-header {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 10px 14px;
  cursor: pointer;
  background: #fff;
  user-select: none;
}
.month-title {
  font-weight: 600;
  font-size: 14px;
  flex: 1;
}
.month-count { font-size: 12px; color: #969799; }
.month-current { margin-left: 4px; }

.month-body { padding: 0 10px 8px; }

.day-group {
  position: relative;
  padding-left: 20px;
  padding-bottom: 8px;
}
.day-dot {
  position: absolute;
  left: 6px;
  top: 6px;
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: #1989fa;
  box-shadow: 0 0 0 3px #dbe9ff;
}
.day-group::before {
  content: '';
  position: absolute;
  left: 9px;
  top: 14px;
  bottom: -4px;
  width: 2px;
  background: #ebedf0;
}
.day-group:last-child::before {
  display: none;
}
.day-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 2px 0 6px;
}
.day-title {
  font-size: 13px;
  color: #646566;
  font-weight: 500;
}
.day-count {
  font-size: 12px;
  color: #c8c9cc;
}

.notes-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}
.note-card {
  background: #f7f8fa;
  border: 1px solid #ebedf0;
  border-radius: 6px;
  padding: 10px 12px;
  transition: box-shadow 0.2s, transform 0.2s;
  cursor: pointer;
}
.note-card:hover {
  box-shadow: 0 2px 8px rgba(0,0,0,0.06);
  transform: translateY(-1px);
}
.note-time {
  font-size: 12px;
  color: #1989fa;
  font-weight: 500;
  margin-bottom: 4px;
}
.note-title {
  font-weight: 600;
  font-size: 15px;
  margin-bottom: 4px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.note-preview {
  font-size: 13px;
  color: #646566;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
  margin-bottom: 6px;
}
.note-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}

/* ========== 桌面端适配 ========== */
@media (min-width: 1024px) {
  .page {
    max-width: 1100px;
    margin: 0 auto;
    padding-bottom: 48px;
  }
  /* 桌面端 NavBar 改为 static 在文档流中，sticky 筛选栏吸顶时要避让 NavBar 高度（约 46px） */
  .filter-bar {
    top: 46px !important;
    z-index: 10;
    border-radius: 0 0 8px 8px;
    margin: 0 -16px 8px;
    padding: 12px 16px 12px;
  }
  .notes-list {
    display: grid;
    grid-template-columns: repeat(2, 1fr);
    gap: 10px;
  }
  .timeline {
    padding: 16px 16px 32px;
  }
  .year-group {
    margin-bottom: 16px;
  }
}
@media (min-width: 1440px) {
  .notes-list {
    grid-template-columns: repeat(3, 1fr);
  }
}
</style>
