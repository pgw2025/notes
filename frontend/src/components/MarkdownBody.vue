<template>
  <div class="markdown-body" v-html="html"></div>
</template>

<script setup>
import { computed } from 'vue'
import { marked } from 'marked'
import DOMPurify from 'dompurify'

const props = defineProps({
  content: { type: String, default: '' }
})

marked.setOptions({ breaks: true, gfm: true })

const html = computed(() => {
  if (!props.content) return ''
  let raw = marked.parse(props.content)

  // 为附件图片 URL 注入访问令牌（<img> 无法设置请求头）
  const token = localStorage.getItem('token')
  if (token) {
    raw = raw.replace(
      /\/api\/attachments\/(\d+)(?!\?)/g,
      `/api/attachments/$1?access_token=${token}`
    )
  }

  return DOMPurify.sanitize(raw)
})
</script>
