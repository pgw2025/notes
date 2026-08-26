<template>
  <div class="sidebar-tags-section">
    <div class="tags-nav-header">
      <div class="header-left">
        <van-icon name="label-o" size="14" class="header-icon" />
        <span class="tags-title">标签</span>
        <span v-if="tags.length" class="tags-count-badge">{{ tags.length }}</span>
      </div>
      <router-link to="/tags" class="tags-manage-link" title="管理所有标签">
        <van-icon name="setting-o" size="14" />
      </router-link>
    </div>

    <!-- 标签列表 -->
    <div v-if="tags.length > 0" class="tags-scroll-container">
      <router-link
        v-for="tag in displayedTags"
        :key="tag.id"
        :to="{ path: '/notes', query: { tagId: tag.id } }"
        class="sidebar-tag-item"
        :class="{ active: isTagActive(tag.id) }"
        :title="`#${tag.name} (${tag.noteCount || 0} 篇笔记)`"
      >
        <span class="tag-hash">#</span>
        <span class="tag-name">{{ tag.name }}</span>
        <span class="tag-count">{{ tag.noteCount || 0 }}</span>
      </router-link>

      <!-- 展开 / 折叠更多按钮 -->
      <button
        v-if="tags.length > collapseThreshold"
        type="button"
        class="tag-expand-btn"
        @click="isExpanded = !isExpanded"
      >
        <span>{{ isExpanded ? '收起部分标签' : `展开其余 ${tags.length - collapseThreshold} 个标签` }}</span>
        <van-icon :name="isExpanded ? 'arrow-up' : 'arrow-down'" size="12" />
      </button>
    </div>

    <!-- 空状态 -->
    <div v-else class="tags-empty">
      <span>暂无标签</span>
      <router-link to="/tags" class="empty-add-link">去创建</router-link>
    </div>
  </div>
</template>

<script setup>
import { ref, computed } from 'vue'
import { useRoute } from 'vue-router'

const props = defineProps({
  tags: {
    type: Array,
    default: () => []
  },
  collapseThreshold: {
    type: Number,
    default: 8
  }
})

const route = useRoute()
const isExpanded = ref(false)

const displayedTags = computed(() => {
  if (isExpanded.value || props.tags.length <= props.collapseThreshold) {
    return props.tags
  }
  return props.tags.slice(0, props.collapseThreshold)
})

function isTagActive(tagId) {
  if (route.path !== '/notes') return false
  return Number(route.query.tagId) === tagId
}
</script>

<style scoped>
.sidebar-tags-section {
  padding: 12px 14px 4px;
}

.tags-nav-header {
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

.tags-title {
  font-size: 11.5px;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.6px;
  color: var(--text-tertiary);
}

.tags-count-badge {
  font-size: 10px;
  background: var(--surface-3);
  color: var(--text-tertiary);
  padding: 1px 6px;
  border-radius: 999px;
  font-weight: 600;
}

.tags-manage-link {
  color: var(--text-tertiary);
  display: inline-flex;
  align-items: center;
  padding: 2px 4px;
  border-radius: 4px;
  transition: all 0.15s ease;
}

.tags-manage-link:hover {
  color: var(--color-primary);
  background: var(--surface-2);
}

.tags-scroll-container {
  display: flex;
  flex-direction: column;
  gap: 3px;
}

.sidebar-tag-item {
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

.sidebar-tag-item:hover {
  background: var(--surface-2);
  color: var(--text-primary);
}

.sidebar-tag-item.active {
  background: rgba(59, 130, 246, 0.12);
  color: var(--color-primary);
  font-weight: 600;
}

:global(body.dark) .sidebar-tag-item.active {
  background: rgba(56, 189, 248, 0.16);
  color: var(--color-primary);
}

.tag-hash {
  color: var(--color-primary);
  font-weight: 700;
  font-size: 13px;
  opacity: 0.85;
}

.tag-name {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.tag-count {
  font-size: 11px;
  color: var(--text-tertiary);
  background: var(--surface-3);
  border-radius: 999px;
  padding: 0 6px;
  line-height: 16px;
}

.sidebar-tag-item.active .tag-count {
  background: rgba(59, 130, 246, 0.2);
  color: var(--color-primary);
  font-weight: 600;
}

.tag-expand-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 4px;
  background: transparent;
  border: none;
  color: var(--text-tertiary);
  font-size: 11.5px;
  padding: 6px 8px;
  margin-top: 2px;
  border-radius: 6px;
  cursor: pointer;
  width: 100%;
  transition: all 0.15s ease;
}

.tag-expand-btn:hover {
  background: var(--surface-2);
  color: var(--color-primary);
}

.tags-empty {
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
