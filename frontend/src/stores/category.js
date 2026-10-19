import { defineStore } from 'pinia'
import { ref } from 'vue'
import http from '../api/http'
import { getAllCategories, putCategories } from '../utils/db'

// 把 Phase 1 和 Phase 2 拆成独立函数，避免 Rolldown minification
// 在同一作用域里把 Dexie 结果数组和 Dexie 写入函数压成同一个短名，
// 导致 `await n(t)` 把数组当函数调用（e is not a function）。
async function _phase1ReadCache() {
  try {
    const cached = await getAllCategories()
    if (cached.length > 0) {
      return cached.sort((a, b) => b.noteCount - a.noteCount)
    }
  } catch { /* IndexedDB 读取失败忽略 */ }
  return null
}

async function _phase2SyncNetwork() {
  const remote = await http.get('/categories')
  const list = (remote || []).sort((a, b) => b.noteCount - a.noteCount)
  try { await putCategories(list) } catch { /* ignore */ }
  return list
}

export const useCategoryStore = defineStore('category', () => {
  const categories = ref([])
  const loading = ref(false)

  async function fetchCategories() {
    // Phase 1: 本地优先
    const cachedList = await _phase1ReadCache()
    let hasLocal = cachedList !== null
    if (hasLocal) categories.value = cachedList

    // Phase 2: 网络同步
    loading.value = !hasLocal
    try {
      const fresh = await _phase2SyncNetwork()
      categories.value = fresh
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
