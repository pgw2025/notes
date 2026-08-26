<template>
  <div class="page-container">
    <el-card class="box-card" shadow="never">
      <!-- Search Toolbar -->
      <div class="toolbar-box">
        <div class="search-input-box">
          <el-input
            v-model="query"
            placeholder="搜索标签名称或用户邮箱..."
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
        <el-table v-loading="loading" :data="tags" stripe style="width: 100%">
          <el-table-column label="标签名称" min-width="160">
            <template #default="{ row }">
              <el-tag size="small" effect="plain" class="tag-badge">
                # {{ row.name }}
              </el-tag>
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

          <el-table-column label="关联笔记" width="110" align="center">
            <template #default="{ row }">
              <el-tag size="small" type="info" effect="light">{{ row.noteCount }} 篇</el-tag>
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
        <div v-if="tags.length === 0 && !loading" class="empty-state">
          <el-empty description="暂无标签数据" :image-size="70" />
        </div>

        <div v-for="tag in tags" :key="tag.id" class="mobile-tag-card">
          <div class="mobile-tag-header">
            <el-tag size="default" effect="plain" class="tag-badge-lg">
              # {{ tag.name }}
            </el-tag>
            <el-tag size="small" type="info">{{ tag.noteCount }} 篇笔记</el-tag>
          </div>

          <div class="mobile-tag-meta">
            <div class="meta-row">
              <span class="meta-label">创建用户：</span>
              <span class="meta-val">{{ tag.userDisplayName || tag.userEmail }}</span>
            </div>
          </div>

          <div class="mobile-tag-actions">
            <el-button size="small" type="primary" plain @click="openRename(tag)">重命名</el-button>
            <el-button size="small" type="danger" plain @click="handleDelete(tag)">删除标签</el-button>
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
    <el-dialog v-model="renameVisible" title="重命名标签" width="400px">
      <el-form label-position="top" @submit.prevent>
        <div class="dialog-tip">
          修改用户 <strong>{{ renameTarget?.userDisplayName || renameTarget?.userEmail }}</strong> 的标签名称
        </div>
        <el-form-item label="标签新名称">
          <el-input
            v-model="renameName"
            maxlength="30"
            placeholder="请输入新标签名称"
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
const tags = ref([])
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

async function load() {
  loading.value = true
  try {
    const data = await http.get('/admin/tags', {
      params: { q: query.value || undefined, page: page.value, pageSize: pageSize.value }
    })
    tags.value = data.items || []
    total.value = data.total || 0
  } catch (err) {
    ElMessage.error(err.message || '加载标签列表失败')
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
    ElMessage.warning('标签名称不能为空')
    return
  }
  renameLoading.value = true
  try {
    await http.put(`/admin/tags/${renameTarget.value.id}`, { name: renameName.value.trim() })
    renameVisible.value = false
    ElMessage.success('标签已重命名')
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
      `确定要删除标签【${row.name}】吗？删除后该标签将从用户【${name}】的所有笔记中移除。`,
      '删除确认',
      { type: 'warning', confirmButtonText: '确定删除', cancelButtonText: '取消' }
    )
    await http.delete(`/admin/tags/${row.id}`)
    ElMessage.success('标签已删除')
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

.tag-badge {
  font-size: 13px;
  font-weight: 500;
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

.mobile-tag-card {
  background: #ffffff;
  border: 1px solid var(--admin-border);
  border-radius: 10px;
  padding: 14px;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.04);
}

.mobile-tag-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 10px;
}

.tag-badge-lg {
  font-size: 14px;
  font-weight: 600;
}

.mobile-tag-meta {
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

.mobile-tag-actions {
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
  border-left: 3px solid #6366f1;
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
