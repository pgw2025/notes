<template>
  <div class="page-container">
    <el-card class="box-card" shadow="never">
      <!-- Search Toolbar -->
      <div class="toolbar-box">
        <div class="search-input-box">
          <el-input
            v-model="query"
            placeholder="搜索分类名称或用户邮箱..."
            clearable
            :prefix-icon="Search"
            @keyup.enter="handleSearch"
            @clear="handleSearch"
          />
        </div>
        <div class="toolbar-actions">
          <el-button type="primary" :icon="Search" @click="handleSearch">搜索</el-button>
          <el-button :icon="Refresh" @click="handleReset">重置</el-button>
        </div>
      </div>

      <!-- Desktop Table -->
      <div class="desktop-table hidden-mobile">
        <el-table v-loading="loading" :data="categories" stripe style="width: 100%">
          <el-table-column label="分类名称" min-width="180">
            <template #default="{ row }">
              <div class="category-cell">
                <el-icon class="cat-icon"><FolderOpened /></el-icon>
                <span class="cat-name">{{ row.name }}</span>
              </div>
            </template>
          </el-table-column>

          <el-table-column label="所属用户" min-width="180">
            <template #default="{ row }">
              <div class="user-meta">
                <div class="u-name">{{ row.userDisplayName || '-' }}</div>
                <div class="u-email">{{ row.userEmail }}</div>
              </div>
            </template>
          </el-table-column>

          <el-table-column label="笔记数量" width="110" align="center">
            <template #default="{ row }">
              <el-tag size="small" type="info" effect="light">{{ row.noteCount }} 篇</el-tag>
            </template>
          </el-table-column>

          <el-table-column label="创建时间" width="165" align="center">
            <template #default="{ row }">
              <span class="date-text">{{ formatDate(row.createdAt) }}</span>
            </template>
          </el-table-column>

          <el-table-column label="操作" width="160" align="center" fixed="right">
            <template #default="{ row }">
              <div class="table-actions">
                <el-button size="small" type="primary" link @click="openRename(row)">重命名</el-button>
                <el-button size="small" type="danger" link @click="handleDelete(row)">删除</el-button>
              </div>
            </template>
          </el-table-column>
        </el-table>
      </div>

      <!-- Mobile Card List -->
      <div class="mobile-card-list visible-mobile" v-loading="loading">
        <div v-if="categories.length === 0 && !loading" class="empty-state">
          <el-empty description="暂无分类数据" :image-size="70" />
        </div>

        <div v-for="cat in categories" :key="cat.id" class="mobile-cat-card">
          <div class="mobile-cat-header">
            <div class="cat-title-box">
              <el-icon class="cat-icon-mobile"><FolderOpened /></el-icon>
              <span class="mobile-cat-name">{{ cat.name }}</span>
            </div>
            <el-tag size="small" type="info">{{ cat.noteCount }} 篇笔记</el-tag>
          </div>

          <div class="mobile-cat-meta">
            <div class="meta-row">
              <span class="meta-label">归属用户：</span>
              <span class="meta-val">{{ cat.userDisplayName || cat.userEmail }}</span>
            </div>
            <div class="meta-row">
              <span class="meta-label">创建时间：</span>
              <span class="meta-val">{{ formatDate(cat.createdAt) }}</span>
            </div>
          </div>

          <div class="mobile-cat-actions">
            <el-button size="small" type="primary" plain @click="openRename(cat)">重命名</el-button>
            <el-button size="small" type="danger" plain @click="handleDelete(cat)">删除分类</el-button>
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

    <!-- Rename Dialog -->
    <el-dialog v-model="renameVisible" title="重命名分类" width="400px">
      <el-form label-position="top" @submit.prevent>
        <div class="dialog-tip">
          修改用户 <strong>{{ renameTarget?.userDisplayName || renameTarget?.userEmail }}</strong> 的分类名称
        </div>
        <el-form-item label="分类新名称">
          <el-input
            v-model="renameName"
            maxlength="50"
            placeholder="请输入新分类名称"
            clearable
            @keyup.enter="handleRename"
          />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="renameVisible = false">取消</el-button>
        <el-button type="primary" :loading="renameLoading" @click="handleRename">保存修改</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, onMounted, computed } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Search, Refresh } from '@element-plus/icons-vue'
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

const isMobile = computed(() => {
  return typeof window !== 'undefined' ? window.innerWidth <= 768 : false
})

function formatDate(dt) {
  if (!dt) return '-'
  const d = new Date(dt)
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')} ${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`
}

async function load() {
  loading.value = true
  try {
    const data = await http.get('/admin/categories', {
      params: { q: query.value || undefined, page: page.value, pageSize: pageSize.value }
    })
    categories.value = data.items || []
    total.value = data.total || 0
  } catch (err) {
    ElMessage.error(err.message || '加载分类列表失败')
  } finally {
    loading.value = false
  }
}

function handleSearch() {
  page.value = 1
  load()
}

function handleReset() {
  query.value = ''
  page.value = 1
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
    ElMessage.success('分类已重命名')
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
      `确定要删除分类【${row.name}】吗？删除后用户【${name}】的笔记将变为未分类。`,
      '删除确认',
      { type: 'warning', confirmButtonText: '确定删除', cancelButtonText: '取消' }
    )
    await http.delete(`/admin/categories/${row.id}`)
    ElMessage.success('分类已删除')
    load()
  } catch (err) {
    if (err !== 'cancel') {
      ElMessage.error(err.response?.data?.message || '删除失败')
    }
  }
}

onMounted(load)
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

.search-input-box {
  flex: 1;
  min-width: 200px;
  max-width: 380px;
}

.toolbar-actions {
  display: flex;
  align-items: center;
  gap: 8px;
}

.category-cell {
  display: flex;
  align-items: center;
  gap: 8px;
}

.cat-icon {
  color: #0ea5e9;
  font-size: 18px;
}

.cat-name {
  font-weight: 600;
  color: #1e293b;
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

.table-actions {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
}

/* Mobile card list */
.mobile-card-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.mobile-cat-card {
  background: #ffffff;
  border: 1px solid var(--admin-border);
  border-radius: 10px;
  padding: 14px;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.04);
}

.mobile-cat-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 8px;
}

.cat-title-box {
  display: flex;
  align-items: center;
  gap: 6px;
}

.cat-icon-mobile {
  color: #0ea5e9;
  font-size: 16px;
}

.mobile-cat-name {
  font-size: 14px;
  font-weight: 600;
  color: #0f172a;
}

.mobile-cat-meta {
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

.mobile-cat-actions {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 8px;
}

.dialog-tip {
  background: #f8fafc;
  padding: 8px 12px;
  border-radius: 6px;
  font-size: 12.5px;
  color: #475569;
  margin-bottom: 14px;
  border-left: 3px solid #0ea5e9;
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
  .toolbar-actions {
    width: 100%;
    display: grid;
    grid-template-columns: 1fr 1fr;
  }
  .toolbar-actions .el-button {
    width: 100%;
  }
  .pagination-wrapper {
    justify-content: center;
  }
}
</style>
