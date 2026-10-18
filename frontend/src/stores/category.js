import { defineStore } from 'pinia'
import { ref } from 'vue'
import http from '../api/http'
import { getAllCategories, putCategories } from '../utils/db'

export const useCategoryStore = defineStore('category', () => {
  const categories = ref([])
  const loading = ref(false)

  /**
   * 先从 IndexedDB 本地缓存读取 → 立即渲染（<10ms），
   * 再后台请求 /categories → 更新 IndexedDB + 刷新 UI。
   * 网络失败时保留本地数据，不影响已渲染的界面。
   */
  async function fetchCategories() {
    let hasLocal = false

    // Phase 1: 本地优先
    try {
      const local = await getAllCategories()
      if (local.length > 0) {
        categories.value = (local || []).sort((a, b) => b.noteCount - a.noteCount)
        hasLocal = true
      }
    } catch { /* IndexedDB 读取失败忽略 */ }

    // Phase 2: 网络同步
    loading.value = !hasLocal // 有本地缓存就不显示 loading，直接渲染
    try {
      const list = await http.get('/categories')
      categories.value = (list || []).sort((a, b) => b.noteCount - a.noteCount)
      await putCategories(list || [])
    } catch {
      // 网络失败 → 保留 Phase 1 的本地数据
    } finally {
      loading.value = false
    }
  }

  return {
    categories,
    loading,
    fetchCategories
  }
})
