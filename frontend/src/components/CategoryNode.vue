<template>
  <div class="cat-node">
    <div
      class="cat-node-row"
      :class="{
        'is-active': isDesktop && activeId === node.id,
        'is-desktop': isDesktop
      }"
      :style="{ paddingLeft: (depth * (isDesktop ? 16 : 20) + 12) + 'px' }"
      @click="onRowClick"
    >
      <!-- 展开/折叠箭头（有子分类时显示） -->
      <span
        v-if="hasChildren"
        class="cat-node-toggle"
        :class="{ expanded: isExpanded }"
        @click.stop="emit('toggle', node.id)"
        title="折叠/展开"
      >
        <van-icon name="arrow" size="12" />
      </span>
      <span v-else class="cat-node-toggle-placeholder"></span>

      <span class="cat-node-icon">{{ isDesktop && activeId === node.id ? '📂' : '📁' }}</span>
      <span class="cat-node-name" :title="node.name">{{ node.name }}</span>
      <span class="cat-node-count">{{ node.noteCount }}</span>

      <div class="cat-node-actions" @click.stop>
        <button class="cat-btn" title="新建子分类" @click="emit('add-child', node)">
          <van-icon name="plus" size="13" />
        </button>
        <button class="cat-btn" title="编辑分类" @click="emit('edit', node)">
          <van-icon name="edit" size="13" />
        </button>
        <button class="cat-btn btn-del" title="删除分类" @click="emit('delete', node)">
          <van-icon name="delete-o" size="13" />
        </button>
      </div>
    </div>

    <!-- 子分类（展开时递归渲染） -->
    <template v-if="isExpanded && hasChildren">
      <CategoryNode
        v-for="child in node.children"
        :key="child.id"
        :node="child"
        :depth="depth + 1"
        :expanded="expanded"
        :active-id="activeId"
        :is-desktop="isDesktop"
        @toggle="(id) => emit('toggle', id)"
        @add-child="(n) => emit('add-child', n)"
        @edit="(n) => emit('edit', n)"
        @delete="(n) => emit('delete', n)"
        @open="(id) => emit('open', id)"
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
  activeId: { type: [Number, String], default: null },
  isDesktop: { type: Boolean, default: false }
})

const emit = defineEmits(['toggle', 'add-child', 'edit', 'delete', 'open', 'select'])

const hasChildren = computed(() => props.node.children && props.node.children.length > 0)
const isExpanded = computed(() => props.expanded.has(props.node.id))

function onRowClick() {
  if (props.isDesktop) {
    emit('select', props.node)
  } else {
    // 移动端：有子分类展开/折叠，无子分类跳转到列表
    if (hasChildren.value) {
      emit('toggle', props.node.id)
    } else {
      emit('open', props.node.id)
    }
  }
}
</script>

<style scoped>
.cat-node-row {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 9px 12px;
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.16s ease;
  font-size: 14px;
  color: var(--text-primary);
  user-select: none;
}

.cat-node-row:hover {
  background: var(--surface-2);
  border-color: var(--border-hover, var(--border));
}

.cat-node-row.is-active {
  background: rgba(15, 169, 140, 0.08);
  border-color: var(--color-primary);
  color: var(--color-primary);
  font-weight: 600;
}

.cat-node-toggle {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 18px;
  height: 18px;
  color: var(--text-tertiary);
  transition: transform 0.2s ease;
  flex-shrink: 0;
  border-radius: 4px;
}
.cat-node-toggle:hover {
  background: var(--surface-3);
  color: var(--text-primary);
}

.cat-node-toggle.expanded {
  transform: rotate(90deg);
}

.cat-node-toggle-placeholder {
  width: 18px;
  height: 18px;
  flex-shrink: 0;
}

.cat-node-icon {
  font-size: 15px;
  flex-shrink: 0;
  line-height: 1;
}

.cat-node-name {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.cat-node-count {
  font-size: 11px;
  color: var(--text-tertiary);
  background: var(--surface-2);
  border-radius: 999px;
  padding: 0 7px;
  line-height: 18px;
  height: 18px;
  flex-shrink: 0;
  font-weight: 500;
}

.cat-node-row.is-active .cat-node-count {
  background: rgba(15, 169, 140, 0.16);
  color: var(--color-primary);
}

.cat-node-actions {
  display: flex;
  gap: 3px;
  opacity: 0;
  transition: opacity 0.15s ease;
}

.cat-node-row:hover .cat-node-actions {
  opacity: 1;
}

/* 移动端始终显示操作按钮（无 hover） */
@media (max-width: 1023px) {
  .cat-node-actions {
    opacity: 1;
  }
}

.cat-btn {
  width: 24px;
  height: 24px;
  border-radius: 5px;
  border: none;
  background: var(--surface-2);
  color: var(--text-secondary);
  display: inline-flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  transition: all 0.15s ease;
}

.cat-btn:hover {
  background: var(--surface-3);
  color: var(--text-primary);
}

.cat-btn.btn-del:hover {
  background: rgba(239, 68, 68, 0.15);
  color: var(--color-danger);
}
</style>
