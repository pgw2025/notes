import { ref, onMounted, onUnmounted } from 'vue'

// 桌面端断点：>= 1024px 视为桌面
const DESKTOP_BREAKPOINT = 1024

export function useResponsive() {
  const isDesktop = ref(typeof window !== 'undefined' && window.innerWidth >= DESKTOP_BREAKPOINT)
  let rafId = null

  const update = () => {
    if (rafId) cancelAnimationFrame(rafId)
    rafId = requestAnimationFrame(() => {
      isDesktop.value = window.innerWidth >= DESKTOP_BREAKPOINT
    })
  }

  onMounted(() => window.addEventListener('resize', update))
  onUnmounted(() => {
    if (rafId) cancelAnimationFrame(rafId)
    window.removeEventListener('resize', update)
  })

  return { isDesktop }
}
