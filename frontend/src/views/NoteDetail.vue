<template>
  <div class="page">
    <van-nav-bar title="笔记详情" left-arrow @click-left="$router.back()">
      <template #right>
        <van-icon name="edit" size="20" @click="$router.push(`/notes/${note.id}/edit`)" />
      </template>
    </van-nav-bar>

    <div v-if="note" class="detail">
      <h1 class="detail-title">{{ note.title || '无标题' }}</h1>

      <div class="detail-meta">
        <van-tag v-if="note.categoryName" type="primary" size="medium">{{ note.categoryName }}</van-tag>
        <van-tag
          v-for="t in note.tags"
          :key="t"
          plain
          size="medium"
          color="#969799"
          text-color="#969799"
        >{{ t }}</van-tag>
      </div>

      <div class="detail-time">
        创建于 {{ formatDateTime(note.createdAt) }} · 更新于 {{ formatDateTime(note.updatedAt) }}
      </div>

      <markdown-body :content="note.content" />

      <div v-if="note.attachments?.length" class="attachments">
        <div class="section-title">附件</div>
        <van-cell-group inset>
          <van-cell
            v-for="a in note.attachments"
            :key="a.id"
            :title="a.fileName"
            :value="formatSize(a.size)"
            is-link
            :url="attachmentUrl(a.id, a.fileName)"
          >
            <template #icon>
              <van-icon :name="isImage(a.contentType) ? 'photo-o' : 'description'" class="att-icon" />
            </template>
          </van-cell>
        </van-cell-group>
      </div>
    </div>

    <van-loading v-else class="loading" type="spinner" />
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { showToast } from 'vant'
import http from '../api/http'
import MarkdownBody from '../components/MarkdownBody.vue'
import { formatDateTime } from '../utils/format'

const route = useRoute()
const note = ref(null)

async function loadNote() {
  try {
    note.value = await http.get(`/notes/${route.params.id}`)
  } catch {
    showToast('加载失败')
  }
}

function isImage(contentType) {
  return contentType?.startsWith('image/')
}

function formatSize(bytes) {
  if (!bytes) return ''
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`
}

function attachmentUrl(id, fileName) {
  const token = localStorage.getItem('token')
  return `/api/attachments/${id}?access_token=${encodeURIComponent(token)}`
}

onMounted(loadNote)
</script>

<style scoped>
.page {
  min-height: 100vh;
  padding-bottom: 40px;
}
.detail {
  padding: 16px;
}
.detail-title {
  font-size: 22px;
  font-weight: 700;
  margin: 0 0 12px;
  line-height: 1.4;
}
.detail-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  margin-bottom: 8px;
}
.detail-time {
  font-size: 12px;
  color: #c8c9cc;
  margin-bottom: 20px;
}
.section-title {
  font-size: 14px;
  font-weight: 600;
  color: #646566;
  margin: 24px 0 8px;
  padding: 0 4px;
}
.att-icon {
  margin-right: 8px;
  font-size: 18px;
  color: #1989fa;
}
.loading {
  display: flex;
  justify-content: center;
  padding: 60px 0;
}

/* 桌面端：居中阅读宽度 */
@media (min-width: 1024px) {
  .page {
    max-width: 820px;
    margin: 0 auto;
    padding-bottom: 48px;
  }
  .detail {
    padding: 24px 32px;
  }
  .detail-title {
    font-size: 26px;
  }
  .markdown-body {
    font-size: 16px;
    line-height: 1.8;
  }
}
</style>
