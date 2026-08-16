<template>
  <div class="page">
    <van-nav-bar title="标签">
      <template #right>
        <van-icon name="plus" size="22" @click="onAdd" />
      </template>
    </van-nav-bar>

    <div v-if="list.length" class="tag-cloud">
      <van-tag
        v-for="t in list"
        :key="t.id"
        size="large"
        plain
        round
        closeable
        color="#1989fa"
        @close="onDelete(t)"
      >
        {{ t.name }} · {{ t.noteCount }}
      </van-tag>
    </div>

    <van-empty v-else description="还没有标签，点击右上角添加" />

    <van-dialog
      v-model:show="showForm"
      title="新建标签"
      show-cancel-button
      :before-close="onBeforeClose"
    >
      <van-field
        v-model="formName"
        placeholder="请输入标签名称"
        maxlength="30"
        style="margin: 12px"
      />
    </van-dialog>
  </div>
</template>

<script setup>
import { ref, onActivated, onMounted } from 'vue'
import { showConfirmDialog, showToast } from 'vant'
import http from '../api/http'

const list = ref([])
const showForm = ref(false)
const formName = ref('')

async function load() {
  list.value = await http.get('/tags')
}

function onAdd() {
  formName.value = ''
  showForm.value = true
}

async function onBeforeClose(action) {
  if (action !== 'confirm') return true
  const name = formName.value.trim()
  if (!name) {
    showToast('请输入标签名称')
    return false
  }
  try {
    await http.post('/tags', { name })
    showToast('已创建')
    await load()
    return true
  } catch {
    return false
  }
}

async function onDelete(t) {
  try {
    await showConfirmDialog({
      title: '删除标签',
      message: `确定删除标签「${t.name}」吗？`
    })
    await http.delete(`/tags/${t.id}`)
    list.value = list.value.filter((x) => x.id !== t.id)
    showToast('已删除')
  } catch {
    // 取消
  }
}

onMounted(load)
onActivated(load)
</script>

<style scoped>
.page {
  min-height: 100vh;
  padding-bottom: 80px;
}
.tag-cloud {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
  padding: 16px;
}

/* 桌面端：居中阅读宽度 */
@media (min-width: 1024px) {
  .page {
    max-width: 720px;
    margin: 0 auto;
    padding-bottom: 32px;
  }
}
</style>
