# 「全部笔记」仅显示未分类笔记 — 修改方案（修正版 v2）

> 状态：待审批（未改动任何代码）
> 日期：2026-09-14
> 目标：当笔记很多时，侧边栏「全部笔记」入口不再堆砌所有笔记，只显示**未分类**（`CategoryId == null`）的笔记；已分类笔记通过侧边栏各分类进入查看。

## 一、现状分析

| 项 | 现状 | 位置 |
|---|---|---|
| 列表接口 | `GET /api/notes?categoryId=&page=&pageSize=`，`categoryId` 为空时返回**全部**笔记 | `backend/Notes.Api/Controllers/NotesController.cs:38-68` |
| 过滤逻辑 | 仅 `if (categoryId.HasValue) query = query.Where(n => n.CategoryId == categoryId)`，无"未分类"分支 | `NotesController.cs:51-52` |
| 未分类概念 | 后端无任何 uncategorized 筛选；仅前端展示文案（"默认未分类"/"未分类"） | `frontend/.../CategorySelectModal.vue:75`、`frontend-admin/src/views/NoteManage.vue:203` |
| 实体关联 | `Note.CategoryId` 为**可空外键** `int?`，"未分类" = `CategoryId == null` | `backend/Notes.Api/Models/Note.cs:14-16` |
| 前端入口 | 侧边栏「全部笔记」→ `selectCategory(null)` → 请求不传 `categoryId` → 全量 | `frontend/src/components/CategoryNav.vue:10-19` |
| 前端请求 | `fetchPage` 同时传 `categoryId` 和 `tagId`（但后端 `List` 无 `tagId` 参数，标签筛选实际**一直不生效**） | `frontend/src/views/NotesList.vue:320-326` |
| 状态建模 | 前端只有 `activeCategoryId` / `activeTagId` 两个变量，`null+null` 即"全部"，**无独立"未分类"状态** | `NotesList.vue:204-207` |
| 影响面 | 消费 `GET /notes` 的仅 `NotesList.vue`；搜索页 `/notes/search`、时间线 `/notes/timeline`、admin 端 `/admin/notes` 均独立，**不受影响** | — |

## 二、改造前后对比

| | Before | After |
|---|---|---|
| 点击「全部笔记」（`activeCategoryId=null && activeTagId=null`） | 返回所有笔记（含已分类） | 仅返回 `CategoryId == null` 的笔记 |
| 点击具体分类 | `Where(CategoryId == id)` | 不变 |
| 点击具体标签 | 后端忽略 `tagId`（本就不生效） | 不变（不在本次范围） |
| 分页/置顶排序 | `IsPinned → PinnedAt → UpdatedAt` | 不变 |
| 搜索 / 时间线 / 回收站 | 全量数据 | 不变 |
| admin 端笔记管理 | 全量 | 不变 |

## 三、实施步骤（方案 A：后端新增显式参数 + 前端状态建模）

### Step 1 — 后端：`NotesController.List` 增加 `uncategorizedOnly` 参数

- `NotesController.cs:39` 签名追加 `bool? uncategorizedOnly = false`：
  ```csharp
  public async Task<ActionResult<NoteListResponseDto>> List(
      [FromQuery] int? categoryId,
      [FromQuery] bool? uncategorizedOnly = false,
      [FromQuery] int page = 1,
      [FromQuery] int pageSize = 20)
  ```
- `NotesController.cs:51-52` 过滤段改为：
  ```csharp
  if (uncategorizedOnly == true)
      query = query.Where(n => !n.CategoryId.HasValue);
  else if (categoryId.HasValue)
      query = query.Where(n => n.CategoryId == categoryId);
  ```
- 互斥语义（决策点 3 → 选①）：`uncategorizedOnly=true` **优先**，此时忽略 `categoryId`。前端本就不会同时传，后端从宽处理即可。

### Step 2 — 前端状态建模：引入显式 `viewMode`

新增一个独立状态变量，避免用 `null` 做隐式判断踩坑：

```js
// viewMode: 'uncategorized' | 'category' | 'tag'
const viewMode = ref('uncategorized')
```

- `NotesList.vue:204-207` 保留 `activeCategoryId` / `activeTagId`，`viewMode` 由二者取值派生（或用 computed 统一推导）。
- 三者关系（唯一真相来源）：
  - `viewMode='uncategorized'` ⟺ `activeCategoryId===null && activeTagId===null`
  - `viewMode='category'` ⟺ `activeCategoryId != null`
  - `viewMode='tag'` ⟺ `activeTagId != null`

### Step 3 — 前端请求：`fetchPage` 按 `viewMode` 透传

`NotesList.vue:320-326` 改为：

```js
async function fetchPage() {
  const params = { page: currentPage.value, pageSize }
  if (viewMode.value === 'category') params.categoryId = activeCategoryId.value
  if (viewMode.value === 'tag') params.tagId = activeTagId.value
  if (viewMode.value === 'uncategorized') params.uncategorizedOnly = true
  const res = await http.get('/notes', { params })
  notes.value.push(...res.items)
  if (!res.hasMore) listFinished.value = true
}
```

> 关键修正：**只有 `viewMode==='uncategorized'` 时才传 `uncategorizedOnly`**，彻底避免「点标签浏览时误传 uncategorizedOnly 导致只显示未分类」的 bug。

### Step 4 — 前端入口：`CategoryNav.vue`

- 「全部笔记」项（`CategoryNav.vue:10-19`）点击后令 `viewMode='uncategorized'`（`selectCategory(null)` 保持，`NotesList` 侧把 `null+null` 解析为 `uncategorized`）。**具体分类点击逻辑不变**。
- 视决策点 1：若选②改名，将第 18 行文案「全部笔记」改为「未分类」，图标 `apps-o` 可改为 `inbox-o`（或保留）。

### Step 5 — 标题与空态文案同步（新增，v1 遗漏）

- `NotesList.vue:217-224` `currentCategoryName`：`viewMode==='uncategorized'` 时返回 `'未分类'`（选②）或保留 `'笔记'`（选①）。
- `NotesList.vue:50-57` 空态文案：未分类视图下改为「还没有未分类笔记」；分类视图下改为「该分类下暂无笔记」。`empty-sub` 建议按视图区分。

### Step 6 — URL 状态同步（新增，v1 遗漏）

- `syncUrl`（`NotesList.vue:349-354`）需体现「未分类」状态，否则刷新后行为不一致。
  - 方案：未分类作为**默认态不写 URL**（`query` 为空即表示未分类），与现状「无 query = 全部」一致；分类/标签仍写 `categoryId` / `tagId`。
  - `onMounted`（`NotesList.vue:415-424`）：query 为空时默认进入 `uncategorized`（原来走 `else` 调 `loadNotes()`，语义天然等价，改动极小）。
  - 结论：**无需引入新 query 键**，"空 query = 未分类" 即可，刷新自洽。

## 四、决策点（请选择）

1. **入口语义**：
   - ②（本版推荐）改名「未分类」，置于分类列表顶部（语义诚实，零额外成本）；
   - ① 保留「全部笔记」名称但只显示未分类（改动最小，但名称与行为矛盾，不推荐）。
2. **后端参数风格**：
   - ①（推荐）新增 `bool? uncategorizedOnly` 显式参数，语义清晰、可测试；
   - ② 复用 `categoryId=0` 约定为"未分类"，零新增参数但属隐式约定，易踩坑。
3. **参数互斥处理**：`uncategorizedOnly=true` 与 `categoryId` 同时出现时——① `uncategorizedOnly` 优先并忽略 categoryId（推荐）；② 返回 400。
4. **附带问题（tagId 筛选不生效）**：前端一直传 `tagId`，但后端 `List` 无此参数，标签筛选实际是坏的。本次**不修**（严格限定范围），另开任务。若你希望顺带修，需单独说明（涉及后端 `List` 增加 `tagId` 过滤 + 前端联动）。
5. **搜索页 / 时间线**：保持全量不动（推荐）。

## 五、改动清单（审批后执行）

| 文件 | 改动 | 预估 |
|---|---|---|
| `backend/Notes.Api/Controllers/NotesController.cs` | `List` 签名 + `uncategorizedOnly` 过滤分支 | 约 4 行 |
| `frontend/src/views/NotesList.vue` | 新增 `viewMode` 状态；`fetchPage` 按视图透传；`currentCategoryName` 标题；空态文案；`syncUrl`/`onMounted` 语义对齐 | 约 15 行 |
| `frontend/src/components/CategoryNav.vue` | 「全部笔记」文案/图标（视决策点 1） | 约 1-2 行 |

预计后端 4 行、前端 15 行左右，**无数据库迁移、无新接口**。

## 六、验证步骤（仅在你要求时执行）

1. 新建一条笔记不选分类 → 侧边栏「未分类」（或「全部笔记」）中可见；
2. 将其归入分类 A → 未分类视图中消失，分类 A 中可见；
3. 点击某标签浏览 → 列表仍按标签全量返回（**验证未误传 uncategorizedOnly**）；
4. 在未分类视图下刷新页面 → 仍停留在未分类（URL 空 query 语义正确）；
5. 各分类切换、分页、置顶排序回归正常；
6. 搜索页仍能搜到已分类笔记。
