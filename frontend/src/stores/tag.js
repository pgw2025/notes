import { defineStore } from 'pinia'
import { ref } from 'vue'
import http from '../api/http'
import { getAllTags, putTags } from '../utils/db'

// 同 category.js：拆成独立函数避免 Rolldown minification 变量名冲突。
async function _phase1ReadCache() {
  try {
    const cached = await getAllTags()
    if (cached.length > 0) {
      return cached.sort((a, b) => (b.noteCount || 0) - (a.noteCount || 0))
    }
  } catch { /* IndexedDB 读取失败忽略 */ }
  return null
}

async function _phase2SyncNetwork() {
  const remote = await http.get('/tags')
  const list = remote.sort((a, b) => (b.noteCount || 0) - (a.noteCount || 0))
  try { await putTags(list) } catch { /* ignore */ }
  return list
}

export const useTagStore = defineStore('tag', () => {
  const tags = ref([])
  const loading = ref(false)

  async function fetchTags() {
    // Phase 1: 本地优先
    const cachedList = await _phase1ReadCache()
    let hasLocal = cachedList !== null
    if (hasLocal) tags.value = cachedList

    // Phase 2: 网络同步
    loading.value = !hasLocal
    try {
      const fresh = await _phase2SyncNetwork()
      tags.value = fresh
    } catch {
      // 网络失败 → 保留 Phase 1 的本地数据
    } finally {
      loading.value = false
    }
  }

  function addTag(tag) {
    if (!tag) return
    const existing = tags.value.find((t) => t.id === tag.id)
    if (!existing) {
      tags.value.unshift(tag)
    }
  }

  function removeTag(tagId) {
    tags.value = tags.value.filter((t) => t.id !== tagId)
  }

  return {
    tags,
    loading,
    fetchTags,
    addTag,
    removeTag
  }
})
