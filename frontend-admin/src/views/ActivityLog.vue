<template>
  <div class="page-container">
    <el-card class="box-card" shadow="never">
      <!-- Filter Bar -->
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

          <div class="filter-item action-select">
            <el-select v-model="filter.action" placeholder="行为类型" clearable class="w-full">
              <el-option
                v-for="a in actionOptions"
                :key="a.value"
                :label="a.label"
                :value="a.value"
              />
            </el-select>
          </div>

          <div class="filter-item search-input">
            <el-input
              v-model="filter.q"
              placeholder="搜索用户名或笔记标题..."
              clearable
              :prefix-icon="Search"
              @keyup.enter="handleSearch"
              @clear="handleSearch"
            />
          </div>

          <div class="filter-item date-picker hidden-mobile">
            <el-date-picker
              v-model="filter.range"
              type="datetimerange"
              range-separator="至"
              start-placeholder="起始时间"
              end-placeholder="截止时间"
              value-format="YYYY-MM-DDTHH:mm:ss"
            />
          </div>
        </div>

        <div class="filter-btn-row">
          <el-button type="primary" :icon="Search" @click="handleSearch">查询</el-button>
          <el-button :icon="Refresh" @click="handleReset">重置</el-button>
          <el-button type="danger" plain :icon="Delete" @click="handleClear" :loading="clearing">
            清空日志
          </el-button>
        </div>
      </div>

      <!-- Desktop Table -->
      <div class="desktop-table hidden-mobile">
        <el-table v-loading="loading" :data="activities" stripe style="width: 100%">
          <el-table-column prop="createdAt" label="时间" width="170" align="center">
            <template #default="{ row }">
              <span class="date-text">{{ formatDate(row.createdAt) }}</span>
            </template>
          </el-table-column>

          <el-table-column label="用户" width="150">
            <template #default="{ row }">
              <div class="user-cell">
                <span class="user-name">{{ row.userName || (row.userId ? '已注销用户' : '（未登录）') }}</span>
              </div>
            </template>
          </el-table-column>

          <el-table-column label="行为" width="130" align="center">
            <template #default="{ row }">
              <el-tag :type="actionTagType(row.action)" size="small" effect="light">
                {{ actionLabel(row.action) }}
              </el-tag>
            </template>
          </el-table-column>

          <el-table-column label="对象" min-width="220">
            <template #default="{ row }">
              <span class="entity-text">{{ entityLabel(row) }}</span>
            </template>
          </el-table-column>

          <el-table-column prop="ip" label="IP" width="140" align="center">
            <template #default="{ row }">
              <span class="text-muted">{{ row.ip || '-' }}</span>
            </template>
          </el-table-column>

          <el-table-column label="浏览器" width="160" align="center">
            <template #default="{ row }">
              <el-tooltip :content="row.userAgent || ''" placement="top" :disabled="!row.userAgent">
                <span class="ua-text">{{ shortUa(row.userAgent) }}</span>
              </el-tooltip>
            </template>
          </el-table-column>
        </el-table>
      </div>

      <!-- Mobile Card List -->
      <div class="mobile-card-list visible-mobile" v-loading="loading">
        <div v-if="activities.length === 0 && !loading" class="empty-state">
          <el-empty description="暂无日志记录" :image-size="70" />
        </div>
        <div v-for="item in activities" :key="item.id" class="mobile-activity-card">
          <div class="mobile-card-header">
            <el-tag :type="actionTagType(item.action)" size="small" effect="light">
              {{ actionLabel(item.action) }}
            </el-tag>
            <span class="mobile-time">{{ formatDate(item.createdAt) }}</span>
          </div>
          <div class="mobile-user">{{ item.userName || (item.userId ? '已注销用户' : '（未登录）') }}</div>
          <div class="mobile-entity">{{ entityLabel(item) }}</div>
          <div class="mobile-meta">{{ item.ip || '-' }}</div>
        </div>
      </div>

      <!-- Pagination -->
      <div class="pagination-wrapper">
        <el-pagination
          v-model:current-page="page"
          v-model:page-size="pageSize"
          :total="total"
          :page-sizes="[10, 20, 50, 100]"
          :layout="isMobile ? 'prev, pager, next' : 'total, sizes, prev, pager, next, jumper'"
          :small="isMobile"
          @size-change="loadActivities"
          @current-change="loadActivities"
        />
      </div>
    </el-card>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted, computed } from 'vue'
import { useRoute } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Search, Refresh, Delete } from '@element-plus/icons-vue'
import http from '../api/http'

const route = useRoute()

const loading = ref(false)
const clearing = ref(false)
const activities = ref([])
const total = ref(0)
const userOptions = ref([])
const actionOptions = ref([])

const page = ref(1)
const pageSize = ref(20)

const filter = reactive({
  userId: '',
  action: null,
  q: '',
  range: null
})

const isMobile = computed(() => {
  return typeof window !== 'undefined' ? window.innerWidth <= 768 : false
})

// ===== 行为类型标签与展示 =====
const ACTION_META = {
  Login: { label: '登录成功', type: 'success' },
  LoginFailed: { label: '登录失败', type: 'danger' },
  LoginDenied: { label: '登录被拒', type: 'warning' },
  Logout: { label: '登出', type: 'info' },
  NoteCreate: { label: '新建笔记', type: 'success' },
  NoteUpdate: { label: '修改笔记', type: 'primary' },
  NoteDelete: { label: '删除笔记', type: 'danger' },
  NoteRestore: { label: '恢复版本', type: 'warning' },
  NotePin: { label: '置顶笔记', type: 'warning' },
  NoteUnpin: { label: '取消置顶', type: 'info' },
  NoteView: { label: '浏览笔记', type: 'info' },
  AttachmentUpload: { label: '上传附件', type: 'primary' },
  AttachmentDelete: { label: '删除附件', type: 'danger' },
  CategoryCreate: { label: '新建分类', type: 'success' },
  CategoryUpdate: { label: '修改分类', type: 'primary' },
  CategoryDelete: { label: '删除分类', type: 'danger' },
  TagCreate: { label: '新建标签', type: 'success' },
  TagDelete: { label: '删除标签', type: 'danger' },
  ProfileUpdate: { label: '修改资料', type: 'primary' },
  AdminSetStatus: { label: '禁用/解封', type: 'warning' },
  AdminResetPassword: { label: '重置密码', type: 'warning' },
  AdminDeleteUser: { label: '删除用户', type: 'danger' },
  AdminDeleteNote: { label: '删除笔记', type: 'danger' },
  Search: { label: '搜索', type: 'info' }
}

const ENTITY_LABEL = {
  0: '',
  1: '笔记',
  2: '分类',
  3: '标签',
  4: '附件',
  5: '用户',
  6: '资料'
}

function actionLabel(action) {
  const key = Object.keys(ACTION_META).find(
    (k) => ACTION_META[k] && actionNameToValue(k) === action
  )
  return key ? ACTION_META[key].label : `#${action}`
}

function actionTagType(action) {
  const key = Object.keys(ACTION_META).find(
    (k) => ACTION_META[k] && actionNameToValue(k) === action
  )
  return key ? ACTION_META[key].type : 'info'
}

// 前端静态映射：枚举 value 与名称对应（与后端 ActivityAction 保持一致）
const ACTION_VALUE_MAP = {
  Login: 1, LoginFailed: 2, LoginDenied: 3, Logout: 4,
  NoteCreate: 10, NoteUpdate: 11, NoteDelete: 12, NoteRestore: 13,
  NotePin: 14, NoteUnpin: 15, NoteView: 16,
  AttachmentUpload: 20, AttachmentDelete: 21,
  CategoryCreate: 30, CategoryUpdate: 31, CategoryDelete: 32,
  TagCreate: 40, TagDelete: 41,
  ProfileUpdate: 50,
  AdminSetStatus: 60, AdminResetPassword: 61, AdminDeleteUser: 62, AdminDeleteNote: 63,
  Search: 70
}

function actionNameToValue(name) {
  return ACTION_VALUE_MAP[name]
}

function entityLabel(row) {
  const typeName = ENTITY_LABEL[row.entityType] || ''
  if (!typeName) {
    // 无实体类型时，仅显示行为本身；登录类不显示对象
    return ''
  }
  const title = row.entityTitle
  return title ? `${typeName}《${title}》` : `${typeName}`
}

function shortUa(ua) {
  if (!ua) return '-'
  // 提取浏览器/系统关键信息（去 token 等冗余）
  const m = ua.match(/(Chrome\/[\d.]+|Firefox\/[\d.]+|Safari\/[\d.]+|Edg\/[\d.]+|MSIE [\d.]+)/)
  return m ? m[1] : ua.slice(0, 40)
}

function formatDate(isoStr) {
  if (!isoStr) return '-'
  const d = new Date(isoStr)
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')} ${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}:${String(d.getSeconds()).padStart(2, '0')}`
}

// ===== 加载 =====
async function loadActions() {
  try {
    const res = await http.get('/admin/activities/actions')
    actionOptions.value = (res || []).map((a) => ({
      value: a.value,
      label: ACTION_META[a.label]?.label || a.label
    }))
  } catch {
    /* 忽略：下拉为空则用静态映射 */
  }
}

async function loadUsers() {
  try {
    const res = await http.get('/admin/users', { params: { page: 1, pageSize: 100 } })
    userOptions.value = res.items || []
  } catch {
    /* 忽略 */
  }
}

async function loadActivities() {
  loading.value = true
  try {
    const params = {
      page: page.value,
      pageSize: pageSize.value
    }
    if (filter.userId) params.userId = filter.userId
    if (filter.action != null && filter.action !== '') params.action = filter.action
    if (filter.q) params.q = filter.q
    if (filter.range && filter.range.length === 2) {
      params.start = filter.range[0]
      params.end = filter.range[1]
    }
    const res = await http.get('/admin/activities', { params })
    activities.value = res.items || []
    total.value = res.total || 0
  } catch (err) {
    ElMessage.error(err.message || '加载日志失败')
  } finally {
    loading.value = false
  }
}

function handleSearch() {
  page.value = 1
  loadActivities()
}

function handleReset() {
  filter.userId = ''
  filter.action = null
  filter.q = ''
  filter.range = null
  page.value = 1
  loadActivities()
}

async function handleClear() {
  try {
    await ElMessageBox.confirm('确定清空所有日志记录吗？此操作不可恢复。', '危险操作确认', {
      confirmButtonText: '确定清空',
      cancelButtonText: '取消',
      type: 'error'
    })
    clearing.value = true
    await http.delete('/admin/activities')
    ElMessage.success('日志已清空')
    loadActivities()
  } catch (err) {
    if (err !== 'cancel') {
      ElMessage.error(err.message || '清空失败')
    }
  } finally {
    clearing.value = false
  }
}

onMounted(() => {
  // 支持从用户管理页「记录」按钮跳转，预置 userId
  const presetUserId = route.query.userId
  if (presetUserId) {
    filter.userId = presetUserId
  }
  loadActions()
  loadUsers()
  loadActivities()
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
  margin-bottom: 20px;
}

.filter-row {
  display: flex;
  align-items: center;
  gap: 12px;
  flex-wrap: wrap;
  margin-bottom: 12px;
}

.filter-item {
  flex: 1;
  min-width: 160px;
}

.filter-item.user-select,
.filter-item.action-select {
  max-width: 200px;
}

.filter-item.search-input {
  min-width: 220px;
}

.filter-item.date-picker {
  min-width: 320px;
}

.filter-btn-row {
  display: flex;
  align-items: center;
  gap: 8px;
}

.user-cell {
  display: flex;
  align-items: center;
}

.user-name {
  font-weight: 600;
  color: #1e293b;
  font-size: 13px;
}

.entity-text {
  font-size: 13px;
  color: #334155;
}

.date-text {
  font-size: 12px;
  color: #64748b;
}

.ua-text {
  font-size: 12px;
  color: #64748b;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  display: inline-block;
  max-width: 140px;
}

.text-muted {
  color: #94a3b8;
  font-size: 12px;
}

/* Mobile */
.mobile-card-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.mobile-activity-card {
  background: #ffffff;
  border: 1px solid var(--admin-border);
  border-radius: 10px;
  padding: 12px;
}

.mobile-card-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 8px;
}

.mobile-time {
  font-size: 11px;
  color: #94a3b8;
}

.mobile-user {
  font-size: 13px;
  font-weight: 600;
  color: #0f172a;
  margin-bottom: 4px;
}

.mobile-entity {
  font-size: 12.5px;
  color: #334155;
  margin-bottom: 4px;
}

.mobile-meta {
  font-size: 11px;
  color: #94a3b8;
}

.pagination-wrapper {
  margin-top: 20px;
  display: flex;
  justify-content: flex-end;
}

@media (max-width: 768px) {
  .filter-item {
    min-width: 100%;
    max-width: 100% !important;
  }
  .filter-btn-row {
    width: 100%;
  }
  .pagination-wrapper {
    justify-content: center;
  }
}
</style>
