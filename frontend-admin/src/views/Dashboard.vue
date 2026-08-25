<template>
  <div class="dashboard">
    <el-row :gutter="16">
      <el-col :xs="12" :sm="12" :md="6" v-for="card in cards" :key="card.label">
        <el-card class="stat-card" shadow="hover">
          <div class="stat-inner">
            <div class="stat-icon" :style="{ background: card.bg }">
              <el-icon :size="24"><component :is="card.icon" /></el-icon>
            </div>
            <div class="stat-info">
              <div class="stat-label">{{ card.label }}</div>
              <div class="stat-value">{{ card.value }}</div>
            </div>
          </div>
        </el-card>
      </el-col>
    </el-row>

    <el-card class="trend-card" shadow="never">
      <template #header>
        <div class="trend-header">
          <span class="trend-title">近 {{ days }} 天趋势</span>
          <el-radio-group v-model="days" size="small" @change="loadTrends">
            <el-radio-button :value="7">7 天</el-radio-button>
            <el-radio-button :value="30">30 天</el-radio-button>
            <el-radio-button :value="90">90 天</el-radio-button>
          </el-radio-group>
        </div>
      </template>
      <div ref="chartRef" class="chart"></div>
    </el-card>
  </div>
</template>

<script setup>
import { ref, reactive, computed, onMounted, onBeforeUnmount, nextTick } from 'vue'
import * as echarts from 'echarts'
import http from '../api/http'

const days = ref(30)
const chartRef = ref()
let chart = null

const overview = reactive({
  userCount: '-',
  noteCount: '-',
  attachmentCount: '-',
  attachTotalSize: '-'
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
  { label: '用户总数', value: overview.userCount, icon: 'User', bg: '#667eea' },
  { label: '笔记总数', value: overview.noteCount, icon: 'Notebook', bg: '#36cfc9' },
  { label: '附件总数', value: overview.attachmentCount, icon: 'Paperclip', bg: '#ffa940' },
  { label: '存储占用', value: overview.attachTotalSize, icon: 'Folder', bg: '#ff7a45' }
])

async function loadOverview() {
  const data = await http.get('/admin/stats/overview')
  overview.userCount = data.userCount
  overview.noteCount = data.noteCount
  overview.attachmentCount = data.attachmentCount
  overview.attachTotalSize = formatSize(data.attachTotalSize)
}

async function loadTrends() {
  const data = await http.get('/admin/stats/trends', { params: { days: days.value } })
  renderChart(data)
}

function renderChart(data) {
  if (!chart) return
  chart.setOption({
    tooltip: { trigger: 'axis' },
    legend: { data: ['新增用户', '新增笔记'] },
    grid: { left: 40, right: 20, top: 40, bottom: 30 },
    xAxis: {
      type: 'category',
      data: data.map((d) => d.date.slice(5))
    },
    yAxis: { type: 'value', minInterval: 1 },
    series: [
      {
        name: '新增用户',
        type: 'line',
        smooth: true,
        data: data.map((d) => d.newUsers),
        itemStyle: { color: '#667eea' },
        areaStyle: { opacity: 0.1 }
      },
      {
        name: '新增笔记',
        type: 'line',
        smooth: true,
        data: data.map((d) => d.newNotes),
        itemStyle: { color: '#36cfc9' },
        areaStyle: { opacity: 0.1 }
      }
    ]
  })
}

function onResize() {
  chart && chart.resize()
}

onMounted(async () => {
  await loadOverview()
  await nextTick()
  chart = echarts.init(chartRef.value)
  await loadTrends()
  window.addEventListener('resize', onResize)
})

onBeforeUnmount(() => {
  window.removeEventListener('resize', onResize)
  chart && chart.dispose()
  chart = null
})
</script>

<style scoped>
.stat-inner {
  display: flex;
  align-items: center;
  gap: 16px;
}
.stat-icon {
  width: 52px;
  height: 52px;
  border-radius: 12px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #fff;
  flex-shrink: 0;
}
.stat-label {
  font-size: 13px;
  color: #909399;
}
.stat-value {
  font-size: 24px;
  font-weight: 600;
  margin-top: 4px;
}
.trend-card {
  margin-top: 16px;
}
.trend-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}
.trend-title {
  font-size: 15px;
  font-weight: 600;
}
.chart {
  height: 360px;
}
</style>