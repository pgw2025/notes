<template>
  <div class="page">
    <van-nav-bar title="笔记详情" left-arrow @click-left="$router.back()">
      <template #right>
        <div class="nav-right">
          <van-icon name="clock-o" size="20" @click="openVersions" />
          <van-icon name="edit" size="20" @click="$router.push(`/notes/${note.id}/edit`)" />
        </div>
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

    <!-- =============== 历史版本 Popup =============== -->
    <van-popup
      v-model:show="showVersionsPopup"
      position="bottom"
      round
      :style="{ height: '75%' }"
    >
      <van-nav-bar title="历史版本">
        <template #right>
          <van-icon name="cross" size="20" @click="showVersionsPopup = false" />
        </template>
      </van-nav-bar>

      <div v-if="versionsLoading" class="versions-loading">
        <van-loading color="#1989fa">加载版本中…</van-loading>
      </div>

      <div v-else-if="!versions.length" class="versions-empty">
        <van-empty description="还没有版本记录" />
      </div>

      <van-cell-group v-else inset class="versions-list">
        <div
          v-for="v in versions"
          :key="v.id"
          class="version-item"
          :class="{ 'is-current': v.isCurrent }"
        >
          <div class="version-row">
            <div class="version-title-row">
              <div class="version-time">{{ formatDateTime(v.createdAt) }}</div>
              <van-tag v-if="v.isCurrent" size="mini" type="success" plain>当前版本</van-tag>
            </div>
            <div class="version-preview-title">{{ v.titlePreview }}</div>
            <div class="version-preview-body">{{ v.contentPreview }}</div>
            <div class="version-meta">
              <van-tag v-if="v.categoryName" size="mini" plain type="primary">{{ v.categoryName }}</van-tag>
              <van-tag v-for="t in v.tags" :key="t" size="mini" plain color="#969799" text-color="#969799">{{ t }}</van-tag>
            </div>
            <div class="version-actions">
              <van-button size="mini" plain @click="viewVersion(v)">查看内容</van-button>
              <van-button
                v-if="!v.isCurrent"
                size="mini"
                type="warning"
                :loading="restoringId === v.id"
                @click="restoreVersion(v)"
              >恢复到此版本</van-button>
            </div>
          </div>
        </div>
      </van-cell-group>
    </van-popup>

    <!-- =============== 查看旧版本 Modal =============== -->
    <van-popup
      v-model:show="showViewModal"
      position="bottom"
      round
      :style="{ height: '85%' }"
    >
      <van-nav-bar>
        <template #left>
          <van-icon name="cross" size="20" @click="showViewModal = false" />
        </template>
        <template #title>
          <span>旧版本 · {{ activeVersion ? formatDateTime(activeVersion.createdAt) : '' }}</span>
        </template>
        <template #right>
          <van-button
            v-if="activeVersion && !activeVersion.isCurrent"
            size="mini"
            type="warning"
            :loading="restoringId === activeVersion.id"
            @click="restoreById(activeVersion.id)"
          >恢复</van-button>
        </template>
      </van-nav-bar>

      <div v-if="viewLoading" class="versions-loading">
        <van-loading color="#1989fa">加载中…</van-loading>
      </div>

      <div v-else-if="activeVersion" class="version-detail">
        <div class="vd-meta">
          <van-tag v-if="activeVersion.categoryName" size="medium" plain type="primary">{{ activeVersion.categoryName }}</van-tag>
          <van-tag
            v-for="t in activeVersion.tagNames"
            :key="t"
            size="medium"
            plain
            color="#969799"
            text-color="#969799"
          >{{ t }}</van-tag>
        </div>
        <h2 class="vd-title">{{ activeVersion.title || '（无标题）' }}</h2>
        <markdown-body :content="activeVersion.content || '*无内容*'" />
      </div>
    </van-popup>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { showToast, showConfirmDialog } from 'vant'
import http from '../api/http'
import MarkdownBody from '../components/MarkdownBody.vue'
import { formatDateTime } from '../utils/format'

const route = useRoute()
const note = ref(null)

const showVersionsPopup = ref(false)
const showViewModal = ref(false)
const versionsLoading = ref(false)
const viewLoading = ref(false)
const versions = ref([])
const activeVersion = ref(null)
const restoringId = ref(null)

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

// ================= 版本列表 =================
async function openVersions() {
  showVersionsPopup.value = true
  versionsLoading.value = true
  try {
    versions.value = await http.get(`/notes/${route.params.id}/versions`)
  } finally {
    versionsLoading.value = false
  }
}

async function viewVersion(v) {
  showViewModal.value = true
  viewLoading.value = true
  try {
    const detail = await http.get(`/notes/${route.params.id}/versions/${v.id}`)
    activeVersion.value = { ...detail, isCurrent: v.isCurrent }
  } finally {
    viewLoading.value = false
  }
}

async function restoreVersion(v) {
  try {
    await showConfirmDialog({
      title: '恢复版本',
      message: `确定要将笔记恢复为「${formatDateTime(v.createdAt)}」的版本吗？\n当前版本会作为历史保留。`
    })
    await restoreById(v.id)
  } catch {
    // 取消
  }
}

async function restoreById(vid) {
  restoringId.value = vid
  try {
    await http.post(`/notes/${route.params.id}/versions/${vid}/restore`)
    showToast('已恢复')
    showVersionsPopup.value = false
    showViewModal.value = false
    // 重新加载详情
    await loadNote()
    // 重新打开版本列表（多了一个新版本）
    versionsLoading.value = true
    try {
      versions.value = await http.get(`/notes/${route.params.id}/versions`)
    } finally {
      versionsLoading.value = false
    }
  } finally {
    restoringId.value = null
  }
}

onMounted(loadNote)
</script>

<style scoped>
.page {
  min-height: 100vh;
  padding-bottom: 40px;
}
.nav-right {
  display: flex;
  gap: 18px;
  align-items: center;
  padding-right: 10px;
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

/* ========== 版本列表 ========== */
.versions-loading {
  padding: 40px 0;
  text-align: center;
}
.versions-empty {
  padding-top: 40px;
}
.versions-list {
  margin: 12px 16px;
  background: transparent;
}
.version-item {
  background: #fff;
  border: 1px solid #ebedf0;
  border-radius: 8px;
  padding: 12px 14px;
  margin-bottom: 10px;
}
.version-item.is-current {
  border-color: #07c160;
  background: #f3fbf5;
}
.version-title-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 6px;
}
.version-time {
  font-size: 13px;
  font-weight: 600;
  color: #323233;
}
.version-preview-title {
  font-size: 14px;
  font-weight: 600;
  margin-bottom: 4px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.version-preview-body {
  font-size: 12px;
  color: #646566;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
  margin-bottom: 8px;
}
.version-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  margin-bottom: 8px;
}
.version-actions {
  display: flex;
  gap: 8px;
  justify-content: flex-end;
  border-top: 1px dashed #ebedf0;
  padding-top: 8px;
}

/* ========== 版本详情查看 Modal ========== */
.version-detail {
  padding: 16px 18px 40px;
  overflow-y: auto;
}
.vd-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  margin-bottom: 10px;
}
.vd-title {
  font-size: 18px;
  font-weight: 700;
  margin: 0 0 16px;
  padding-bottom: 10px;
  border-bottom: 1px solid #ebedf0;
}

/* 桌面端：居中阅读宽度 + 版本模态改为居中 Dialog 样式 */
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

  /* 桌面端把版本 Popup 改为居中 Modal 风格 */
  :deep(.van-popup--bottom) {
    max-width: 720px;
    margin: 3vh auto;
    left: 50%;
    transform: translateX(-50%);
    border-radius: 12px;
    box-shadow: 0 8px 30px rgba(0,0,0,0.12);
  }
}
</style>
