import { defineStore } from 'pinia'
import { ref } from 'vue'
import http from '../api/http'
import { getAllTags, putTags } from '../utils/db'

export const useTagStore = defineStore('tag', () => {
  const tags = ref([])
  const loading = ref(false)

  /**
   * 先从 IndexedDB 本地缓存读取 → 立即渲染，
   * 再后台请求 /tags → 更新 IndexedDB + 刷新 UI。
   * 网络失败时保留本地数据。
   */
  async function fetchTags() {
    let hasLocal = false

    // Phase 1: 本地优先
    try {
      const local = await getAllTags()
      if (local.length > 0) {
        tags.value = local.sort((a, b) => (b.noteCount || 0) - (a.noteCount || 0))
        hasLocal = true
      }
    } catch { /* IndexedDB 读取失败忽略 */ }

    // Phase 2: 网络同步
    loading.value = !hasLocal
    try {
      const list = await http.get('/tags')
      tags.value = list.sort((a, b) => (b.noteCount || 0) - (a.noteCount || 0))
      await putTags(list)
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
