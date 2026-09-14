<template>
  <div class="cat-select-node">
    <div
      class="cat-option-row"
      :class="{ active: modelValue === node.id }"
      :style="{ paddingLeft: 12 + depth * 18 + 'px' }"
      @click="select(node.id)"
    >
      <!-- 折叠/展开箭头 -->
      <span
        v-if="hasChildren"
        class="cat-select-chevron"
        :class="{ expanded: isExpanded }"
        @click.stop="isExpanded = !isExpanded"
      >
        <van-icon name="arrow" size="11" />
      </span>
      <span v-else class="cat-select-chevron-placeholder"></span>

      <span class="cat-icon">📁</span>
      <span class="cat-row-name">{{ node.name }}</span>
      <span class="cat-row-count">{{ node.noteCount || 0 }} 篇</span>
      <div v-if="modelValue === node.id" class="check-badge">
        <van-icon name="success" size="12" />
      </div>
    </div>

    <template v-if="isExpanded && hasChildren">
      <CategorySelectNode
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
  modelValue: { type: [Number, String, null], default: null }
})

const emit = defineEmits(['select'])

const isExpanded = ref(false)
const hasChildren = computed(() => props.node.children && props.node.children.length > 0)

function select(id) {
  emit('select', id)
}
</script>

<style scoped>
/* 本组件为递归渲染的选择项，需自包含布局样式（父组件 scoped 样式无法穿透到这里） */

.cat-option-row {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 12px;
  background: var(--surface-2);
  border: 1.5px solid var(--border);
  border-radius: 10px;
  cursor: pointer;
  transition: all 0.18s cubic-bezier(0.4, 0, 0.2, 1);
  position: relative;
  margin-bottom: 6px;
}

.cat-option-row:hover {
  border-color: var(--color-primary);
  box-shadow: var(--shadow-xs);
}

.cat-option-row.active {
  background: rgba(59, 130, 246, 0.08);
  border-color: var(--color-primary);
}

:global(body.dark) .cat-option-row.active {
  background: rgba(56, 189, 248, 0.12);
}

.cat-select-chevron {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 14px;
  height: 14px;
  color: var(--text-tertiary);
  transition: transform 0.18s ease;
  flex-shrink: 0;
}

.cat-select-chevron.expanded {
  transform: rotate(90deg);
}

.cat-select-chevron-placeholder {
  width: 14px;
  height: 14px;
  flex-shrink: 0;
}

.cat-icon {
  font-size: 18px;
  flex-shrink: 0;
}

.cat-row-name {
  flex: 1;
  font-size: 14px;
  font-weight: 500;
  color: var(--text-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.cat-row-count {
  font-size: 11.5px;
  color: var(--text-tertiary);
  flex-shrink: 0;
}

.check-badge {
  width: 18px;
  height: 18px;
  border-radius: 50%;
  background: var(--color-primary);
  color: #fff;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}
</style>
