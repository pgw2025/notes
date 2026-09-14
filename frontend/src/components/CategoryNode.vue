<template>
  <div class="cat-node">
    <div
      class="cat-node-row"
      :style="{ paddingLeft: depth * 24 + 'px' }"
      @click="onOpen"
    >
      <!-- 展开/折叠箭头（有子分类时显示） -->
      <span
        v-if="hasChildren"
        class="cat-node-toggle"
        :class="{ expanded: isExpanded }"
        @click.stop="emit('toggle', node.id)"
      >
        <van-icon name="arrow" size="12" />
      </span>
      <span v-else class="cat-node-toggle-placeholder"></span>

      <span class="cat-node-icon">📁</span>
      <span class="cat-node-name">{{ node.name }}</span>
      <span class="cat-node-count">{{ node.noteCount }} 篇</span>

      <div class="cat-node-actions" @click.stop>
        <button class="cat-btn" title="新建子分类" @click="emit('add-child', node)">
          <van-icon name="plus" size="14" />
        </button>
        <button class="cat-btn" title="编辑" @click="emit('edit', node)">
          <van-icon name="edit" size="14" />
        </button>
        <button class="cat-btn btn-del" title="删除" @click="emit('delete', node)">
          <van-icon name="delete-o" size="14" />
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
        @toggle="(id) => emit('toggle', id)"
        @add-child="(n) => emit('add-child', n)"
        @edit="(n) => emit('edit', n)"
        @delete="(n) => emit('delete', n)"
        @open="(id) => emit('open', id)"
      />
    </template>
  </div>
</template>

<script setup>
import { computed } from 'vue'

const props = defineProps({
  node: { type: Object, required: true },
  depth: { type: Number, default: 0 },
  expanded: { type: Set, required: true }
})

const emit = defineEmits(['toggle', 'add-child', 'edit', 'delete', 'open'])

const hasChildren = computed(() => props.node.children && props.node.children.length > 0)
const isExpanded = computed(() => props.expanded.has(props.node.id))

function onOpen() {
  // 有子分类：点击展开/折叠子分类；无子分类（叶子）：跳转到笔记列表
  if (hasChildren.value) {
    emit('toggle', props.node.id)
  } else {
    emit('open', props.node.id)
  }
}
</script>

<style scoped>
.cat-node-row {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 14px;
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 10px;
  cursor: pointer;
  transition: all 0.15s ease;
  font-size: 14px;
  color: var(--text-primary);
}

.cat-node-row:hover {
  background: var(--surface-2);
  border-color: var(--color-primary);
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
  font-size: 16px;
  flex-shrink: 0;
}

.cat-node-name {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-weight: 500;
}

.cat-node-count {
  font-size: 12px;
  color: var(--text-tertiary);
  background: var(--surface-2);
  border-radius: 999px;
  padding: 0 8px;
  line-height: 18px;
  flex-shrink: 0;
}

.cat-node-actions {
  display: flex;
  gap: 4px;
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
  width: 26px;
  height: 26px;
  border-radius: 6px;
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
