import { ref, computed } from 'vue'
import http from '../api/http'

const DAY_MS = 86400000

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

// 时间范围配置
const RANGE_OPTIONS = {
  '3m': { weeks: 14, label: '近3月' },
  '6m': { weeks: 27, label: '近6月' },
  '1y': { weeks: 53, label: '近1年' }
}

// 动态分位数色阶：按数据分布自动划分 5 档
function quantileLevels(sortedCounts, levels = 5) {
  const nonZero = sortedCounts.filter(c => c > 0)
  if (nonZero.length === 0) {
    return [0, 1, 2, 3, 4, 5] // fallback
  }
  const thresholds = [0]
  for (let i = 1; i < levels; i++) {
    const idx = Math.floor((nonZero.length - 1) * (i / levels))
    thresholds.push(nonZero[idx])
  }
  thresholds.push(nonZero[nonZero.length - 1] + 1)
  // 去重并确保单调递增
  const unique = [...new Set(thresholds)]
  while (unique.length < levels + 1) {
    unique.push(unique[unique.length - 1] + 1)
  }
  return unique
}

function countToLevel(count, thresholds) {
  if (count <= 0) return 0
  for (let i = thresholds.length - 1; i >= 1; i--) {
    if (count >= thresholds[i - 1]) return i
  }
  return 1
}

// 计算当前连续活跃天数（从今天往回数）
function calcStreak(countMap, endDateStr) {
  let streak = 0
  const d = new Date(`${endDateStr}T00:00:00Z`)
  while (true) {
    const dateStr = toDateStr(d)
    if ((countMap[dateStr] || 0) > 0) {
      streak++
      d.setTime(d.getTime() - DAY_MS)
    } else {
      break
    }
  }
  return streak
}

// 计算月度统计（用于条形图）
function calcMonthlyStats(countMap, startDate, weeks) {
  const months = []
  const endDate = new Date(startDate.getTime() + weeks * 7 * DAY_MS - DAY_MS)
  let curYear = startDate.getUTCFullYear()
  let curMonth = startDate.getUTCMonth()
  const endYear = endDate.getUTCFullYear()
  const endMonth = endDate.getUTCMonth()

  while (curYear < endYear || (curYear === endYear && curMonth <= endMonth)) {
    const monthKey = `${curYear}-${String(curMonth + 1).padStart(2, '0')}`
    let total = 0
    let activeDays = 0
    // 遍历这个月的每一天
    const monthStart = new Date(Date.UTC(curYear, curMonth, 1))
    const nextMonth = new Date(Date.UTC(curYear, curMonth + 1, 1))
    const d = new Date(monthStart)
    while (d < nextMonth) {
      const dateStr = toDateStr(d)
      const cnt = countMap[dateStr] || 0
      if (cnt > 0) {
        total += cnt
        activeDays++
      }
      d.setTime(d.getTime() + DAY_MS)
    }
    months.push({
      key: monthKey,
      label: `${curMonth + 1}月`,
      year: curYear,
      month: curMonth + 1,
      total,
      activeDays,
      daysInMonth: nextMonth.getUTCDate()
    })
    curMonth++
    if (curMonth > 11) {
      curMonth = 0
      curYear++
    }
  }
  return months
}

export function useHeatmap() {
  const loading = ref(false)
  const error = ref('')
  const weeks = ref([])
  const monthLabels = ref([])
  const peak = ref(0)
  const hasData = ref(false)
  const viewBox = ref('')
  const streak = ref(0)
  const monthlyStats = ref([])
  const levelThresholds = ref([0, 1, 4, 7, 10, 100])
  const rangeKey = ref('1y')
  const useQuantile = ref(true) // 是否使用动态分位数色阶

  const rangeConfig = computed(() => RANGE_OPTIONS[rangeKey.value] || RANGE_OPTIONS['1y'])
  const weekCount = computed(() => rangeConfig.value.weeks)

  const levelTexts = computed(() => {
    if (!useQuantile.value) {
      return ['无', '低', '中', '高', '峰值']
    }
    const t = levelThresholds.value
    return [
      '无',
      `${t[1]}-${t[2] - 1}篇`,
      `${t[2]}-${t[3] - 1}篇`,
      `${t[3]}-${t[4] - 1}篇`,
      `${t[4]}篇+`
    ]
  })

  function currentRange() {
    const now = new Date()
    const utcToday = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate()))
    const weekday = utcToday.getUTCDay() || 7 // 周一=1..周日=7
    const thisMonday = new Date(utcToday.getTime() - (weekday - 1) * DAY_MS)
    const start = new Date(thisMonday.getTime() - (weekCount.value - 1) * 7 * DAY_MS)
    return { start, thisMonday, today: utcToday }
  }

  function buildGrid(countMap) {
    const { start } = currentRange()
    const cols = []
    const labels = []
    let prevMonth = null
    let prevCol = -99
    let maxCount = 0
    const allCounts = []

    for (let c = 0; c < weekCount.value; c++) {
      const days = []
      for (let wd = 0; wd < 7; wd++) {
        const t = start.getTime() + (c * 7 + wd) * DAY_MS
        const date = toDateStr(new Date(t))
        const count = countMap[date] || 0
        maxCount = Math.max(maxCount, count)
        allCounts.push(count)
        days.push({ date, count, level: 0, weekday: wd })
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

    // 计算色阶阈值
    if (useQuantile.value && allCounts.some(c => c > 0)) {
      const sorted = [...allCounts].filter(c => c > 0).sort((a, b) => a - b)
      levelThresholds.value = quantileLevels(sorted, 5)
    }

    // 重新分配 level
    for (const col of cols) {
      for (const day of col.days) {
        day.level = countToLevel(day.count, levelThresholds.value)
      }
    }

    const width = PLOT_X0 + weekCount.value * STEP - 3 + 10
    const height = ROW_BASE + 7 * STEP - 3 + 10
    weeks.value = cols
    monthLabels.value = labels
    peak.value = maxCount
    hasData.value = cols.some((w) => w.days.some((d) => d.count > 0))
    viewBox.value = `0 0 ${width} ${height}`

    // 计算 streak
    const { today } = currentRange()
    streak.value = calcStreak(countMap, toDateStr(today))

    // 计算月度统计
    monthlyStats.value = calcMonthlyStats(countMap, start, weekCount.value)
  }

  async function load() {
    loading.value = true
    error.value = ''
    try {
      const { start } = currentRange()
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

  function setRange(key) {
    if (RANGE_OPTIONS[key]) {
      rangeKey.value = key
      load()
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
    streak,
    monthlyStats,
    levelThresholds,
    levelTexts,
    rangeKey,
    rangeConfig,
    useQuantile,
    plotX0: PLOT_X0,
    cell: CELL,
    step: STEP,
    rowBase: ROW_BASE,
    load,
    setRange
  }
}
