<template>
  <van-popup
    :show="show"
    position="bottom"
    round
    class="cat-modal-popup"
    :style="{ maxHeight: '80vh' }"
    @update:show="$emit('update:show', $event)"
  >
    <div class="cat-modal">
      <!-- 头部 -->
      <div class="modal-header">
        <div class="header-main">
          <div class="header-icon">📁</div>
          <div>
            <h3 class="modal-title">选择笔记分类</h3>
            <p class="modal-sub">将笔记归入对应分类，便于整理与检索</p>
          </div>
        </div>
        <button class="close-btn" @click="$emit('update:show', false)">
          <van-icon name="cross" size="18" />
        </button>
      </div>

      <!-- 搜索与快速创建栏 -->
      <div class="search-create-bar">
        <div class="search-input-wrap">
          <van-icon name="search" size="16" class="search-icon" />
          <input
            v-model="searchQuery"
            type="text"
            placeholder="搜索分类或输入新分类名称..."
            maxlength="30"
            class="filter-input"
            @keydown.enter.prevent="handleQuickCreate"
          />
          <button
            v-if="searchQuery"
            class="clear-input-btn"
            @click="searchQuery = ''"
          >
            <van-icon name="clear" size="14" />
          </button>
        </div>

        <button
          v-if="canQuickCreate"
          class="quick-create-btn"
          :disabled="creating"
          @click="handleQuickCreate"
        >
          <van-icon name="plus" size="13" />
          <span>创建「{{ trimmedQuery }}」</span>
        </button>
      </div>

      <!-- 分类选项网格 -->
      <div class="categories-content">
        <div class="category-grid">
          <!-- 无分类选项 -->
          <div
            class="cat-option-card"
            :class="{ active: modelValue === null || modelValue === undefined }"
            @click="selectCategory(null)"
          >
            <div class="cat-card-top">
              <span class="cat-icon">📂</span>
              <div v-if="modelValue === null || modelValue === undefined" class="check-badge">
                <van-icon name="success" size="12" />
              </div>
            </div>
            <div class="cat-card-info">
              <div class="cat-name">无分类</div>
              <div class="cat-count">默认未分类</div>
            </div>
          </div>

          <!-- 各分类选项 -->
          <div
            v-for="c in filteredCategories"
            :key="c.id"
            class="cat-option-card"
            :class="{ active: modelValue === c.id }"
            @click="selectCategory(c.id)"
          >
            <div class="cat-card-top">
              <span class="cat-icon">📁</span>
              <div v-if="modelValue === c.id" class="check-badge">
                <van-icon name="success" size="12" />
              </div>
            </div>
            <div class="cat-card-info">
              <div class="cat-name" :title="c.name">{{ c.name }}</div>
              <div class="cat-count">{{ c.noteCount || 0 }} 篇笔记</div>
            </div>
          </div>
        </div>

        <!-- 搜索无结果且不可创建时的空状态 -->
        <div v-if="filteredCategories.length === 0 && !canQuickCreate" class="empty-results">
          <p>未找到匹配的分类</p>
        </div>
      </div>

      <!-- 底部操作 -->
      <div class="modal-footer">
        <span class="footer-tip">
          当前选中：<b>{{ currentCategoryName }}</b>
        </span>
        <van-button round type="primary" size="small" @click="$emit('update:show', false)">
          确定
        </van-button>
      </div>
    </div>
  </van-popup>
</template>

<script setup>
import { ref, computed } from 'vue'
import { showToast } from 'vant'
import http from '../api/http'

const props = defineProps({
  show: {
    type: Boolean,
    default: false
  },
  modelValue: {
    type: [Number, String, null],
    default: null
  },
  categories: {
    type: Array,
    default: () => []
  }
})

const emit = defineEmits(['update:show', 'update:modelValue', 'category-created'])

const searchQuery = ref('')
const creating = ref(false)

const trimmedQuery = computed(() => searchQuery.value.trim())

const filteredCategories = computed(() => {
  if (!trimmedQuery.value) return props.categories
  const q = trimmedQuery.value.toLowerCase()
  return props.categories.filter((c) => c.name.toLowerCase().includes(q))
})

const exactMatchExists = computed(() => {
  if (!trimmedQuery.value) return false
  const q = trimmedQuery.value.toLowerCase()
  return props.categories.some((c) => c.name.toLowerCase() === q)
})

const canQuickCreate = computed(() => {
  return trimmedQuery.value.length > 0 && !exactMatchExists.value
})

const currentCategoryName = computed(() => {
  if (props.modelValue === null || props.modelValue === undefined) return '无分类'
  const found = props.categories.find((c) => c.id === props.modelValue)
  return found ? found.name : '无分类'
})

function selectCategory(id) {
  emit('update:modelValue', id)
}

async function handleQuickCreate() {
  const name = trimmedQuery.value
  if (!name) return

  // 如果已存在则直接选中
  const exist = props.categories.find((c) => c.name.toLowerCase() === name.toLowerCase())
  if (exist) {
    emit('update:modelValue', exist.id)
    searchQuery.value = ''
    showToast(`已选择「${exist.name}」`)
    return
  }

  creating.value = true
  try {
    const res = await http.post('/categories', { name })
    const record = res && res.id ? res : { id: res?.id, name, noteCount: 0 }
    
    // 如果返回数据无 id，重新拉取列表以兜底
    if (!record.id) {
      const fresh = await http.get('/categories')
      const just = fresh.find((c) => c.name === name)
      if (just) {
        record.id = just.id
        record.noteCount = just.noteCount ?? 0
      }
    }

    emit('category-created', record)
    if (record.id) {
      emit('update:modelValue', record.id)
    }
    searchQuery.value = ''
    showToast(`已创建并应用「${name}」`)
  } catch (err) {
    showToast('创建分类失败：' + (err.message || ''))
  } finally {
    creating.value = false
  }
}
</script>

<style scoped>
.cat-modal {
  display: flex;
  flex-direction: column;
  background: var(--surface);
  color: var(--text-primary);
  border-radius: 16px 16px 0 0;
  max-height: 80vh;
}

.modal-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 18px 20px 14px;
  border-bottom: 1px solid var(--border);
}

.header-main {
  display: flex;
  align-items: center;
  gap: 12px;
}

.header-icon {
  font-size: 26px;
  line-height: 1;
}

.modal-title {
  font-size: 16px;
  font-weight: 700;
  color: var(--text-primary);
  margin: 0 0 3px;
}

.modal-sub {
  font-size: 12px;
  color: var(--text-tertiary);
  margin: 0;
}

.close-btn {
  background: var(--surface-2);
  border: none;
  border-radius: 50%;
  width: 32px;
  height: 32px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--text-secondary);
  cursor: pointer;
  transition: all 0.15s ease;
}

.close-btn:hover {
  background: var(--surface-3);
  color: var(--text-primary);
}

.search-create-bar {
  padding: 12px 20px;
  background: var(--surface-2);
  border-bottom: 1px solid var(--border);
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.search-input-wrap {
  display: flex;
  align-items: center;
  gap: 8px;
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 10px;
  padding: 8px 12px;
}

.search-icon {
  color: var(--text-tertiary);
  flex-shrink: 0;
}

.filter-input {
  flex: 1;
  border: none;
  background: transparent;
  outline: none;
  font-size: 14px;
  color: var(--text-primary);
}

.filter-input::placeholder {
  color: var(--text-tertiary);
}

.clear-input-btn {
  background: transparent;
  border: none;
  color: var(--text-tertiary);
  cursor: pointer;
  padding: 2px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.quick-create-btn {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 6px 12px;
  background: rgba(59, 130, 246, 0.1);
  color: var(--color-primary);
  border: 1px dashed var(--color-primary);
  border-radius: 8px;
  font-size: 13px;
  font-weight: 600;
  cursor: pointer;
  align-self: flex-start;
  transition: all 0.15s ease;
}

.quick-create-btn:hover {
  background: rgba(59, 130, 246, 0.18);
}

.categories-content {
  padding: 16px 20px;
  overflow-y: auto;
  min-height: 180px;
  max-height: 48vh;
}

.category-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(135px, 1fr));
  gap: 12px;
}

.cat-option-card {
  background: var(--surface-2);
  border: 1.5px solid var(--border);
  border-radius: 12px;
  padding: 12px 14px;
  cursor: pointer;
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  min-height: 80px;
  transition: all 0.18s cubic-bezier(0.4, 0, 0.2, 1);
  position: relative;
}

.cat-option-card:hover {
  transform: translateY(-2px);
  border-color: var(--color-primary);
  box-shadow: var(--shadow-xs);
}

.cat-option-card.active {
  background: rgba(59, 130, 246, 0.08);
  border-color: var(--color-primary);
}

:global(body.dark) .cat-option-card.active {
  background: rgba(56, 189, 248, 0.12);
}

.cat-card-top {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 6px;
}

.cat-icon {
  font-size: 20px;
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
}

.cat-name {
  font-size: 14px;
  font-weight: 600;
  color: var(--text-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  margin-bottom: 2px;
}

.cat-count {
  font-size: 11.5px;
  color: var(--text-tertiary);
}

.empty-results {
  text-align: center;
  padding: 36px 0;
  color: var(--text-tertiary);
  font-size: 13.5px;
}

.modal-footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 14px 20px;
  border-top: 1px solid var(--border);
  background: var(--surface);
}

.footer-tip {
  font-size: 13px;
  color: var(--text-secondary);
}

.footer-tip b {
  color: var(--color-primary);
}

@media (min-width: 1024px) {
  :deep(.cat-modal-popup) {
    max-width: 540px;
    margin: 0 auto;
    left: 50%;
    transform: translateX(-50%);
    border-radius: 16px 16px 0 0;
  }
}
</style>
