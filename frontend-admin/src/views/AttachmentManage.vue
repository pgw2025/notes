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
              placeholder="搜索附件文件名..."
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
        <el-table v-loading="loading" :data="attachments" stripe style="width: 100%">
          <el-table-column label="文件名" min-width="220">
            <template #default="{ row }">
              <div class="file-cell">
                <el-icon class="file-icon"><Document /></el-icon>
                <span class="file-name" :title="row.fileName">{{ row.fileName }}</span>
              </div>
            </template>
          </el-table-column>

          <el-table-column label="大小" width="100">
            <template #default="{ row }">
              <span class="size-badge">{{ formatSize(row.size) }}</span>
            </template>
          </el-table-column>

          <el-table-column label="类型" prop="contentType" min-width="130" show-overflow-tooltip />

          <el-table-column label="所属笔记" prop="noteTitle" min-width="160" show-overflow-tooltip>
            <template #default="{ row }">
              <span v-if="row.noteTitle" class="note-link-text">{{ row.noteTitle }}</span>
              <span v-else class="text-muted">（已脱离或无标题）</span>
            </template>
          </el-table-column>

          <el-table-column label="所属用户" min-width="160">
            <template #default="{ row }">
              <div class="user-meta">
                <div class="u-name">{{ row.userDisplayName || '-' }}</div>
                <div class="u-email">{{ row.userEmail }}</div>
              </div>
            </template>
          </el-table-column>

          <el-table-column label="上传时间" width="165" align="center">
            <template #default="{ row }">
              <span class="date-text">{{ formatDate(row.createdAt) }}</span>
            </template>
          </el-table-column>

          <el-table-column label="操作" width="150" align="center" fixed="right">
            <template #default="{ row }">
              <div class="table-actions">
                <el-button size="small" type="primary" link @click="handleDownload(row)">下载</el-button>
                <el-button size="small" type="danger" link @click="handleDelete(row)">删除</el-button>
              </div>
            </template>
          </el-table-column>
        </el-table>
      </div>

      <!-- Mobile Card List -->
      <div class="mobile-card-list visible-mobile" v-loading="loading">
        <div v-if="attachments.length === 0 && !loading" class="empty-state">
          <el-empty description="暂无附件文件" :image-size="70" />
        </div>

        <div v-for="att in attachments" :key="att.id" class="mobile-att-card">
          <div class="mobile-att-header">
            <div class="att-name-row">
              <el-icon class="file-icon-mobile"><Paperclip /></el-icon>
              <span class="mobile-file-name">{{ att.fileName }}</span>
            </div>
            <span class="mobile-size-badge">{{ formatSize(att.size) }}</span>
          </div>

          <div class="mobile-att-meta">
            <div class="meta-row">
              <span class="meta-label">所属笔记：</span>
              <span class="meta-val">{{ att.noteTitle || '（未关联）' }}</span>
            </div>
            <div class="meta-row">
              <span class="meta-label">上传用户：</span>
              <span class="meta-val">{{ att.userDisplayName || att.userEmail }}</span>
            </div>
            <div class="meta-row">
              <span class="meta-label">上传日期：</span>
              <span class="meta-val">{{ formatDate(att.createdAt) }}</span>
            </div>
          </div>

          <div class="mobile-att-actions">
            <el-button size="small" type="primary" plain @click="handleDownload(att)">下载文件</el-button>
            <el-button size="small" type="danger" plain @click="handleDelete(att)">删除文件</el-button>
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

      <!-- Orphan File Cleanup Section -->
      <el-divider content-position="left">
        <div class="divider-title">
          <el-icon><Brush /></el-icon>
          <span>孤立磁盘文件排查与清理</span>
        </div>
      </el-divider>

      <div class="orphan-panel">
        <div class="orphan-control-bar">
          <el-button type="primary" plain :loading="scanning" :icon="Search" @click="handleScan">
            扫描未被引用的孤立文件
          </el-button>
          
          <div v-if="orphanSummary" class="orphan-stats-badge" :class="{ 'has-orphans': orphanSummary.count > 0 }">
            <span v-if="orphanSummary.count > 0">
              ⚠️ 发现 <strong>{{ orphanSummary.count }}</strong> 个孤立文件，共占用 <strong>{{ formatSize(orphanSummary.totalSize) }}</strong> 空间
            </span>
            <span v-else>
              ✅ 暂未发现孤立文件，磁盘存储很健康
            </span>
          </div>

          <el-button
            v-if="orphanSummary && orphanSummary.count > 0"
            type="danger"
            :loading="cleaning"
            :icon="Delete"
            @click="handleClean"
          >
            一键清理全部孤立文件
          </el-button>
        </div>

        <div v-if="orphans.length" class="orphan-list-wrapper">
          <el-table :data="orphans" size="small" stripe>
            <el-table-column prop="relativePath" label="文件相对路径" min-width="240" show-overflow-tooltip />
            <el-table-column label="大小" width="100">
              <template #default="{ row }">{{ formatSize(row.size) }}</template>
            </el-table-column>
            <el-table-column label="修改时间" width="170">
              <template #default="{ row }">{{ formatDate(row.lastWriteTimeUtc) }}</template>
            </el-table-column>
          </el-table>
        </div>
      </div>
    </el-card>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted, computed } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Search, Refresh, Delete, Paperclip, Document, Brush } from '@element-plus/icons-vue'
import http from '../api/http'

const attachments = ref([])
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
const scanning = ref(false)
const cleaning = ref(false)
const orphanSummary = ref(null)
const orphans = ref([])

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
    const data = await http.get('/admin/attachments', { params })
    attachments.value = data.items || []
    total.value = data.total || 0
  } catch (err) {
    ElMessage.error(err.message || '加载附件列表失败')
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

async function handleDownload(row) {
  try {
    const blob = await http.get(`/admin/attachments/${row.id}/download`, { responseType: 'blob' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = row.fileName
    document.body.appendChild(a)
    a.click()
    document.body.removeChild(a)
    URL.revokeObjectURL(url)
  } catch (e) {
    ElMessage.error(e.response?.data?.message || '下载失败')
  }
}

async function handleDelete(row) {
  try {
    await ElMessageBox.confirm(
      `确定要删除附件【${row.fileName}】吗？删除后相关笔记的引用将失效。`,
      '删除确认',
      { type: 'error', confirmButtonText: '确定删除', cancelButtonText: '取消' }
    )
    await http.delete(`/admin/attachments/${row.id}`)
    ElMessage.success('附件已删除')
    load()
  } catch (err) {
    if (err !== 'cancel') {
      ElMessage.error(err.response?.data?.message || '删除失败')
    }
  }
}

async function handleScan() {
  scanning.value = true
  try {
    const data = await http.get('/admin/attachments/orphans/scan')
    orphanSummary.value = data.summary
    orphans.value = data.orphans || []
    ElMessage.success(`扫描完成，发现 ${data.summary.count} 个孤立文件`)
  } catch (err) {
    ElMessage.error(err.response?.data?.message || '扫描孤立文件失败')
  } finally {
    scanning.value = false
  }
}

async function handleClean() {
  if (!orphanSummary.value || orphanSummary.value.count === 0) return
  try {
    await ElMessageBox.confirm(
      `确定要永久清理 ${orphanSummary.value.count} 个孤立文件吗？清理将释放 ${formatSize(orphanSummary.value.totalSize)} 磁盘空间。`,
      '清理确认',
      { type: 'warning', confirmButtonText: '立即清理', cancelButtonText: '取消' }
    )
    cleaning.value = true
    const res = await http.post('/admin/attachments/orphans/clean')
    ElMessage.success(`成功清理 ${res.deletedCount || orphanSummary.value.count} 个孤立文件`)
    orphanSummary.value = null
    orphans.value = []
  } catch (err) {
    if (err !== 'cancel') {
      ElMessage.error(err.response?.data?.message || '清理失败')
    }
  } finally {
    cleaning.value = false
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

.file-cell {
  display: flex;
  align-items: center;
  gap: 8px;
}

.file-icon {
  color: #f59e0b;
  font-size: 18px;
  flex-shrink: 0;
}

.file-name {
  font-weight: 500;
  color: #1e293b;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.size-badge {
  font-size: 12px;
  color: #64748b;
  font-weight: 500;
}

.note-link-text {
  color: #4f46e5;
  font-size: 12.5px;
}

.user-meta {
  display: flex;
  flex-direction: column;
}

.u-name {
  font-size: 13px;
  font-weight: 500;
  color: #1e293b;
}

.u-email {
  font-size: 11px;
  color: #94a3b8;
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

/* Mobile Card List */
.mobile-card-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.mobile-att-card {
  background: #ffffff;
  border: 1px solid var(--admin-border);
  border-radius: 10px;
  padding: 14px;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.04);
}

.mobile-att-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  margin-bottom: 8px;
}

.att-name-row {
  display: flex;
  align-items: center;
  gap: 6px;
  flex: 1;
  min-width: 0;
}

.file-icon-mobile {
  color: #f59e0b;
  font-size: 16px;
}

.mobile-file-name {
  font-size: 13.5px;
  font-weight: 600;
  color: #0f172a;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.mobile-size-badge {
  font-size: 11.5px;
  color: #64748b;
  background: #f1f5f9;
  padding: 2px 6px;
  border-radius: 4px;
  flex-shrink: 0;
}

.mobile-att-meta {
  background: #f8fafc;
  border-radius: 6px;
  padding: 8px 10px;
  display: flex;
  flex-direction: column;
  gap: 4px;
  font-size: 12px;
  margin-bottom: 10px;
}

.meta-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.meta-label {
  color: #64748b;
}

.meta-val {
  color: #1e293b;
  font-weight: 500;
}

.mobile-att-actions {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 8px;
}

.divider-title {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 14px;
  font-weight: 600;
  color: #334155;
}

.orphan-panel {
  margin-top: 14px;
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.orphan-control-bar {
  display: flex;
  align-items: center;
  gap: 12px;
  flex-wrap: wrap;
}

.orphan-stats-badge {
  padding: 8px 12px;
  border-radius: 6px;
  background: #ecfdf5;
  color: #065f46;
  font-size: 13px;
  border: 1px solid #a7f3d0;
}

.orphan-stats-badge.has-orphans {
  background: #fffbeb;
  color: #92400e;
  border-color: #fde68a;
}

.orphan-list-wrapper {
  margin-top: 8px;
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
  .orphan-control-bar {
    flex-direction: column;
    align-items: stretch;
  }
  .orphan-control-bar .el-button {
    width: 100%;
  }
  .pagination-wrapper {
    justify-content: center;
  }
}
</style>
