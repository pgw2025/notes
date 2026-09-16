<template>
  <!-- ========== bar 模式：月度条形图（移动端默认态） ========== -->
  <div v-if="mode === 'bar'" class="heatmap-bar-card" @click="$emit('expand')">
    <div class="bar-header">
      <div class="bar-title-row">
        <span class="bar-title">活跃概览</span>
        <span v-if="streak > 0" class="bar-streak">
          <span class="streak-icon">🔥</span>
          连续 {{ streak }} 天
        </span>
      </div>
      <span class="bar-expand-btn">
        查看热力图
        <van-icon name="arrow" size="12" />
      </span>
    </div>

    <div v-if="loading" class="bar-loading">
      <van-loading size="16" color="#1989fa">加载中…</van-loading>
    </div>
    <div v-else-if="error" class="bar-error">{{ error }}</div>
    <div v-else-if="!hasData" class="bar-empty">暂无数据</div>
    <template v-else>
      <div class="bar-chart">
        <div
          v-for="m in recentMonths"
          :key="m.key"
          class="bar-col"
          :title="`${m.year}年${m.month}月：${m.total} 篇 / ${m.activeDays} 天活跃`"
        >
          <div class="bar-col-inner">
            <div
              class="bar-fill"
              :style="{ height: barHeight(m.total) + '%' }"
              :class="['bar-lv-' + barLevel(m.total)]"
            ></div>
          </div>
          <span class="bar-label">{{ m.label }}</span>
        </div>
      </div>
      <div class="bar-footer">
        <span class="bar-peak">峰值 {{ peak }} 篇/天</span>
        <span class="bar-total">共 {{ totalNotes }} 篇</span>
      </div>
    </template>
  </div>

  <!-- ========== compact 模式：迷你热力图（桌面端左侧栏） ========== -->
  <div v-else-if="mode === 'compact'" class="heatmap-compact-card">
    <div class="compact-header">
      <span class="compact-title">活跃热力图</span>
      <span v-if="streak > 0" class="compact-streak" title="连续活跃天数">
        🔥 {{ streak }}
      </span>
    </div>

    <div v-if="loading" class="compact-state">
      <van-loading size="14" color="#1989fa" />
    </div>
    <div v-else-if="error" class="compact-state compact-error">加载失败</div>
    <div v-else-if="!hasData" class="compact-state compact-empty">暂无数据</div>
    <template v-else>
      <div class="compact-scroll">
        <svg :viewBox="compactViewBox" class="compact-svg" role="img" aria-label="活跃热力图">
          <g v-for="w in weeks" :key="`c-${w.col}`">
            <rect
              v-for="d in w.days"
              :key="d.date"
              :x="w.col * compactStep"
              :y="d.weekday * compactStep"
              :width="compactCell"
              :height="compactCell"
              rx="2"
              :class="['compact-cell', `lv-${d.level}`, { selected: d.date === selectedDate }]"
              role="button"
              :aria-label="`${d.date}，${d.count} 篇笔记`"
              @click="selectCell(d.date)"
            />
          </g>
        </svg>
      </div>
      <div class="compact-legend">
        <span>少</span>
        <span v-for="i in 5" :key="`l-${i}`" class="compact-legend-cell" :class="[`lv-${i - 1}`]"></span>
        <span>多</span>
      </div>
      <div class="compact-footer" @click.stop="$emit('expand')">
        <span class="compact-expand-text">查看完整热力图</span>
        <van-icon name="arrow" size="12" />
      </div>
    </template>
  </div>

  <!-- ========== full 模式：完整热力图 ========== -->
  <div v-else ref="cardEl" class="heatmap-card" :class="{ 'is-in-popup': inPopup }">
    <div class="heatmap-header">
      <div class="heatmap-title-row">
        <span class="heatmap-title">活跃热力图</span>
        <span v-if="streak > 0" class="heatmap-streak" title="连续活跃天数">
          <span class="streak-icon">🔥</span>
          连续 {{ streak }} 天
        </span>
      </div>
      <div class="heatmap-header-right">
        <span v-if="!loading && hasData && peak > 0" class="heatmap-peak">峰值 {{ peak }} 篇</span>
        <button v-if="inPopup" class="heatmap-close-btn" @click="$emit('close')" aria-label="关闭">
          <van-icon name="cross" size="16" />
        </button>
      </div>
    </div>

    <!-- 时间范围切换 -->
    <div class="heatmap-range-tabs">
      <span
        v-for="opt in rangeOptions"
        :key="opt.key"
        class="range-tab"
        :class="{ active: rangeKey === opt.key }"
        @click="setRange(opt.key)"
      >{{ opt.label }}</span>
    </div>

    <div v-if="loading" class="heatmap-state">
      <van-loading size="18" color="#1989fa">加载中…</van-loading>
    </div>
    <div v-else-if="error" class="heatmap-state heatmap-error">{{ error }}</div>
    <div v-else-if="!hasData" class="heatmap-state heatmap-empty">
      <span>近一年还没有编辑记录</span>
      <span class="heatmap-empty-sub">写笔记后会在这里点亮每一天</span>
    </div>

    <template v-else>
      <div class="heatmap-scroll">
        <svg :viewBox="viewBox" class="heatmap-svg" role="img" aria-label="近一年笔记编辑活跃热力图">
          <text
            v-for="l in monthLabels"
            :key="`m-${l.col}`"
            :x="plotX0 + l.col * step + cell / 2"
            y="15"
            text-anchor="middle"
            class="hm-month"
          >{{ l.label }}</text>
          <text
            v-for="(wd, i) in weekdayLabels"
            :key="`w-${i}`"
            x="14"
            :y="rowBase + i * step + cell / 2"
            dominant-baseline="central"
            text-anchor="middle"
            class="hm-weekday"
          >{{ wd }}</text>
          <g v-for="w in weeks" :key="`c-${w.col}`">
            <rect
              v-for="d in w.days"
              :key="d.date"
              :x="plotX0 + w.col * step"
              :y="rowBase + d.weekday * step"
              :width="cell"
              :height="cell"
              rx="3"
              :class="['hm-cell', `lv-${d.level}`, { selected: d.date === selectedDate, dim: isDim(d.level) }]"
              role="button"
              :aria-label="`${d.date}，${d.count} 篇笔记`"
              :tabindex="d.count > 0 ? 0 : -1"
              @click="selectCell(d.date)"
              @mouseenter="onEnter($event, d)"
              @mousemove="onMove"
              @mouseleave="onLeave"
              @focus="onEnter($event, d)"
              @blur="onLeave"
              @keydown.enter="selectCell(d.date)"
            />
          </g>
        </svg>
      </div>

      <!-- 桌面端 tooltip -->
      <div
        v-if="!isMobile"
        v-show="tooltip.show"
        class="hm-tooltip"
        :class="{ 'place-bottom': tooltip.place === 'bottom' }"
        :style="{ left: tooltip.x + 'px', top: tooltip.y + 'px' }"
      >
        <span class="hm-tip-date">{{ tooltip.dateText }}</span>
        <span class="hm-tip-count">{{ tooltip.count }} 篇</span>
        <span class="hm-tip-level">强度 {{ tooltip.levelText }}</span>
      </div>

      <!-- 移动端：点击选中后的信息条 -->
      <div v-if="isMobile && selectedDate" class="hm-mobile-info">
        <span class="hm-mob-date">{{ selectedDateText }}</span>
        <span class="hm-mob-count">{{ selectedCount }} 篇笔记</span>
        <button class="hm-mob-clear" @click="clearSelection">清除</button>
      </div>

      <div class="hm-legend" aria-label="活跃强度图例">
        <span>少</span>
        <span
          v-for="i in 5"
          :key="`l-${i}`"
          class="hm-legend-cell"
          :class="[`lv-${i - 1}`, { active: activeLevel() === i - 1 }]"
          role="button"
          :aria-label="`强度级别：${levelTexts[i - 1]}，点击高亮该级别`"
          :tabindex="0"
          @mouseenter="levelPreview = i - 1"
          @mouseleave="levelPreview = -1"
          @click="toggleLevelFilter(i - 1)"
          @keydown.enter="toggleLevelFilter(i - 1)"
        ></span>
        <span>多</span>
      </div>

      <!-- 底部操作栏（弹层模式下） -->
      <div v-if="inPopup" class="hm-popup-footer">
        <button class="hm-today-btn" @click="scrollToToday">
          <van-icon name="location-o" size="14" />
          定位到今天
        </button>
      </div>
    </template>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, watch } from 'vue'
import { useHeatmap } from '../composables/useHeatmap'
import { useResponsive } from '../composables/useResponsive'

const props = defineProps({
  mode: {
    type: String,
    default: 'full' // full | compact | bar
  },
  inPopup: {
    type: Boolean,
    default: false
  },
  initialRange: {
    type: String,
    default: '1y'
  }
})

const emit = defineEmits(['select', 'expand', 'close'])

const { isDesktop } = useResponsive()
const isMobile = computed(() => !isDesktop.value)

const {
  loading,
  error,
  weeks,
  monthLabels,
  peak,
  hasData,
  viewBox,
  streak,
  monthlyStats,
  levelTexts,
  rangeKey,
  useQuantile,
  plotX0,
  cell,
  step,
  rowBase,
  load,
  setRange
} = useHeatmap()

const weekdayLabels = ['一', '二', '三', '四', '五', '六', '日']

const rangeOptions = [
  { key: '3m', label: '近3月' },
  { key: '6m', label: '近6月' },
  { key: '1y', label: '近1年' }
]

const selectedDate = ref(null)
const cardEl = ref(null)
const tooltip = ref({ show: false, x: 0, y: 0, dateText: '', count: 0, levelText: '', place: 'top' })
const levelFilter = ref(-1)
const levelPreview = ref(-1)

// compact 模式尺寸
const compactCell = 14
const compactStep = 17
const compactViewBox = computed(() => {
  const w = weeks.value.length * compactStep
  const h = 7 * compactStep
  return `0 0 ${w} ${h}`
})

// bar 模式：最近 3 个月
const recentMonths = computed(() => {
  const months = monthlyStats.value || []
  return months.slice(-3)
})

const barMax = computed(() => {
  return Math.max(1, ...recentMonths.value.map(m => m.total))
})

const totalNotes = computed(() => {
  return monthlyStats.value.reduce((sum, m) => sum + m.total, 0)
})

function barHeight(total) {
  return Math.max(6, (total / barMax.value) * 100)
}

function barLevel(total) {
  const ratio = total / barMax.value
  if (ratio < 0.25) return 1
  if (ratio < 0.5) return 2
  if (ratio < 0.75) return 3
  return 4
}

// 选中日期的信息
const selectedDateText = computed(() => {
  if (!selectedDate.value) return ''
  const d = new Date(`${selectedDate.value}T00:00:00Z`)
  return `${d.getUTCFullYear()}年${d.getUTCMonth() + 1}月${d.getUTCDate()}日`
})

const selectedCount = computed(() => {
  if (!selectedDate.value) return 0
  for (const w of weeks.value) {
    for (const d of w.days) {
      if (d.date === selectedDate.value) return d.count
    }
  }
  return 0
})

function activeLevel() {
  return levelPreview.value >= 0 ? levelPreview.value : levelFilter.value
}

function isDim(level) {
  const active = activeLevel()
  return active >= 0 && level !== active
}

function toggleLevelFilter(level) {
  levelFilter.value = levelFilter.value === level ? -1 : level
}

function selectCell(date) {
  selectedDate.value = selectedDate.value === date ? null : date
  emit('select', selectedDate.value)
  // 移动端：在弹层中选择后，通知父组件关闭
  if (props.inPopup && isMobile.value && selectedDate.value) {
    setTimeout(() => emit('close'), 200)
  }
}

function clearSelection() {
  selectedDate.value = null
  emit('select', null)
}

function formatDateText(dateStr) {
  const d = new Date(`${dateStr}T00:00:00Z`)
  return `${d.getUTCFullYear()}年${d.getUTCMonth() + 1}月${d.getUTCDate()}日`
}

function onEnter(e, d) {
  if (isMobile.value) return
  const rect = cardEl.value.getBoundingClientRect()
  const x = e.clientX - rect.left
  const y = e.clientY - rect.top
  tooltip.value = {
    show: true,
    x,
    y,
    dateText: formatDateText(d.date),
    count: d.count,
    levelText: levelTexts.value[d.level],
    place: y < 64 ? 'bottom' : 'top'
  }
}

function onMove(e) {
  if (isMobile.value) return
  const rect = cardEl.value.getBoundingClientRect()
  const x = e.clientX - rect.left
  const y = e.clientY - rect.top
  const maxX = rect.width - 70
  const maxY = rect.height - 12
  tooltip.value.x = Math.min(Math.max(x, 70), Math.max(70, maxX))
  tooltip.value.y = Math.min(Math.max(y, 26), Math.max(26, maxY))
  tooltip.value.place = y < 64 ? 'bottom' : 'top'
}

function onLeave() {
  tooltip.value.show = false
}

function scrollToToday() {
  // 获取今天的日期字符串（UTC）
  const now = new Date()
  const todayStr = `${now.getUTCFullYear()}-${String(now.getUTCMonth() + 1).padStart(2, '0')}-${String(now.getUTCDate()).padStart(2, '0')}`
  // 选中今天的格子
  selectedDate.value = todayStr
  emit('select', todayStr)
  // 如果有滚动空间，滚动到最右边
  const scrollEl = cardEl.value?.querySelector('.heatmap-scroll')
  if (scrollEl && scrollEl.scrollWidth > scrollEl.clientWidth) {
    scrollEl.scrollTo({ left: scrollEl.scrollWidth, behavior: 'smooth' })
  }
}

onMounted(() => {
  if (props.initialRange && props.initialRange !== rangeKey.value) {
    rangeKey.value = props.initialRange
  }
  load()
})

watch(() => props.initialRange, (val) => {
  if (val && val !== rangeKey.value) {
    setRange(val)
  }
})
</script>

<style scoped>
.heatmap-card {
  --hm-lv1: rgba(25, 137, 250, 0.25);
  --hm-lv2: rgba(25, 137, 250, 0.5);
  --hm-lv3: rgba(25, 137, 250, 0.78);
  position: relative;
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 14px;
  padding: 14px 16px;
  box-shadow: var(--shadow-xs);
}

:global(body.dark) .heatmap-card {
  --hm-lv1: rgba(56, 189, 248, 0.22);
  --hm-lv2: rgba(56, 189, 248, 0.48);
  --hm-lv3: rgba(56, 189, 248, 0.76);
}

.heatmap-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 8px;
  margin-bottom: 10px;
}

.heatmap-title-row {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.heatmap-title {
  font-size: 14px;
  font-weight: 600;
  color: var(--text-primary);
}

.heatmap-streak {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  font-size: 12px;
  color: #ff7d00;
  font-weight: 500;
}

.streak-icon {
  font-size: 13px;
}

.heatmap-header-right {
  display: flex;
  align-items: center;
  gap: 8px;
}

.heatmap-peak {
  padding: 2px 10px;
  border-radius: 999px;
  background: rgba(25, 137, 250, 0.1);
  color: var(--color-primary);
  font-size: 12px;
  font-weight: 500;
}

.heatmap-close-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 28px;
  height: 28px;
  border: none;
  border-radius: 50%;
  background: var(--surface-2);
  color: var(--text-secondary);
  cursor: pointer;
  transition: background 0.15s ease;
}

.heatmap-close-btn:hover {
  background: var(--surface-3);
}

/* 时间范围切换 */
.heatmap-range-tabs {
  display: flex;
  gap: 4px;
  margin-bottom: 12px;
  padding: 3px;
  background: var(--surface-2);
  border-radius: 999px;
  width: fit-content;
}

.range-tab {
  padding: 4px 12px;
  border-radius: 999px;
  font-size: 12px;
  color: var(--text-secondary);
  cursor: pointer;
  transition: all 0.15s ease;
  user-select: none;
}

.range-tab:hover {
  color: var(--text-primary);
}

.range-tab.active {
  background: var(--surface);
  color: var(--color-primary);
  font-weight: 600;
  box-shadow: 0 1px 2px rgba(0,0,0,0.06);
}

.heatmap-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 6px;
  padding: 22px 0;
  font-size: 13px;
  color: var(--text-secondary);
}

.heatmap-error {
  color: var(--color-danger, #e8463a);
}

.heatmap-empty-sub {
  font-size: 12px;
  color: var(--text-tertiary);
}

.heatmap-scroll {
  position: relative;
  overflow-x: auto;
  -webkit-overflow-scrolling: touch;
}

.heatmap-svg {
  display: block;
  width: 100%;
  height: auto;
  min-height: 112px;
}

.hm-month {
  fill: var(--text-tertiary);
  font-size: 11px;
  font-weight: 500;
}

.hm-weekday {
  fill: var(--text-tertiary);
  font-size: 10px;
}

.hm-cell {
  fill: var(--surface-3);
  stroke: transparent;
  cursor: pointer;
  transition: transform 0.12s ease, stroke 0.12s ease, opacity 0.12s ease;
}

.hm-cell.lv-1 {
  fill: var(--hm-lv1);
}

.hm-cell.lv-2 {
  fill: var(--hm-lv2);
}

.hm-cell.lv-3 {
  fill: var(--hm-lv3);
}

.hm-cell.lv-4 {
  fill: var(--color-primary);
}

.hm-cell:hover,
.hm-cell:focus {
  outline: none;
  stroke: var(--text-primary);
  stroke-width: 1.2;
  transform: scale(1.12);
  transform-origin: center;
  transform-box: fill-box;
}

.hm-cell.dim {
  opacity: 0.3;
}

.hm-cell.selected {
  stroke: var(--text-primary);
  stroke-width: 1.5;
}

.hm-tooltip {
  position: absolute;
  z-index: 30;
  display: flex;
  flex-direction: column;
  gap: 2px;
  min-width: 118px;
  padding: 7px 10px;
  border-radius: 8px;
  background: var(--surface-2);
  border: 1px solid var(--border);
  box-shadow: var(--shadow-sm);
  pointer-events: none;
  transform: translate(-50%, -115%);
  white-space: nowrap;
}

.hm-tooltip.place-bottom {
  transform: translate(-50%, 12px);
}

.hm-tip-date {
  font-size: 12px;
  font-weight: 600;
  color: var(--text-primary);
}

.hm-tip-count {
  font-size: 12px;
  color: var(--color-primary);
  font-weight: 500;
}

.hm-tip-level {
  font-size: 11px;
  color: var(--text-tertiary);
}

/* 移动端选中信息条 */
.hm-mobile-info {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 8px 12px;
  margin: 10px 0;
  background: var(--surface-2);
  border-radius: 8px;
}

.hm-mob-date {
  font-size: 13px;
  font-weight: 600;
  color: var(--text-primary);
}

.hm-mob-count {
  font-size: 13px;
  color: var(--color-primary);
  font-weight: 500;
}

.hm-mob-clear {
  margin-left: auto;
  padding: 3px 10px;
  border: none;
  border-radius: 999px;
  background: var(--surface-3);
  color: var(--text-secondary);
  font-size: 12px;
  cursor: pointer;
}

.hm-legend {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 5px;
  margin-top: 10px;
  font-size: 11px;
  color: var(--text-tertiary);
}

.hm-legend-cell {
  width: 12px;
  height: 12px;
  border-radius: 3px;
  background: var(--surface-3);
  cursor: pointer;
  transition: transform 0.12s ease, box-shadow 0.12s ease;
}

.hm-legend-cell:hover,
.hm-legend-cell.active {
  transform: scale(1.25);
  box-shadow: 0 0 0 1.5px var(--text-primary);
}

.hm-legend-cell.lv-1 {
  background: var(--hm-lv1);
}

.hm-legend-cell.lv-2 {
  background: var(--hm-lv2);
}

.hm-legend-cell.lv-3 {
  background: var(--hm-lv3);
}

.hm-legend-cell.lv-4 {
  background: var(--color-primary);
}

/* 弹层底部操作栏 */
.hm-popup-footer {
  display: flex;
  justify-content: center;
  padding-top: 12px;
  margin-top: 12px;
  border-top: 1px solid var(--border);
}

.hm-today-btn {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 8px 16px;
  border: 1px solid var(--border);
  border-radius: 999px;
  background: var(--surface-2);
  color: var(--text-secondary);
  font-size: 13px;
  cursor: pointer;
  transition: all 0.15s ease;
}

.hm-today-btn:hover {
  border-color: var(--color-primary);
  color: var(--color-primary);
}

/* ========== compact 模式 ========== */
.heatmap-compact-card {
  --hm-lv1: rgba(25, 137, 250, 0.25);
  --hm-lv2: rgba(25, 137, 250, 0.5);
  --hm-lv3: rgba(25, 137, 250, 0.78);
  background: var(--surface-2);
  border: 1px solid var(--border);
  border-radius: 10px;
  padding: 10px;
}

:global(body.dark) .heatmap-compact-card {
  --hm-lv1: rgba(56, 189, 248, 0.22);
  --hm-lv2: rgba(56, 189, 248, 0.48);
  --hm-lv3: rgba(56, 189, 248, 0.76);
}

.compact-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 8px;
}

.compact-title {
  font-size: 12px;
  font-weight: 600;
  color: var(--text-primary);
}

.compact-streak {
  font-size: 11px;
  color: #ff7d00;
  font-weight: 500;
}

.compact-state {
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 16px 0;
  font-size: 12px;
  color: var(--text-tertiary);
}

.compact-error {
  color: var(--color-danger, #e8463a);
}

.compact-scroll {
  overflow-x: auto;
  -webkit-overflow-scrolling: touch;
  margin-bottom: 6px;
}

.compact-svg {
  display: block;
  width: 100%;
  height: auto;
  min-width: 100%;
}

.compact-cell {
  fill: var(--surface-3);
  cursor: pointer;
  transition: opacity 0.12s ease;
}

.compact-cell.lv-1 { fill: var(--hm-lv1); }
.compact-cell.lv-2 { fill: var(--hm-lv2); }
.compact-cell.lv-3 { fill: var(--hm-lv3); }
.compact-cell.lv-4 { fill: var(--color-primary); }

.compact-cell.selected {
  stroke: var(--text-primary);
  stroke-width: 1;
}

.compact-legend {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 3px;
  font-size: 10px;
  color: var(--text-tertiary);
  margin-bottom: 6px;
}

.compact-legend-cell {
  width: 8px;
  height: 8px;
  border-radius: 2px;
  background: var(--surface-3);
}

.compact-legend-cell.lv-1 { background: var(--hm-lv1); }
.compact-legend-cell.lv-2 { background: var(--hm-lv2); }
.compact-legend-cell.lv-3 { background: var(--hm-lv3); }
.compact-legend-cell.lv-4 { background: var(--color-primary); }

.compact-footer {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 4px;
  padding-top: 6px;
  border-top: 1px solid var(--border);
  font-size: 11px;
  color: var(--color-primary);
  cursor: pointer;
}

.compact-expand-text {
  font-weight: 500;
}

/* ========== bar 模式 ========== */
.heatmap-bar-card {
  --hm-lv1: rgba(25, 137, 250, 0.25);
  --hm-lv2: rgba(25, 137, 250, 0.5);
  --hm-lv3: rgba(25, 137, 250, 0.78);
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 12px;
  padding: 12px 14px;
  cursor: pointer;
  transition: all 0.15s ease;
  box-shadow: var(--shadow-xs);
}

.heatmap-bar-card:active {
  transform: scale(0.98);
  background: var(--surface-2);
}

:global(body.dark) .heatmap-bar-card {
  --hm-lv1: rgba(56, 189, 248, 0.22);
  --hm-lv2: rgba(56, 189, 248, 0.48);
  --hm-lv3: rgba(56, 189, 248, 0.76);
}

.bar-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 10px;
}

.bar-title-row {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.bar-title {
  font-size: 14px;
  font-weight: 600;
  color: var(--text-primary);
}

.bar-streak {
  font-size: 12px;
  color: #ff7d00;
  font-weight: 500;
}

.bar-expand-btn {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  font-size: 12px;
  color: var(--color-primary);
  font-weight: 500;
}

.bar-loading,
.bar-error,
.bar-empty {
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 20px 0;
  font-size: 13px;
  color: var(--text-secondary);
}

.bar-error {
  color: var(--color-danger, #e8463a);
}

.bar-chart {
  display: flex;
  align-items: flex-end;
  gap: 12px;
  height: 88px;
  margin-bottom: 8px;
}

.bar-col {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 4px;
}

.bar-col-inner {
  width: 100%;
  height: 68px;
  display: flex;
  align-items: flex-end;
  justify-content: center;
}

.bar-fill {
  width: 50%;
  min-height: 4px;
  border-radius: 3px 3px 0 0;
  background: var(--hm-lv1);
  transition: height 0.3s ease;
}

.bar-fill.bar-lv-1 { background: var(--hm-lv1); }
.bar-fill.bar-lv-2 { background: var(--hm-lv2); }
.bar-fill.bar-lv-3 { background: var(--hm-lv3); }
.bar-fill.bar-lv-4 { background: var(--color-primary); }

.bar-label {
  font-size: 11px;
  color: var(--text-tertiary);
}

.bar-footer {
  display: flex;
  justify-content: space-between;
  font-size: 11px;
  color: var(--text-tertiary);
  padding-top: 6px;
  border-top: 1px solid var(--border);
}

.bar-peak {
  color: var(--text-tertiary);
}

.bar-total {
  font-weight: 500;
  color: var(--text-secondary);
}

/* 响应式 */
@media (max-width: 520px) {
  .heatmap-card {
    padding: 12px;
  }
  .heatmap-card.is-in-popup {
    border-radius: 16px 16px 0 0;
    border-bottom: none;
  }
}
</style>
