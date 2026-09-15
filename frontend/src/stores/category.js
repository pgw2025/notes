import { defineStore } from 'pinia'
import { ref } from 'vue'
import http from '../api/http'

export const useCategoryStore = defineStore('category', () => {
  const categories = ref([])
  const loading = ref(false)

  async function fetchCategories() {
    loading.value = true
    try {
      const list = await http.get('/categories')
      // 常用分类（笔记数多）排前面，便于快速定位（仅对顶层排序，保留树结构）
      categories.value = (list || []).sort((a, b) => b.noteCount - a.noteCount)
    } catch {
      // 忽略
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
