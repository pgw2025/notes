# 笔记列表移动端滚动问题修复 —— 方案 A 实施计划

> 状态：已实施 ✅（2026-09-16，代码已修改并通过 `npm run build`）
> 目标文件：`frontend/src/views/NotesList.vue`（仅新增移动端媒体查询样式，未改模板与逻辑，对应新增代码位于该文件 1447–1480 行）
> 作用域：仅移动端（`max-width: 1023px`），桌面端不受影响
> 验证：见 `checklist.md`，核心故障项（1–5）需在真机/移动端模拟器回归确认

---

## 1. 问题根因（复述）

- 移动端下 `.workbench-list-pane.mobile-list` 无固定高度、非 flex，内层 `.list-scroll-area` 的 `flex:1` 失效、`overflow-y:auto` 因无高度约束而不产生内部滚动（`scrollTop` 恒为 0）。
- 真正滚动的是 document body。
- Vant `useScrollParent` 依据 `overflow-y:auto` 把滚动父级错误判定为 `.list-scroll-area` → `reachTop` 恒为 `true`。
- `van-pull-refresh` 因此在任意“手指下滑”手势下命中 `preventDefault`，导致页面无法向上滚动（滑到底部后卡死）；`van-list` 触底判定也因此错乱（首屏拉光全部数据）。

本次方案 A：把所有包裹层改为**固定高度的 flex 列**，让 `.list-scroll-area` 成为唯一真实滚动容器，恢复 Vant 判定正确性。

---

## 2. 当前现状（改动前基线）

`NotesList.vue` `<style scoped>` 关键现状：

| 选择器 | 当前规则 | 行号（参考） |
|---|---|---|
| `.page` | `min-height: 100vh;` | 1161 |
| `.layout` | `min-height: calc(100vh - 46px);` | 1166 |
| `@media (min-width:1024px)` | 桌面端三栏 fixed 高度布局 | 1171 |
| `.list-scroll-area` | `flex:1; overflow-y:auto; outline:none;` | 1441 |

移动端目前**没有任何**同类固定高度的 media query。

**模板结构（移动端分支）**：

```
.page (根，含 class is-desktop/is-stage-*)
├─ van-nav-bar (top, 高 46px, 仅移动端)
└─ .layout
   └─ .workbench-list-pane.mobile-list
      └─ .list-scroll-area
         └─ van-pull-refresh > van-list > .notes-grid
```

---

## 3. 具体改动

仅需在 `<style scoped>` 中新增一段移动端 media query。**不改模板、不改 `<script>`。**

### 3.1 新增移动端布局覆盖（放在文件样式区适当位置，例如 `.list-scroll-area` 规则之后）

```css
/* ==================== 移动端：列表区收敛为内部滚动 ==================== */
@media (max-width: 1023px) {
  .page {
    height: 100dvh;                   /* 固定高度，替代 min-height */
    min-height: 0;
    display: flex;
    flex-direction: column;
    overflow: hidden;                 /* 页面不再整体随内容滚动 */
  }

  .layout {
    flex: 1;                          /* 占满 nav-bar 以下剩余空间 */
    min-height: 0;                    /* 关键：允许 flex 子项收缩 */
    display: flex;
    flex-direction: column;
    overflow: hidden;
    height: auto;                     /* 覆盖原 min-height 语义 */
  }

  .workbench-list-pane.mobile-list {
    flex: 1;
    min-height: 0;
    display: flex;
    flex-direction: column;
    overflow: hidden;
  }

  .list-scroll-area {
    flex: 1;
    min-height: 0;                    /* 关键：使 overflow 真正可滚动 */
    -webkit-overflow-scrolling: touch;
    overscroll-behavior: contain;
    /* 底部留出 fixed tabbar(50px) + 底部安全区 */
    padding-bottom: calc(var(--van-tabbar-height, 50px) + env(safe-area-inset-bottom));
  }
}
```

### 3.2 需要核对/可能一并微调的点

- **`.page` 顶部 nav-bar**：`--van-nav-bar-height: 46px`。移动端改为 flex 列后，flex 高度分配会自动让出 46px，无需手工减。
- **抽屉/其它移动端页面**：本次只作用于 `NotesList`（scoped），其它路由页面不受影响。
- **模板补充 class（可选）**：当前 CSS 直接按 `.page` / `.layout` 移动端宽度覆盖，与 `isDesktop` 逻辑一致（`useResponsive` 以 1024px 为界），无需改模板。若保守起见也可给根节点加 `.is-mobile` 类并用它做选择器，但会增加模板改动，非必要。

---

## 4. 改动前后行为对比

| 行为 | 改动前 | 改动后 |
|---|---|---|
| 滚动容器 | document body（整页） | `.list-scroll-area`（列表内部） |
| 滑到底部后向上滑 | 被 pull-refresh `preventDefault` 卡死 | 正常向上滚动 |
| 下拉刷新 | reachTop 恒真，任意位置下拉都触发 | 仅真正滚到顶部时才触发 |
| van-list 触底加载 | 首屏拉光全部页 + “没有更多了”错位 | 正常分页，到最后才显示“没有更多了” |
| 底部最后一条笔记 | 可能被 fixed tabbar 遮挡 | 有 padding 避让，不遮挡 |

---

## 5. 潜在风险与注意事项

1. **`100dvh` 与浏览器 URL 栏伸缩**：`dvh` 会随 URL 栏收起/展开变化。部分旧 WebView 不支持 `dvh`，建议真机核对；若异常可回退为 `100vh`（`dvh` 与 `vh` 差异主要在移动浏览器动态工具栏）。
2. **安全区**：刘海屏底部需 `env(safe-area-inset-bottom)`，已在 `padding-bottom` 中考虑。
3. **`-webkit-overflow-scrolling: touch`**：仅影响 iOS 惯性滚动，Android 无副作用。
4. **`overscroll-behavior: contain`**：约束列表区到边界时不把滚动“链式”传导给上方，配合内部滚动更顺滑；兼容性良好。
5. **桌面端回归**：改动全在 `max-width:1023px` 内，1024px+ 沿用原先三栏布局，不受影响——仍需在桌面端跑一遍回归。

---

## 6. 改动文件清单

| 文件 | 改动类型 | 内容 |
|---|---|---|
| `frontend/src/views/NotesList.vue` | 仅新增 CSS（媒体查询） | 新增移动端 flex 定高布局 + `.list-scroll-area` 内部滚动 |

无其它文件改动。