<template>
  <div class="page-container">
    <el-card class="box-card" shadow="never">
      <!-- Toolbar Filters -->
      <div class="toolbar-box">
        <div class="filter-row">
          <div class="filter-item user-select">
            <el-select
              v-model="filter.userId"
              placeholder="选择用户筛选"
              clearable
              filterable
              class="w-full"
            >
              <el-option
                v-for="u in userOptions"
                :key="u.id"
                :label="u.displayName || u.email"
                :value="u.id"
              />
            </el-select>
          </div>

          <div class="filter-item search-input">
            <el-input
              v-model="filter.q"
              placeholder="搜索笔记标题或正文关键词..."
              clearable
              :prefix-icon="Search"
              @keyup.enter="handleSearch"
              @clear="handleSearch"
            />
          </div>

          <div class="filter-item date-picker hidden-mobile">
            <el-date-picker
              v-model="filter.range"
              type="daterange"
              range-separator="至"
              start-placeholder="起始日期"
              end-placeholder="截止日期"
              value-format="YYYY-MM-DD"
            />
          </div>
        </div>

        <div class="filter-btn-row">
          <el-button type="primary" :icon="Search" @click="handleSearch">查询</el-button>
          <el-button :icon="Refresh" @click="handleReset">重置</el-button>
        </div>
      </div>

      <!-- Desktop Table -->
      <div class="desktop-table hidden-mobile">
        <el-table v-loading="loading" :data="notes" stripe style="width: 100%">
          <el-table-column label="标题" min-width="220">
            <template #default="{ row }">
              <div class="note-title-cell">
                <el-tag v-if="row.isPinned" type="danger" size="small" effect="dark" class="pin-tag">置顶</el-tag>
                <span class="note-title-text" @click="openDetail(row)">{{ row.title || '（无标题）' }}</span>
              </div>
              <div class="note-preview-text">{{ row.contentPreview || '（无内容）' }}</div>
            </template>
          </el-table-column>

          <el-table-column label="作者" width="160">
            <template #default="{ row }">
              <div class="author-meta">
                <div class="author-name">{{ row.userDisplayName || '-' }}</div>
                <div class="author-email">{{ row.userEmail }}</div>
              </div>
            </template>
          </el-table-column>

          <el-table-column label="分类" width="120" align="center">
            <template #default="{ row }">
              <el-tag v-if="row.categoryName" size="small" type="primary" effect="plain">
                {{ row.categoryName }}
              </el-tag>
              <span v-else class="text-muted">-</span>
            </template>
          </el-table-column>

          <el-table-column label="标签" min-width="140">
            <template #default="{ row }">
              <div class="tags-wrap" v-if="row.tags && row.tags.length">
                <el-tag v-for="t in row.tags.slice(0, 2)" :key="t" size="small" effect="light" class="tag-chip">
                  {{ t }}
                </el-tag>
                <span v-if="row.tags.length > 2" class="tag-more">+{{ row.tags.length - 2 }}</span>
              </div>
              <span v-else class="text-muted">-</span>
            </template>
          </el-table-column>

          <el-table-column label="附件" width="80" align="center">
            <template #default="{ row }">
              <span v-if="row.attachmentCount" class="attach-badge">
                <el-icon><Paperclip /></el-icon>
                {{ row.attachmentCount }}
              </span>
              <span v-else class="text-muted">0</span>
            </template>
          </el-table-column>

          <el-table-column label="更新时间" width="155" align="center">
            <template #default="{ row }">
              <span class="date-text">{{ formatDate(row.updatedAt) }}</span>
            </template>
          </el-table-column>

          <el-table-column label="操作" width="140" align="center" fixed="right">
            <template #default="{ row }">
              <div class="table-actions">
                <el-button size="small" type="primary" link @click="openDetail(row)">详情</el-button>
                <el-button size="small" type="danger" link @click="handleDelete(row)">删除</el-button>
              </div>
            </template>
          </el-table-column>
        </el-table>
      </div>

      <!-- Mobile Card List -->
      <div class="mobile-note-list visible-mobile" v-loading="loading">
        <div v-if="notes.length === 0 && !loading" class="empty-state">
          <el-empty description="暂无符合条件的笔记" :image-size="70" />
        </div>

        <div v-for="note in notes" :key="note.id" class="mobile-note-card" @click="openDetail(note)">
          <div class="mobile-note-header">
            <div class="mobile-title-row">
              <el-tag v-if="note.isPinned" type="danger" size="small" effect="dark" class="pin-tag">置顶</el-tag>
              <span class="mobile-note-title">{{ note.title || '（无标题）' }}</span>
            </div>
            <el-tag v-if="note.categoryName" size="small" type="primary" effect="plain">
              {{ note.categoryName }}
            </el-tag>
          </div>

          <p class="mobile-note-preview">{{ note.contentPreview || '（无预览内容）' }}</p>

          <div class="mobile-note-tags" v-if="note.tags && note.tags.length">
            <el-tag v-for="t in note.tags" :key="t" size="small" effect="light" class="tag-chip">
              #{{ t }}
            </el-tag>
          </div>

          <div class="mobile-note-footer">
            <div class="mobile-author-box">
              <span class="mobile-author-name">{{ note.userDisplayName || note.userEmail }}</span>
              <span class="mobile-time">{{ formatDate(note.updatedAt) }}</span>
            </div>

            <div class="mobile-card-btns" @click.stop>
              <el-button size="small" type="primary" plain @click="openDetail(note)">查看</el-button>
              <el-button size="small" type="danger" plain @click="handleDelete(note)">删除</el-button>
            </div>
          </div>
        </div>
      </div>

      <!-- Pagination -->
      <div class="pagination-wrapper">
        <el-pagination
          v-model:current-page="page"
          v-model:page-size="pageSize"
          :total="total"
          :page-sizes="[10, 20, 50]"
          :layout="isMobile ? 'prev, pager, next' : 'total, sizes, prev, pager, next, jumper'"
          :small="isMobile"
          @size-change="load"
          @current-change="load"
        />
      </div>
    </el-card>

    <!-- Note Detail Drawer (Mobile Responsive) -->
    <el-drawer
      v-model="detailVisible"
      :title="detail?.title || '笔记详情'"
      :size="isMobile ? '92%' : '560px'"
      direction="rtl"
      class="note-detail-drawer"
    >
      <div v-if="detail" class="detail-container">
        <!-- Author info block -->
        <div class="detail-user-card">
          <el-avatar :size="40" class="detail-avatar">
            {{ (detail.userDisplayName || detail.userEmail || 'U').charAt(0).toUpperCase() }}
          </el-avatar>
          <div class="detail-user-info">
            <div class="detail-user-name">{{ detail.userDisplayName || '未设昵称' }}</div>
            <div class="detail-user-email">{{ detail.userEmail }}</div>
          </div>
          <div v-if="detail.backgroundColor" class="color-indicator" :style="{ background: detail.backgroundColor }" title="卡片主题色"></div>
        </div>

        <!-- Meta tags -->
        <div class="detail-meta-box">
          <div class="meta-row">
            <span class="meta-label">所属分类：</span>
            <el-tag v-if="detail.categoryName" size="small" type="primary">{{ detail.categoryName }}</el-tag>
            <span v-else class="text-muted">未分类</span>
          </div>

          <div class="meta-row">
            <span class="meta-label">关联标签：</span>
            <div class="tags-group" v-if="detail.tags && detail.tags.length">
              <el-tag v-for="t in detail.tags" :key="t" size="small" effect="plain"># {{ t }}</el-tag>
            </div>
            <span v-else class="text-muted">无标签</span>
          </div>

          <div class="meta-dates">
            <div>创建时间：{{ formatDate(detail.createdAt) }}</div>
            <div>最近更新：{{ formatDate(detail.updatedAt) }}</div>
          </div>
        </div>

        <el-divider content-position="left">正文内容</el-divider>

        <div class="detail-content-box">
          <pre class="content-text">{{ detail.content || '（该笔记暂无正文内容）' }}</pre>
        </div>

        <!-- Attachments Section -->
        <template v-if="detail.attachments && detail.attachments.length">
          <el-divider content-position="left">附件清单 ({{ detail.attachments.length }})</el-divider>
          <div class="attachments-list">
            <div v-for="att in detail.attachments" :key="att.id" class="attachment-item">
              <el-icon class="att-icon"><Paperclip /></el-icon>
              <div class="att-info">
                <div class="att-name">{{ att.fileName }}</div>
                <div class="att-size">{{ formatSize(att.size) }}</div>
              </div>
              <a :href="`/api/attachments/${att.id}`" target="_blank" class="att-download-btn">
                <el-icon><Download /></el-icon>
              </a>
            </div>
          </div>
        </template>
      </div>
    </el-drawer>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted, computed } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Search, Refresh, Paperclip, Download } from '@element-plus/icons-vue'
import http from '../api/http'

const notes = ref([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(10)
const loading = ref(false)

const filter = reactive({
  userId: null,
  q: '',
  range: null
})

const userOptions = ref([])
const detailVisible = ref(false)
const detail = ref(null)

const isMobile = computed(() => {
  return typeof window !== 'undefined' ? window.innerWidth <= 768 : false
})

function formatDate(dt) {
  if (!dt) return '-'
  const d = new Date(dt)
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')} ${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`
}

function formatSize(bytes) {
  if (bytes == null || isNaN(bytes)) return '-'
  if (bytes < 1024) return bytes + ' B'
  const units = ['KB', 'MB', 'GB']
  let size = bytes
  let unit = -1
  do {
    size /= 1024
    unit++
  } while (size >= 1024 && unit < units.length - 1)
  return size.toFixed(1) + ' ' + units[unit]
}

async function loadUserOptions() {
  try {
    const data = await http.get('/admin/users', { params: { pageSize: 200 } })
    userOptions.value = data.items || []
  } catch (err) {
    console.error('Failed to load users for filter:', err)
  }
}

async function load() {
  loading.value = true
  try {
    const params = { page: page.value, pageSize: pageSize.value }
    if (filter.userId) params.userId = filter.userId
    if (filter.q) params.q = filter.q.trim()
    if (filter.range && filter.range.length === 2) {
      params.from = filter.range[0] + 'T00:00:00Z'
      params.to = filter.range[1] + 'T23:59:59Z'
    }
    const data = await http.get('/admin/notes', { params })
    notes.value = data.items || []
    total.value = data.total || 0
  } catch (err) {
    ElMessage.error(err.message || '加载笔记列表失败')
  } finally {
    loading.value = false
  }
}

function handleSearch() {
  page.value = 1
  load()
}

function handleReset() {
  filter.userId = null
  filter.q = ''
  filter.range = null
  handleSearch()
}

async function openDetail(row) {
  detail.value = null
  detailVisible.value = true
  try {
    detail.value = await http.get(`/admin/notes/${row.id}`)
  } catch (err) {
    ElMessage.error('获取笔记详情失败')
  }
}

async function handleDelete(row) {
  try {
    await ElMessageBox.confirm(
      `确定要删除笔记【${row.title || '无标题'}】吗？该笔记的历史快照与附件将一并删除。`,
      '删除确认',
      {
        confirmButtonText: '确定删除',
        cancelButtonText: '取消',
        type: 'error'
      }
    )
    await http.delete(`/admin/notes/${row.id}`)
    ElMessage.success('笔记已删除')
    load()
    if (detailVisible.value && detail.value?.id === row.id) {
      detailVisible.value = false
    }
  } catch (err) {
    if (err !== 'cancel') {
      ElMessage.error(err.message || '删除失败')
    }
  }
}

onMounted(() => {
  loadUserOptions()
  load()
})
</script>

<style scoped>
.page-container {
  max-width: 1200px;
  margin: 0 auto;
}

.box-card {
  border-radius: var(--admin-radius-md);
}

.toolbar-box {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 20px;
  flex-wrap: wrap;
}

.filter-row {
  display: flex;
  align-items: center;
  gap: 12px;
  flex: 1;
  flex-wrap: wrap;
}

.user-select {
  width: 180px;
}

.search-input {
  flex: 1;
  min-width: 200px;
}

.date-picker {
  width: 250px;
}

.filter-btn-row {
  display: flex;
  align-items: center;
  gap: 8px;
}

/* Note title in table */
.note-title-cell {
  display: flex;
  align-items: center;
  gap: 6px;
}

.pin-tag {
  font-size: 11px;
  padding: 0 4px;
}

.note-title-text {
  font-weight: 600;
  color: #1e293b;
  cursor: pointer;
  transition: color 0.15s ease;
}

.note-title-text:hover {
  color: var(--admin-primary);
}

.note-preview-text {
  font-size: 12px;
  color: #64748b;
  margin-top: 3px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  max-width: 320px;
}

.author-meta {
  display: flex;
  flex-direction: column;
}

.author-name {
  font-size: 13px;
  font-weight: 500;
  color: #1e293b;
}

.author-email {
  font-size: 11px;
  color: #94a3b8;
}

.tags-wrap {
  display: flex;
  align-items: center;
  gap: 4px;
  flex-wrap: wrap;
}

.tag-chip {
  font-size: 11px;
}

.tag-more {
  font-size: 11px;
  color: #94a3b8;
}

.attach-badge {
  display: inline-flex;
  align-items: center;
  gap: 3px;
  color: #6366f1;
  font-weight: 600;
  font-size: 12px;
}

.date-text {
  font-size: 12px;
  color: #64748b;
}

.text-muted {
  color: #cbd5e1;
  font-size: 12px;
}

.table-actions {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
}

/* Mobile Note List View */
.mobile-note-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.mobile-note-card {
  background: #ffffff;
  border: 1px solid var(--admin-border);
  border-radius: 10px;
  padding: 14px;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.04);
  cursor: pointer;
  transition: transform 0.15s ease, border-color 0.15s ease;
}

.mobile-note-card:active {
  background: #f8fafc;
}

.mobile-note-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  margin-bottom: 6px;
}

.mobile-title-row {
  display: flex;
  align-items: center;
  gap: 6px;
  flex: 1;
  min-width: 0;
}

.mobile-note-title {
  font-size: 14px;
  font-weight: 600;
  color: #0f172a;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.mobile-note-preview {
  margin: 0 0 10px 0;
  font-size: 12.5px;
  color: #64748b;
  line-height: 1.5;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.mobile-note-tags {
  display: flex;
  gap: 6px;
  flex-wrap: wrap;
  margin-bottom: 10px;
}

.mobile-note-footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  border-top: 1px dashed var(--admin-border);
  padding-top: 10px;
}

.mobile-author-box {
  display: flex;
  flex-direction: column;
}

.mobile-author-name {
  font-size: 12px;
  font-weight: 500;
  color: #334155;
}

.mobile-time {
  font-size: 11px;
  color: #94a3b8;
}

.mobile-card-btns {
  display: flex;
  gap: 6px;
}

/* Detail Drawer Styles */
.detail-container {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.detail-user-card {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 12px;
  background: #f8fafc;
  border-radius: 10px;
  border: 1px solid var(--admin-border);
}

.detail-avatar {
  background: linear-gradient(135deg, #6366f1 0%, #4f46e5 100%);
  color: #fff;
  font-weight: 600;
}

.detail-user-info {
  flex: 1;
}

.detail-user-name {
  font-size: 14px;
  font-weight: 600;
  color: #0f172a;
}

.detail-user-email {
  font-size: 12px;
  color: #64748b;
}

.color-indicator {
  width: 24px;
  height: 24px;
  border-radius: 50%;
  border: 2px solid #ffffff;
  box-shadow: 0 0 0 1px #cbd5e1;
}

.detail-meta-box {
  display: flex;
  flex-direction: column;
  gap: 8px;
  font-size: 13px;
}

.meta-row {
  display: flex;
  align-items: center;
  gap: 8px;
}

.meta-label {
  color: #64748b;
  font-size: 12.5px;
}

.tags-group {
  display: flex;
  gap: 6px;
  flex-wrap: wrap;
}

.meta-dates {
  margin-top: 4px;
  font-size: 11.5px;
  color: #94a3b8;
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.detail-content-box {
  background: #f8fafc;
  border-radius: 8px;
  padding: 16px;
  border: 1px solid var(--admin-border);
  max-height: 400px;
  overflow-y: auto;
}

.content-text {
  margin: 0;
  white-space: pre-wrap;
  word-break: break-word;
  font-family: inherit;
  font-size: 13.5px;
  line-height: 1.7;
  color: #1e293b;
}

.attachments-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.attachment-item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 12px;
  background: #ffffff;
  border: 1px solid var(--admin-border);
  border-radius: 8px;
}

.att-icon {
  color: #6366f1;
  font-size: 18px;
}

.att-info {
  flex: 1;
  min-width: 0;
}

.att-name {
  font-size: 13px;
  font-weight: 500;
  color: #1e293b;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.att-size {
  font-size: 11px;
  color: #94a3b8;
}

.att-download-btn {
  color: #6366f1;
  padding: 6px;
  border-radius: 6px;
  background: #eef2ff;
  display: flex;
  align-items: center;
  justify-content: center;
  text-decoration: none;
}

.pagination-wrapper {
  margin-top: 20px;
  display: flex;
  justify-content: flex-end;
}

@media (max-width: 768px) {
  .user-select {
    width: 100%;
  }
  .search-input {
    width: 100%;
  }
  .filter-btn-row {
    width: 100%;
    display: grid;
    grid-template-columns: 1fr 1fr;
  }
  .filter-btn-row .el-button {
    width: 100%;
  }
  .pagination-wrapper {
    justify-content: center;
  }
}
</style>
