# 导出/导入只处理前 20 条笔记 — Bug 修复方案

> 状态：待审批（未改动任何代码）
> 日期：2026-09-14
> 影响：导出（JSON / Markdown / Markdown+Meta）与导入去重，均只基于列表接口第一页（20 条），笔记超过 20 篇时**数据丢失 / 去重失效**。

## 一、问题定位

### 根因

`frontend/src/utils/exportImport.js` 两处依赖 `GET /notes` 列表接口，但该接口是**分页**的：

| 调用点 | 用途 | 问题 |
|---|---|---|
| `exportImport.js:41-48` `fetchFullNotes()` | 导出全部笔记的数据源 | 只拿到 `List` 接口默认第一页 `pageSize=20`，超过 20 篇的笔记**不会被导出**（静默丢失） |
| `exportImport.js:327-335` `getExistingNoteFingerprints()` | 导入前构建去重指纹集合 | 只拿到前 20 篇的指纹，第 21 篇起已存在的笔记**去重失效**，会被重复导入 |

### 关键事实（已核实代码）

1. **列表接口是分页的**：`backend/Notes.Api/Controllers/NotesController.cs:39` 的 `List` 有 `page` / `pageSize`，默认 `pageSize=20`。
2. **pageSize 有上限**：`NotesController.cs:42` `if (pageSize < 1 || pageSize > 100) pageSize = 20;` —— 即使前端传 `pageSize=100000` 也会被钳制到 100，**无法通过单次大 pageSize 一次拉全**。
3. **列表项不含完整正文**：`NoteListItemDto`（`DTOs/NoteDtos.cs:19-29`）只有 `ContentPreview`（`Preview` 截断到 120 字符）和 `Tags`（标签名字符串数组），**无完整 `Content`、无 `CreatedAt`**。
4. **导出需要完整正文 + createdAt**：`exportImport.js:61-70` 导出 JSON 需要 `n.content`（完整）和 `n.createdAt`；Markdown 导出需要 `n.content`。
5. **现有 `fetchFullNotes` 已经是「列表拿 ID → 逐篇调详情」模式**：`exportImport.js:44-46` 对每个列表项 `http.get('/notes/${n.id}')` 拿完整详情（`NoteDto` 含 `content`、`createdAt`）。

## 二、方案对比

| 方案 | 思路 | 改动面 | 评价 |
|---|---|---|---|
| **A（推荐）前端循环分页拉全量 ID** | `fetchFullNotes` / `getExistingNoteFingerprints` 改为 `while` 循环按 `page` 递增拉取，直到 `hasMore=false`，收集全部 ID | 仅前端 `exportImport.js`，约 20 行 | 无需后端改动；复用现有 `hasMore`/`total` 字段；`fetchFullNotes` 的逐篇调详情逻辑保留 |
| B 改用 `/notes/timeline` 接口 | timeline 不分页返回全量 | 前端 + 需展开 年→月→日 结构 | timeline 的 `NoteListItemDto` 同样只有 `ContentPreview`，仍需逐篇调详情；且数据结构复杂，**不如 A 简洁** |
| C 后端新增「导出专用全量接口」 | 后端加 `GET /notes/export` 返回全量完整 `NoteDto` | 后端 + 前端 | 最彻底（一次拿全量完整数据），但**新增接口**、超出最小改动原则，且需处理大数据量内存/响应体 |

**结论**：推荐方案 A。理由：① 零后端改动；② 完全复用现有接口契约（`hasMore`、`total` 已存在）；③ `fetchFullNotes` 现有「逐篇调详情」的模式已成立，只需把「ID 来源」从单页改为多页。

## 三、方案 A 详细设计

### 3.1 新增一个通用「拉全量列表」辅助函数

在 `exportImport.js` 顶部辅助区新增：

```js
/**
 * 循环分页拉取全部笔记列表项（返回 NoteListItemDto 数组）
 * 列表接口分页默认 pageSize=20，这里按 hasMore 逐页拉满
 */
async function fetchAllNoteListItems() {
  const items = []
  const pageSize = 100 // 后端上限内，减少请求次数
  let page = 1
  while (true) {
    const res = await http.get('/notes', { params: { page, pageSize } })
    const list = Array.isArray(res) ? res : (res?.items || [])
    items.push(...list)
    const hasMore = res?.hasMore
    if (!hasMore || list.length === 0) break
    page++
  }
  return items
}
```

> 说明：`pageSize=100` 是后端 `List` 钳制的上限（`NotesController.cs:42`），比默认 20 减少 80% 请求次数。`hasMore` 字段后端已返回（`NoteListResponseDto.HasMore`）。

### 3.2 改造 `fetchFullNotes()`

```js
async function fetchFullNotes() {
  const list = await fetchAllNoteListItems()
  const fullNotes = await Promise.all(
    list.map((n) => http.get(`/notes/${n.id}`))
  )
  return fullNotes
}
```

### 3.3 改造 `getExistingNoteFingerprints()`

```js
async function getExistingNoteFingerprints() {
  const list = await fetchAllNoteListItems()
  const set = new Set()
  for (const n of list) {
    set.add(`${n.title}||${n.contentPreview}`)
  }
  return set
}
```

### 3.4 需一并修正的潜在问题（重要）

**问题：导入去重指纹口径不一致。**

- `getExistingNoteFingerprints()`（第 332 行）用 `${n.title}||${n.contentPreview}`，其中 `contentPreview` 是**后端截断到 120 字符**的（`NotesController.cs:560` `Preview(n.Content, 120)`）。
- 导入时的去重判断（第 394 行）用 `${note.title}||${note.content.slice(0, 120)}`。

这两者**在 120 字符边界上才可能对齐**，且依赖后端 `Preview` 的截断规则（会把 `\n`→空格、`\r` 删除，见 `NotesController.cs:613-618`），前端 `content.slice(0, 120)` 不做这些清洗。因此**现有去重本就是「接近但不精确」的**，超过 120 字符的笔记可能误判或漏判。

> 此问题属**既有的次要缺陷**，本次主目标是修复「只取前 20 条」的严重 bug。是否一并修复指纹口径，见决策点 2。

## 四、决策点（请选择）

1. **方案选型**：
   - A（推荐）前端循环分页拉全量 ID（零后端改动）；
   - C 后端新增 `GET /notes/export` 全量接口（最彻底，一次拿完整数据，但需后端改动 + 考虑大响应体）。
2. **导入去重指纹口径**：
   - ①（推荐）仅修「前 20 条」主 bug，指纹口径保持现状（`contentPreview` vs `content.slice(0,120)` 的不精确问题另开任务）；
   - ② 顺带统一指纹口径（例如两端都改用「标题 + 完整 content 的哈希」，更可靠，但需同步改 `fetchAllNoteListItems` 拿不到完整 content，得额外逐篇拉详情，改动变大）。
3. **导出性能**：
   - ①（推荐）`fetchFullNotes` 用 `Promise.all` 并发逐篇调详情（现状如此，笔记量大时并发请求多，可能触发限流）；
   - ② 引入分批并发（如每批 5~10 篇），更稳妥，但代码更复杂。

## 五、改动清单（方案 A + 决策点默认①）

| 文件 | 改动 | 预估 |
|---|---|---|
| `frontend/src/utils/exportImport.js` | 新增 `fetchAllNoteListItems()` 辅助函数；`fetchFullNotes` 与 `getExistingNoteFingerprints` 改用该函数 | 约 20 行 |

**后端零改动，无数据库迁移。**

## 六、验证步骤（仅在你要求时执行）

1. 造 >20 篇笔记（如 25 篇）→ 导出 JSON / Markdown / Markdown+Meta，确认导出条数 = 25（此前应只有 20）；
2. 导出 JSON 后删除其中一篇 → 再导入该 JSON，确认该篇被重新导入（去重未漏）；
3. 导入一个含 >20 篇笔记的 JSON，其中前 20 篇已存在 → 确认已存在的 20 篇被跳过、新增的被导入（去重覆盖全量）；
4. 导出内容完整性：抽查第 21 篇及以后的笔记，正文完整无截断、`createdAt` 正确。
