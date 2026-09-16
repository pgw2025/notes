<template>
  <div class="diff-viewer">
    <div class="diff-toolbar">
      <div class="diff-meta">
        <div class="diff-version-tag">历史快照：{{ oldTime ? formatTime(oldTime) : '历史版本' }}</div>
        <div class="diff-vs">对比</div>
        <div class="diff-version-tag is-current">当前版本：{{ newTime ? formatTime(newTime) : '当前内容' }}</div>
      </div>
      <div class="diff-actions">
        <div class="diff-stats">
          <span class="stat-added">+{{ addedCount }} 行</span>
          <span class="stat-removed">-{{ removedCount }} 行</span>
        </div>
        <van-button
          size="small"
          type="primary"
          icon="replay"
          :loading="restoring"
          @click="$emit('restore')"
        >
          恢复此版本
        </van-button>
      </div>
    </div>

    <!-- 差异内容区 -->
    <div class="diff-content-wrapper">
      <div class="diff-lines">
        <div
          v-for="(part, idx) in diffParts"
          :key="idx"
          class="diff-chunk"
          :class="{
            'is-added': part.added,
            'is-removed': part.removed,
            'is-unchanged': !part.added && !part.removed
          }"
        >
          <div class="chunk-prefix">
            <span v-if="part.added">+</span>
            <span v-else-if="part.removed">-</span>
            <span v-else>&nbsp;</span>
          </div>
          <pre class="chunk-text">{{ part.value }}</pre>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { computed } from 'vue'
import { diffLines } from 'diff'
import { formatTime } from '../utils/format'

const props = defineProps({
  oldContent: { type: String, default: '' },
  newContent: { type: String, default: '' },
  oldTime: { type: [String, Date], default: null },
  newTime: { type: [String, Date], default: null },
  restoring: { type: Boolean, default: false }
})

defineEmits(['restore'])

const diffParts = computed(() => {
  return diffLines(props.oldContent || '', props.newContent || '')
})

const addedCount = computed(() => {
  return diffParts.value
    .filter((p) => p.added)
    .reduce((sum, p) => sum + (p.count || 1), 0)
})

const removedCount = computed(() => {
  return diffParts.value
    .filter((p) => p.removed)
    .reduce((sum, p) => sum + (p.count || 1), 0)
})
</script>

<style scoped>
.diff-viewer {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: var(--surface);
}

.diff-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 16px;
  border-bottom: 1px solid var(--border);
  background: var(--surface-2);
  gap: 12px;
  flex-wrap: wrap;
}

.diff-meta {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
}

.diff-version-tag {
  padding: 3px 8px;
  border-radius: 6px;
  background: var(--surface);
  border: 1px solid var(--border);
  color: var(--text-secondary);
  font-size: 12px;
}

.diff-version-tag.is-current {
  color: var(--color-primary);
  border-color: rgba(59, 130, 246, 0.3);
  background: rgba(59, 130, 246, 0.08);
  font-weight: 600;
}

.diff-vs {
  color: var(--text-tertiary);
  font-size: 11px;
}

.diff-actions {
  display: flex;
  align-items: center;
  gap: 12px;
}

.diff-stats {
  display: flex;
  gap: 8px;
  font-size: 12px;
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
}

.stat-added {
  color: #16a34a;
  font-weight: 600;
}

.stat-removed {
  color: #dc2626;
  font-weight: 600;
}

.diff-content-wrapper {
  flex: 1;
  overflow-y: auto;
  padding: 16px;
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
  font-size: 13px;
  line-height: 1.6;
}

.diff-lines {
  display: flex;
  flex-direction: column;
  border-radius: 8px;
  overflow: hidden;
  border: 1px solid var(--border);
}

.diff-chunk {
  display: flex;
  width: 100%;
}

.diff-chunk.is-added {
  background: rgba(34, 197, 94, 0.12);
  color: #15803d;
}

:global(body.dark) .diff-chunk.is-added {
  background: rgba(34, 197, 94, 0.2);
  color: #4ade80;
}

.diff-chunk.is-removed {
  background: rgba(239, 68, 68, 0.12);
  color: #b91c1c;
  text-decoration: line-through;
  opacity: 0.85;
}

:global(body.dark) .diff-chunk.is-removed {
  background: rgba(239, 68, 68, 0.2);
  color: #f87171;
}

.diff-chunk.is-unchanged {
  background: var(--surface);
  color: var(--text-secondary);
}

.chunk-prefix {
  width: 32px;
  flex-shrink: 0;
  text-align: center;
  user-select: none;
  font-weight: 700;
  border-right: 1px solid var(--border);
  padding: 4px 0;
  opacity: 0.6;
}

.chunk-text {
  flex: 1;
  margin: 0;
  padding: 4px 12px;
  white-space: pre-wrap;
  word-break: break-all;
  font-family: inherit;
}
</style>
