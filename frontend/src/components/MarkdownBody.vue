<template>
  <div
    ref="containerRef"
    class="markdown-body"
    :class="{ 'is-collapsible-enabled': collapsible }"
    v-html="html"
    @click="onContainerClick"
  ></div>
</template>

<script setup>
import { ref, computed, nextTick, watch, onUnmounted } from 'vue'
import { Marked } from 'marked'
import markedKatex from 'marked-katex-extension'
import DOMPurify from 'dompurify'
import 'katex/dist/katex.min.css'

const props = defineProps({
  content: { type: String, default: '' },
  collapsible: { type: Boolean, default: true }
})

const emit = defineEmits(['outline-change', 'collapse-change'])

const containerRef = ref(null)
const collapsedHeadingIds = ref(new Set())
const headingParentsMap = new Map()
const headingList = ref([])

let headingCounter = 0
let currentHeadings = []

const markedInstance = new Marked()
markedInstance.setOptions({ breaks: true, gfm: true })
markedInstance.use(markedKatex({
  throwOnError: false,  // 公式有错时降级显示源码，不报错
  nonStandard: true     // 允许 $ 前后无空格的行内公式（如 $U=IR$）
}))

markedInstance.use({
  renderer: {
    heading(token) {
      const id = `heading-${headingCounter++}`
      const rawText = token.text ? token.text.replace(/<[^>]+>/g, '').trim() : ''
      currentHeadings.push({
        id,
        level: token.depth,
        text: rawText
      })

      if (!props.collapsible) {
        return `<h${token.depth} id="${id}">${this.parser.parseInline(token.tokens)}</h${token.depth}>\n`
      }

      return `<h${token.depth} id="${id}" class="heading-collapsible" data-level="${token.depth}" data-heading-id="${id}">` +
        `<button type="button" class="heading-collapse-toggle" data-heading-id="${id}" aria-label="折叠/展开本章节" title="折叠/展开本章节">` +
        `<span class="heading-collapse-icon">▾</span>` +
        `</button>` +
        `<span class="heading-text">${this.parser.parseInline(token.tokens)}</span>` +
        `</h${token.depth}>\n`
    }
  }
})

const html = computed(() => {
  if (!props.content) {
    headingList.value = []
    emit('outline-change', [])
    return ''
  }
  headingCounter = 0
  currentHeadings = []
  let raw = markedInstance.parse(props.content)

  // 为附件图片 URL 注入访问令牌（<img> 无法设置请求头）
  const token = localStorage.getItem('token')
  if (token) {
    raw = raw.replace(
      /\/api\/attachments\/(\d+)(?!\?)/g,
      `/api/attachments/$1?access_token=${token}`
    )
  }

  headingList.value = [...currentHeadings]
  emit('outline-change', headingList.value)

  // DOMPurify 允许 MathML 和大纲折叠属性
  return DOMPurify.sanitize(raw, {
    ADD_TAGS: [
      'math', 'maction', 'maligngroup', 'malignmark', 'menclose',
      'merror', 'mfenced', 'mfrac', 'mi', 'mlongdiv', 'mmultiscripts',
      'mn', 'mo', 'mover', 'mpadded', 'mphantom', 'mroot', 'mrow',
      'mscarries', 'mscarry', 'msgroup', 'msline', 'mspace', 'msqrt',
      'mstack', 'mstyle', 'msub', 'msup', 'msubsup', 'mtable', 'mtd',
      'mtext', 'mtr', 'munder', 'munderover', 'semantics', 'annotation',
      'annotation-xml', 'button', 'span'
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
      'symmetric', 'voffset', 'width', 'xmlns', 'xlink:href',
      'class', 'data-level', 'data-heading-id', 'title', 'aria-label', 'type'
    ]
  })
})

function rebuildHeadingHierarchy() {
  const container = containerRef.value
  if (!container) return

  const stack = []
  headingParentsMap.clear()

  // 清理不存在的旧折叠 ID
  const existingIds = new Set()
  for (const child of Array.from(container.children)) {
    if (/^H[1-6]$/.test(child.tagName) && child.id) {
      existingIds.add(child.id)
    }
  }
  for (const cid of Array.from(collapsedHeadingIds.value)) {
    if (!existingIds.has(cid)) {
      collapsedHeadingIds.value.delete(cid)
    }
  }

  for (const child of Array.from(container.children)) {
    if (child.classList.contains('heading-fold-banner')) {
      continue
    }
    if (/^H[1-6]$/.test(child.tagName)) {
      const level = parseInt(child.tagName.substring(1), 10)
      while (stack.length > 0 && stack[stack.length - 1].level >= level) {
        stack.pop()
      }
      headingParentsMap.set(child, stack.map(s => s.id))
      stack.push({ id: child.id, level })
    } else {
      headingParentsMap.set(child, stack.map(s => s.id))
    }
  }

  applyCollapseState()
}

function applyCollapseState() {
  const container = containerRef.value
  if (!container) return

  const children = Array.from(container.children)
  for (const child of children) {
    if (child.classList.contains('heading-fold-banner')) {
      continue
    }

    const parents = headingParentsMap.get(child) || []
    const isHiddenByParent = parents.some(pid => collapsedHeadingIds.value.has(pid))

    if (/^H[1-6]$/.test(child.tagName)) {
      const isSelfCollapsed = collapsedHeadingIds.value.has(child.id)
      child.classList.toggle('is-collapsed', isSelfCollapsed)

      const icon = child.querySelector('.heading-collapse-icon')
      if (icon) {
        icon.textContent = isSelfCollapsed ? '▸' : '▾'
      }

      // 查找或创建折叠提示横幅
      const nextEl = child.nextElementSibling
      const banner = (nextEl && nextEl.classList.contains('heading-fold-banner') && nextEl.dataset.headingId === child.id) ? nextEl : null

      if (isSelfCollapsed && !isHiddenByParent) {
        child.style.display = ''
        if (!banner) {
          const newBanner = document.createElement('div')
          newBanner.className = 'heading-fold-banner'
          newBanner.dataset.headingId = child.id
          newBanner.title = '点击展开本章节'
          newBanner.innerHTML = `<span class="fold-banner-dots">···</span><span class="fold-banner-text">本章节内容已折叠</span><span class="fold-banner-action">展开 ▾</span>`
          newBanner.addEventListener('click', (e) => {
            e.stopPropagation()
            toggleHeading(child.id)
          })
          child.insertAdjacentElement('afterend', newBanner)
        } else {
          banner.style.display = ''
        }
      } else {
        if (banner) {
          banner.style.display = 'none'
        }
        child.style.display = isHiddenByParent ? 'none' : ''
      }
    } else {
      child.style.display = isHiddenByParent ? 'none' : ''
    }
  }

  emit('collapse-change', Array.from(collapsedHeadingIds.value))
}

function toggleHeading(headingId) {
  if (!headingId) return
  if (collapsedHeadingIds.value.has(headingId)) {
    collapsedHeadingIds.value.delete(headingId)
  } else {
    collapsedHeadingIds.value.add(headingId)
  }
  applyCollapseState()
}

function expandHeading(headingId) {
  if (!headingId) return
  const container = containerRef.value
  if (!container) return
  const headingEl = document.getElementById(headingId)
  if (headingEl) {
    const parents = headingParentsMap.get(headingEl) || []
    for (const pid of parents) {
      collapsedHeadingIds.value.delete(pid)
    }
  }
  collapsedHeadingIds.value.delete(headingId)
  applyCollapseState()
}

function foldAll() {
  for (const h of headingList.value) {
    collapsedHeadingIds.value.add(h.id)
  }
  applyCollapseState()
}

function unfoldAll() {
  collapsedHeadingIds.value.clear()
  applyCollapseState()
}

function onContainerClick(e) {
  if (!props.collapsible) return
  const toggleBtn = e.target.closest('.heading-collapse-toggle')
  if (toggleBtn) {
    e.preventDefault()
    e.stopPropagation()
    const headingId = toggleBtn.dataset.headingId
    toggleHeading(headingId)
    return
  }

  const heading = e.target.closest('.heading-collapsible')
  if (heading && !e.target.closest('a')) {
    const selection = window.getSelection()
    if (selection && selection.toString().trim().length > 0) {
      return
    }
    toggleHeading(heading.id)
  }
}

watch(html, () => {
  clearSearch()
  if (!props.collapsible) return
  nextTick(() => {
    rebuildHeadingHierarchy()
  })
}, { immediate: true })

// ================= 正文搜索与联动 =================
let searchMatches = []
let currentMatchIndex = -1

function clearSearch() {
  const container = containerRef.value
  if (!container) return
  const marks = container.querySelectorAll('mark.search-highlight')
  for (const mark of Array.from(marks)) {
    const parent = mark.parentNode
    if (parent) {
      while (mark.firstChild) {
        parent.insertBefore(mark.firstChild, mark)
      }
      parent.removeChild(mark)
    }
  }
  container.normalize()
  searchMatches = []
  currentMatchIndex = -1
}

function highlightSearch(query) {
  clearSearch()
  if (!query || !query.trim()) {
    return { total: 0, current: -1 }
  }
  const container = containerRef.value
  if (!container) {
    return { total: 0, current: -1 }
  }

  const q = query.trim().toLowerCase()
  const walker = document.createTreeWalker(
    container,
    NodeFilter.SHOW_TEXT,
    {
      acceptNode(node) {
        const parent = node.parentElement
        if (!parent) return NodeFilter.FILTER_REJECT
        const tag = parent.tagName
        if (tag === 'SCRIPT' || tag === 'STYLE' || tag === 'BUTTON') {
          return NodeFilter.FILTER_REJECT
        }
        if (
          parent.closest('.heading-collapse-toggle') ||
          parent.closest('.heading-fold-banner')
        ) {
          return NodeFilter.FILTER_REJECT
        }
        if (!node.nodeValue || !node.nodeValue.toLowerCase().includes(q)) {
          return NodeFilter.FILTER_SKIP
        }
        return NodeFilter.FILTER_ACCEPT
      }
    }
  )

  const textNodes = []
  while (walker.nextNode()) {
    textNodes.push(walker.currentNode)
  }

  const marks = []
  for (const node of textNodes) {
    let text = node.nodeValue
    let lower = text.toLowerCase()
    let index = lower.indexOf(q)
    if (index === -1) continue

    let currentNode = node
    while (index !== -1) {
      try {
        const matchNode = currentNode.splitText(index)
        const remainingNode = matchNode.splitText(q.length)

        const mark = document.createElement('mark')
        mark.className = 'search-highlight'
        mark.textContent = matchNode.nodeValue
        matchNode.parentNode.replaceChild(mark, matchNode)
        marks.push(mark)

        currentNode = remainingNode
        text = currentNode.nodeValue
        lower = text.toLowerCase()
        index = lower.indexOf(q)
      } catch {
        break
      }
    }
  }

  searchMatches = marks
  if (marks.length > 0) {
    currentMatchIndex = 0
    focusMatch(0)
    return { total: marks.length, current: 0 }
  } else {
    currentMatchIndex = -1
    return { total: 0, current: -1 }
  }
}

function focusMatch(index) {
  if (!searchMatches || searchMatches.length === 0) return
  if (index < 0 || index >= searchMatches.length) return

  currentMatchIndex = index
  const container = containerRef.value
  if (!container) return

  for (let i = 0; i < searchMatches.length; i++) {
    searchMatches[i].classList.toggle('is-current', i === index)
  }

  const targetMark = searchMatches[index]
  if (!targetMark) return

  // 1. 如果匹配项处于折叠章节中，自动逆向展开其所属的章节
  let cur = targetMark
  while (cur && cur.parentElement && cur.parentElement !== container) {
    cur = cur.parentElement
  }

  if (cur) {
    let stateChanged = false
    if (/^H[1-6]$/.test(cur.tagName) && collapsedHeadingIds.value.has(cur.id)) {
      collapsedHeadingIds.value.delete(cur.id)
      stateChanged = true
    }
    const parents = headingParentsMap.get(cur) || []
    for (const pid of parents) {
      if (collapsedHeadingIds.value.has(pid)) {
        collapsedHeadingIds.value.delete(pid)
        stateChanged = true
      }
    }
    if (stateChanged) {
      applyCollapseState()
    }
  }

  // 2. 居中平滑滚动聚焦
  nextTick(() => {
    targetMark.scrollIntoView({ behavior: 'smooth', block: 'center' })
  })
}

function nextSearchMatch() {
  if (!searchMatches || searchMatches.length === 0) {
    return { total: 0, current: -1 }
  }
  const nextIdx = (currentMatchIndex + 1) % searchMatches.length
  focusMatch(nextIdx)
  return { total: searchMatches.length, current: nextIdx }
}

function prevSearchMatch() {
  if (!searchMatches || searchMatches.length === 0) {
    return { total: 0, current: -1 }
  }
  const prevIdx = (currentMatchIndex - 1 + searchMatches.length) % searchMatches.length
  focusMatch(prevIdx)
  return { total: searchMatches.length, current: prevIdx }
}

onUnmounted(() => {
  clearSearch()
})

defineExpose({
  foldAll,
  unfoldAll,
  toggleHeading,
  expandHeading,
  getHeadings: () => headingList.value,
  getCollapsedIds: () => Array.from(collapsedHeadingIds.value),
  highlightSearch,
  nextSearchMatch,
  prevSearchMatch,
  clearSearch
})
</script>

<style>
/* 章节折叠样式（非 scoped，作用于 v-html 生成的内容） */
.markdown-body.is-collapsible-enabled .heading-collapsible {
  position: relative;
  display: flex;
  align-items: center;
  gap: 6px;
  cursor: pointer;
  user-select: text;
  transition: opacity 0.15s ease;
}

.markdown-body.is-collapsible-enabled .heading-collapsible:hover {
  opacity: 0.88;
}

.markdown-body .heading-collapse-toggle {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 24px;
  height: 24px;
  border-radius: 6px;
  border: none;
  background: transparent;
  color: inherit;
  opacity: 0.5;
  cursor: pointer;
  padding: 0;
  flex-shrink: 0;
  transition: opacity 0.15s ease, background-color 0.15s ease, transform 0.15s ease;
}

.markdown-body .heading-collapse-toggle:hover {
  opacity: 1;
  background: rgba(128, 128, 128, 0.12);
}

.markdown-body .heading-collapse-icon {
  font-size: 13px;
  line-height: 1;
  display: inline-block;
  user-select: none;
  transition: transform 0.18s cubic-bezier(0.2, 0, 0.2, 1);
}

.markdown-body .heading-collapsible.is-collapsed .heading-collapse-icon {
  transform: rotate(-90deg);
}

.markdown-body .heading-text {
  flex: 1;
  min-width: 0;
}

/* 折叠后的提示胶囊横幅 */
.markdown-body .heading-fold-banner {
  display: flex;
  align-items: center;
  gap: 10px;
  margin: 6px 0 16px;
  padding: 8px 14px;
  border-radius: 8px;
  background: rgba(128, 128, 128, 0.07);
  border: 1px dashed rgba(128, 128, 128, 0.28);
  font-size: 13px;
  cursor: pointer;
  user-select: none;
  transition: all 0.15s ease;
}

.markdown-body .heading-fold-banner:hover {
  background: rgba(25, 137, 250, 0.08);
  border-color: rgba(25, 137, 250, 0.4);
}

.markdown-body .fold-banner-dots {
  font-weight: bold;
  letter-spacing: 2px;
  opacity: 0.6;
}

.markdown-body .fold-banner-text {
  opacity: 0.75;
}

.markdown-body .fold-banner-action {
  color: #1989fa;
  font-weight: 500;
  font-size: 12px;
  margin-left: auto;
}

/* 搜索高亮与聚焦动画 */
.markdown-body mark.search-highlight {
  background-color: #ffe57f;
  color: #1f2328;
  border-radius: 2px;
  padding: 1px 2px;
  margin: 0 -1px;
  box-shadow: 0 0 0 1px rgba(255, 193, 7, 0.35);
  transition: all 0.15s ease;
}

:global(body.dark) .markdown-body mark.search-highlight {
  background-color: #c99710;
  color: #111418;
  box-shadow: 0 0 0 1px rgba(255, 235, 59, 0.4);
}

.markdown-body mark.search-highlight.is-current {
  background-color: #ff976a !important;
  color: #ffffff !important;
  font-weight: 600;
  border-radius: 3px;
  box-shadow: 0 0 0 2px #ff7043, 0 2px 8px rgba(255, 112, 67, 0.45) !important;
  animation: search-highlight-bounce 0.35s cubic-bezier(0.175, 0.885, 0.32, 1.275);
}

@keyframes search-highlight-bounce {
  0% { transform: scale(1); }
  40% { transform: scale(1.16); }
  100% { transform: scale(1); }
}
</style>
