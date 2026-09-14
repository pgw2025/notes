# 置顶笔记显示在默认列表（「首页」）— 修改方案

> 目标：默认入口不再是「仅未分类」，而是升级为「首页」——显示**所有未分类笔记 + 所有置顶笔记**（置顶笔记跨分类冗余显示，无论属于哪个分类都能在首页看到）。

## 背景与现状

置顶能力后端早已完整实现（`Note.IsPinned` / `Note.PinnedAt` 字段、`POST /api/notes/{id}/pin`、`/unpin` 端点、列表 `OrderByDescending(IsPinned).ThenByDescending(PinnedAt).ThenByDescending(UpdatedAt)` 排序），前端也有置顶按钮（卡片悬浮按钮 + 滑动操作）与「置顶」标签。

问题：默认入口「未分类」列表（`uncategorizedOnly=true`）只返回 `CategoryId == null` 的笔记。**属于某个分类的置顶笔记，在默认入口里看不到**，用户需要进到对应分类才能发现它。这违背了「置顶 = 最重要、最常看」的直觉。

## 决策点与最终决定

| # | 决策点 | 决定 |
|---|--------|------|
| 1 | 方案 A（改默认入口为「首页」= 未分类 + 置顶）vs 方案 B（另设独立「置顶」入口） | **方案 A** |
| 2 | 置顶笔记是否跨分类冗余显示在首页 | **要** |
| 3 | 首页列表排序规则 | **保持现状**（`IsPinned desc → PinnedAt desc → UpdatedAt desc`，后端已具备） |
| 4 | 默认入口命名 | **改名「首页」** |

## 改动清单

### 后端 `backend/Notes.Api/Controllers/NotesController.cs`

`List` 方法新增 `bool? homeOnly = false` 参数；过滤优先级：

```csharp
if (homeOnly == true)
    query = query.Where(n => !n.CategoryId.HasValue || n.IsPinned);
else if (uncategorizedOnly == true)
    query = query.Where(n => !n.CategoryId.HasValue);
else if (categoryId.HasValue)
    query = query.Where(...); // 原子孙分类逻辑不变
```

- 排序逻辑不变（已满足决策点 3）。
- `uncategorizedOnly` 参数保留，向后兼容（exportImport.js 等未受影响）。

### 前端 `frontend/src/views/NotesList.vue`

- `viewMode` 的 `'uncategorized'` 分支语义升级为 `'home'`（内部可保留字符串或改名，建议直接改 `'home'` 更语义化）。
- `fetchPage`：`viewMode === 'home'` 时传 `homeOnly=true`（不再传 `uncategorizedOnly`）。
- `currentCategoryName`：默认态返回 `'首页'`。
- 空态文案：`emptyTitle` 改「还没有笔记」/`emptySub` 改「置顶的笔记和未分类的笔记会显示在这里」。

### 前端 `frontend/src/components/CategoryNav.vue`

- 侧栏首项文案「未分类」→「首页」。
- 图标 `inbox-o` → `home-o`。
- 注释 `null = 未分类` → `null = 首页`。

## 验证

1. 置顶一条已分类笔记 → 首页顶部可见（带置顶标签）。
2. 置顶一条未分类笔记 → 首页可见且不重复。
3. 取消置顶已分类笔记 → 从首页消失（回到原分类）。
4. 未分类笔记 → 首页正常显示。
5. 点击具体分类 → 仍只看该分类（含子孙），不混入其他分类的置顶笔记。
6. 首页刷新（空 query）→ 仍停留首页。

## 范围说明

- 不新增数据库字段（`IsPinned`/`PinnedAt` 已存在）。
- 不改导出/导入逻辑。
- `uncategorizedOnly` 语义保持不变，仅新增 `homeOnly` 作为前端首页入口使用。
