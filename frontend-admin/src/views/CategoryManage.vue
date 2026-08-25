<template>
  <div class="page-card">
    <div class="toolbar">
      <el-input v-model="query" placeholder="搜索分类名 / 用户邮箱" clearable style="width: 260px" @keyup.enter="handleSearch" @clear="handleSearch">
        <template #prefix><el-icon><Search /></el-icon></template>
      </el-input>
      <el-button type="primary" @click="handleSearch">搜索</el-button>
    </div>

    <el-table :data="categories" v-loading="loading" stripe>
      <el-table-column label="分类名" prop="name" min-width="180" show-overflow-tooltip />
      <el-table-column label="所属用户" min-width="180">
        <template #default="{ row }">
          <div class="u-cell">
            <div class="u-name">{{ row.userDisplayName || '-' }}</div>
            <div class="u-email">{{ row.userEmail }}</div>
          </div>
        </template>
      </el-table-column>
      <el-table-column label="笔记数" prop="noteCount" width="90" align="center" />
      <el-table-column label="创建时间" width="170">
        <template #default="{ row }">{{ formatDate(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column label="操作" width="160" align="center">
        <template #default="{ row }">
          <el-button size="small" @click="openRename(row)">重命名</el-button>
          <el-button size="small" type="danger" plain @click="handleDelete(row)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>

    <div class="pager">
      <el-pagination background layout="total, prev, pager, next" :total="total"
        :page-size="pageSize" :current-page="page" @current-change="handlePage" />
    </div>

    <!-- 重命名弹窗 -->
    <el-dialog v-model="renameVisible" title="重命名分类" width="420px">
      <el-form label-width="90px" @submit.prevent>
        <el-form-item label="所属用户">
          <span>{{ renameTarget ? (renameTarget.userDisplayName || renameTarget.userEmail) : '' }}</span>
        </el-form-item>
        <el-form-item label="分类名">
          <el-input v-model="renameName" maxlength="50" placeholder="请输入新名称" @keyup.enter="handleRename" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="renameVisible = false">取消</el-button>
        <el-button type="primary" :loading="renameLoading" @click="handleRename">确定</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import http from '../api/http'

const query = ref('')
const categories = ref([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(10)
const loading = ref(false)

const renameVisible = ref(false)
const renameLoading = ref(false)
const renameTarget = ref(null)
const renameName = ref('')

function formatDate(dt) {
  if (!dt) return '-'
  return new Date(dt).toLocaleString('zh-CN', { hour12: false })
}

async function load() {
  loading.value = true
  try {
    const data = await http.get('/admin/categories', {
      params: { q: query.value || undefined, page: page.value, pageSize: pageSize.value }
    })
    categories.value = data.items
    total.value = data.total
  } finally {
    loading.value = false
  }
}

function handleSearch() {
  page.value = 1
  load()
}

function handlePage(p) {
  page.value = p
  load()
}

function openRename(row) {
  renameTarget.value = row
  renameName.value = row.name
  renameVisible.value = true
}

async function handleRename() {
  if (!renameName.value || !renameName.value.trim()) {
    ElMessage.warning('分类名称不能为空')
    return
  }
  renameLoading.value = true
  try {
    await http.put(`/admin/categories/${renameTarget.value.id}`, { name: renameName.value.trim() })
    renameVisible.value = false
    ElMessage.success('已重命名')
    load()
  } catch (e) {
    ElMessage.error(e.response?.data?.message || '重命名失败')
  } finally {
    renameLoading.value = false
  }
}

async function handleDelete(row) {
  const name = row.userDisplayName || row.userEmail
  try {
    await ElMessageBox.confirm(
      `确定要删除分类「${row.name}」吗？删除后用户「${name}」的笔记将变为未分类。`,
      '删除确认',
      { type: 'warning', confirmButtonText: '删除', cancelButtonText: '取消' }
    )
  } catch {
    return
  }
  try {
    await http.delete(`/admin/categories/${row.id}`)
    ElMessage.success('已删除')
    load()
  } catch (e) {
    ElMessage.error(e.response?.data?.message || '删除失败')
  }
}

onMounted(load)
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
}
.u-name {
  font-weight: 500;
}
.u-email {
  font-size: 12px;
  color: #909399;
}
.pager {
  margin-top: 16px;
  display: flex;
  justify-content: flex-end;
}
</style>
