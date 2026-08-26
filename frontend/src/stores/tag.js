import { defineStore } from 'pinia'
import { ref } from 'vue'
import http from '../api/http'

export const useTagStore = defineStore('tag', () => {
  const tags = ref([])
  const loading = ref(false)

  async function fetchTags() {
    loading.value = true
    try {
      const list = await http.get('/tags')
      tags.value = list.sort((a, b) => (b.noteCount || 0) - (a.noteCount || 0))
    } catch {
      // 忽略
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
