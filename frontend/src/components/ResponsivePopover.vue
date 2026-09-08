<template>
  <!-- 移动端（< 1024px）：保持原生底部抽屉滑动交互与圆角 -->
  <van-popup
    v-if="!isDesktop"
    :show="show"
    position="bottom"
    round
    :class="modalClass"
    :style="mobilePopupStyle"
    @update:show="$emit('update:show', $event)"
  >
    <slot />
  </van-popup>

  <!-- 桌面端（>= 1024px）：就近锚定浮动面板 Popover (Notion / Linear 风格) -->
  <Teleport to="body" v-else>
    <transition name="popover-fade">
      <div v-if="show" class="popover-portal-container">
        <!-- 透明背景点击遮罩：点击外部即刻收起，不遮挡/不暗化笔记正文 -->
        <div class="popover-backdrop" @click="close" />

        <!-- 悬浮卡片主体 -->
        <div
          ref="cardRef"
          class="popover-floating-card"
          :class="[modalClass, { 'is-centered': isCenteredFallback }]"
          :style="popoverStyle"
          tabindex="-1"
          @keydown.esc="close"
        >
          <slot />
        </div>
      </div>
    </transition>
  </Teleport>
</template>

<script setup>
import { ref, computed, watch, onMounted, onUnmounted, nextTick } from 'vue'
import { useResponsive } from '../composables/useResponsive'

const props = defineProps({
  show: {
    type: Boolean,
    default: false
  },
  anchorEl: {
    type: [Object, null],
    default: null
  },
  width: {
    type: Number,
    default: 380
  },
  maxHeight: {
    type: Number,
    default: 480
  },
  modalClass: {
    type: String,
    default: ''
  },
  mobilePopupStyle: {
    type: Object,
    default: () => ({ maxHeight: '80vh' })
  }
})

const emit = defineEmits(['update:show'])

const { isDesktop } = useResponsive()
const cardRef = ref(null)
const isCenteredFallback = ref(false)
const popoverStyle = ref({})

const close = () => {
  emit('update:show', false)
}

const updatePosition = () => {
  if (!props.show || !isDesktop.value) return

  const target = props.anchorEl?.$el || props.anchorEl
  const width = props.width || 380
  const margin = 16
  const estimatedHeight = props.maxHeight || 460

  // 若未传入锚点或锚点不在文档流中，降级居中呈现
  if (!target || typeof target.getBoundingClientRect !== 'function') {
    isCenteredFallback.value = true
    popoverStyle.value = {
      width: `${width}px`,
      maxHeight: `${estimatedHeight}px`,
      top: '50%',
      left: '50%',
      transform: 'translate(-50%, -50%)'
    }
    return
  }

  isCenteredFallback.value = false
  const rect = target.getBoundingClientRect()

  // 1. 水平定位：优先与锚点左边缘齐平
  let left = rect.left
  if (left + width > window.innerWidth - margin) {
    left = window.innerWidth - width - margin
  }
  if (left < margin) {
    left = margin
  }

  // 2. 垂直定位：优先位于锚点正下方 8px
  let top = rect.bottom + 8
  // 若视口下方空间不足，智能向上翻转展开
  if (top + estimatedHeight > window.innerHeight - margin) {
    if (rect.top - estimatedHeight - 8 > margin) {
      top = rect.top - estimatedHeight - 8
    } else {
      top = Math.max(margin, window.innerHeight - estimatedHeight - margin)
    }
  }

  popoverStyle.value = {
    width: `${width}px`,
    maxHeight: `${estimatedHeight}px`,
    top: `${Math.round(top)}px`,
    left: `${Math.round(left)}px`
  }
}

watch(
  () => props.show,
  (newVal) => {
    if (newVal && isDesktop.value) {
      nextTick(() => {
        updatePosition()
        cardRef.value?.focus?.()
      })
    }
  }
)

watch(
  () => props.anchorEl,
  () => {
    if (props.show && isDesktop.value) {
      nextTick(updatePosition)
    }
  }
)

onMounted(() => {
  window.addEventListener('resize', updatePosition)
  window.addEventListener('scroll', updatePosition, true)
})

onUnmounted(() => {
  window.removeEventListener('resize', updatePosition)
  window.removeEventListener('scroll', updatePosition, true)
})
</script>

<style scoped>
.popover-portal-container {
  position: fixed;
  inset: 0;
  z-index: 2050;
  pointer-events: auto;
}

/* 隐形点击遮罩：带极轻微的微光感知，既不挡正文又可点击关闭 */
.popover-backdrop {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.03);
  z-index: 1;
}

/* Notion / Linear 风格高质感悬浮卡片 */
.popover-floating-card {
  position: fixed;
  z-index: 2;
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 12px;
  box-shadow: 0 16px 36px -6px rgba(0, 0, 0, 0.2), 0 4px 14px rgba(0, 0, 0, 0.06);
  overflow: hidden;
  outline: none;
  display: flex;
  flex-direction: column;
}

body.dark .popover-floating-card {
  border-color: var(--border-strong, #333);
  box-shadow: 0 20px 40px -8px rgba(0, 0, 0, 0.6), 0 0 0 1px rgba(255, 255, 255, 0.08);
}

/* 动效：弹性质感微缩放淡入 */
.popover-fade-enter-active {
  transition: opacity 0.16s cubic-bezier(0.16, 1, 0.3, 1), transform 0.16s cubic-bezier(0.16, 1, 0.3, 1);
}
.popover-fade-leave-active {
  transition: opacity 0.12s ease-in, transform 0.12s ease-in;
}

.popover-fade-enter-from {
  opacity: 0;
  transform: translateY(-4px) scale(0.98);
}
.popover-fade-leave-to {
  opacity: 0;
  transform: translateY(-2px) scale(0.99);
}
</style>
