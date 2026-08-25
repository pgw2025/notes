<template>
  <div class="page-card">
    <div class="toolbar">
      <el-select v-model="filter.userId" placeholder="全部用户" clearable style="width: 200px">
        <el-option v-for="u in userOptions" :key="u.id" :label="u.displayName || u.email" :value="u.id" />
      </el-select>
      <el-input v-model="filter.q" placeholder="搜索标题 / 内容" clearable style="width: 240px" @keyup.enter="handleSearch" @clear="handleSearch">
        <template #prefix><el-icon><Search /></el-icon></template>
      </el-input>
      <el-date-picker v-model="filter.range" type="daterange" range-separator="至" start-placeholder="开始时间"
        end-placeholder="结束时间" value-format="YYYY-MM-DD" style="width: 260px" />
      <el-button type="primary" @click="handleSearch">查询</el-button>
      <el-button @click="handleReset">重置</el-button>
    </div>

    <el-table :data="notes" v-loading="loading" stripe>
      <el-table-column label="标题" prop="title" min-width="200" show-overflow-tooltip />
      <el-table-column label="作者" min-width="150" show-overflow-tooltip>
        <template #default="{ row }">{{ row.userDisplayName || row.userEmail }}</template>
      </el-table-column>
      <el-table-column label="分类" min-width="110">
        <template #default="{ row }">
          <el-tag v-if="row.categoryName" size="small" type="info">{{ row.categoryName }}</el-tag>
          <span v-else>-</span>
        </template>
      </el-table-column>
      <el-table-column label="标签" min-width="150">
        <template #default="{ row }">
          <template v-if="row.tags && row.tags.length">
            <el-tag v-for="t in row.tags.slice(0, 3)" :key="t" size="small" style="margin-right: 4px">{{ t }}</el-tag>
            <span v-if="row.tags.length > 3">+{{ row.tags.length - 3 }}</span>
          </template>
          <span v-else>-</span>
        </template>
      </el-table-column>
      <el-table-column label="附件" prop="attachmentCount" width="70" align="center" />
      <el-table-column label="更新时间" width="160">
        <template #default="{ row }">{{ formatDate(row.updatedAt) }}</template>
      </el-table-column>
      <el-table-column label="操作" width="140" align="center">
        <template #default="{ row }">
          <el-button size="small" @click="openDetail(row)">查看</el-button>
          <el-button size="small" type="danger" plain @click="handleDelete(row)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>

    <div class="pager">
      <el-pagination background layout="total, prev, pager, next" :total="total"
        :page-size="pageSize" :current-page="page" @current-change="handlePage" />
    </div>

    <!-- 笔记详情抽屉 -->
    <el-drawer v-model="detailVisible" :title="detail?.title || '笔记详情'" size="520px">
      <div v-if="detail" class="detail">
        <div class="detail-user">
          <el-avatar :size="32" class="d-avatar">{{ (detail.userDisplayName || detail.userEmail || '?').charAt(0).toUpperCase() }}</el-avatar>
          <div>
            <div class="d-name">{{ detail.userDisplayName || '未知用户' }}</div>
            <div class="d-meta">{{ detail.userEmail }}</div>
          </div>
        </div>
        <div class="detail-row">
          <el-tag v-if="detail.categoryName" size="small" type="info">{{ detail.categoryName }}</el-tag>
          <el-tag v-for="t in detail.tags" :key="t" size="small" style="margin-right: 4px">{{ t }}</el-tag>
        </div>
        <div class="detail-time">创建：{{ formatDate(detail.createdAt) }}　更新：{{ formatDate(detail.updatedAt) }}</div>
        <el-divider />
        <div class="detail-content">{{ detail.content || '（无正文内容）' }}</div>
        <template v-if="detail.attachments && detail.attachments.length">
          <el-divider />
          <div class="detail-attach">
            <div class="attach-title">附件（{{ detail.attachments.length }}）</div>
            <el-table :data="detail.attachments" size="small">
              <el-table-column prop="fileName" label="文件名" show-overflow-tooltip />
              <el-table-column prop="size" label="大小" width="100">
                <template #default="{ row }">{{ formatSize(row.size) }}</template>
              </el-table-column>
            </el-table>
          </div>
        </template>
      </div>
    </el-drawer>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import http from '../api/http'

const notes = ref([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(10)
const loading = ref(false)

const filter = reactive({ userId: null, q: '', range: null })

const userOptions = ref([])
const detailVisible = ref(false)
const detail = ref(null)

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
    const data = await http.get('/admin/notes', { params })
    notes.value = data.items
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

async function openDetail(row) {
  detail.value = null
  detailVisible.value = true
  detail.value = await http.get(`/admin/notes/${row.id}`)
}

async function handleDelete(row) {
  try {
    await ElMessageBox.confirm(`确定要删除笔记「${row.title}」吗？删除后不可恢复，其附件也会一并删除。`, '删除确认', {
      type: 'error', confirmButtonText: '删除', cancelButtonText: '取消'
    })
  } catch {
    return
  }
  try {
    await http.delete(`/admin/notes/${row.id}`)
    ElMessage.success('已删除')
    load()
  } catch (e) {
    ElMessage.error(e.response?.data?.message || '删除失败')
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
.detail-user {
  display: flex;
  align-items: center;
  gap: 12px;
}
.d-avatar {
  background-color: #667eea;
  color: #fff;
}
.d-name {
  font-weight: 500;
}
.d-meta {
  font-size: 12px;
  color: #909399;
}
.detail-row {
  margin-top: 12px;
}
.detail-time {
  margin-top: 8px;
  font-size: 12px;
  color: #909399;
}
.detail-content {
  white-space: pre-wrap;
  line-height: 1.7;
  color: #303133;
}
.attach-title {
  font-weight: 500;
  margin-bottom: 8px;
}
</style>