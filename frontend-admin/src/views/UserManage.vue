<template>
  <div class="page-card">
    <div class="toolbar">
      <el-input v-model="query" placeholder="搜索邮箱 / 昵称" clearable style="width: 260px" @keyup.enter="handleSearch" @clear="handleSearch">
        <template #prefix><el-icon><Search /></el-icon></template>
      </el-input>
      <el-button type="primary" @click="handleSearch">搜索</el-button>
    </div>

    <el-table :data="users" v-loading="loading" stripe>
      <el-table-column label="用户" min-width="220">
        <template #default="{ row }">
          <div class="user-cell">
            <el-avatar :size="34" :src="row.avatarUrl || undefined" class="u-avatar">{{ (row.displayName || row.email).charAt(0).toUpperCase() }}</el-avatar>
            <div>
              <div class="u-name">{{ row.displayName || '-' }}</div>
              <div class="u-email">{{ row.email }}</div>
            </div>
          </div>
        </template>
      </el-table-column>
      <el-table-column label="笔记数" prop="noteCount" width="90" align="center" />
      <el-table-column label="状态" width="90" align="center">
        <template #default="{ row }">
          <el-tag :type="row.lockedOut ? 'danger' : 'success'" size="small">
            {{ row.lockedOut ? '已禁用' : '正常' }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column label="注册时间" width="170">
        <template #default="{ row }">{{ formatDate(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column label="操作" width="180" align="center">
        <template #default="{ row }">
          <el-button size="small" @click="openResetPwd(row)">重置密码</el-button>
          <el-button v-if="!row.lockedOut" size="small" type="danger" plain @click="toggleStatus(row, true)">禁用</el-button>
          <el-button v-else size="small" type="success" plain @click="toggleStatus(row, false)">启用</el-button>
        </template>
      </el-table-column>
    </el-table>

    <div class="pager">
      <el-pagination background layout="total, prev, pager, next" :total="total"
        :page-size="pageSize" :current-page="page" @current-change="handlePage" />
    </div>

    <!-- 重置密码弹窗 -->
    <el-dialog v-model="pwdVisible" title="重置密码" width="420px">
      <el-form label-width="90px">
        <el-form-item label="用户">
          <span>{{ pwdTarget ? (pwdTarget.displayName || pwdTarget.email) : '' }}</span>
        </el-form-item>
        <el-form-item label="新密码">
          <el-input v-model="newPassword" type="password" show-password placeholder="请输入新密码（至少 6 位）" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="pwdVisible = false">取消</el-button>
        <el-button type="primary" :loading="pwdLoading" @click="handleResetPwd">确定</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import http from '../api/http'

const query = ref('')
const users = ref([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(10)
const loading = ref(false)

const pwdVisible = ref(false)
const pwdLoading = ref(false)
const pwdTarget = ref(null)
const newPassword = ref('')

function formatDate(dt) {
  if (!dt) return '-'
  return new Date(dt).toLocaleString('zh-CN', { hour12: false })
}

async function load() {
  loading.value = true
  try {
    const data = await http.get('/admin/users', {
      params: { q: query.value || undefined, page: page.value, pageSize: pageSize.value }
    })
    users.value = data.items
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

async function toggleStatus(row, locked) {
  const name = row.displayName || row.email
  try {
    await ElMessageBox.confirm(
      locked ? `确定要禁用用户「${name}」吗？禁用后该账号将无法登录。` : `确定要启用用户「${name}」吗？`,
      '操作确认',
      { type: 'warning', confirmButtonText: '确定', cancelButtonText: '取消' }
    )
  } catch {
    return
  }
  try {
    await http.put(`/admin/users/${row.id}/status`, { locked })
    row.lockedOut = locked
    ElMessage.success(locked ? '已禁用' : '已启用')
  } catch (e) {
    ElMessage.error(e.response?.data?.message || '操作失败')
  }
}

function openResetPwd(row) {
  pwdTarget.value = row
  newPassword.value = ''
  pwdVisible.value = true
}

async function handleResetPwd() {
  if (!pwdTarget.value) return
  if (!newPassword.value || newPassword.value.length < 6) {
    ElMessage.warning('请输入至少 6 位的新密码')
    return
  }
  pwdLoading.value = true
  try {
    await http.put(`/admin/users/${pwdTarget.value.id}/password`, { newPassword: newPassword.value })
    pwdVisible.value = false
    ElMessage.success('密码已重置')
  } finally {
    pwdLoading.value = false
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
.user-cell {
  display: flex;
  align-items: center;
  gap: 10px;
}
.u-avatar {
  background-color: #667eea;
  color: #fff;
  flex-shrink: 0;
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