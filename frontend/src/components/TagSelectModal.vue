<template>
  <ResponsivePopover
    :show="show"
    :anchor-el="anchorEl"
    :width="410"
    :max-height="480"
    modal-class="tag-modal-popup"
    @update:show="$emit('update:show', $event)"
  >
    <div class="tag-modal" :class="{ 'is-popover': isDesktop }">
      <!-- 头部 -->
      <div class="modal-header">
        <div class="header-main">
          <div class="header-icon">🏷️</div>
          <div>
            <h3 class="modal-title">选择笔记标签</h3>
            <p v-if="!isDesktop" class="modal-sub">多选标签，帮助你从不同维度交叉检索笔记</p>
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
            placeholder="搜索标签或输入新标签名称..."
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
          <span>创建并选择「#{{ trimmedQuery }}」</span>
        </button>
      </div>

      <!-- 已选标签速览栏 -->
      <div v-if="selectedTagList.length > 0" class="selected-summary-bar">
        <div class="summary-label">
          <span>已选 ({{ selectedTagList.length }})：</span>
          <button class="clear-all-btn" @click="clearAllSelected">清空已选</button>
        </div>
        <div class="selected-pills-row">
          <span
            v-for="t in selectedTagList"
            :key="'sel-' + t.id"
            class="selected-pill"
            @click="toggleTag(t.id)"
            title="点击取消选择"
          >
            <span class="pill-hash">#</span>
            <span class="pill-text">{{ t.name }}</span>
            <span class="pill-remove">×</span>
          </span>
        </div>
      </div>

      <!-- 标签库云状网格 -->
      <div class="tags-content">
        <div class="tags-cloud-header">
          <span class="cloud-title">
            {{ searchQuery ? '搜索结果' : '全部标签' }} ({{ filteredTags.length }})
          </span>
        </div>

        <div v-if="filteredTags.length > 0" class="tag-cloud">
          <button
            v-for="t in filteredTags"
            :key="t.id"
            type="button"
            class="tag-pill-btn"
            :class="{ active: isTagSelected(t.id) }"
            @click="toggleTag(t.id)"
          >
            <span class="tag-pill-hash">#</span>
            <span class="tag-pill-name">{{ t.name }}</span>
            <span class="tag-pill-count">{{ t.noteCount || 0 }}</span>
            <div v-if="isTagSelected(t.id)" class="tag-pill-check">
              <van-icon name="success" size="10" />
            </div>
          </button>
        </div>

        <!-- 空状态 -->
        <div v-else-if="!canQuickCreate" class="empty-tags">
          <div class="empty-icon">🏷️</div>
          <p class="empty-text">暂无相关标签</p>
          <p class="empty-hint">在上方输入框中输入名称即可创建新标签</p>
        </div>
      </div>

      <!-- 底部操作 -->
      <div class="modal-footer">
        <span class="footer-tip">
          已选择 <b>{{ selectedTagList.length }}</b> 个标签
        </span>
        <van-button round type="primary" size="small" @click="$emit('update:show', false)">
          完成
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
import ResponsivePopover from './ResponsivePopover.vue'

const props = defineProps({
  show: {
    type: Boolean,
    default: false
  },
  modelValue: {
    type: Array,
    default: () => []
  },
  tags: {
    type: Array,
    default: () => []
  },
  anchorEl: {
    type: [Object, null],
    default: null
  }
})

const emit = defineEmits(['update:show', 'update:modelValue', 'tag-created'])

const { isDesktop } = useResponsive()
const searchQuery = ref('')
const creating = ref(false)
const searchInputRef = ref(null)

const trimmedQuery = computed(() => searchQuery.value.trim())

const filteredTags = computed(() => {
  if (!trimmedQuery.value) return props.tags
  const q = trimmedQuery.value.toLowerCase()
  return props.tags.filter((t) => t.name.toLowerCase().includes(q))
})

const exactMatchExists = computed(() => {
  if (!trimmedQuery.value) return false
  const q = trimmedQuery.value.toLowerCase()
  return props.tags.some((t) => t.name.toLowerCase() === q)
})

const canQuickCreate = computed(() => {
  return trimmedQuery.value.length > 0 && !exactMatchExists.value
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

const selectedTagList = computed(() => {
  return props.tags.filter((t) => props.modelValue.includes(t.id))
})

function isTagSelected(id) {
  return props.modelValue.includes(id)
}

function toggleTag(id) {
  const current = [...props.modelValue]
  const idx = current.indexOf(id)
  if (idx >= 0) {
    current.splice(idx, 1)
  } else {
    current.push(id)
  }
  emit('update:modelValue', current)
}

function clearAllSelected() {
  emit('update:modelValue', [])
}

async function handleQuickCreate() {
  const name = trimmedQuery.value
  if (!name) return

  // 如果已存在则直接勾选
  const exist = props.tags.find((t) => t.name.toLowerCase() === name.toLowerCase())
  if (exist) {
    if (!props.modelValue.includes(exist.id)) {
      emit('update:modelValue', [...props.modelValue, exist.id])
    }
    searchQuery.value = ''
    showToast(`已勾选「#${exist.name}」`)
    return
  }

  creating.value = true
  try {
    const res = await http.post('/tags', { name })
    const record = res && res.id ? res : { id: res?.id, name, noteCount: 0 }

    if (!record.id) {
      const fresh = await http.get('/tags')
      const just = fresh.find((t) => t.name === name)
      if (just) {
        record.id = just.id
        record.noteCount = just.noteCount ?? 0
      }
    }

    emit('tag-created', record)
    if (record.id && !props.modelValue.includes(record.id)) {
      emit('update:modelValue', [...props.modelValue, record.id])
    }
    searchQuery.value = ''
    showToast(`已创建并选择「#${name}」`)
  } catch (err) {
    showToast('创建标签失败：' + (err.message || ''))
  } finally {
    creating.value = false
  }
}
</script>

<style scoped>
.tag-modal {
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
  background: rgba(16, 185, 129, 0.1);
  color: #059669;
  border: 1px dashed #059669;
  border-radius: 8px;
  font-size: 13px;
  font-weight: 600;
  cursor: pointer;
  align-self: flex-start;
  transition: all 0.15s ease;
}

:global(body.dark) .quick-create-btn {
  background: rgba(52, 211, 153, 0.15);
  color: #34d399;
  border-color: #34d399;
}

.quick-create-btn:hover {
  background: rgba(16, 185, 129, 0.2);
}

/* 已选标签概览 */
.selected-summary-bar {
  padding: 10px 20px;
  background: var(--surface-2);
  border-bottom: 1px dashed var(--border);
}

.summary-label {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: 12px;
  color: var(--text-tertiary);
  margin-bottom: 6px;
}

.clear-all-btn {
  background: transparent;
  border: none;
  color: var(--color-danger);
  font-size: 12px;
  cursor: pointer;
  padding: 0;
}

.clear-all-btn:hover {
  text-decoration: underline;
}

.selected-pills-row {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}

.selected-pill {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 3px 8px;
  background: var(--surface);
  border: 1px solid var(--color-primary);
  color: var(--color-primary);
  border-radius: 14px;
  font-size: 12.5px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.15s ease;
}

.selected-pill:hover {
  background: rgba(239, 68, 68, 0.1);
  border-color: var(--color-danger);
  color: var(--color-danger);
}

.pill-remove {
  font-size: 14px;
  line-height: 1;
  margin-left: 2px;
}

/* 标签云 */
.tags-content {
  padding: 16px 20px;
  overflow-y: auto;
  min-height: 180px;
  max-height: 44vh;
}

.tags-cloud-header {
  margin-bottom: 12px;
}

.cloud-title {
  font-size: 12.5px;
  font-weight: 600;
  color: var(--text-tertiary);
}

.tag-cloud {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.tag-pill-btn {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 6px 12px;
  background: var(--surface-2);
  border: 1px solid var(--border);
  border-radius: 20px;
  font-size: 13.5px;
  color: var(--text-primary);
  cursor: pointer;
  transition: all 0.16s ease;
  user-select: none;
}

.tag-pill-btn:hover {
  border-color: var(--color-primary);
  transform: translateY(-1px);
  background: var(--surface-3);
}

.tag-pill-btn.active {
  background: rgba(59, 130, 246, 0.1);
  border-color: var(--color-primary);
  color: var(--color-primary);
  font-weight: 600;
}

:global(body.dark) .tag-pill-btn.active {
  background: rgba(56, 189, 248, 0.16);
}

.tag-pill-hash {
  color: var(--color-primary);
  font-weight: 700;
}

.tag-pill-count {
  font-size: 11px;
  background: var(--surface-3);
  color: var(--text-secondary);
  border-radius: 10px;
  padding: 0 5px;
  line-height: 16px;
}

.tag-pill-btn.active .tag-pill-count {
  background: rgba(59, 130, 246, 0.2);
  color: var(--color-primary);
}

.tag-pill-check {
  width: 14px;
  height: 14px;
  border-radius: 50%;
  background: var(--color-primary);
  color: #fff;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  margin-left: 2px;
}

.empty-tags {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 36px 0;
  text-align: center;
}

.empty-icon {
  font-size: 36px;
  margin-bottom: 8px;
}

.empty-text {
  font-size: 14px;
  font-weight: 600;
  color: var(--text-primary);
  margin: 0 0 4px;
}

.empty-hint {
  font-size: 12px;
  color: var(--text-tertiary);
  margin: 0;
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
  .tag-modal.is-popover {
    border-radius: 12px;
    max-height: 480px;
    box-shadow: none;
    border: none;
  }

  .tag-modal.is-popover .modal-header {
    padding: 12px 14px 10px;
  }

  .tag-modal.is-popover .modal-title {
    font-size: 14px;
    font-weight: 600;
  }

  .tag-modal.is-popover .header-icon {
    font-size: 18px;
    width: 26px;
    height: 26px;
    border-radius: 6px;
  }

  .tag-modal.is-popover .modal-sub {
    display: none;
  }

  .tag-modal.is-popover .search-create-bar {
    padding: 0 14px 10px;
  }

  .tag-modal.is-popover .filter-input {
    height: 34px;
    font-size: 13px;
  }

  .tag-modal.is-popover .selected-summary-bar {
    padding: 6px 14px 8px;
  }

  .tag-modal.is-popover .tags-content {
    max-height: 220px;
    padding: 0 14px 10px;
    overflow-y: auto;
  }

  .tag-modal.is-popover .tag-cloud {
    gap: 6px;
  }

  .tag-modal.is-popover .tag-pill-btn {
    padding: 4px 10px;
    font-size: 12px;
  }

  .tag-modal.is-popover .modal-footer {
    padding: 10px 14px;
  }
}
</style>
