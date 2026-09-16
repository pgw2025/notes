<template>
  <Teleport to="body">
    <transition name="palette-fade">
      <div v-if="show" class="palette-overlay" @click.self="close">
        <div class="palette-panel" role="dialog" aria-label="命令面板">
          <div class="palette-input-row">
            <van-icon name="search" class="palette-search-icon" />
            <input
              ref="inputRef"
              v-model="keyword"
              type="text"
              class="palette-input"
              placeholder="搜索笔记或输入命令..."
              autocomplete="off"
              @input="onInput"
              @keydown="onKeydown"
            />
            <kbd class="palette-kbd">ESC</kbd>
          </div>

          <div class="palette-results">
            <!-- 命令组 -->
            <div class="palette-group-label">命令</div>
            <button
              v-for="(item, idx) in filteredCommands"
              :key="item.id"
              type="button"
              class="palette-item"
              :class="{ 'is-active': activeIndex === idx }"
              @click="runCommand(item)"
              @mousemove="setActive(idx)"
            >
              <van-icon :name="item.icon" class="palette-item-icon" />
              <span class="palette-item-label">{{ item.label }}</span>
              <kbd v-if="item.hint" class="palette-item-hint">{{ item.hint }}</kbd>
            </button>

            <!-- 笔记组 -->
            <template v-if="keyword.trim()">
              <div class="palette-group-label">笔记</div>
              <div v-if="searching" class="palette-loading">
                <van-loading size="16px" color="var(--color-primary)">检索中…</van-loading>
              </div>
              <div v-else-if="noteResults.length === 0" class="palette-empty">
                未找到相关笔记
              </div>
              <button
                v-for="(n, idx) in noteResults"
                :key="n.id"
                type="button"
                class="palette-item"
                :class="{ 'is-active': activeIndex === filteredCommands.length + idx }"
                @click="goNote(n)"
                @mousemove="setActive(filteredCommands.length + idx)"
              >
                <van-icon name="notes-o" class="palette-item-icon" />
                <span class="palette-note-main">
                  <span class="palette-note-title" v-html="highlight(n.title || '无标题笔记')"></span>
                  <span class="palette-note-meta">
                    {{ n.categoryName || '未分类' }} · {{ formatTime(n.updatedAt) }}
                  </span>
                </span>
                <van-icon name="arrow" class="palette-item-arrow" />
              </button>
            </template>
          </div>

          <div class="palette-footer">
            <span class="palette-footer-hint"><kbd>↑</kbd><kbd>↓</kbd> 选择</span>
            <span class="palette-footer-hint"><kbd>Enter</kbd> 确认</span>
            <span class="palette-footer-hint"><kbd>Esc</kbd> 关闭</span>
          </div>
        </div>
      </div>
    </transition>
  </Teleport>
</template>

<script setup>
import { ref, computed, watch, nextTick, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import http from '../api/http'
import { useThemeStore } from '../stores/theme'
import { useAuthStore } from '../stores/auth'
import { formatTime } from '../utils/format'

const props = defineProps({
  show: { type: Boolean, default: false }
})
const emit = defineEmits(['update:show', 'navigate'])

const router = useRouter()
const theme = useThemeStore()
const auth = useAuthStore()

const keyword = ref('')
const noteResults = ref([])
const searching = ref(false)
const activeIndex = ref(0)
const inputRef = ref(null)

let debounceTimer = null

const isAdmin = computed(() => {
  return auth.user?.roles?.includes('Admin') || auth.user?.isAdmin === true
})

const baseCommands = [
  { id: 'new-note', icon: 'plus', label: '新建笔记', hint: '⌘N', action: () => router.push('/notes/new') },
  { id: 'notes', icon: 'notes-o', label: '全部笔记工作台', hint: '', action: () => router.push('/notes') },
  { id: 'timeline', icon: 'clock-o', label: '时间线视图', hint: '', action: () => router.push('/timeline') },
  { id: 'categories', icon: 'apps-o', label: '分类与标签', hint: '', action: () => router.push('/categories') },
  { id: 'search', icon: 'search', label: '全文搜索中心', hint: '', action: () => router.push('/search') },
  { id: 'settings', icon: 'contact', label: '偏好设置', hint: '', action: () => router.push('/settings') },
  { id: 'theme-light', icon: 'sun-o', label: '切换浅色模式', hint: '', action: () => theme.setMode('light') },
  { id: 'theme-dark', icon: 'moon-o', label: '切换深色模式', hint: '', action: () => theme.setMode('dark') },
  { id: 'theme-auto', icon: 'desktop-o', label: '跟随系统主题', hint: '', action: () => theme.setMode('auto') }
]

const commands = computed(() => {
  const list = [...baseCommands]
  if (isAdmin.value) {
    list.push({
      id: 'admin',
      icon: 'shield-o',
      label: '系统管理后台',
      hint: 'Admin',
      action: () => window.open('/admin/', '_blank')
    })
  }
  return list
})

const filteredCommands = computed(() => {
  const q = keyword.value.trim().toLowerCase()
  if (!q) return commands.value
  return commands.value.filter((c) => c.label.toLowerCase().includes(q))
})

const totalItems = computed(() => filteredCommands.value.length + noteResults.value.length)

// 防抖检索
function onInput() {
  clearTimeout(debounceTimer)
  const q = keyword.value.trim()
  if (!q) {
    noteResults.value = []
    searching.value = false
    activeIndex.value = 0
    return
  }
  debounceTimer = setTimeout(() => search(q), 300)
}

async function search(q) {
  searching.value = true
  try {
    const res = await http.get('/notes/quicksearch', { params: { q, pageSize: 8 } })
    noteResults.value = res.items || []
  } catch {
    noteResults.value = []
  } finally {
    searching.value = false
    activeIndex.value = 0
  }
}

function escapeHtml(str) {
  return String(str).replace(/[&<>"']/g, (c) => ({
    '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
  }[c]))
}

function highlight(text) {
  const safe = escapeHtml(text || '')
  const q = keyword.value.trim()
  if (!q) return safe
  const safeQ = escapeHtml(q)
  // 用转义后的词做不区分大小写的命中高亮
  const re = new RegExp(safeQ.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'), 'gi')
  return safe.replace(re, (m) => `<mark>${m}</mark>`)
}

function onKeydown(e) {
  if (e.key === 'ArrowDown') {
    e.preventDefault()
    setActive((activeIndex.value + 1) % Math.max(totalItems.value, 1))
  } else if (e.key === 'ArrowUp') {
    e.preventDefault()
    setActive((activeIndex.value - 1 + Math.max(totalItems.value, 1)) % Math.max(totalItems.value, 1))
  } else if (e.key === 'Enter') {
    e.preventDefault()
    executeActive()
  } else if (e.key === 'Escape') {
    e.preventDefault()
    close()
  }
}

function setActive(idx) {
  activeIndex.value = idx
}

function executeActive() {
  const cmdCount = filteredCommands.value.length
  if (activeIndex.value < cmdCount) {
    runCommand(filteredCommands.value[activeIndex.value])
  } else {
    const note = noteResults.value[activeIndex.value - cmdCount]
    if (note) goNote(note)
  }
}

function runCommand(cmd) {
  if (!cmd) return
  close()
  cmd.action()
}

function goNote(n) {
  if (!n) return
  close()
  router.push(`/notes/${n.id}`)
}

function close() {
  emit('update:show', false)
}

function reset() {
  keyword.value = ''
  noteResults.value = []
  searching.value = false
  activeIndex.value = 0
}

watch(
  () => props.show,
  (val) => {
    if (val) {
      reset()
      nextTick(() => inputRef.value?.focus())
    }
  }
)

onUnmounted(() => clearTimeout(debounceTimer))
</script>

<style scoped>
.palette-overlay {
  position: fixed;
  inset: 0;
  z-index: 2000;
  background: rgba(0, 0, 0, 0.4);
  backdrop-filter: blur(2px);
  -webkit-backdrop-filter: blur(2px);
  display: flex;
  align-items: flex-start;
  justify-content: center;
  padding-top: 12vh;
}

.palette-panel {
  width: 620px;
  max-width: calc(100vw - 32px);
  max-height: 70vh;
  display: flex;
  flex-direction: column;
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 14px;
  box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3);
  overflow: hidden;
}

.palette-input-row {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 14px 16px;
  border-bottom: 1px solid var(--border);
}

.palette-search-icon {
  font-size: 18px;
  color: var(--color-primary);
  flex-shrink: 0;
}

.palette-input {
  flex: 1;
  min-width: 0;
  border: none;
  background: transparent;
  outline: none;
  font-size: 16px;
  color: var(--text-primary);
}

.palette-input::placeholder {
  color: var(--text-tertiary);
}

.palette-kbd {
  font-size: 11px;
  padding: 2px 6px;
  border-radius: 5px;
  background: var(--surface-2);
  border: 1px solid var(--border);
  color: var(--text-tertiary);
  font-family: inherit;
}

.palette-results {
  flex: 1;
  overflow-y: auto;
  padding: 8px;
  min-height: 0;
}

.palette-group-label {
  font-size: 11px;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.6px;
  color: var(--text-tertiary);
  padding: 8px 10px 4px;
}

.palette-item {
  display: flex;
  align-items: center;
  gap: 12px;
  width: 100%;
  padding: 10px 12px;
  border: none;
  background: transparent;
  border-radius: 8px;
  cursor: pointer;
  text-align: left;
  font-size: 14px;
  color: var(--text-secondary);
  transition: background 0.12s ease, color 0.12s ease;
}

.palette-item.is-active {
  background: rgba(59, 130, 246, 0.12);
  color: var(--color-primary);
}

:global(body.dark) .palette-item.is-active {
  background: rgba(56, 189, 248, 0.16);
}

.palette-item-icon {
  font-size: 17px;
  flex-shrink: 0;
}

.palette-item-label {
  flex: 1;
  min-width: 0;
}

.palette-item-hint {
  font-size: 11px;
  padding: 1px 6px;
  border-radius: 4px;
  background: var(--surface-2);
  color: var(--text-tertiary);
  font-family: inherit;
}

.palette-item-arrow {
  font-size: 14px;
  color: var(--text-tertiary);
}

.palette-note-main {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.palette-note-title {
  font-size: 14px;
  color: var(--text-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.palette-note-title :deep(mark) {
  background: #ffe57f;
  color: #1f2328;
  border-radius: 2px;
  padding: 0 1px;
}

.palette-note-meta {
  font-size: 12px;
  color: var(--text-tertiary);
}

.palette-loading,
.palette-empty {
  padding: 16px;
  text-align: center;
  font-size: 13px;
  color: var(--text-tertiary);
}

.palette-footer {
  display: flex;
  gap: 16px;
  padding: 8px 16px;
  border-top: 1px solid var(--border);
  background: var(--surface-2);
}

.palette-footer-hint {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  font-size: 11px;
  color: var(--text-tertiary);
}

.palette-footer-hint kbd {
  font-size: 10px;
  padding: 1px 5px;
  border-radius: 4px;
  background: var(--surface);
  border: 1px solid var(--border);
  font-family: inherit;
}

.palette-fade-enter-active,
.palette-fade-leave-active {
  transition: opacity 0.15s ease;
}
.palette-fade-enter-active .palette-panel,
.palette-fade-leave-active .palette-panel {
  transition: transform 0.15s ease;
}
.palette-fade-enter-from,
.palette-fade-leave-to {
  opacity: 0;
}
.palette-fade-enter-from .palette-panel,
.palette-fade-leave-to .palette-panel {
  transform: translateY(-12px) scale(0.98);
}
</style>
