<template>
  <nav v-if="headings.length > 0" class="markdown-toc" aria-label="文章目录">
    <div class="toc-header">
      <van-icon name="bars" class="toc-icon" />
      <span class="toc-title">目录大纲</span>
      <span class="toc-count">{{ headings.length }}</span>
    </div>
    <ul class="toc-list">
      <li
        v-for="(h, idx) in headings"
        :key="idx"
        class="toc-item"
        :class="[
          `level-${h.level}`,
          { 'is-active': activeHeadingId === h.id }
        ]"
        @click="onClickHeading(h)"
      >
        <span class="toc-bullet"></span>
        <span class="toc-text" :title="h.text">{{ h.text }}</span>
      </li>
    </ul>
  </nav>
</template>

<script setup>
import { computed, ref, onMounted, onUnmounted, watch } from 'vue'

const props = defineProps({
  content: { type: String, default: '' },
  containerSelector: { type: String, default: '.pane-reading-content' }
})

const activeHeadingId = ref('')

const headings = computed(() => {
  if (!props.content) return []
  const lines = props.content.split('\n')
  const list = []
  let inCodeBlock = false

  for (let i = 0; i < lines.length; i++) {
    const line = lines[i].trim()
    if (line.startsWith('```')) {
      inCodeBlock = !inCodeBlock
      continue
    }
    if (inCodeBlock) continue

    const match = line.match(/^(#{1,4})\s+(.+)$/)
    if (match) {
      const level = match[1].length
      const text = match[2].trim().replace(/[*_~`]/g, '')
      const id = `toc-heading-${i}-${text.slice(0, 16).replace(/\s+/g, '-').toLowerCase()}`
      list.push({ level, text, id, lineIndex: i })
    }
  }

  return list
})

function onClickHeading(h) {
  activeHeadingId.value = h.id
  // Find heading element in reading container
  const container = document.querySelector(props.containerSelector)
  if (!container) return

  const allHeadings = container.querySelectorAll('h1, h2, h3, h4')
  // Match by text or index
  for (const el of allHeadings) {
    if (el.textContent?.trim().includes(h.text) || h.text.includes(el.textContent?.trim())) {
      el.scrollIntoView({ behavior: 'smooth', block: 'start' })
      el.classList.add('heading-highlight-flash')
      setTimeout(() => el.classList.remove('heading-highlight-flash'), 1500)
      break
    }
  }
}
</script>

<style scoped>
.markdown-toc {
  width: 220px;
  flex-shrink: 0;
  position: sticky;
  top: 24px;
  max-height: calc(100vh - 180px);
  overflow-y: auto;
  padding: 12px;
  border-radius: 12px;
  background: var(--surface-2);
  border: 1px solid var(--border);
  font-size: 13px;
}

.toc-header {
  display: flex;
  align-items: center;
  gap: 6px;
  padding-bottom: 8px;
  margin-bottom: 8px;
  border-bottom: 1px solid var(--border);
  font-size: 12px;
  font-weight: 600;
  color: var(--text-tertiary);
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.toc-icon {
  font-size: 14px;
  color: var(--color-primary);
}

.toc-title {
  flex: 1;
}

.toc-count {
  font-size: 11px;
  padding: 1px 6px;
  border-radius: 10px;
  background: var(--surface);
  border: 1px solid var(--border);
  color: var(--text-tertiary);
}

.toc-list {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.toc-item {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 5px 8px;
  border-radius: 6px;
  color: var(--text-secondary);
  cursor: pointer;
  transition: all 0.15s ease;
  line-height: 1.4;
}

.toc-item:hover {
  background: var(--surface);
  color: var(--color-primary);
}

.toc-item.is-active {
  background: rgba(59, 130, 246, 0.12);
  color: var(--color-primary);
  font-weight: 600;
}

:global(body.dark) .toc-item.is-active {
  background: rgba(56, 189, 248, 0.16);
}

.toc-bullet {
  width: 4px;
  height: 4px;
  border-radius: 50%;
  background: var(--text-tertiary);
  flex-shrink: 0;
  transition: all 0.15s ease;
}

.toc-item:hover .toc-bullet,
.toc-item.is-active .toc-bullet {
  background: var(--color-primary);
  transform: scale(1.4);
}

.toc-text {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.toc-item.level-1 {
  font-weight: 600;
  padding-left: 6px;
}

.toc-item.level-2 {
  padding-left: 14px;
}

.toc-item.level-3 {
  padding-left: 22px;
  font-size: 12px;
  opacity: 0.85;
}

.toc-item.level-4 {
  padding-left: 28px;
  font-size: 11.5px;
  opacity: 0.75;
}
</style>
