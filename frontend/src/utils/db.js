import Dexie from 'dexie'

/**
 * 本地持久化层（IndexedDB via Dexie）。
 * 职责：在 Service Worker 的 HTTP 缓存之上，提供结构化的可查询缓存，
 * 供"先本地渲染、后网络同步"策略使用。
 *
 * 表设计原则：
 *   - notes      : 只缓存首页 (homeOnly + page=1) 的笔记列表字段，不存 Content 全文
 *   - categories / tags : 全量缓存（量小，几百条撑死）
 *   - user       : 单条用户信息
 *   - 不做分页缓存，不存附件（附件已由 CacheFirst 策略兜底）
 *
 * 多用户隔离：同一浏览器不同账号共用一个 IndexedDB 实例，
 * 在用户登出时通过 clearUserData() 清空所有表，防止串数据。
 */
const db = new Dexie('notes-app')

db.version(1).stores({
  // 笔记列表（首页 homeOnly + page=1 的 20 条左右）
  // keyPath=id，按 updatedAt / pinned 建索引方便 UI 排序
  notes: '&id, updatedAt, isPinned, pinnedAt',

  // 分类 & 标签（全量）
  categories: '&id',
  tags: '&id',

  // 用户信息（单条，keyPath=userId）
  user: '&userId'
})

/**
 * 清空所有业务数据表。
 * 在用户主动登出时调用，防止不同账号在同一设备上串数据。
 */
export async function clearAllData() {
  await Promise.all([
    db.notes.clear(),
    db.categories.clear(),
    db.tags.clear(),
    db.user.clear()
  ])
}

// ---------- notes ----------

/** 覆写首页笔记缓存（批量覆盖） */
export async function putNotes(items) {
  if (!Array.isArray(items) || items.length === 0) return
  await db.notes.bulkPut(items)
}

/** 清空首页笔记缓存 */
export async function clearNotes() {
  await db.notes.clear()
}

/** 读取所有缓存的笔记（已按 isPinned → pinnedAt → updatedAt 排序） */
export async function getAllNotes() {
  const list = await db.notes.orderBy('isPinned').reverse().toArray()
  return list.sort((a, b) => {
    if (a.isPinned !== b.isPinned) return b.isPinned - a.isPinned
    if (a.pinnedAt && b.pinnedAt) return new Date(b.pinnedAt) - new Date(a.pinnedAt)
    if (a.pinnedAt) return -1
    if (b.pinnedAt) return 1
    return new Date(b.updatedAt) - new Date(a.updatedAt)
  })
}

// ---------- categories ----------

export async function putCategories(list) {
  if (!Array.isArray(list)) return
  await db.categories.clear()
  if (list.length > 0) await db.categories.bulkPut(list)
}

export async function getAllCategories() {
  return db.categories.toArray()
}

// ---------- tags ----------

export async function putTags(list) {
  if (!Array.isArray(list)) return
  await db.tags.clear()
  if (list.length > 0) await db.tags.bulkPut(list)
}

export async function getAllTags() {
  return db.tags.toArray()
}

// ---------- user ----------

export async function putUser(user) {
  if (!user?.id) return
  await db.user.put({ ...user, userId: user.id })
}

export async function getUser() {
  const list = await db.user.toArray()
  return list.length > 0 ? list[0] : null
}

export async function clearUser() {
  await db.user.clear()
}

export default db
