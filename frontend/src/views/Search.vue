<template>
  <div class="page">
    <van-nav-bar title="搜索" />
    <van-search
      v-model="keyword"
      placeholder="搜索笔记标题和内容"
      show-action
      shape="round"
      @search="onSearch"
      @clear="onClear"
    >
      <template #action>
        <span @click="onSearch">搜索</span>
      </template>
    </van-search>

    <div v-if="searched">
      <div v-if="results.length === 0" class="empty">
        <van-empty description="未找到相关笔记" />
      </div>
      <van-cell-group v-else inset style="margin-top: 8px">
        <van-cell
          v-for="n in results"
          :key="n.id"
          center
          is-link
          @click="$router.push(`/notes/${n.id}`)"
        >
          <template #title>
            <div class="result-title">{{ n.title || '无标题' }}</div>
            <div class="result-preview">{{ n.contentPreview || '暂无内容' }}</div>
          </template>
          <template #value>
            <span class="result-time">{{ formatTime(n.updatedAt) }}</span>
          </template>
        </van-cell>
      </van-cell-group>
    </div>

    <div v-else class="empty">
      <van-empty description="输入关键词搜索笔记标题与正文" />
    </div>
  </div>
</template>

<script setup>
import { ref } from 'vue'
import http from '../api/http'
import { formatTime } from '../utils/format'

const keyword = ref('')
const results = ref([])
const searched = ref(false)
let timer = null

async function onSearch() {
  const q = keyword.value.trim()
  if (!q) {
    searched.value = false
    results.value = []
    return
  }
  results.value = await http.get('/notes/search', { params: { q } })
  searched.value = true
}

function onClear() {
  searched.value = false
  results.value = []
}
</script>

<style scoped>
.page {
  min-height: 100vh;
  padding-bottom: 80px;
}
.empty {
  padding-top: 30px;
}
.result-title {
  font-size: 15px;
  font-weight: 600;
  margin-bottom: 4px;
}
.result-preview {
  font-size: 13px;
  color: var(--text-tertiary);
  display: -webkit-box;
  -webkit-line-clamp: 1;
  -webkit-box-orient: vertical;
  overflow: hidden;
}
.result-time {
  font-size: 12px;
  color: var(--text-disabled);
}

/* 桌面端：居中阅读宽度 */
@media (min-width: 1024px) {
  .page {
    max-width: 820px;
    margin: 0 auto;
    padding-bottom: 32px;
  }
}
</style>
