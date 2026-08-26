<template>
  <div class="dashboard-container">
    <!-- Welcome Banner / Quick Stats -->
    <div class="welcome-banner">
      <div class="welcome-text">
        <h2 class="welcome-title">欢迎回来，{{ auth.displayName || '管理员' }} 👋</h2>
        <p class="welcome-desc">以下是 Notes 笔记系统的实时运行指标与数据概览。</p>
      </div>
      <div class="welcome-badge">
        <span class="pulse-dot"></span>
        <span>系统运行正常</span>
      </div>
    </div>

    <!-- Stat Cards Grid -->
    <div class="stat-grid">
      <div class="stat-card" v-for="card in cards" :key="card.label">
        <div class="stat-icon-box" :style="{ background: card.gradient }">
          <el-icon :size="22"><component :is="card.icon" /></el-icon>
        </div>
        <div class="stat-content">
          <div class="stat-label">{{ card.label }}</div>
          <div class="stat-val-row">
            <span class="stat-number">{{ card.value }}</span>
            <span v-if="card.extra" class="stat-extra">{{ card.extra }}</span>
          </div>
        </div>
      </div>
    </div>

    <!-- Quick Actions on Mobile / Tablet -->
    <div class="quick-action-section">
      <div class="section-title">快捷入口</div>
      <div class="quick-action-grid">
        <router-link to="/users" class="quick-btn">
          <div class="quick-icon user-bg"><el-icon><User /></el-icon></div>
          <span class="quick-label">用户管理</span>
        </router-link>
        <router-link to="/notes" class="quick-btn">
          <div class="quick-icon note-bg"><el-icon><Notebook /></el-icon></div>
          <span class="quick-label">笔记审核</span>
        </router-link>
        <router-link to="/categories" class="quick-btn">
          <div class="quick-icon cat-bg"><el-icon><FolderOpened /></el-icon></div>
          <span class="quick-label">分类管理</span>
        </router-link>
        <router-link to="/attachments" class="quick-btn">
          <div class="quick-icon att-bg"><el-icon><Paperclip /></el-icon></div>
          <span class="quick-label">附件清理</span>
        </router-link>
      </div>
    </div>

    <!-- Chart Section -->
    <div class="trend-section">
      <el-card class="trend-card" shadow="hover">
        <template #header>
          <div class="trend-header">
            <div class="trend-title-box">
              <el-icon class="trend-icon"><TrendCharts /></el-icon>
              <span class="trend-title">业务增长趋势</span>
            </div>
            <el-radio-group v-model="days" size="small" @change="loadTrends">
              <el-radio-button :value="7">7天</el-radio-button>
              <el-radio-button :value="30">30天</el-radio-button>
              <el-radio-button :value="90">90天</el-radio-button>
            </el-radio-group>
          </div>
        </template>
        <div v-loading="chartLoading" class="chart-wrapper">
          <div ref="chartRef" class="chart-canvas"></div>
        </div>
      </el-card>
    </div>
  </div>
</template>

<script setup>
import { ref, reactive, computed, onMounted, onBeforeUnmount, nextTick, watch } from 'vue'
import * as echarts from 'echarts'
import http from '../api/http'
import { useAuthStore } from '../stores/auth'
import { useAdminThemeStore } from '../stores/theme'

const auth = useAuthStore()
const theme = useAdminThemeStore()
const days = ref(30)
const chartRef = ref()
const chartLoading = ref(false)
let chart = null
let currentTrendData = []

const overview = reactive({
  userCount: '-',
  noteCount: '-',
  attachmentCount: '-',
  attachTotalSize: '-',
  todayNewNotes: 0
})

function formatSize(bytes) {
  if (bytes == null || isNaN(bytes)) return '-'
  if (bytes < 1024) return bytes + ' B'
  const units = ['KB', 'MB', 'GB', 'TB']
  let size = bytes
  let unit = -1
  do {
    size /= 1024
    unit++
  } while (size >= 1024 && unit < units.length - 1)
  return size.toFixed(1) + ' ' + units[unit]
}

const cards = computed(() => [
  {
    label: '用户总数',
    value: overview.userCount,
    icon: 'User',
    gradient: 'linear-gradient(135deg, #6366f1 0%, #4f46e5 100%)',
    extra: '注册用户'
  },
  {
    label: '笔记总数',
    value: overview.noteCount,
    icon: 'Notebook',
    gradient: 'linear-gradient(135deg, #10b981 0%, #059669 100%)',
    extra: overview.todayNewNotes ? `今日+${overview.todayNewNotes}` : '总记录'
  },
  {
    label: '附件文件',
    value: overview.attachmentCount,
    icon: 'Paperclip',
    gradient: 'linear-gradient(135deg, #f59e0b 0%, #d97706 100%)',
    extra: '个媒体文件'
  },
  {
    label: '磁盘占用',
    value: overview.attachTotalSize,
    icon: 'Coin',
    gradient: 'linear-gradient(135deg, #ec4899 0%, #db2777 100%)',
    extra: '存储已用'
  }
])

async function loadOverview() {
  try {
    const data = await http.get('/admin/stats/overview')
    overview.userCount = data.userCount ?? 0
    overview.noteCount = data.noteCount ?? 0
    overview.attachmentCount = data.attachmentCount ?? 0
    overview.attachTotalSize = formatSize(data.attachTotalSize)
    overview.todayNewNotes = data.todayNewNotes ?? 0
  } catch (err) {
    console.error('Failed to load overview:', err)
  }
}

async function loadTrends() {
  chartLoading.value = true
  try {
    const data = await http.get('/admin/stats/trends', { params: { days: days.value } })
    currentTrendData = data || []
    renderChart(currentTrendData)
  } catch (err) {
    console.error('Failed to load trends:', err)
  } finally {
    chartLoading.value = false
  }
}

function renderChart(data) {
  if (!chart || !chartRef.value) return
  const isMobile = window.innerWidth <= 768
  const isDark = theme.isDarkEffective

  const textColor = isDark ? '#94a3b8' : '#64748b'
  const splitLineColor = isDark ? 'rgba(255, 255, 255, 0.07)' : '#f1f5f9'
  const axisLineColor = isDark ? 'rgba(255, 255, 255, 0.12)' : '#cbd5e1'
  const tooltipBg = isDark ? 'rgba(19, 27, 46, 0.95)' : 'rgba(15, 23, 42, 0.9)'
  const tooltipBorder = isDark ? 'rgba(255, 255, 255, 0.15)' : '#334155'

  chart.setOption({
    tooltip: {
      trigger: 'axis',
      backgroundColor: tooltipBg,
      borderColor: tooltipBorder,
      textStyle: { color: '#f8fafc', fontSize: 12 }
    },
    legend: {
      data: ['新增用户', '新增笔记'],
      bottom: 0,
      textStyle: { color: textColor, fontSize: isMobile ? 11 : 12 }
    },
    grid: {
      left: isMobile ? 25 : 35,
      right: isMobile ? 15 : 25,
      top: 20,
      bottom: 35,
      containLabel: true
    },
    xAxis: {
      type: 'category',
      data: data.map((d) => d.date.slice(5)),
      axisLine: { lineStyle: { color: axisLineColor } },
      axisLabel: { color: textColor, fontSize: isMobile ? 10 : 11 }
    },
    yAxis: {
      type: 'value',
      minInterval: 1,
      splitLine: { lineStyle: { color: splitLineColor } },
      axisLabel: { color: textColor, fontSize: isMobile ? 10 : 11 }
    },
    series: [
      {
        name: '新增用户',
        type: 'line',
        smooth: true,
        data: data.map((d) => d.newUsers),
        itemStyle: { color: '#6366f1' },
        areaStyle: {
          color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
            { offset: 0, color: 'rgba(99, 102, 241, 0.3)' },
            { offset: 1, color: 'rgba(99, 102, 241, 0.0)' }
          ])
        }
      },
      {
        name: '新增笔记',
        type: 'line',
        smooth: true,
        data: data.map((d) => d.newNotes),
        itemStyle: { color: '#10b981' },
        areaStyle: {
          color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
            { offset: 0, color: 'rgba(16, 185, 129, 0.3)' },
            { offset: 1, color: 'rgba(16, 185, 129, 0.0)' }
          ])
        }
      }
    ]
  })
}

let resizeRaf = null
function onResize() {
  if (resizeRaf) cancelAnimationFrame(resizeRaf)
  resizeRaf = requestAnimationFrame(() => {
    if (chart) {
      chart.resize()
    }
  })
}

watch(() => theme.isDarkEffective, () => {
  if (currentTrendData && currentTrendData.length) {
    renderChart(currentTrendData)
  }
})

onMounted(async () => {
  await loadOverview()
  await nextTick()
  if (chartRef.value) {
    chart = echarts.init(chartRef.value)
    await loadTrends()
  }
  window.addEventListener('resize', onResize)
})

onBeforeUnmount(() => {
  window.removeEventListener('resize', onResize)
  if (chart) {
    chart.dispose()
    chart = null
  }
})
</script>

<style scoped>
.dashboard-container {
  display: flex;
  flex-direction: column;
  gap: 20px;
  max-width: 1200px;
  margin: 0 auto;
}

/* Welcome Banner */
.welcome-banner {
  background: var(--admin-card-bg);
  border-radius: var(--admin-radius-md);
  padding: 18px 20px;
  border: 1px solid var(--admin-border);
  display: flex;
  align-items: center;
  justify-content: space-between;
  box-shadow: var(--admin-shadow-sm);
  transition: background-color 0.2s ease, border-color 0.2s ease;
}

.welcome-title {
  margin: 0 0 4px 0;
  font-size: 17px;
  font-weight: 700;
  color: var(--admin-text-main);
}

.welcome-desc {
  margin: 0;
  font-size: 13px;
  color: var(--admin-text-sub);
}

.welcome-badge {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 6px 12px;
  border-radius: 20px;
  background: rgba(16, 185, 129, 0.12);
  color: #10b981;
  font-size: 12px;
  font-weight: 600;
  flex-shrink: 0;
  border: 1px solid rgba(16, 185, 129, 0.25);
}

.pulse-dot {
  width: 7px;
  height: 7px;
  border-radius: 50%;
  background-color: #10b981;
  box-shadow: 0 0 0 0 rgba(16, 185, 129, 0.7);
  animation: pulse 1.8s infinite cubic-bezier(0.66, 0, 0, 1);
}

@keyframes pulse {
  to {
    box-shadow: 0 0 0 7px rgba(16, 185, 129, 0);
  }
}

/* Stat Grid */
.stat-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 16px;
}

.stat-card {
  background: var(--admin-card-bg);
  border-radius: var(--admin-radius-md);
  padding: 18px;
  border: 1px solid var(--admin-border);
  box-shadow: var(--admin-shadow-sm);
  display: flex;
  align-items: center;
  gap: 14px;
  transition: transform 0.2s ease, box-shadow 0.2s ease, background-color 0.2s ease, border-color 0.2s ease;
}

.stat-card:hover {
  transform: translateY(-2px);
  box-shadow: var(--admin-shadow);
}

.stat-icon-box {
  width: 48px;
  height: 48px;
  border-radius: 12px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #ffffff;
  flex-shrink: 0;
  box-shadow: 0 4px 10px rgba(0, 0, 0, 0.1);
}

.stat-content {
  flex: 1;
  min-width: 0;
}

.stat-label {
  font-size: 12.5px;
  color: var(--admin-text-sub);
  font-weight: 500;
  margin-bottom: 4px;
}

.stat-val-row {
  display: flex;
  align-items: baseline;
  gap: 6px;
}

.stat-number {
  font-size: 22px;
  font-weight: 700;
  color: var(--admin-text-main);
  line-height: 1.1;
}

.stat-extra {
  font-size: 11px;
  color: var(--admin-text-muted);
}

/* Quick Action Section */
.quick-action-section {
  background: var(--admin-card-bg);
  border-radius: var(--admin-radius-md);
  padding: 16px 20px;
  border: 1px solid var(--admin-border);
  box-shadow: var(--admin-shadow-sm);
  transition: background-color 0.2s ease, border-color 0.2s ease;
}

.section-title {
  font-size: 14px;
  font-weight: 600;
  color: var(--admin-text-main);
  margin-bottom: 12px;
}

.quick-action-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 12px;
}

.quick-btn {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
  padding: 12px 8px;
  background: var(--admin-subcard-bg);
  border-radius: 10px;
  text-decoration: none;
  border: 1px solid var(--admin-border);
  transition: all 0.15s ease;
}

.quick-btn:hover {
  background: var(--admin-primary-light);
  border-color: var(--admin-primary);
  transform: translateY(-1px);
}

.quick-btn:active {
  transform: scale(0.98);
}

.quick-icon {
  width: 38px;
  height: 38px;
  border-radius: 10px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #ffffff;
  font-size: 18px;
}

.user-bg { background: linear-gradient(135deg, #6366f1, #4f46e5); }
.note-bg { background: linear-gradient(135deg, #10b981, #059669); }
.cat-bg { background: linear-gradient(135deg, #0ea5e9, #0284c7); }
.att-bg { background: linear-gradient(135deg, #f59e0b, #d97706); }

.quick-label {
  font-size: 12.5px;
  font-weight: 500;
  color: var(--admin-text-main);
}

/* Trend Section */
.trend-section {
  width: 100%;
}

.trend-card {
  border-radius: var(--admin-radius-md);
  background: var(--admin-card-bg);
}

.trend-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 10px;
}

.trend-title-box {
  display: flex;
  align-items: center;
  gap: 8px;
}

.trend-icon {
  color: var(--admin-primary);
  font-size: 18px;
}

.trend-title {
  font-size: 15px;
  font-weight: 600;
  color: var(--admin-text-main);
}

.chart-wrapper {
  width: 100%;
  height: 340px;
}

.chart-canvas {
  width: 100%;
  height: 100%;
}

/* Mobile Breakpoint Adjustments */
@media (max-width: 768px) {
  .dashboard-container {
    gap: 14px;
  }
  .welcome-banner {
    padding: 14px;
    flex-direction: column;
    align-items: flex-start;
    gap: 10px;
  }
  .welcome-badge {
    align-self: flex-start;
  }
  .stat-grid {
    grid-template-columns: repeat(2, 1fr);
    gap: 10px;
  }
  .stat-card {
    padding: 12px;
    gap: 10px;
  }
  .stat-icon-box {
    width: 38px;
    height: 38px;
    border-radius: 8px;
  }
  .stat-number {
    font-size: 18px;
  }
  .stat-extra {
    display: none;
  }
  .quick-action-grid {
    grid-template-columns: repeat(4, 1fr);
    gap: 8px;
  }
  .quick-btn {
    padding: 8px 4px;
    gap: 6px;
  }
  .quick-icon {
    width: 32px;
    height: 32px;
    border-radius: 8px;
    font-size: 16px;
  }
  .quick-label {
    font-size: 11px;
  }
  .chart-wrapper {
    height: 280px;
  }
}
</style>
