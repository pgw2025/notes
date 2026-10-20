<template>
  <div class="sidebar-cats-section">
    <div class="cats-nav-header">
      <div class="header-left">
        <van-icon name="apps-o" size="14" class="header-icon" />
        <span class="cats-title">分类</span>
        <span v-if="categories.length" class="cats-count-badge">{{ categories.length }}</span>
      </div>
      <router-link to="/categories" class="cats-manage-link" title="管理所有分类">
        <van-icon name="setting-o" size="14" />
      </router-link>
    </div>

    <!-- 分类树（递归渲染，父分类可展开/折叠，点击跳转筛选） -->
    <div v-if="categories.length > 0" class="cats-scroll-container">
      <SidebarCategoryNode
        v-for="c in categories"
        :key="c.id"
        :node="c"
        :depth="0"
        :expanded="expanded"
        :active-id="activeId"
        @toggle="toggle"
        @select="select"
      />
    </div>

    <!-- 空状态 -->
    <div v-else class="cats-empty">
      <span>暂无分类</span>
      <router-link to="/categories" class="empty-add-link">去创建</router-link>
    </div>
  </div>
</template>

<script setup>
import { computed, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { flattenCategories } from '../utils/categoryTree'
import SidebarCategoryNode from './SidebarCategoryNode.vue'

const props = defineProps({
  categories: {
    type: Array,
    default: () => []
  }
})

const route = useRoute()
const router = useRouter()

// 展开的分类 id 集合（父分类含子分类时才展开）
const expanded = ref(new Set())

// 当前激活分类 id（用于高亮，含父/子分类）
const activeId = computed(() => {
  if (route.path !== '/notes') return null
  const id = Number(route.query.categoryId)
  return Number.isFinite(id) && id > 0 ? id : null
})

function toggle(id) {
  const next = new Set(expanded.value)
  if (next.has(id)) {
    next.delete(id)
  } else {
    next.add(id)
  }
  expanded.value = next
}

function select(node) {
  router.push({ path: '/notes', query: { categoryId: node.id } })
}

// 切换分类源时，把当前激活分类及其祖先路径自动展开，方便定位
watch(
  () => props.categories,
  (list) => {
    if (!activeId.value) return
    const flat = flattenCategories(list)
    const target = flat.find((n) => n.id === activeId.value)
    if (!target || target.parentId == null) return
    const ancestors = []
    let cur = target
    const byId = new Map(flat.map((n) => [n.id, n]))
    while (cur && cur.parentId != null) {
      const parent = byId.get(cur.parentId)
      if (!parent) break
      ancestors.push(parent.id)
      cur = parent
    }
    if (ancestors.length > 0) {
      const next = new Set(expanded.value)
      ancestors.forEach((id) => next.add(id))
      expanded.value = next
    }
  },
  { immediate: true }
)
</script>

<style scoped>
.sidebar-cats-section {
  padding: 12px 14px 4px;
}

.cats-nav-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 4px 8px;
}

.header-left {
  display: flex;
  align-items: center;
  gap: 6px;
}

.header-icon {
  color: var(--text-tertiary);
}

.cats-title {
  font-size: 11.5px;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.6px;
  color: var(--text-tertiary);
}

.cats-count-badge {
  font-size: 10px;
  background: var(--surface-3);
  color: var(--text-tertiary);
  padding: 1px 6px;
  border-radius: 999px;
  font-weight: 600;
}

.cats-manage-link {
  color: var(--text-tertiary);
  display: inline-flex;
  align-items: center;
  padding: 2px 4px;
  border-radius: 4px;
  transition: all 0.15s ease;
}

.cats-manage-link:hover {
  color: var(--color-primary);
  background: var(--surface-2);
}

.cats-scroll-container {
  display: flex;
  flex-direction: column;
  gap: 3px;
}

.cats-empty {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 6px 8px;
  font-size: 12px;
  color: var(--text-tertiary);
}

.empty-add-link {
  color: var(--color-primary);
  text-decoration: none;
  font-size: 12px;
}

.empty-add-link:hover {
  text-decoration: underline;
}
</style>
