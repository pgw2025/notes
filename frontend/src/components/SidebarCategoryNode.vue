<template>
  <div class="scat-node">
    <div
      class="scat-row"
      :class="{ active: activeId === node.id, 'has-children': hasChildren }"
      :style="{ paddingLeft: (10 + depth * 14) + 'px' }"
      @click="onRowClick"
    >
      <!-- 展开/折叠箭头（有子分类时显示） -->
      <span
        v-if="hasChildren"
        class="scat-chevron"
        :class="{ expanded: isExpanded }"
        @click.stop="emit('toggle', node.id)"
        :title="isExpanded ? '折叠子分类' : '展开子分类'"
      >
        <van-icon name="arrow" size="11" />
      </span>
      <span v-else class="scat-chevron-placeholder"></span>

      <span class="scat-folder">{{ isExpanded ? '📂' : '📁' }}</span>
      <span class="scat-name" :title="node.name">{{ node.name }}</span>
      <span class="scat-count">{{ node.noteCount || 0 }}</span>
    </div>

    <!-- 子分类（展开时递归渲染） -->
    <template v-if="isExpanded && hasChildren">
      <SidebarCategoryNode
        v-for="child in node.children"
        :key="child.id"
        :node="child"
        :depth="depth + 1"
        :expanded="expanded"
        :active-id="activeId"
        @toggle="(id) => emit('toggle', id)"
        @select="(n) => emit('select', n)"
      />
    </template>
  </div>
</template>

<script setup>
import { computed } from 'vue'

const props = defineProps({
  node: { type: Object, required: true },
  depth: { type: Number, default: 0 },
  expanded: { type: Set, required: true },
  activeId: { type: [Number, String], default: null }
})

const emit = defineEmits(['toggle', 'select'])

const hasChildren = computed(() => props.node.children && props.node.children.length > 0)
const isExpanded = computed(() => props.expanded.has(props.node.id))

// 点击行：有子分类则同时切换展开态并筛选（含子孙分类笔记）；无子分类则仅跳转筛选
function onRowClick() {
  if (hasChildren.value) {
    emit('toggle', props.node.id)
  }
  emit('select', props.node)
}
</script>

<style scoped>
.scat-row {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 6px 10px;
  border-radius: 8px;
  color: var(--text-secondary);
  font-size: 13px;
  font-weight: 500;
  text-decoration: none;
  transition: all 0.15s ease;
  cursor: pointer;
  user-select: none;
}

.scat-row:hover {
  background: var(--surface-2);
  color: var(--text-primary);
}

.scat-row.active {
  background: rgba(59, 130, 246, 0.12);
  color: var(--color-primary);
  font-weight: 600;
}

:global(body.dark) .scat-row.active {
  background: rgba(56, 189, 248, 0.16);
  color: var(--color-primary);
}

.scat-chevron {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 14px;
  height: 14px;
  color: var(--text-tertiary);
  transition: transform 0.18s ease;
  flex-shrink: 0;
  border-radius: 3px;
}

.scat-chevron:hover {
  background: var(--surface-3);
  color: var(--text-primary);
}

.scat-chevron.expanded {
  transform: rotate(90deg);
}

.scat-chevron-placeholder {
  width: 14px;
  height: 14px;
  flex-shrink: 0;
}

.scat-folder {
  font-size: 13px;
  opacity: 0.8;
  flex-shrink: 0;
  line-height: 1;
}

.scat-name {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.scat-count {
  font-size: 11px;
  color: var(--text-tertiary);
  background: var(--surface-3);
  border-radius: 999px;
  padding: 0 6px;
  line-height: 16px;
  flex-shrink: 0;
}

.scat-row.active .scat-count {
  background: rgba(59, 130, 246, 0.2);
  color: var(--color-primary);
  font-weight: 600;
}
</style>
