import { ref } from 'vue'
import http from '../api/http'

// 固定阈值色阶：0 / 1-3 / 4-6 / 7-9 / >=10（与服务端按 UpdatedAt 按天计数对齐）
function toLevel(count) {
  if (count <= 0) return 0
  if (count <= 3) return 1
  if (count <= 6) return 2
  if (count <= 9) return 3
  return 4
}

const DAY_MS = 86400000
const WEEKS = 53

function toDateStr(d) {
  const y = d.getUTCFullYear()
  const m = String(d.getUTCMonth() + 1).padStart(2, '0')
  const day = String(d.getUTCDate()).padStart(2, '0')
  return `${y}-${m}-${day}`
}

// 网格坐标常量（SVG viewBox 布局）
const PLOT_X0 = 34
const CELL = 13
const STEP = 16
const ROW_BASE = 36

export function useHeatmap() {
  const loading = ref(false)
  const error = ref('')
  const weeks = ref([]) // [{ col, days: [{ date, count, level, weekday }] }]
  const monthLabels = ref([]) // [{ col, label }]
  const peak = ref(0)
  const hasData = ref(false)
  const viewBox = ref('')

  // 以 UTC 日期计算网格范围，与服务端按 UpdatedAt(UTC).Date 分组保持严格一致，避免时区错位
  function currentRange() {
    const now = new Date()
    const utcToday = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate()))
    const weekday = utcToday.getUTCDay() || 7 // 周一=1..周日=7
    const thisMonday = new Date(utcToday.getTime() - (weekday - 1) * DAY_MS)
    const start = new Date(thisMonday.getTime() - (WEEKS - 1) * 7 * DAY_MS)
    return { start, thisMonday }
  }

  function buildGrid(countMap) {
    const { start } = currentRange()
    const cols = []
    const labels = []
    let prevMonth = null
    let prevCol = -99
    let maxCount = 0

    for (let c = 0; c < WEEKS; c++) {
      const days = []
      for (let wd = 0; wd < 7; wd++) {
        const t = start.getTime() + (c * 7 + wd) * DAY_MS
        const date = toDateStr(new Date(t))
        const count = countMap[date] || 0
        maxCount = Math.max(maxCount, count)
        days.push({ date, count, level: toLevel(count), weekday: wd })
      }
      // 月标签：取该列第一天所在月份，距上一标签至少 2 列才放置，避免重叠
      const firstDate = new Date(start.getTime() + c * 7 * DAY_MS)
      const m = firstDate.getUTCMonth() + 1
      if (m !== prevMonth) {
        if (c - prevCol >= 2) {
          labels.push({ col: c, label: `${m}月` })
          prevCol = c
        }
        prevMonth = m
      }
      cols.push({ col: c, days })
    }

    const width = PLOT_X0 + WEEKS * STEP - 3 + 10
    const height = ROW_BASE + 7 * STEP - 3 + 10
    weeks.value = cols
    monthLabels.value = labels
    peak.value = maxCount
    hasData.value = cols.some((w) => w.days.some((d) => d.count > 0))
    viewBox.value = `0 0 ${width} ${height}`
  }

  async function load() {
    loading.value = true
    error.value = ''
    try {
      const { start } = currentRange()
      // 复用时间线接口的日期过滤：只拉近一年，按 UpdatedAt 分组，天然得到「每天编辑过的笔记数」
      const data = await http.get('/notes/timeline', { params: { fromDate: toDateStr(start) } })
      const countMap = {}
      for (const y of data?.years || []) {
        for (const m of y.months || []) {
          for (const d of m.days || []) {
            countMap[String(d.date).slice(0, 10)] = d.noteCount
          }
        }
      }
      buildGrid(countMap)
    } catch (e) {
      error.value = '热力图加载失败'
    } finally {
      loading.value = false
    }
  }

  return {
    loading,
    error,
    weeks,
    monthLabels,
    peak,
    hasData,
    viewBox,
    plotX0: PLOT_X0,
    cell: CELL,
    step: STEP,
    rowBase: ROW_BASE,
    load
  }
}
