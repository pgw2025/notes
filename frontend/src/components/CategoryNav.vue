<template>
  <nav class="cat-nav">
    <div class="cat-header">
      <span class="cat-title">分类筛选</span>
      <router-link to="/categories" class="cat-manage-link" title="管理分类">
        <van-icon name="setting-o" size="14" />
      </router-link>
    </div>

    <div
      class="cat-item"
      :class="{ active: modelValue === null && tagModelValue === null }"
      @click="selectCategory(null)"
    >
      <div class="cat-icon-wrap">
        <van-icon name="apps-o" size="15" />
      </div>
      <span class="cat-name">全部笔记</span>
    </div>

    <div
      v-for="c in categories"
      :key="c.id"
      class="cat-item"
      :class="{ active: modelValue === c.id, 'is-empty': c.noteCount === 0 }"
      @click="selectCategory(c.id)"
    >
      <div class="cat-icon-wrap">
        <span class="cat-bullet"></span>
      </div>
      <span class="cat-name">{{ c.name }}</span>
      <span class="cat-count">{{ c.noteCount }}</span>
    </div>

    <!-- 侧栏标签展示与筛选区 -->
    <div v-if="tags && tags.length > 0" class="tags-divider-wrap">
      <div class="tags-sub-header">
        <span class="cat-title">标签筛选</span>
        <router-link to="/tags" class="cat-manage-link" title="管理标签">
          <van-icon name="setting-o" size="14" />
        </router-link>
      </div>

      <div class="tags-sidebar-list">
        <div
          v-for="t in tags"
          :key="t.id"
          class="cat-item tag-sidebar-item"
          :class="{ active: tagModelValue === t.id }"
          @click="selectTag(t.id)"
        >
          <div class="cat-icon-wrap tag-hash-wrap">
            <span class="tag-hash-icon">#</span>
          </div>
          <span class="cat-name">{{ t.name }}</span>
          <span class="cat-count">{{ t.noteCount || 0 }}</span>
        </div>
      </div>
    </div>
  </nav>
</template>

<script setup>
const props = defineProps({
  categories: { type: Array, required: true },
  tags: { type: Array, default: () => [] },
  // null = 全部
  modelValue: { type: [Number, null], default: null },
  tagModelValue: { type: [Number, null], default: null }
})

const emit = defineEmits(['update:modelValue', 'update:tagModelValue'])

function selectCategory(id) {
  emit('update:tagModelValue', null)
  emit('update:modelValue', id)
}

function selectTag(id) {
  emit('update:modelValue', null)
  emit('update:tagModelValue', id)
}
</script>

<style scoped>
.cat-nav {
  padding: 12px 10px;
  background: var(--surface);
  border-radius: 12px;
  border: 1px solid var(--border);
  box-shadow: var(--shadow-xs);
}

.cat-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 4px 8px 10px;
  border-bottom: 1px solid var(--divider);
  margin-bottom: 6px;
}

.tags-divider-wrap {
  margin-top: 14px;
  padding-top: 12px;
  border-top: 1px solid var(--divider);
}

.tags-sub-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 8px 8px;
}

.cat-title {
  font-size: 11.5px;
  font-weight: 600;
  color: var(--text-tertiary);
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.cat-manage-link {
  color: var(--text-tertiary);
  display: flex;
  align-items: center;
  padding: 2px 4px;
  border-radius: 4px;
  transition: all 0.15s ease;
}

.cat-manage-link:hover {
  color: var(--color-primary);
  background: var(--surface-2);
}

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

.cat-icon-wrap {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 18px;
  height: 18px;
  color: var(--text-tertiary);
}

.tag-hash-wrap {
  color: var(--color-primary);
}

.tag-hash-icon {
  font-weight: 700;
  font-size: 13px;
}

.cat-bullet {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: var(--text-tertiary);
  opacity: 0.6;
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

.cat-item.active .cat-bullet {
  background: var(--color-primary);
  opacity: 1;
  transform: scale(1.3);
}

.cat-item.active .cat-icon-wrap {
  color: var(--color-primary);
}

.cat-item.active .cat-count {
  color: var(--color-primary);
  background: rgba(59, 130, 246, 0.18);
  font-weight: 600;
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
}

/* 空分类淡显 */
.cat-item:not(.active).is-empty .cat-name {
  opacity: 0.65;
}
</style>
