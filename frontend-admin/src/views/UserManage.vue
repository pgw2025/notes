<template>
  <div class="page-container">
    <el-card class="box-card" shadow="never">
      <!-- Search & Action Bar -->
      <div class="filter-bar">
        <div class="search-input-box">
          <el-input
            v-model="query.q"
            placeholder="搜索邮箱或昵称..."
            clearable
            :prefix-icon="Search"
            @keyup.enter="handleSearch"
            @clear="handleSearch"
          />
        </div>
        <div class="filter-actions">
          <el-button type="primary" :icon="Search" @click="handleSearch">搜索</el-button>
          <el-button :icon="Refresh" @click="handleReset">重置</el-button>
        </div>
      </div>

      <!-- Desktop Table View -->
      <div class="desktop-table hidden-mobile">
        <el-table v-loading="loading" :data="users" stripe style="width: 100%">
          <el-table-column prop="id" label="ID" width="70" align="center" />
          
          <el-table-column label="用户信息" min-width="180">
            <template #default="{ row }">
              <div class="user-cell">
                <el-avatar :size="34" :src="row.avatarUrl" class="cell-avatar">
                  {{ (row.displayName || row.email || 'U').charAt(0).toUpperCase() }}
                </el-avatar>
                <div class="user-names">
                  <div class="user-nick">{{ row.displayName || '-' }}</div>
                  <div class="user-mail">{{ row.email }}</div>
                </div>
              </div>
            </template>
          </el-table-column>

          <el-table-column label="角色" width="100" align="center">
            <template #default="{ row }">
              <el-tag :type="row.isAdmin ? 'danger' : 'info'" size="small" effect="light">
                {{ row.isAdmin ? '管理员' : '普通用户' }}
              </el-tag>
            </template>
          </el-table-column>

          <el-table-column prop="noteCount" label="笔记数" width="90" align="center">
            <template #default="{ row }">
              <span class="note-count-badge">{{ row.noteCount }} 篇</span>
            </template>
          </el-table-column>

          <el-table-column label="账号状态" width="100" align="center">
            <template #default="{ row }">
              <el-tag :type="row.lockedOut ? 'danger' : 'success'" size="small">
                {{ row.lockedOut ? '已锁定' : '正常' }}
              </el-tag>
            </template>
          </el-table-column>

          <el-table-column prop="createdAt" label="注册时间" width="160" align="center">
            <template #default="{ row }">
              <span class="date-text">{{ formatDate(row.createdAt) }}</span>
            </template>
          </el-table-column>

          <el-table-column label="操作" width="220" align="center" fixed="right">
            <template #default="{ row }">
              <div class="table-actions">
                <el-button
                  size="small"
                  :type="row.lockedOut ? 'success' : 'warning'"
                  link
                  @click="toggleLock(row)"
                >
                  {{ row.lockedOut ? '解封' : '禁用' }}
                </el-button>
                <el-button size="small" type="primary" link @click="openResetPwd(row)">
                  重置密码
                </el-button>
                <el-button
                  size="small"
                  type="danger"
                  link
                  :disabled="row.isAdmin"
                  @click="deleteUser(row)"
                >
                  删除
                </el-button>
              </div>
            </template>
          </el-table-column>
        </el-table>
      </div>

      <!-- Mobile Card List View -->
      <div class="mobile-card-list visible-mobile" v-loading="loading">
        <div v-if="users.length === 0 && !loading" class="empty-state">
          <el-empty description="暂无匹配用户" :image-size="70" />
        </div>
        
        <div v-for="user in users" :key="user.id" class="mobile-user-card">
          <div class="mobile-card-header">
            <div class="mobile-user-info">
              <el-avatar :size="36" :src="user.avatarUrl" class="cell-avatar">
                {{ (user.displayName || user.email || 'U').charAt(0).toUpperCase() }}
              </el-avatar>
              <div>
                <div class="mobile-user-name">
                  <span>{{ user.displayName || '未设昵称' }}</span>
                  <el-tag :type="user.isAdmin ? 'danger' : 'info'" size="small" effect="plain">
                    {{ user.isAdmin ? '管理员' : '普通用户' }}
                  </el-tag>
                </div>
                <div class="mobile-user-email">{{ user.email }}</div>
              </div>
            </div>
            <el-tag :type="user.lockedOut ? 'danger' : 'success'" size="small" effect="dark">
              {{ user.lockedOut ? '已禁用' : '正常' }}
            </el-tag>
          </div>

          <div class="mobile-card-meta">
            <div class="meta-item">
              <span class="meta-label">笔记数：</span>
              <span class="meta-val">{{ user.noteCount }} 篇</span>
            </div>
            <div class="meta-item">
              <span class="meta-label">注册于：</span>
              <span class="meta-val">{{ formatDate(user.createdAt) }}</span>
            </div>
          </div>

          <div class="mobile-card-actions">
            <el-button
              size="small"
              :type="user.lockedOut ? 'success' : 'warning'"
              plain
              @click="toggleLock(user)"
            >
              {{ user.lockedOut ? '解除禁用' : '禁用账号' }}
            </el-button>
            <el-button size="small" type="primary" plain @click="openResetPwd(user)">
              重置密码
            </el-button>
            <el-button
              size="small"
              type="danger"
              plain
              :disabled="user.isAdmin"
              @click="deleteUser(user)"
            >
              删除用户
            </el-button>
          </div>
        </div>
      </div>

      <!-- Pagination -->
      <div class="pagination-wrapper">
        <el-pagination
          v-model:current-page="query.page"
          v-model:page-size="query.pageSize"
          :total="total"
          :page-sizes="[10, 20, 50]"
          :layout="isMobile ? 'prev, pager, next' : 'total, sizes, prev, pager, next, jumper'"
          :small="isMobile"
          @size-change="loadUsers"
          @current-change="loadUsers"
        />
      </div>
    </el-card>

    <!-- Reset Password Dialog -->
    <el-dialog v-model="resetPwdDialog" title="重置用户密码" width="420px">
      <el-form ref="pwdFormRef" :model="pwdForm" :rules="pwdRules" label-position="top">
        <div class="target-user-tip">
          为用户 <strong>{{ targetUser?.displayName || targetUser?.email }}</strong> 设置新密码
        </div>
        <el-form-item label="新密码" prop="newPassword">
          <el-input
            v-model="pwdForm.newPassword"
            type="password"
            placeholder="请输入至少 6 位的新密码"
            show-password
          />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="resetPwdDialog = false">取消</el-button>
        <el-button type="primary" :loading="pwdLoading" @click="handleResetPwd">确认重置</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted, computed } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Search, Refresh } from '@element-plus/icons-vue'
import http from '../api/http'

const loading = ref(false)
const users = ref([])
const total = ref(0)

const query = reactive({
  page: 1,
  pageSize: 10,
  q: ''
})

const isMobile = computed(() => {
  return typeof window !== 'undefined' ? window.innerWidth <= 768 : false
})

const resetPwdDialog = ref(false)
const pwdLoading = ref(false)
const targetUser = ref(null)
const pwdFormRef = ref()
const pwdForm = reactive({
  newPassword: ''
})

const pwdRules = {
  newPassword: [
    { required: true, message: '请输入新密码', trigger: 'blur' },
    { min: 6, message: '密码长度不能少于 6 位', trigger: 'blur' }
  ]
}

function formatDate(isoStr) {
  if (!isoStr) return '-'
  const d = new Date(isoStr)
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')} ${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`
}

async function loadUsers() {
  loading.value = true
  try {
    const res = await http.get('/admin/users', { params: query })
    users.value = res.items || []
    total.value = res.total || 0
  } catch (err) {
    ElMessage.error(err.message || '加载用户列表失败')
  } finally {
    loading.value = false
  }
}

function handleSearch() {
  query.page = 1
  loadUsers()
}

function handleReset() {
  query.q = ''
  query.page = 1
  loadUsers()
}

async function toggleLock(user) {
  const willLock = !user.lockedOut
  try {
    await ElMessageBox.confirm(
      `确定要${willLock ? '禁用' : '解除禁用'}用户【${user.displayName || user.email}】吗？`,
      '状态变更确认',
      {
        confirmButtonText: '确定',
        cancelButtonText: '取消',
        type: willLock ? 'warning' : 'info'
      }
    )
    await http.put(`/admin/users/${user.id}/status`, { locked: willLock })
    ElMessage.success(`用户已${willLock ? '禁用' : '解除禁用'}`)
    user.lockedOut = willLock
  } catch (err) {
    if (err !== 'cancel') {
      ElMessage.error(err.message || '操作失败')
    }
  }
}

function openResetPwd(user) {
  targetUser.value = user
  pwdForm.newPassword = ''
  resetPwdDialog.value = true
}

async function handleResetPwd() {
  await pwdFormRef.value.validate()
  pwdLoading.value = true
  try {
    await http.put(`/admin/users/${targetUser.value.id}/password`, {
      newPassword: pwdForm.newPassword
    })
    ElMessage.success('密码重置成功')
    resetPwdDialog.value = false
  } catch (err) {
    ElMessage.error(err.message || '重置密码失败')
  } finally {
    pwdLoading.value = false
  }
}

async function deleteUser(user) {
  try {
    await ElMessageBox.confirm(
      `删除用户将同时清理该用户的所有笔记、分类和附件，操作不可逆。确定删除【${user.displayName || user.email}】吗？`,
      '危险操作确认',
      {
        confirmButtonText: '确定删除',
        cancelButtonText: '取消',
        type: 'error'
      }
    )
    await http.delete(`/admin/users/${user.id}`)
    ElMessage.success('用户已删除')
    loadUsers()
  } catch (err) {
    if (err !== 'cancel') {
      ElMessage.error(err.message || '删除失败')
    }
  }
}

onMounted(() => {
  loadUsers()
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

.filter-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 20px;
  flex-wrap: wrap;
}

.search-input-box {
  flex: 1;
  min-width: 200px;
  max-width: 380px;
}

.filter-actions {
  display: flex;
  align-items: center;
  gap: 8px;
}

/* User cell in table */
.user-cell {
  display: flex;
  align-items: center;
  gap: 10px;
}

.cell-avatar {
  background: linear-gradient(135deg, #6366f1 0%, #4f46e5 100%);
  color: #fff;
  font-weight: 600;
  font-size: 13px;
  flex-shrink: 0;
}

.user-names {
  display: flex;
  flex-direction: column;
}

.user-nick {
  font-weight: 600;
  color: #1e293b;
  font-size: 13px;
}

.user-mail {
  font-size: 12px;
  color: #64748b;
}

.note-count-badge {
  display: inline-block;
  padding: 2px 8px;
  background: #f1f5f9;
  border-radius: 12px;
  font-size: 12px;
  font-weight: 500;
  color: #334155;
}

.date-text {
  font-size: 12px;
  color: #64748b;
}

.table-actions {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 4px;
}

/* Mobile Card List */
.mobile-card-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.mobile-user-card {
  background: #ffffff;
  border: 1px solid var(--admin-border);
  border-radius: 10px;
  padding: 14px;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.04);
}

.mobile-card-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  margin-bottom: 10px;
}

.mobile-user-info {
  display: flex;
  align-items: center;
  gap: 10px;
}

.mobile-user-name {
  display: flex;
  align-items: center;
  gap: 6px;
  font-weight: 600;
  color: #0f172a;
  font-size: 13.5px;
}

.mobile-user-email {
  font-size: 11.5px;
  color: #64748b;
}

.mobile-card-meta {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8px 10px;
  background: #f8fafc;
  border-radius: 6px;
  font-size: 12px;
  margin-bottom: 12px;
}

.meta-label {
  color: #64748b;
}

.meta-val {
  font-weight: 500;
  color: #1e293b;
}

.mobile-card-actions {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 8px;
}

.mobile-card-actions .el-button {
  width: 100%;
  margin: 0;
  padding: 6px 4px;
  font-size: 11.5px;
}

.target-user-tip {
  background: #f8fafc;
  padding: 10px 12px;
  border-radius: 6px;
  font-size: 13px;
  color: #475569;
  margin-bottom: 16px;
  border-left: 3px solid #4f46e5;
}

.pagination-wrapper {
  margin-top: 20px;
  display: flex;
  justify-content: flex-end;
}

@media (max-width: 768px) {
  .search-input-box {
    max-width: 100%;
    width: 100%;
  }
  .filter-actions {
    width: 100%;
    display: grid;
    grid-template-columns: 1fr 1fr;
  }
  .filter-actions .el-button {
    width: 100%;
  }
  .pagination-wrapper {
    justify-content: center;
  }
}
</style>
