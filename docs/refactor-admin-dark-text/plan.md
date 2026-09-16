# 后台深色模式字体颜色变量化改造

## 背景

后台管理页（frontend-admin）大部分组件样式中字体颜色写死为**浅色主题色值**（如 `#1e293b`、`#64748b`、`#94a3b8`），切换深色模式后不随 `html.dark` 的主题变量变化，导致深色背景下文字对比度低、难以辨认。

`src/style.css` 已定义主题变量：

| 变量 | 浅色值 | 深色值 | 语义 |
|---|---|---|---|
| `--admin-text-main` | `#1e293b` | `#f8fafc` | 主文字 |
| `--admin-text-sub` | `#64748b` | `#94a3b8` | 次要文字 |
| `--admin-text-muted` | `#94a3b8` | `#64748b` | 弱化文字 |
| `--admin-primary` | `#4f46e5` | `#6366f1` | 主题主色 |
| `--admin-border` | `#e2e8f0` | `rgba(255,255,255,0.09)` | 边框 |

CSS 变量在 Vue scoped 样式中运行时从 `:root` / `html.dark` 继承解析，替换后深浅模式自动生效。

## 映射规则

| 原写死值 | 替换为 | 说明 |
|---|---|---|
| `#1e293b` | `var(--admin-text-main)` | 主文字 |
| `#334155` | `var(--admin-text-main)` | 深灰蓝，主文字级 |
| `#0f172a` | `var(--admin-text-main)` | 最深主文字 |
| `#475569` | `var(--admin-text-sub)` | 次级文字 |
| `#64748b` | `var(--admin-text-sub)` | 次要文字 |
| `#94a3b8` | `var(--admin-text-muted)` | 弱化文字 |
| `#cbd5e1`（文字色） | `var(--admin-text-muted)` | 弱化文字 |
| `#cbd5e1`（色块外圈边框） | `var(--admin-border)` | 仅 NoteManage `.color-indicator` |
| `#6366f1` / `#4f46e5`（文字/边框主色） | `var(--admin-primary)` | 主题主色 |

## 保留不替换（有意写死）

- `#ffffff` / `#fff`：主色按钮、头像、激活菜单上的白字（深浅主题通用）
- 语义状态色：`#f59e0b`（橙）、`#ef4444` / `#f87171`（红）、`#10b981`（绿）、`#0ea5e9`（蓝）、`#065f46`、`#92400e`、`#fde68a`
- 背景色：`#f8fafc`、`#f1f5f9`（浅色卡片背景）、背景渐变 `linear-gradient(135deg, #6366f1, #4f46e5)`
- `AdminLayout.vue` 全部写死文字：位于固定深色背景的侧边栏 / 移动端抽屉内，浅色文字为有意设计
- `Dashboard.vue` ECharts 颜色：JS 中已按 `isDark` 三元计算，无需改动
- `Login.vue`：仅按钮白字 + 背景渐变，无需改动

## 待修改文件

| 文件 | 替换处数（约） |
|---|---|
| `src/views/NoteManage.vue` | 22 |
| `src/views/AttachmentManage.vue` | 11 |
| `src/views/UserManage.vue` | 8 |
| `src/views/CategoryManage.vue` | 9 |
| `src/views/ActivityLog.vue` | 9 |
| `src/views/TagManage.vue` | 6 |
| `src/views/Login.vue` | 0（无需修改） |
| `src/views/Dashboard.vue` | 0（无需修改） |
| `src/layout/AdminLayout.vue` | 0（无需修改） |
