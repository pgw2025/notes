import JSZip from 'jszip'
import http from '../api/http'

// ===================== 辅助函数 =====================

/**
 * 触发浏览器下载文件
 */
function downloadBlob(blob, filename) {
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = filename
  document.body.appendChild(a)
  a.click()
  document.body.removeChild(a)
  URL.revokeObjectURL(url)
}

/**
 * 文件名安全化：替换非法字符
 */
function sanitizeFilename(name) {
  if (!name) return '无标题'
  return name.replace(/[<>:"/\\|?*\n\r]/g, '_').slice(0, 100)
}

/**
 * 生成带时间戳的文件名前缀
 */
function timestampPrefix() {
  const d = new Date()
  const p = (n) => String(n).padStart(2, '0')
  return `${d.getFullYear()}${p(d.getMonth() + 1)}${p(d.getDate())}_${p(d.getHours())}${p(d.getMinutes())}`
}

/**
 * 获取笔记的完整数据（用于导出）
 * 列表接口返回的是 ContentPreview，需要用详情接口获取完整 Content
 */
async function fetchFullNotes() {
  const res = await http.get('/notes')
  const list = Array.isArray(res) ? res : (res?.items || [])
  const fullNotes = await Promise.all(
    list.map((n) => http.get(`/notes/${n.id}`))
  )
  return fullNotes
}

// ===================== 导出：JSON =====================

/**
 * 导出全部笔记为 JSON 文件
 */
export async function exportAllAsJSON(onProgress) {
  const notes = await fetchFullNotes()
  const data = {
    version: 1,
    exportedAt: new Date().toISOString(),
    noteCount: notes.length,
    notes: notes.map((n) => ({
      title: n.title,
      content: n.content,
      categoryName: n.categoryName,
      tags: n.tags || [],
      backgroundColor: n.backgroundColor,
      isPinned: n.isPinned,
      createdAt: n.createdAt,
      updatedAt: n.updatedAt
    }))
  }
  const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' })
  downloadBlob(blob, `notes_${timestampPrefix()}.json`)
  return notes.length
}

/**
 * 导出单篇笔记为 JSON 文件
 */
export async function exportNoteAsJSON(note) {
  const data = {
    version: 1,
    exportedAt: new Date().toISOString(),
    noteCount: 1,
    notes: [{
      title: note.title,
      content: note.content,
      categoryName: note.categoryName,
      tags: note.tags || [],
      backgroundColor: note.backgroundColor,
      isPinned: note.isPinned,
      createdAt: note.createdAt,
      updatedAt: note.updatedAt
    }]
  }
  const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' })
  downloadBlob(blob, `${sanitizeFilename(note.title)}.json`)
}

// ===================== 导出：Markdown =====================

/**
 * 生成 Markdown 内容（纯正文，不含 frontmatter）
 */
function toMarkdown(note) {
  const parts = []
  if (note.title) parts.push(`# ${note.title}\n`)
  if (note.content) parts.push(note.content)
  return parts.join('\n')
}

/**
 * 生成带 frontmatter 元数据的 Markdown
 */
function toMarkdownWithMeta(note) {
  const fm = []
  fm.push(`title: "${(note.title || '').replace(/"/g, '\\"')}"`)
  if (note.categoryName) fm.push(`category: "${note.categoryName}"`)
  if (note.tags && note.tags.length) {
    fm.push(`tags: [${note.tags.map((t) => `"${t.replace(/"/g, '\\"')}"`).join(', ')}]`)
  }
  if (note.backgroundColor) fm.push(`backgroundColor: "${note.backgroundColor}"`)
  if (note.isPinned) fm.push(`isPinned: true`)
  if (note.createdAt) fm.push(`createdAt: "${note.createdAt}"`)
  if (note.updatedAt) fm.push(`updatedAt: "${note.updatedAt}"`)

  const body = note.content || ''
  return `---\n${fm.join('\n')}\n---\n\n${body}`
}

/**
 * 导出全部笔记为 Markdown ZIP
 * @param {boolean} withMetadata 是否包含 frontmatter 元数据
 */
export async function exportAllAsMarkdown(withMetadata, onProgress) {
  const notes = await fetchFullNotes()
  const zip = new JSZip()

  const usedNames = new Set()
  notes.forEach((note, i) => {
    let name = sanitizeFilename(note.title)
    let filename = `${name}.md`
    // 避免重名
    let counter = 1
    while (usedNames.has(filename)) {
      filename = `${name}_${counter}.md`
      counter++
    }
    usedNames.add(filename)

    const md = withMetadata ? toMarkdownWithMeta(note) : toMarkdown(note)
    zip.file(filename, md)

    if (onProgress && i % 10 === 0) onProgress(i + 1, notes.length)
  })

  const blob = await zip.generateAsync({ type: 'blob' })
  const suffix = withMetadata ? 'md_meta' : 'markdown'
  downloadBlob(blob, `notes_${suffix}_${timestampPrefix()}.zip`)
  return notes.length
}

/**
 * 导出单篇笔记为 Markdown 文件
 * @param {boolean} withMetadata 是否包含 frontmatter 元数据
 */
export function exportNoteAsMarkdown(note, withMetadata) {
  const md = withMetadata ? toMarkdownWithMeta(note) : toMarkdown(note)
  const blob = new Blob([md], { type: 'text/markdown' })
  downloadBlob(blob, `${sanitizeFilename(note.title)}.md`)
}

// ===================== 导出入口 =====================

/**
 * 导出全部笔记
 * @param {'json'|'markdown'|'markdown-meta'} format
 */
export async function exportAllNotes(format, onProgress) {
  switch (format) {
    case 'json':
      return await exportAllAsJSON(onProgress)
    case 'markdown':
      return await exportAllAsMarkdown(false, onProgress)
    case 'markdown-meta':
      return await exportAllAsMarkdown(true, onProgress)
    default:
      throw new Error('不支持的导出格式')
  }
}

/**
 * 导出单篇笔记
 * @param {'json'|'markdown'|'markdown-meta'} format
 */
export function exportSingleNote(note, format) {
  switch (format) {
    case 'json':
      return exportNoteAsJSON(note)
    case 'markdown':
      return exportNoteAsMarkdown(note, false)
    case 'markdown-meta':
      return exportNoteAsMarkdown(note, true)
    default:
      throw new Error('不支持的导出格式')
  }
}

// ===================== 导入 =====================

/**
 * 解析 frontmatter 元数据
 * 格式：
 * ---
 * title: "标题"
 * category: "分类"
 * tags: ["标签1", "标签2"]
 * backgroundColor: "#FFF9C4"
 * isPinned: true
 * ---
 * 正文内容
 */
function parseFrontmatter(text) {
  const fmMatch = text.match(/^---\n([\s\S]*?)\n---\n?([\s\S]*)$/)
  if (!fmMatch) return { meta: {}, content: text }

  const fmText = fmMatch[1]
  const content = fmMatch[2].trim()
  const meta = {}

  // 逐行解析 YAML frontmatter（简易解析，不引入完整 YAML 库）
  const lines = fmText.split('\n')
  for (const line of lines) {
    const m = line.match(/^(\w+):\s*(.*)$/)
    if (!m) continue
    const key = m[1]
    let val = m[2].trim()

    if (val.startsWith('[') && val.endsWith(']')) {
      // 数组：["a", "b"]
      val = val.slice(1, -1)
        .split(',')
        .map((s) => s.trim().replace(/^"(.*)"$/, '$1'))
        .filter(Boolean)
    } else if (val.startsWith('"') && val.endsWith('"')) {
      val = val.slice(1, -1).replace(/\\"/g, '"')
    } else if (val === 'true') {
      val = true
    } else if (val === 'false') {
      val = false
    }

    meta[key] = val
  }

  return { meta, content }
}

/**
 * 从 Markdown 文本提取笔记数据
 */
function parseMarkdownNote(text, filename) {
  const { meta, content } = parseFrontmatter(text)

  // 如果没有 frontmatter 的 title，尝试从正文第一个 # 标题提取，或用文件名
  let title = meta.title
  if (!title) {
    const h1Match = content.match(/^#\s+(.+)$/m)
    title = h1Match ? h1Match[1] : filename.replace(/\.md$/i, '')
  }

  // 如果正文以 # 标题开头且 title 来自 frontmatter，去掉正文的重复标题
  let finalContent = content
  if (meta.title) {
    finalContent = content.replace(/^#\s+.+\n?/m, '').trim()
  }

  return {
    title,
    content: finalContent,
    categoryName: meta.category || null,
    tags: Array.isArray(meta.tags) ? meta.tags : [],
    backgroundColor: meta.backgroundColor || null,
    isPinned: meta.isPinned || false
  }
}

/**
 * 查找分类 ID（按名称），不存在则创建
 */
async function resolveCategoryId(categoryName) {
  if (!categoryName) return null
  const categories = await http.get('/categories')
  const found = categories.find((c) => c.name === categoryName)
  if (found) return found.id

  // 创建新分类
  const created = await http.post('/categories', { name: categoryName })
  return created.id
}

/**
 * 查找标签 ID（按名称），不存在则创建
 */
async function resolveTagIds(tagNames) {
  if (!tagNames || tagNames.length === 0) return []
  const tags = await http.get('/tags')
  const result = []

  for (const name of tagNames) {
    const found = tags.find((t) => t.name === name)
    if (found) {
      result.push(found.id)
    } else {
      // 创建新标签
      const created = await http.post('/tags', { name })
      result.push(created.id)
    }
  }

  return result
}

/**
 * 获取已有笔记的标题+内容指纹集合，用于去重
 */
async function getExistingNoteFingerprints() {
  const res = await http.get('/notes')
  const list = Array.isArray(res) ? res : (res?.items || [])
  const set = new Set()
  for (const n of list) {
    set.add(`${n.title}||${n.contentPreview}`)
  }
  return set
}

/**
 * 导入笔记数据
 * @param {File|File[]} files 一个或多个文件（JSON 或 Markdown）
 * @param {(current, total, action) => void} onProgress 进度回调
 * @returns {{imported: number, skipped: number, failed: number}}
 */
export async function importNotes(files, onProgress) {
  const fileList = Array.isArray(files) ? files : [files]
  const existing = await getExistingNoteFingerprints()

  let imported = 0
  let skipped = 0
  let failed = 0
  let total = 0

  // 先统计需要处理的笔记数（JSON 可能包含多条）
  const parsedNotes = []

  for (const file of fileList) {
    const text = await file.text()

    if (file.name.endsWith('.json')) {
      try {
        const data = JSON.parse(text)
        const notes = Array.isArray(data.notes) ? data.notes : (Array.isArray(data) ? data : [])
        for (const n of notes) {
          parsedNotes.push({
            title: n.title || '',
            content: n.content || '',
            categoryName: n.categoryName || null,
            tags: n.tags || [],
            backgroundColor: n.backgroundColor || null,
            isPinned: n.isPinned || false
          })
        }
      } catch {
        failed++
      }
    } else if (file.name.endsWith('.md') || file.name.endsWith('.markdown')) {
      const note = parseMarkdownNote(text, file.name)
      parsedNotes.push(note)
    } else {
      failed++
    }
  }

  total = parsedNotes.length

  // 缓存分类和标签，避免重复查询
  const categoryCache = new Map()
  const tagCache = new Map()

  for (let i = 0; i < parsedNotes.length; i++) {
    const note = parsedNotes[i]
    if (onProgress) onProgress(i + 1, total, note.title || '无标题')

    // 去重检查：标题+内容前120字符完全相同则跳过
    const fingerprint = `${note.title}||${note.content.slice(0, 120)}`
    if (existing.has(fingerprint)) {
      skipped++
      continue
    }

    try {
      // 解析分类
      let categoryId = null
      if (note.categoryName) {
        if (categoryCache.has(note.categoryName)) {
          categoryId = categoryCache.get(note.categoryName)
        } else {
          categoryId = await resolveCategoryId(note.categoryName)
          categoryCache.set(note.categoryName, categoryId)
        }
      }

      // 解析标签
      let tagIds = []
      if (note.tags && note.tags.length) {
        const uncached = note.tags.filter((t) => !tagCache.has(t))
        if (uncached.length) {
          const tags = await http.get('/tags')
          for (const name of note.tags) {
            if (tagCache.has(name)) continue
            const found = tags.find((t) => t.name === name)
            if (found) {
              tagCache.set(name, found.id)
            } else {
              const created = await http.post('/tags', { name })
              tagCache.set(name, created.id)
            }
          }
        }
        tagIds = note.tags.map((t) => tagCache.get(t)).filter(Boolean)
      }

      await http.post('/notes', {
        title: note.title,
        content: note.content,
        categoryId,
        tagIds,
        backgroundColor: note.backgroundColor || '',
        isPinned: note.isPinned
      })

      existing.add(fingerprint)
      imported++
    } catch {
      failed++
    }
  }

  return { imported, skipped, failed, total }
}
