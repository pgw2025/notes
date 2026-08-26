import { defineStore } from 'pinia'

/**
 * 在线/离线状态 store。
 * 通过网络事件实时维护 isOffline，供全局离线横幅、写操作拦截等使用。
 * 初始化前 isOffline 基于 navigator.onLine 计算（SSR 安全）。
 */
export const useOfflineStore = defineStore('offline', {
  state: () => ({
    isOffline:
      typeof navigator !== 'undefined' && typeof navigator.onLine === 'boolean'
        ? !navigator.onLine
        : false
  }),
  actions: {
    /** 在应用挂载时调用一次，监听 online/offline 事件 */
    init() {
      if (typeof window === 'undefined') return
      const update = () => {
        this.isOffline = !navigator.onLine
      }
      window.addEventListener('online', update)
      window.addEventListener('offline', update)
    }
  }
})