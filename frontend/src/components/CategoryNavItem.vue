<template>
  <div class="cat-nav-item">
    <div
      class="cat-item"
      :class="{ active: modelValue === node.id, 'is-empty': node.noteCount === 0 }"
      :style="{ paddingLeft: 10 + depth * 16 + 'px' }"
      @click="select(node.id)"
    >
      <!-- 折叠/展开箭头 -->
      <span
        v-if="hasChildren"
        class="cat-chevron"
        :class="{ expanded: isExpanded }"
        @click.stop="isExpanded = !isExpanded"
      >
        <van-icon name="arrow" size="11" />
      </span>
      <span v-else class="cat-chevron-placeholder"></span>

      <span class="cat-name">{{ node.name }}</span>
      <span class="cat-count">{{ node.noteCount }}</span>
    </div>

    <template v-if="isExpanded && hasChildren">
      <CategoryNavItem
        v-for="child in node.children"
        :key="child.id"
        :node="child"
        :depth="depth + 1"
        :model-value="modelValue"
        @select="(id) => emit('select', id)"
      />
    </template>
  </div>
</template>

<script setup>
import { ref, computed } from 'vue'

const props = defineProps({
  node: { type: Object, required: true },
  depth: { type: Number, default: 0 },
  modelValue: { type: [Number, null], default: null }
})

const emit = defineEmits(['select'])

const isExpanded = ref(false)
const hasChildren = computed(() => props.node.children && props.node.children.length > 0)

function select(id) {
  emit('select', id)
}
</script>

<style scoped>
/* 本组件为递归渲染的分类行，需自包含布局样式（父组件 scoped 样式无法穿透到这里） */

.cat-item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 8px 10px;
  border-radius: 8px;
  cursor: pointer;
  font-size: 13.5px;
  color: var(--text-secondary);
  transition: all 0.15s ease;
  margin-bottom: 2px;
}

.cat-item:hover {
  background: var(--surface-2);
  color: var(--text-primary);
}

.cat-item.active {
  background: rgba(59, 130, 246, 0.12);
  color: var(--color-primary);
  font-weight: 600;
}

:global(body.dark) .cat-item.active {
  background: rgba(56, 189, 248, 0.16);
  color: var(--color-primary);
}

.cat-item.active .cat-count {
  color: var(--color-primary);
  background: rgba(59, 130, 246, 0.18);
  font-weight: 600;
}

.cat-item:not(.active).is-empty .cat-name {
  opacity: 0.65;
}

.cat-chevron {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 14px;
  height: 14px;
  color: var(--text-tertiary);
  transition: transform 0.18s ease;
  flex-shrink: 0;
}

.cat-chevron.expanded {
  transform: rotate(90deg);
}

.cat-chevron-placeholder {
  width: 14px;
  height: 14px;
  flex-shrink: 0;
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
  background: var(--surface-2);
  border-radius: 999px;
  padding: 0 7px;
  min-width: 20px;
  text-align: center;
  line-height: 18px;
  font-weight: 500;
  flex-shrink: 0;
}
</style>
