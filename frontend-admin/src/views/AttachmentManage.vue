<template>
  <div class="page-card">
    <div class="toolbar">
      <el-select v-model="filter.userId" placeholder="全部用户" clearable style="width: 200px">
        <el-option v-for="u in userOptions" :key="u.id" :label="u.displayName || u.email" :value="u.id" />
      </el-select>
      <el-input v-model="filter.q" placeholder="搜索文件名" clearable style="width: 220px" @keyup.enter="handleSearch" @clear="handleSearch">
        <template #prefix><el-icon><Search /></el-icon></template>
      </el-input>
      <el-date-picker v-model="filter.range" type="daterange" range-separator="至" start-placeholder="开始时间"
        end-placeholder="结束时间" value-format="YYYY-MM-DD" style="width: 260px" />
      <el-button type="primary" @click="handleSearch">查询</el-button>
      <el-button @click="handleReset">重置</el-button>
    </div>

    <el-table :data="attachments" v-loading="loading" stripe>
      <el-table-column label="文件名" prop="fileName" min-width="220" show-overflow-tooltip />
      <el-table-column label="大小" width="100">
        <template #default="{ row }">{{ formatSize(row.size) }}</template>
      </el-table-column>
      <el-table-column label="类型" prop="contentType" min-width="130" show-overflow-tooltip />
      <el-table-column label="所属笔记" prop="noteTitle" min-width="160" show-overflow-tooltip />
      <el-table-column label="所属用户" min-width="150" show-overflow-tooltip>
        <template #default="{ row }">{{ row.userDisplayName || row.userEmail }}</template>
      </el-table-column>
      <el-table-column label="上传时间" width="160">
        <template #default="{ row }">{{ formatDate(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column label="操作" width="140" align="center">
        <template #default="{ row }">
          <el-button size="small" @click="handleDownload(row)">下载</el-button>
          <el-button size="small" type="danger" plain @click="handleDelete(row)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>

    <div class="pager">
      <el-pagination background layout="total, prev, pager, next" :total="total"
        :page-size="pageSize" :current-page="page" @current-change="handlePage" />
    </div>

    <!-- 孤立文件清理区块 -->
    <el-divider content-position="left">孤立文件清理</el-divider>
    <div class="orphan-bar">
      <el-button :loading="scanning" @click="handleScan">扫描孤立文件</el-button>
      <span v-if="orphanSummary" class="orphan-summary">
        发现 {{ orphanSummary.count }} 个孤立文件，共 {{ formatSize(orphanSummary.totalSize) }}
      </span>
      <el-button v-if="orphanSummary && orphanSummary.count > 0" type="danger" plain :loading="cleaning" @click="handleClean">
        一键清理
      </el-button>
    </div>
    <el-table v-if="orphans.length" :data="orphans" size="small" stripe class="orphan-table">
      <el-table-column prop="relativePath" label="路径" show-overflow-tooltip />
      <el-table-column label="大小" width="100">
        <template #default="{ row }">{{ formatSize(row.size) }}</template>
      </el-table-column>
      <el-table-column label="最后修改" width="170">
        <template #default="{ row }">{{ formatDate(row.lastWriteTimeUtc) }}</template>
      </el-table-column>
    </el-table>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import http from '../api/http'

const attachments = ref([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(10)
const loading = ref(false)

const filter = reactive({ userId: null, q: '', range: null })
const userOptions = ref([])

const scanning = ref(false)
const cleaning = ref(false)
const orphanSummary = ref(null)
const orphans = ref([])

function formatDate(dt) {
  if (!dt) return '-'
  return new Date(dt).toLocaleString('zh-CN', { hour12: false })
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
  const data = await http.get('/admin/users', { params: { pageSize: 200 } })
  userOptions.value = data.items
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
    attachments.value = data.items
    total.value = data.total
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

function handlePage(p) {
  page.value = p
  load()
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
      `确定要删除附件「${row.fileName}」吗？删除后不可恢复，服务器上的物理文件也会一并删除。`,
      '删除确认',
      { type: 'error', confirmButtonText: '删除', cancelButtonText: '取消' }
    )
  } catch {
    return
  }
  try {
    await http.delete(`/admin/attachments/${row.id}`)
    ElMessage.success('已删除')
    load()
  } catch (e) {
    ElMessage.error(e.response?.data?.message || '删除失败')
  }
}

async function handleScan() {
  scanning.value = true
  try {
    const data = await http.get('/admin/attachments/orphans')
    orphanSummary.value = { count: data.count, totalSize: data.totalSize }
    orphans.value = data.items
    if (data.count === 0) {
      ElMessage.success('未发现孤立文件')
    }
  } finally {
    scanning.value = false
  }
}

async function handleClean() {
  try {
    await ElMessageBox.confirm(
      `确定要清理 ${orphanSummary.value.count} 个孤立文件（共 ${formatSize(orphanSummary.value.totalSize)}）吗？` +
      '这些文件在数据库中已无对应记录，清理后不可恢复。',
      '清理确认',
      { type: 'error', confirmButtonText: '清理', cancelButtonText: '取消' }
    )
  } catch {
    return
  }
  cleaning.value = true
  try {
    const data = await http.delete('/admin/attachments/orphans')
    ElMessage.success(`已清理 ${data.count} 个孤立文件`)
    orphanSummary.value = { count: 0, totalSize: 0 }
    orphans.value = []
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
.page-card {
  background: #fff;
  border-radius: 8px;
  padding: 16px;
}
.toolbar {
  display: flex;
  gap: 12px;
  margin-bottom: 16px;
  flex-wrap: wrap;
}
.pager {
  margin-top: 16px;
  display: flex;
  justify-content: flex-end;
}
.orphan-bar {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 12px;
}
.orphan-summary {
  color: #e6a23c;
  font-size: 14px;
}
.orphan-table {
  margin-top: 4px;
}
</style>
