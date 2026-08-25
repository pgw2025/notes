<template>
  <nav class="cat-nav">
    <div class="cat-title">分类</div>
    <div
      class="cat-item"
      :class="{ active: modelValue === null }"
      @click="$emit('update:modelValue', null)"
    >
      <span class="cat-name">全部</span>
    </div>
    <div
      v-for="c in categories"
      :key="c.id"
      class="cat-item"
      :class="{ active: modelValue === c.id, 'is-empty': c.noteCount === 0 }"
      @click="$emit('update:modelValue', c.id)"
    >
      <span class="cat-name">{{ c.name }}</span>
      <span class="cat-count">{{ c.noteCount }}</span>
    </div>
  </nav>
</template>

<script setup>
defineProps({
  categories: { type: Array, required: true },
  // null = 全部
  modelValue: { type: [Number, null], default: null }
})
defineEmits(['update:modelValue'])
</script>

<style scoped>
.cat-title {
  font-size: 12px;
  color: var(--text-tertiary, #999);
  padding: 8px 12px;
}
.cat-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 10px 12px;
  border-radius: 8px;
  cursor: pointer;
  font-size: 14px;
  color: var(--text-primary, #323233);
}
.cat-item:hover {
  background: var(--van-gray-2, #f5f5f5);
}
.cat-item.active {
  background: var(--van-primary-color-light, #e8f3ff);
  color: var(--van-primary-color, #1989fa);
  font-weight: 600;
}
.cat-item.active .cat-count {
  color: var(--van-primary-color, #1989fa);
  background: rgba(25, 137, 250, 0.12);
}
/* 空分类淡显 */
.cat-item:not(.active).is-empty .cat-name {
  opacity: 0.55;
}
.cat-name {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.cat-count {
  font-size: 12px;
  color: var(--text-tertiary, #999);
  background: var(--van-gray-2, #f5f5f5);
  border-radius: 999px;
  padding: 0 8px;
  min-width: 24px;
  text-align: center;
  line-height: 18px;
}
/* 暗色模式适配 */
:global(body.dark) .cat-item:hover {
  background: rgba(255, 255, 255, 0.08);
}
:global(body.dark) .cat-item.active {
  background: var(--van-primary-color-light, rgba(25, 137, 250, 0.25));
}
:global(body.dark) .cat-count {
  background: rgba(255, 255, 255, 0.1);
}
:global(body.dark) .cat-item.active .cat-count {
  background: rgba(96, 84, 241, 0.3);
}
</style>