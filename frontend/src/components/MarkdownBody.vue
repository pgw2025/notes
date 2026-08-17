<template>
  <div class="markdown-body" v-html="html"></div>
</template>

<script setup>
import { computed } from 'vue'
import { marked } from 'marked'
import markedKatex from 'marked-katex-extension'
import DOMPurify from 'dompurify'
import 'katex/dist/katex.min.css'

const props = defineProps({
  content: { type: String, default: '' }
})

marked.setOptions({ breaks: true, gfm: true })
marked.use(markedKatex({
  throwOnError: false,  // 公式有错时降级显示源码，不报错
  nonStandard: true     // 允许 $ 前后无空格的行内公式（如 $U=IR$）
}))

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

  // DOMPurify 默认会过滤 KaTeX 生成的 MathML 标签和属性，这里添加允许规则
  return DOMPurify.sanitize(raw, {
    ADD_TAGS: [
      'math', 'maction', 'maligngroup', 'malignmark', 'menclose',
      'merror', 'mfenced', 'mfrac', 'mi', 'mlongdiv', 'mmultiscripts',
      'mn', 'mo', 'mover', 'mpadded', 'mphantom', 'mroot', 'mrow',
      'mscarries', 'mscarry', 'msgroup', 'msline', 'mspace', 'msqrt',
      'mstack', 'mstyle', 'msub', 'msup', 'msubsup', 'mtable', 'mtd',
      'mtext', 'mtr', 'munder', 'munderover', 'semantics', 'annotation',
      'annotation-xml'
    ],
    ADD_ATTR: ['mathvariant', 'accent', 'accentunder', 'bevelled',
      'close', 'columnsalign', 'columnsline', 'columnspan', 'denomalign',
      'depth', 'dir', 'displaystyle', 'encoding', 'fence', 'frame',
      'height', 'href', 'id', 'largeop', 'length', 'linethickness',
      'lspace', 'lquote', 'mathbackground', 'mathcolor', 'mathsize',
      'mathvariant', 'maxsize', 'minsize', 'mode', 'movablelimits',
      'notation', 'numalign', 'open', 'rowalign', 'rowsline', 'rowspan',
      'rspace', 'rquote', 'scriptlevel', 'scriptsizemultiplier',
      'scriptminsize', 'separator', 'separators', 'shift',
      'src', 'subscriptshift', 'supscriptshift',
      'symmetric', 'voffset', 'width', 'xmlns', 'xlink:href'
    ]
  })
})
</script>
