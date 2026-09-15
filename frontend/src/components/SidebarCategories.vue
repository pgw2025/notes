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

    <!-- 分类树（导航式：点击跳转 /notes?categoryId=x） -->
    <div v-if="categories.length > 0" class="cats-scroll-container">
      <router-link
        v-for="c in categories"
        :key="c.id"
        :to="{ path: '/notes', query: { categoryId: c.id } }"
        class="sidebar-cat-item"
        :class="{ active: isCatActive(c.id) }"
        :title="`${c.name} (${c.noteCount || 0} 篇笔记)`"
      >
        <span class="cat-folder-icon">📁</span>
        <span class="cat-name">{{ c.name }}</span>
        <span class="cat-count">{{ c.noteCount || 0 }}</span>
      </router-link>
    </div>

    <!-- 空状态 -->
    <div v-else class="cats-empty">
      <span>暂无分类</span>
      <router-link to="/categories" class="empty-add-link">去创建</router-link>
    </div>
  </div>
</template>

<script setup>
import { computed } from 'vue'
import { useRoute } from 'vue-router'

const props = defineProps({
  categories: {
    type: Array,
    default: () => []
  }
})

const route = useRoute()

function isCatActive(catId) {
  if (route.path !== '/notes') return false
  return Number(route.query.categoryId) === catId
}
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

.sidebar-cat-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px 10px;
  border-radius: 8px;
  color: var(--text-secondary);
  font-size: 13px;
  font-weight: 500;
  text-decoration: none;
  transition: all 0.15s ease;
  user-select: none;
}

.sidebar-cat-item:hover {
  background: var(--surface-2);
  color: var(--text-primary);
}

.sidebar-cat-item.active {
  background: rgba(59, 130, 246, 0.12);
  color: var(--color-primary);
  font-weight: 600;
}

:global(body.dark) .sidebar-cat-item.active {
  background: rgba(56, 189, 248, 0.16);
  color: var(--color-primary);
}

.cat-folder-icon {
  font-size: 13px;
  opacity: 0.8;
}

.cat-name {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.cat-count {
  font-size: 11px;
  color: var(--text-tertiary);
  background: var(--surface-3);
  border-radius: 999px;
  padding: 0 6px;
  line-height: 16px;
}

.sidebar-cat-item.active .cat-count {
  background: rgba(59, 130, 246, 0.2);
  color: var(--color-primary);
  font-weight: 600;
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
