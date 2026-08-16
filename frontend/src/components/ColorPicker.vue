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

      <!-- 颜色列表（用户自定义管理） -->
      <div v-if="colors.length > 0" class="color-grid">
        <div
          v-for="c in colors"
          :key="c"
          class="color-item"
          :class="{ active: isActive(c), 'delete-mode': deleteMode }"
          :style="{ background: c }"
          @click="onSelect(c)"
          @touchstart="onTouchStart(c)"
          @touchend="onTouchEnd"
          @touchmove="onTouchEnd"
        >
          <van-icon v-if="isActive(c) && !deleteMode" name="success" :color="getContrastColor(c)" size="20" class="check-icon" />
          <van-icon v-if="deleteMode" name="cross" color="#fff" size="16" class="delete-icon" />
        </div>

        <!-- 添加新颜色按钮 -->
        <label class="color-item add-btn" @click.stop>
          <van-icon name="plus" size="20" color="#969799" />
          <input
            type="color"
            :value="pendingColor"
            class="hidden-color-input"
            @input="onPickNewColor($event.target.value)"
          />
        </label>
      </div>

      <!-- 空状态：色板被删空了 -->
      <div v-else class="empty-palette">
        <van-icon name="brush-o" size="48" color="#dcdee0" />
        <p class="empty-text">还没有颜色，点击下方按钮添加</p>
        <label class="add-first-btn">
          <van-button type="primary" size="small" plain>添加颜色</van-button>
          <input
            type="color"
            :value="pendingColor"
            class="hidden-color-input"
            @input="onPickNewColor($event.target.value)"
          />
        </label>
      </div>

      <!-- 操作按钮 -->
      <div class="action-row">
        <van-button block plain @click="onToggleDeleteMode">
          {{ deleteMode ? '完成删除' : '管理颜色' }}
        </van-button>
        <van-button block plain @click="onReset">重置为默认</van-button>
        <van-button block type="primary" @click="onConfirm">确定</van-button>
      </div>

      <div v-if="deleteMode" class="delete-hint">
        点击颜色块即可删除
      </div>
    </div>
  </van-popup>
</template>

<script setup>
import { ref, computed, watch } from 'vue'
import { showToast } from 'vant'
import { getContrastColor, isValidHex, DEFAULT_NOTE_COLOR } from '../utils/color'
import { useAuthStore } from '../stores/auth'

const props = defineProps({
  show: { type: Boolean, default: false },
  /** 当前选中的颜色（null 表示用默认色） */
  modelValue: { type: String, default: null }
})

const emit = defineEmits(['update:show', 'update:modelValue', 'confirm', 'reset'])

const auth = useAuthStore()

// 用户色板（来自 store）
const colors = computed(() => Array.isArray(auth.user?.customColors) ? auth.user.customColors : [])

// 内部选中值：null 表示用默认色，否则为 #RRGGBB
const selected = ref(null)
const deleteMode = ref(false)
const pendingColor = ref(DEFAULT_NOTE_COLOR)

// 弹窗打开时同步 props.modelValue
watch(() => props.show, async (newShow) => {
  if (newShow) {
    selected.value = props.modelValue && isValidHex(props.modelValue) ? props.modelValue : null
    deleteMode.value = false
    // 确保用户信息已加载（含 customColors）
    if (!auth.user) {
      try { await auth.fetchUser() } catch { /* ignore */ }
    }
  }
})

function isActive(hex) {
  const current = selected.value || DEFAULT_NOTE_COLOR
  return current.toUpperCase() === hex.toUpperCase()
}

function onSelect(hex) {
  if (deleteMode.value) {
    // 删除模式下点击 = 删除
    onDeleteColor(hex)
    return
  }
  selected.value = hex
}

// 长按触发删除模式（移动端）
let longPressTimer = null
function onTouchStart(hex) {
  longPressTimer = setTimeout(() => {
    deleteMode.value = true
    showToast('已进入删除模式')
  }, 600)
}
function onTouchEnd() {
  if (longPressTimer) {
    clearTimeout(longPressTimer)
    longPressTimer = null
  }
}

async function onDeleteColor(hex) {
  try {
    await auth.removeCustomColor(hex)
    showToast('已删除')
    // 如果删除的是当前选中色，回退到默认色
    if (selected.value && selected.value.toUpperCase() === hex.toUpperCase()) {
      selected.value = null
    }
  } catch {
    showToast('删除失败')
  }
}

async function onPickNewColor(hex) {
  pendingColor.value = hex
  if (!isValidHex(hex)) return
  try {
    const added = await auth.addCustomColor(hex)
    if (added) {
      showToast('已添加到色板')
      // 自动选中新添加的颜色
      selected.value = hex.toUpperCase()
    } else {
      showToast('该颜色已存在')
    }
  } catch {
    showToast('添加失败')
  }
}

function onToggleDeleteMode() {
  deleteMode.value = !deleteMode.value
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

.color-grid {
  display: grid;
  grid-template-columns: repeat(5, 1fr);
  gap: 10px;
  margin-bottom: 20px;
}

.color-item {
  position: relative;
  height: 56px;
  border-radius: 8px;
  border: 1px solid rgba(0, 0, 0, 0.06);
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  transition: transform 0.15s;
  overflow: hidden;
}

.color-item:active {
  transform: scale(0.95);
}

.color-item.active {
  border: 2px solid #1989fa;
  box-shadow: 0 0 0 2px rgba(25, 137, 250, 0.2);
}

.color-item.delete-mode {
  border: 2px dashed #ee0a24;
}

.check-icon {
  position: absolute;
  top: 8px;
  right: 6px;
}

.delete-icon {
  position: absolute;
  top: 4px;
  right: 4px;
  background: #ee0a24;
  border-radius: 50%;
  padding: 2px;
  box-shadow: 0 1px 4px rgba(0, 0, 0, 0.2);
}

.add-btn {
  background: #f7f8fa;
  border: 1px dashed #dcdee0;
  cursor: pointer;
}

.add-btn:hover {
  background: #f2f3f5;
}

.hidden-color-input {
  position: absolute;
  inset: 0;
  opacity: 0;
  cursor: pointer;
}

.empty-palette {
  text-align: center;
  padding: 24px 0;
  margin-bottom: 20px;
}

.empty-text {
  font-size: 13px;
  color: #969799;
  margin: 12px 0 16px;
}

.add-first-btn {
  position: relative;
  display: inline-block;
  cursor: pointer;
}

.action-row {
  display: flex;
  gap: 8px;
}

.action-row .van-button {
  flex: 1;
}

.delete-hint {
  text-align: center;
  font-size: 12px;
  color: #ee0a24;
  margin-top: 10px;
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
