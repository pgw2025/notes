<template>
  <div ref="cardEl" class="heatmap-card">
    <div class="heatmap-header">
      <span class="heatmap-title">活跃热力图</span>
      <span v-if="!loading && hasData && peak > 0" class="heatmap-peak">峰值 {{ peak }} 篇</span>
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

      <div
        v-show="tooltip.show"
        class="hm-tooltip"
        :class="{ 'place-bottom': tooltip.place === 'bottom' }"
        :style="{ left: tooltip.x + 'px', top: tooltip.y + 'px' }"
      >
        <span class="hm-tip-date">{{ tooltip.dateText }}</span>
        <span class="hm-tip-count">{{ tooltip.count }} 篇</span>
        <span class="hm-tip-level">强度 {{ tooltip.levelText }}</span>
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
    </template>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { useHeatmap } from '../composables/useHeatmap'

const emit = defineEmits(['select'])

const {
  loading,
  error,
  weeks,
  monthLabels,
  peak,
  hasData,
  viewBox,
  plotX0,
  cell,
  step,
  rowBase,
  load
} = useHeatmap()

const weekdayLabels = ['一', '二', '三', '四', '五', '六', '日']
const levelTexts = ['无', '低', '中', '高', '峰值']

const selectedDate = ref(null)
const cardEl = ref(null)
const tooltip = ref({ show: false, x: 0, y: 0, dateText: '', count: 0, levelText: '', place: 'top' })
const levelFilter = ref(-1) // 图例点击锁定：-1 表示未锁定
const levelPreview = ref(-1) // 图例 hover 预览：-1 表示未预览

// 当前生效的高亮级别（预览优先于锁定）
function activeLevel() {
  return levelPreview.value >= 0 ? levelPreview.value : levelFilter.value
}

// 非目标级别的格子弱化
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
}

function formatDateText(dateStr) {
  const d = new Date(`${dateStr}T00:00:00Z`)
  return `${d.getUTCFullYear()}年${d.getUTCMonth() + 1}月${d.getUTCDate()}日`
}

function onEnter(e, d) {
  const rect = cardEl.value.getBoundingClientRect()
  const x = e.clientX - rect.left
  const y = e.clientY - rect.top
  tooltip.value = {
    show: true,
    x,
    y,
    dateText: formatDateText(d.date),
    count: d.count,
    levelText: levelTexts[d.level],
    place: y < 64 ? 'bottom' : 'top'
  }
}

function onMove(e) {
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

onMounted(load)
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
  align-items: center;
  gap: 8px;
  margin-bottom: 12px;
}

.heatmap-title {
  font-size: 14px;
  font-weight: 600;
  color: var(--text-primary);
}

.heatmap-peak {
  margin-left: auto;
  padding: 2px 10px;
  border-radius: 999px;
  background: rgba(25, 137, 250, 0.1);
  color: var(--color-primary);
  font-size: 12px;
  font-weight: 500;
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
  min-width: 889px;
  width: 100%;
  height: auto;
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

@media (max-width: 520px) {
  .heatmap-card {
    padding: 12px;
  }
}
</style>
