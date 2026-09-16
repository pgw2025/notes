# 移动端编辑工具栏 · 键盘遮挡改造方案（A+B 组合）

> 对应会话对话：用户选择「A. 悬浮于键盘 + 收起键」与「B. 键盘弹出自动隐藏」的组合方案。
> 本文件仅描述设计意图与实现要点，**不改动任何代码**，供后续实施时参考。

## 一、背景与目标

当前移动端笔记编辑页的快捷工具栏使用 `position: sticky` 吸底，并通过 `visualViewport`
的 `resize/scroll` 事件计算出 `keyboardHeight`，再用内联 `bottom: ${keyboardHeight}px`
把工具栏往上顶。实测在键盘弹出时，工具栏仍可能被键盘遮挡。

**根因**

1. `position: sticky` 与动态 `bottom` 语义冲突：sticky 只"粘"在滚动容器底部，而软键盘弹出
   并不撑大滚动高度，因此工具栏不会稳定地贴到新视口底部，被键盘盖上。
2. iOS 上 `fixed` 元素不随 `visualViewport` 缩放而移动，即便改成 `fixed` 也需手动补偿，
   现有实现对补偿点（offsetTop）的处理在不同浏览器间不可靠。

**方案目标**

- 输入过程中，工具栏**永不遮挡编辑区**。
- 键盘弹出后工具栏自动"收窄"，只保留高频格式化动作，其余收起。
- 提供一个**一键收起键盘**的常驻入口，让"编辑 / 看正文"自由切换。
- 键盘收起时，工具栏回落到底部安全区，保持单手触达。

---

## 二、总体设计（A+B 组合）

将原"常驻吸底工具条"拆成两种显隐状态，由 `keyboardHeight` 驱动切换：

| 状态 | 触发 | 内容 | 定位 |
|---|---|---|---|
| 展开态 | 键盘收起（`keyboardHeight === 0`） | 现有 7 个黄金按键（H/B/I/列表/行内代码/图片/更多） | `position: fixed`，贴底安全区上方 |
| 精简态 | 键盘弹出（`keyboardHeight > 0`） | 精简按键 + 一键收起键盘按钮 | `position: fixed`，贴到键盘顶部（随 `visualViewport` 偏移） |

> B 方案的思想落在"精简态"里：键盘弹出时不强行把完整工具条顶到键盘上方跟内容抢高度，
> 而是压缩成一条细工具条 + 增加收起键，让编辑区获得最大空间。

---

## 三、具体实现要点（供后续编码时对照）

以下均指向 `frontend/src/views/NoteEdit.vue` 及其样式 `frontend/src/views/NoteEdit.css`。

### 1) 定位方式：`sticky` → `fixed`

- 修改 `NoteEdit.css` 中 `.mobile-toolbar` 的定位：
  - `position: sticky` → `position: fixed`
  - 保留 `left: 0; right: 0; z-index: 20;`
  - 去掉原来对内联 `bottom` 的依赖，`bottom` 统一由内联 `mobileToolbarStyle` 决定。

### 2) 键盘补偿逻辑增强（`onViewportChange`）

- 现有实现（约 L798-803）已能算出 `keyboardHeight`，保留。
- 最关键补偿点：键盘弹出时工具栏应贴到**键盘可视区上沿**，即相对 `visualViewport` 定位。
  建议新增一个 `toolbarFixedBottom`（或直接扩展 `mobileToolbarStyle`）：
  - 键盘收起：`bottom = safe-area-inset-bottom`（默认回落到屏幕底部）
  - 键盘弹出：`bottom = keyboardHeight`（贴到键盘顶部）
- 需在 `resize` 与 `scroll` 两个监听里都刷新，并处理 `visualViewport.offsetTop`
  （iOS 滚动吞地址栏时 offsetTop 非 0）。现有 `onViewportChange` 已同时减去 offsetTop，
  逻辑可复用；重点是把它正确传导给 `fixed` 工具栏的 `bottom`。

### 3) 工具栏"精简态"（B）

- 在 `NoteEdit.vue` 的 `<template>`（移动工具栏容器，约 L241-332）中，为 7 个主按键按
  `keyboardHeight > 0` 做条件显隐：
  - **保留**：H / B / I / 列表 & 待办 / 行内代码（高频格式化）
  - **隐藏**：图片 🖼️、更多 `···`（低频，需要时收起键盘再取）
- 新增一个**收起键盘按钮**（放在工具条最右端），点击调用系统键盘收起。
  实现方式：
  - `textareaRef.value.blur()` 可收起大部分软键盘；
  - 或尝试 `document.activeElement.blur()`；
  - 收起后 `focus` 会自动触发 `onViewportChange`，`keyboardHeight` 归零，工具栏回落到展开态。

### 4) 内联样式 `mobileToolbarStyle` 调整

- 现有 `computed`（约 L964-973）中：
  - 需要把 `bottom` 从"基于 sticky 的占位偏移"改为"基于 fixed + 键盘补偿"的语义。
  - 精简态时 `padding-bottom` 不必再加 `keyboardHeight`，因为工具栏已贴到键盘顶部，
    只需保留键盘收起态的安全区 padding。

### 5) 顺带修正：`textareaDynamicStyle` 的冗余补偿

- 现有 `textareaDynamicStyle`（约 L957-962）给 textarea 固定加 `paddingBottom: 90 + keyboardHeight`。
- 若工具条改为 fixed 悬浮于键盘顶部，`paddingBottom` 仍建议保留一定的"防止最后一个字符被
  工具栏遮挡"容差，但其值可简化（见第四节视觉稿），避免编辑区被过度撑高。

---

## 四、精简要视觉稿（文字描述，取代原颜色取值）

- 展开态（无键盘）：
  - 工具栏悬浮在屏幕底部，`background` 采用半透明毛玻璃，`border-top` 细分割线，
    与正文视觉层级区分。
  - 7 个按键等宽分布（`flex:1; max-width:52px; height:36px;`），维持单指触达。
- 精简态（键盘弹出）：
  - 工具条收窄，仅左起 5 个高频键 + 最右端一个「收起键盘」竖向小按钮
    （图标如 ⌄ 或键盘收起符号）。
  - 工具条整体贴到键盘可视区上沿，`border-radius` 顶部圆角，营造"浮在键盘上"的观感。
  - 隐藏图片与更多两个按键，仅在键盘收起态恢复。

---

## 五、交互边界与注意事项

- **多浏览器差异**：`visualViewport` 在 iOS 与 Android 行为不同，`offsetTop` 补偿必须做
  兼容判断，避免地址栏收起/展开时工具栏跳动。
- **气泡菜单**：heading/list/more 三个气泡（`bubble--heading/--list/--more`）定位在按钮
  上方，若键盘弹出时精简态隐藏了 more 按钮，需保证"更多"气泡在收起键盘态才可打开。
- **强制弹窗/图片上传**：图片按钮在精简态隐藏后，用户需先收起键盘再插入图片，这是 B 方案
  的可接受代价；若体验不接受，可将图片按钮保留、仅隐藏 `···`。
- **性能**：`resize` 高频触发，`onViewportChange` 内应避免重 layout，采用 `requestAnimationFrame`
  或直接写内联 transform 仅轻量更新 `bottom`，避免整页重排。

---

## 六、涉及文件

| 文件 | 改动性质 |
|---|---|
| `frontend/src/views/NoteEdit.vue` | 修改：移动工具栏 template 精简态、新增收起键盘处理、`mobileToolbarStyle`/`onViewportChange` 逻辑 |
| `frontend/src/views/NoteEdit.css` | 修改：`.mobile-toolbar` 定位 sticky→fixed、新增精简态与收起按钮样式 |
| `frontend/src/views/NoteEdit.vue` | 新增：可能的 `collapseKeyboard()` 辅助函数 |

（若要复用，可考虑抽成独立组件，但当前范围保持最小改动，不新增组件。）