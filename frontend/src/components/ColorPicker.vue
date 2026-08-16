<template>
  <van-popup
    :show="show"
    position="bottom"
    round
    closeable
    @update:show="(v) => $emit('update:show', v)"
  >
    <div class="color-picker">
      <div class="picker-title">选择背景色</div>

      <!-- 预设色板 -->
      <div class="preset-grid">
        <div
          v-for="c in PRESET_COLORS"
          :key="c.hex"
          class="preset-item"
          :class="{ active: isActive(c.hex) }"
          :style="{ background: c.hex }"
          @click="onSelect(c.hex)"
        >
          <van-icon v-if="isActive(c.hex)" name="success" :color="getContrastColor(c.hex)" size="20" />
          <span class="preset-name" :style="{ color: getContrastColor(c.hex) }">{{ c.name }}</span>
        </div>
      </div>

      <!-- 自定义颜色 -->
      <div class="custom-row">
        <span class="custom-label">自定义颜色</span>
        <label class="custom-input-wrap">
          <input
            type="color"
            :value="customValue"
            class="custom-color-input"
            @input="onCustomInput($event.target.value)"
          />
          <span class="custom-hex">{{ customValue }}</span>
        </label>
      </div>

      <!-- 操作按钮 -->
      <div class="action-row">
        <van-button block plain @click="onReset">重置为默认</van-button>
        <van-button block type="primary" @click="onConfirm">确定</van-button>
      </div>
    </div>
  </van-popup>
</template>

<script setup>
import { ref, watch, computed } from 'vue'
import { PRESET_COLORS, getContrastColor, isValidHex, DEFAULT_NOTE_COLOR } from '../utils/color'

const props = defineProps({
  show: { type: Boolean, default: false },
  /** 当前选中的颜色（null 表示用默认色） */
  modelValue: { type: String, default: null }
})

const emit = defineEmits(['update:show', 'update:modelValue', 'confirm', 'reset'])

// 内部选中值：null 表示用默认色，否则为 #RRGGBB
const selected = ref(null)
// 自定义颜色 input 的值（始终是合法 HEX）
const customValue = ref(DEFAULT_NOTE_COLOR)

// 弹窗打开时同步 props.modelValue
watch(() => props.show, (newShow) => {
  if (newShow) {
    selected.value = props.modelValue && isValidHex(props.modelValue) ? props.modelValue : null
    customValue.value = selected.value || DEFAULT_NOTE_COLOR
  }
})

function isActive(hex) {
  // null 模式下高亮"经典白"（系统默认色）
  const current = selected.value || DEFAULT_NOTE_COLOR
  return current.toUpperCase() === hex.toUpperCase()
}

function onSelect(hex) {
  selected.value = hex
  customValue.value = hex
}

function onCustomInput(hex) {
  customValue.value = hex
  selected.value = hex
}

function onReset() {
  selected.value = null
  emit('update:modelValue', null)
  emit('reset')
  emit('update:show', false)
}

function onConfirm() {
  emit('update:modelValue', selected.value)
  emit('confirm', selected.value)
  emit('update:show', false)
}
</script>

<style scoped>
.color-picker {
  padding: 20px 16px 24px;
}

.picker-title {
  font-size: 16px;
  font-weight: 600;
  text-align: center;
  margin-bottom: 16px;
  color: #323233;
}

.preset-grid {
  display: grid;
  grid-template-columns: repeat(5, 1fr);
  gap: 10px;
  margin-bottom: 20px;
}

.preset-item {
  position: relative;
  height: 56px;
  border-radius: 8px;
  border: 1px solid rgba(0, 0, 0, 0.06);
  display: flex;
  align-items: flex-end;
  justify-content: center;
  padding-bottom: 4px;
  cursor: pointer;
  transition: transform 0.15s;
  overflow: hidden;
}

.preset-item:active {
  transform: scale(0.95);
}

.preset-item.active {
  border: 2px solid #1989fa;
  box-shadow: 0 0 0 2px rgba(25, 137, 250, 0.2);
}

.preset-item .van-icon {
  position: absolute;
  top: 8px;
  right: 6px;
}

.preset-name {
  font-size: 11px;
  font-weight: 500;
  text-shadow: 0 1px 2px rgba(255, 255, 255, 0.4);
}

.custom-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 0;
  border-top: 1px solid #ebedf0;
  margin-bottom: 16px;
}

.custom-label {
  font-size: 14px;
  color: #323233;
}

.custom-input-wrap {
  display: flex;
  align-items: center;
  gap: 8px;
  cursor: pointer;
}

.custom-color-input {
  width: 40px;
  height: 32px;
  border: 1px solid #ebedf0;
  border-radius: 6px;
  padding: 0;
  background: transparent;
  cursor: pointer;
}

.custom-hex {
  font-size: 13px;
  color: #969799;
  font-family: 'SFMono-Regular', Consolas, monospace;
}

.action-row {
  display: flex;
  gap: 12px;
}

/* 桌面端居中弹窗 */
@media (min-width: 1024px) {
  :deep(.van-popup--bottom) {
    top: 50%;
    bottom: auto;
    left: 50%;
    transform: translate(-50%, -50%);
    width: 480px;
    max-width: 90vw;
    border-radius: 12px;
  }
}
</style>
