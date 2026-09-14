<template>
  <ResponsivePopover
    :show="show"
    :anchor-el="anchorEl"
    :width="390"
    :max-height="480"
    modal-class="cat-modal-popup"
    @update:show="$emit('update:show', $event)"
  >
    <div class="cat-modal" :class="{ 'is-popover': isDesktop }">
      <!-- 头部 -->
      <div class="modal-header">
        <div class="header-main">
          <div class="header-icon">📁</div>
          <div>
            <h3 class="modal-title">选择笔记分类</h3>
            <p v-if="!isDesktop" class="modal-sub">将笔记归入对应分类，便于整理与检索</p>
          </div>
        </div>
        <button class="close-btn" @click="$emit('update:show', false)" title="关闭 (Esc)">
          <van-icon name="cross" size="16" />
        </button>
      </div>

      <!-- 搜索与快速创建栏 -->
      <div class="search-create-bar">
        <div class="search-input-wrap">
          <van-icon name="search" size="16" class="search-icon" />
          <input
            ref="searchInputRef"
            v-model="searchQuery"
            type="text"
            placeholder="搜索分类或输入新名称..."
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

      <!-- 分类选项：树形（无搜索时）或扁平列表（搜索时） -->
      <div class="categories-content">
        <!-- 无分类选项 -->
        <div
          class="cat-option-row"
          :class="{ active: modelValue === null || modelValue === undefined }"
          @click="selectCategory(null)"
        >
          <span class="cat-icon">📂</span>
          <span class="cat-row-name">无分类</span>
          <span class="cat-row-count">默认未分类</span>
          <div v-if="modelValue === null || modelValue === undefined" class="check-badge">
            <van-icon name="success" size="12" />
          </div>
        </div>

        <!-- 有搜索词：扁平匹配结果 -->
        <template v-if="trimmedQuery">
          <div
            v-for="c in filteredFlatCategories"
            :key="c.id"
            class="cat-option-row"
            :class="{ active: modelValue === c.id }"
            :style="{ paddingLeft: 12 + c.depth * 16 + 'px' }"
            @click="selectCategory(c.id)"
          >
            <span class="cat-icon">📁</span>
            <span class="cat-row-name" :title="c.path">{{ c.name }}</span>
            <span class="cat-row-count">{{ c.noteCount || 0 }} 篇</span>
            <div v-if="modelValue === c.id" class="check-badge">
              <van-icon name="success" size="12" />
            </div>
          </div>
        </template>

        <!-- 无搜索词：树形展示 -->
        <template v-else>
          <CategorySelectNode
            v-for="c in props.categories"
            :key="c.id"
            :node="c"
            :depth="0"
            :model-value="modelValue"
            @select="selectCategory"
          />
        </template>

        <!-- 搜索无结果且不可创建时的空状态 -->
        <div v-if="trimmedQuery && filteredFlatCategories.length === 0 && !canQuickCreate" class="empty-results">
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
  </ResponsivePopover>
</template>

<script setup>
import { ref, computed, watch, nextTick } from 'vue'
import { showToast } from 'vant'
import http from '../api/http'
import { useResponsive } from '../composables/useResponsive'
import { flattenCategories, findCategoryById, getCategoryPath } from '../utils/categoryTree'
import ResponsivePopover from './ResponsivePopover.vue'
import CategorySelectNode from './CategorySelectNode.vue'

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
  },
  anchorEl: {
    type: [Object, null],
    default: null
  }
})

const emit = defineEmits(['update:show', 'update:modelValue', 'category-created'])

const { isDesktop } = useResponsive()
const searchQuery = ref('')
const creating = ref(false)
const searchInputRef = ref(null)

const trimmedQuery = computed(() => searchQuery.value.trim())

// 扁平化的分类列表（用于搜索匹配），附带 depth 和 path
const flatCategories = computed(() =>
  flattenCategories(props.categories).map((c) => ({
    ...c,
    path: getCategoryPath(props.categories, c.id)
  }))
)

const filteredFlatCategories = computed(() => {
  if (!trimmedQuery.value) return flatCategories.value
  const q = trimmedQuery.value.toLowerCase()
  return flatCategories.value.filter(
    (c) => c.name.toLowerCase().includes(q) || (c.path && c.path.toLowerCase().includes(q))
  )
})

const exactMatchExists = computed(() => {
  if (!trimmedQuery.value) return false
  const q = trimmedQuery.value.toLowerCase()
  return flatCategories.value.some((c) => c.name.toLowerCase() === q)
})

const canQuickCreate = computed(() => {
  return trimmedQuery.value.length > 0 && !exactMatchExists.value
})

const currentCategoryName = computed(() => {
  if (props.modelValue === null || props.modelValue === undefined) return '无分类'
  const found = findCategoryById(props.categories, props.modelValue)
  return found ? getCategoryPath(props.categories, props.modelValue) || found.name : '无分类'
})

watch(
  () => props.show,
  (newVal) => {
    if (newVal && isDesktop.value) {
      nextTick(() => {
        searchInputRef.value?.focus?.()
      })
    } else if (!newVal) {
      searchQuery.value = ''
    }
  }
)

function selectCategory(id) {
  emit('update:modelValue', id)
  if (isDesktop.value) {
    emit('update:show', false)
  }
}

async function handleQuickCreate() {
  const name = trimmedQuery.value
  if (!name) return

  // 如果已存在则直接选中
  const exist = flatCategories.value.find((c) => c.name.toLowerCase() === name.toLowerCase())
  if (exist) {
    emit('update:modelValue', exist.id)
    searchQuery.value = ''
    showToast(`已选择「${exist.name}」`)
    if (isDesktop.value) emit('update:show', false)
    return
  }

  creating.value = true
  try {
    const res = await http.post('/categories', { name })
    const record = res && res.id ? res : { id: res?.id, name, noteCount: 0 }

    // 如果返回数据无 id，重新拉取列表以兜底
    if (!record.id) {
      const fresh = await http.get('/categories')
      const just = flattenCategories(fresh).find((c) => c.name === name)
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
    if (isDesktop.value) emit('update:show', false)
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
  padding: 12px 20px;
  overflow-y: auto;
  min-height: 180px;
  max-height: 48vh;
}

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
  .cat-modal.is-popover {
    border-radius: 12px;
    max-height: 480px;
    box-shadow: none;
    border: none;
  }

  .cat-modal.is-popover .modal-header {
    padding: 12px 14px 10px;
  }

  .cat-modal.is-popover .modal-title {
    font-size: 14px;
    font-weight: 600;
  }

  .cat-modal.is-popover .header-icon {
    font-size: 18px;
    width: 26px;
    height: 26px;
    border-radius: 6px;
  }

  .cat-modal.is-popover .search-create-bar {
    padding: 0 14px 10px;
  }

  .cat-modal.is-popover .filter-input {
    height: 34px;
    font-size: 13px;
  }

  .cat-modal.is-popover .categories-content {
    max-height: 260px;
    padding: 8px 14px 10px;
    overflow-y: auto;
  }

  .cat-modal.is-popover .cat-option-row {
    padding: 8px 10px;
    border-radius: 8px;
    margin-bottom: 4px;
  }

  .cat-modal.is-popover .modal-footer {
    padding: 10px 14px;
  }
}
</style>
