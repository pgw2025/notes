# 后台深色模式字体颜色变量化改造 — 执行清单

## 变更文件

| 文件 | 变更内容 | 处数 |
|---|---|---|
| `frontend-admin/src/views/NoteManage.vue` | `#1e293b→main`×3、`#64748b→sub`×5、`#94a3b8→muted`×5、`#6366f1→primary`×3、`#0f172a→main`×2、`#334155→main`×1、`#cbd5e1→muted`×1、`#cbd5e1(色块边框)→border`×1 | 21 |
| `frontend-admin/src/views/AttachmentManage.vue` | `#1e293b→main`×3、`#64748b→sub`×4、`#94a3b8→muted`×1、`#4f46e5→primary`×1、`#cbd5e1→muted`×1、`#0f172a→main`×1、`#334155→main`×1 | 12 |
| `frontend-admin/src/views/CategoryManage.vue` | `#1e293b→main`×3、`#94a3b8→muted`×2、`#64748b→sub`×2、`#0f172a→main`×1、`#475569→sub`×1 | 9 |
| `frontend-admin/src/views/ActivityLog.vue` | `#1e293b→main`×1、`#334155→main`×2、`#64748b→sub`×2、`#94a3b8→muted`×3、`#0f172a→main`×1 | 9 |
| `frontend-admin/src/views/UserManage.vue` | `#64748b→sub`×4、`#334155→main`×1、`#1e293b→main`×1、`#475569→sub`×1、`border #4f46e5→primary`×1 | 8 |
| `frontend-admin/src/views/TagManage.vue` | `#1e293b→main`×2、`#94a3b8→muted`×1、`#64748b→sub`×1、`#475569→sub`×1、`border #6366f1→primary`×1 | 6 |

**合计替换 65 处**，涉及 6 个文件。

## 未修改文件（有意保留）

- `AdminLayout.vue`：写死文字均位于固定深色背景（侧边栏 `#0f172a`、移动端抽屉 `#0f172a`）上，浅色文字为有意设计
- `Login.vue`：仅按钮白字 `#ffffff` 与背景渐变，无需修改
- `Dashboard.vue`：ECharts 颜色已在 JS 中按 `isDark` 计算；CSS 均用变量
- `style.css`：主题变量定义处，本身是变量来源

## 验证清单

- [x] 全部 6 个视图组件中不再存在写死的中性文字色（`#1e293b` / `#334155` / `#475569` / `#0f172a` / `#64748b` / `#94a3b8` / `#cbd5e1`）
- [x] `rg` 复查：残留仅 `AdminLayout.vue` 中有意保留的侧边栏/抽屉写死文字
- [x] `npm run build` 构建通过（25.27s，无新增错误，仅有既存 chunk 大小警告）
- [x] 背景色（`#f8fafc` / `#f1f5f9`）、背景渐变、语义状态色（红/绿/橙/蓝）均未被动用
- [ ] 浏览器手动验证：后台切换深色/浅色模式，检查各列表页（用户/笔记/分类/标签/附件/日志）文字可读性
