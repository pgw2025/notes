<template>
  <div class="page" :class="{ 'is-desktop': isDesktop, 'is-stage-fullscreen': isStageFullscreen, 'is-stage-collapsed': stageCollapsed }">
    <!-- 移动端顶部导航栏 -->
    <van-nav-bar v-if="!isDesktop" :title="currentCategoryName" class="top-nav-bar">
      <template #left>
        <van-icon name="bars" size="20" class="mobile-only menu-btn" @click="showDrawer = true" />
      </template>
      <template #right>
        <div class="nav-right-actions">
          <van-icon name="search" size="20" class="nav-search-btn" @click="$router.push('/search')" title="搜索" />
          <van-icon name="plus" size="22" class="nav-plus-btn" @click="$router.push('/notes/new')" title="新建笔记" />
        </div>
      </template>
    </van-nav-bar>

    <div class="layout" :class="{ 'desktop-workbench': isDesktop }">
      <!-- ==================== 中栏：笔记列表区 ==================== -->
      <aside class="workbench-list-pane" :class="{ 'mobile-list': !isDesktop }">
        <!-- 列表顶部工具条 -->
        <div class="list-toolbar">
          <div class="list-toolbar-top">
            <div class="list-title-wrap">
              <h2 class="list-title">{{ currentCategoryName }}</h2>
              <span class="list-count-badge">{{ filteredNotes.length }}</span>
            </div>
            <div class="list-toolbar-actions">
              <!-- 桌面端密度切换 -->
              <button
                v-if="isDesktop"
                class="toolbar-btn"
                :title="densityMode === 'compact' ? '切换为卡片视图' : '切换为紧凑列表'"
                @click="toggleDensityMode"
              >
                <van-icon :name="densityMode === 'compact' ? 'apps-o' : 'bars'" size="15" />
              </button>

              <!-- 快速新建 -->
              <router-link to="/notes/new" class="toolbar-new-btn" title="新建笔记 (Ctrl+N)">
                <van-icon name="plus" size="14" />
                <span v-if="!isDesktop">写笔记</span>
              </router-link>

              <!-- 收起态：展开主舞台（带文字，提高可发现性） -->
              <button
                v-if="isDesktop && stageCollapsed"
                class="toolbar-btn is-accent"
                title="展开主舞台 (Ctrl+Shift+→)"
                aria-label="展开主舞台"
                @click="toggleStageCollapsed"
              >
                <van-icon name="arrow-left" size="15" />
                <span class="toolbar-btn-text">展开</span>
              </button>
            </div>
          </div>

          <!-- 搜索过滤条 -->
          <div class="list-search-wrap">
            <van-icon name="search" class="list-search-icon" />
            <input
              v-model="listFilterQuery"
              type="text"
              class="list-search-input"
              placeholder="快速过滤标题与内容..."
            />
            <button
              v-if="listFilterQuery"
              class="list-search-clear"
              @click="listFilterQuery = ''"
            >
              <van-icon name="cross" size="12" />
            </button>
          </div>
        </div>

        <!-- 笔记滚动列表 -->
        <div class="list-scroll-area" tabindex="0" @keydown="handleListKeydown">
          <van-pull-refresh v-if="!isDesktop" v-model="refreshing" @refresh="onRefresh">
            <van-list
              v-model:loading="listLoading"
              :finished="listFinished"
              finished-text="没有更多了"
              @load="onLoadMore"
            >
              <!-- 移动端空状态 -->
              <div v-if="!loading && filteredNotes.length === 0" class="empty-wrap">
                <div class="empty-icon-box">📝</div>
                <p class="empty-title">{{ emptyTitle }}</p>
                <p class="empty-sub">{{ emptySub }}</p>
                <van-button type="primary" round icon="plus" size="small" @click="$router.push('/notes/new')">
                  新建笔记
                </van-button>
              </div>

              <!-- 移动端卡片网格 -->
              <div v-if="filteredNotes.length > 0" class="notes-grid">
                <van-swipe-cell
                  v-for="n in filteredNotes"
                  :key="n.id"
                  class="notes-grid-item"
                  :class="{ 'is-pinned': n.isPinned, 'is-selected': n.id === selectedNoteId }"
                >
                  <div class="note-card" :style="cardStyle(n)" @click="goDetail(n.id)">
                    <div v-if="n.isPinned" class="pinned-badge">
                      <van-icon name="star" size="12" />
                      <span>置顶</span>
                    </div>
                    <div class="note-title-row">
                      <div class="note-title">{{ n.title || '无标题' }}</div>
                    </div>
                    <div class="note-preview">{{ n.contentPreview || '暂无内容' }}</div>
                    <div class="note-meta">
                      <div class="tags-row">
                        <span v-if="n.categoryName" class="meta-tag cat-tag" :style="tagStyle(n)">
                          📁 {{ n.categoryName }}
                        </span>
                        <span v-for="t in n.tags" :key="t" class="meta-tag" :style="tagStyle(n)">
                          #{{ t }}
                        </span>
                      </div>
                      <span class="note-time" :style="{ color: timeColor(n) }">{{ formatTime(n.updatedAt) }}</span>
                    </div>
                  </div>
                  <template #right>
                    <van-button square type="primary" text="编辑" class="swipe-action-btn edit-btn" @click.stop="goEdit(n)" />
                    <van-button square type="warning" :text="n.isPinned ? '取消置顶' : '置顶'" class="swipe-action-btn pin-btn" @click.stop="onTogglePin(n)" />
                    <van-button square type="danger" text="删除" class="swipe-action-btn del-btn" @click.stop="onDelete(n)" />
                  </template>
                </van-swipe-cell>
              </div>
            </van-list>
          </van-pull-refresh>

          <!-- 桌面端列表容器 -->
          <div v-else class="desktop-list-container">
            <!-- 桌面端空状态 -->
            <div v-if="!loading && filteredNotes.length === 0" class="empty-wrap">
              <div class="empty-icon-box">📝</div>
              <p class="empty-title">{{ emptyTitle }}</p>
              <p class="empty-sub">{{ emptySub }}</p>
              <van-button type="primary" round icon="plus" size="small" @click="$router.push('/notes/new')">
                新建笔记
              </van-button>
            </div>

            <!-- 紧凑行模式 -->
            <div v-if="densityMode === 'compact'" class="compact-list-wrap">
              <div
                v-for="n in filteredNotes"
                :key="n.id"
                class="compact-list-row"
                :class="{ 'is-selected': n.id === selectedNoteId, 'is-pinned': n.isPinned }"
                @click="selectNote(n.id)"
              >
                <div class="row-color-indicator" :style="{ background: n.backgroundColor || 'var(--color-primary)' }"></div>
                <div class="row-main">
                  <div class="row-title-line">
                    <span v-if="n.isPinned" class="row-pin-star" title="置顶">★</span>
                    <span class="row-title">{{ n.title || '无标题' }}</span>
                  </div>
                  <div class="row-sub-line">
                    <span v-if="n.categoryName" class="row-cat">📁 {{ n.categoryName }}</span>
                    <span class="row-time">{{ formatTime(n.updatedAt) }}</span>
                  </div>
                </div>
                <div class="row-actions">
                  <button class="row-action-btn" :title="n.isPinned ? '取消置顶' : '置顶'" @click.stop="onTogglePin(n)">
                    <van-icon :name="n.isPinned ? 'star' : 'star-o'" size="13" />
                  </button>
                  <button class="row-action-btn row-del" title="删除" @click.stop="onDelete(n)">
                    <van-icon name="delete-o" size="13" />
                  </button>
                </div>
              </div>
            </div>

            <!-- 卡片模式 -->
            <div v-else class="card-list-wrap">
              <div
                v-for="n in filteredNotes"
                :key="n.id"
                class="desktop-note-card-item"
                :class="{ 'is-selected': n.id === selectedNoteId, 'is-pinned': n.isPinned }"
                :style="cardStyle(n)"
                @click="selectNote(n.id)"
              >
                <div v-if="n.isPinned" class="pinned-badge">
                  <van-icon name="star" size="11" />
                  <span>置顶</span>
                </div>
                <div class="note-title-row">
                  <div class="note-title">{{ n.title || '无标题' }}</div>
                  <div class="card-hover-actions">
                    <button class="card-action-btn" :title="n.isPinned ? '取消置顶' : '置顶'" @click.stop="onTogglePin(n)">
                      <van-icon :name="n.isPinned ? 'star' : 'star-o'" size="13" />
                    </button>
                    <button class="card-action-btn" title="编辑" @click.stop="selectNote(n.id, { edit: true })">
                      <van-icon name="edit" size="13" />
                    </button>
                    <button class="card-action-btn btn-danger" title="删除" @click.stop="onDelete(n)">
                      <van-icon name="delete-o" size="13" />
                    </button>
                  </div>
                </div>
                <div class="note-preview">{{ n.contentPreview || '暂无内容' }}</div>
                <div class="note-meta">
                  <div class="tags-row">
                    <span v-if="n.categoryName" class="meta-tag cat-tag" :style="tagStyle(n)">
                      📁 {{ n.categoryName }}
                    </span>
                    <span v-for="t in n.tags" :key="t" class="meta-tag" :style="tagStyle(n)">
                      #{{ t }}
                    </span>
                  </div>
                  <span class="note-time" :style="{ color: timeColor(n) }">{{ formatTime(n.updatedAt) }}</span>
                </div>
              </div>
            </div>
          </div>
        </div>
      </aside>

      <!-- 桌面端：中栏与舞台之间的分隔手柄，点击收起/展开舞台 -->
      <div
        v-if="isDesktop"
        class="stage-divider"
        :class="{ 'is-stage-collapsed': stageCollapsed }"
        :title="stageCollapsed ? '展开主舞台 (Ctrl+Shift+→)' : '收起主舞台，仅显示笔记列表'"
        @click="toggleStageCollapsed"
      >
        <span class="divider-grip"></span>
        <span v-if="stageCollapsed" class="expand-capsule" aria-hidden="true"></span>
      </div>

      <!-- ==================== 右栏：主工作舞台 (桌面端) ==================== -->
      <main v-if="isDesktop" class="workbench-main-stage">
        <!-- 加载态 -->
        <div v-if="detailLoading" class="stage-loading">
          <van-loading size="32" color="var(--color-primary)">加载笔记中...</van-loading>
        </div>

        <!-- 选中笔记工作台 -->
        <template v-else-if="detailNote">
          <div class="stage-container" :style="stageBackgroundStyle">
            <!-- 顶部工作台主工具条 -->
            <header class="stage-header">
              <div class="stage-header-left">
                <!-- 模式切换：阅读 vs 分屏编辑 -->
                <div class="mode-toggle-group">
                  <button
                    class="mode-btn"
                    :class="{ 'is-active': !editMode }"
                    title="阅读模式 (Ctrl+E)"
                    @click="editMode = false"
                  >
                    <van-icon name="notes-o" size="14" />
                    <span>阅读</span>
                  </button>
                  <button
                    class="mode-btn"
                    :class="{ 'is-active': editMode }"
                    title="双栏编辑模式 (Ctrl+E)"
                    @click="enterEdit"
                  >
                    <van-icon name="edit" size="14" />
                    <span>编辑</span>
                  </button>
                </div>

                <!-- 自动保存/状态提示 -->
                <div v-if="editMode" class="stage-save-status">
                  <span class="status-dot" :class="{ 'is-saving': saving, 'is-saved': !saving }"></span>
                  <span class="status-text">{{ saving ? '保存中...' : (lastSavedTimeText ? `已保存 ${lastSavedTimeText}` : '准备就绪') }}</span>
                </div>
              </div>

              <!-- 右侧动作按钮组 -->
              <div class="stage-header-actions">
                <!-- 调色盘快速选用 -->
                <div class="color-picker-wrapper">
                  <button
                    class="stage-action-btn"
                    :style="{ background: detailNote.backgroundColor || 'var(--surface)' }"
                    title="修改笔记背景色"
                    @click="showColorDropdown = !showColorDropdown"
                  >
                    🎨
                  </button>
                  <!-- 悬浮颜色气泡 -->
                  <div v-if="showColorDropdown" class="color-dropdown-bubble">
                    <button
                      v-for="color in presetColors"
                      :key="color"
                      class="color-dot-btn"
                      :style="{ background: color }"
                      :class="{ 'is-current': detailNote.backgroundColor === color }"
                      @click="changeNoteColor(color)"
                    ></button>
                    <button
                      class="color-reset-btn"
                      title="重置为系统默认"
                      @click="changeNoteColor(null)"
                    >
                      默认
                    </button>
                  </div>
                </div>

                <!-- 版本历史入口 (Diff 比对) -->
                <button
                  class="stage-action-btn"
                  title="版本历史与对比"
                  @click="openVersionDrawer"
                >
                  <van-icon name="clock-o" size="16" />
                  <span class="btn-text">历史</span>
                </button>

                <!-- 置顶按钮 -->
                <button
                  class="stage-action-btn"
                  :class="{ 'is-starred': detailNote.isPinned }"
                  :title="detailNote.isPinned ? '取消置顶' : '置顶'"
                  @click="onTogglePin(detailNote)"
                >
                  <van-icon :name="detailNote.isPinned ? 'star' : 'star-o'" size="16" />
                </button>

                <!-- 全屏切换 -->
                <button
                  class="stage-action-btn"
                  :title="isStageFullscreen ? '退出全屏 (Esc)' : '全屏专注'"
                  @click="isStageFullscreen = !isStageFullscreen"
                >
                  <van-icon :name="isStageFullscreen ? 'shrink' : 'expand-o'" size="16" />
                </button>

                <!-- 收起主舞台（仅显示笔记列表） -->
                <button
                  class="stage-action-btn stage-collapse-btn"
                  title="收起主舞台，仅显示笔记列表"
                  aria-label="收起主舞台"
                  @click="toggleStageCollapsed"
                >
                  <van-icon name="arrow" size="16" />
                </button>

                <!-- 删除笔记 -->
                <button
                  class="stage-action-btn btn-danger"
                  title="删除笔记"
                  @click="onDelete(detailNote)"
                >
                  <van-icon name="delete-o" size="16" />
                </button>
              </div>
            </header>

            <!-- ==================== 1. 阅读模式 ==================== -->
            <div v-if="!editMode" class="stage-view-mode">
              <div class="reading-scroll-viewport">
                <article class="reading-article-content pane-reading-content">
                  <!-- 标题 -->
                  <h1 class="reading-title">{{ detailNote.title || '无标题' }}</h1>

                  <!-- 元信息栏 -->
                  <div class="reading-meta-bar">
                    <div class="meta-chips-wrap">
                      <span v-if="detailNote.categoryName" class="meta-chip chip-cat">
                        📁 {{ detailNote.categoryName }}
                      </span>
                      <span v-for="tag in detailNote.tags" :key="tag" class="meta-chip chip-tag">
                        #{{ tag }}
                      </span>
                    </div>
                    <div class="meta-extra-info">
                      <span>更新于 {{ formatTime(detailNote.updatedAt) }}</span>
                      <span class="meta-divider">·</span>
                      <span>{{ (detailNote.content || '').length }} 字符</span>
                    </div>
                  </div>

                  <!-- Markdown 正文 (支持 KaTeX 与代码折叠) -->
                  <div class="reading-body">
                    <MarkdownBody :content="detailNote.content" :collapsible="true" />
                  </div>
                </article>

                <!-- 右侧悬浮目录大纲 (TOC) -->
                <aside class="reading-toc-aside">
                  <MarkdownToc :content="detailNote.content" container-selector=".pane-reading-content" />
                </aside>
              </div>
            </div>

            <!-- ==================== 2. 分屏编辑模式 ==================== -->
            <div v-else class="stage-edit-mode">
              <!-- 快捷格式排版辅助条 -->
              <div class="edit-formatting-toolbar">
                <button class="fmt-btn" title="一级标题" @click="insertFormat('# ')">H1</button>
                <button class="fmt-btn" title="二级标题" @click="insertFormat('## ')">H2</button>
                <button class="fmt-btn" title="三级标题" @click="insertFormat('### ')">H3</button>
                <span class="fmt-divider"></span>
                <button class="fmt-btn" title="加粗 (Ctrl+B)" @click="insertWrap('**', '**')"><b>B</b></button>
                <button class="fmt-btn" title="斜体 (Ctrl+I)" @click="insertWrap('*', '*')"><i>I</i></button>
                <button class="fmt-btn" title="删除线" @click="insertWrap('~~', '~~')"><s>S</s></button>
                <span class="fmt-divider"></span>
                <button class="fmt-btn" title="代码块" @click="insertFormat('```\n\n```', 4)">Code</button>
                <button class="fmt-btn" title="行内数学公式" @click="insertWrap('$', '$')">$x$</button>
                <button class="fmt-btn" title="块级数学公式" @click="insertFormat('$$\n\\frac{a}{b}\n$$\n')">$$</button>
                <button class="fmt-btn" title="任务列表" @click="insertFormat('- [ ] ')">Task</button>
                <button class="fmt-btn" title="表格" @click="insertFormat('| 标题 1 | 标题 2 |\n|---|---|\n| 内容 1 | 内容 2 |\n')">Table</button>
                <button class="fmt-btn" title="引用块" @click="insertFormat('> ')">Quote</button>
                <div class="fmt-right-actions">
                  <van-button size="small" type="primary" :loading="saving" @click="saveFullEdit(true)">
                    保存版本 (Ctrl+S)
                  </van-button>
                </div>
              </div>

              <!-- 编辑主区域：左源码编辑 + 右实时预览 -->
              <div class="split-editor-grid">
                <!-- 左侧源码输入 -->
                <div class="editor-pane-left">
                  <input
                    v-model="editTitle"
                    class="editor-title-input"
                    placeholder="笔记标题..."
                    @input="onContentChanged"
                  />
                  <textarea
                    ref="contentTextareaRef"
                    v-model="editContent"
                    class="editor-content-textarea"
                    placeholder="使用 Markdown 书写正文...（支持 KaTeX 数学公式、代码高亮、表格）"
                    @input="onContentChanged"
                    @keydown="handleEditorKeydown"
                  ></textarea>
                </div>

                <!-- 右侧实时预览 -->
                <div class="editor-pane-right">
                  <div class="preview-header-label">实时渲染预览</div>
                  <div class="preview-body-content">
                    <h1 class="preview-rendered-title">{{ editTitle || '无标题' }}</h1>
                    <MarkdownBody :content="editContent" :collapsible="true" />
                  </div>
                </div>
              </div>
            </div>
          </div>
        </template>

        <!-- 未选择笔记时的桌面欢迎空面板 -->
        <div v-else class="stage-empty-placeholder">
          <div class="empty-hero">
            <div class="hero-logo-box">
              <img src="/pwa-192x192.png" alt="云笺笔记" class="hero-logo-img" />
            </div>
            <h3 class="hero-title">云笺笔记工作台</h3>
            <p class="hero-subtitle">轻量、极简、专注的知识与灵感空间</p>

            <div class="shortcuts-card">
              <div class="shortcuts-card-title">桌面快捷键速查</div>
              <div class="shortcuts-grid">
                <div class="shortcut-item">
                  <span class="sc-desc">全局命令搜索</span>
                  <kbd class="sc-kbd">⌘ / Ctrl + K</kbd>
                </div>
                <div class="shortcut-item">
                  <span class="sc-desc">快速新建笔记</span>
                  <kbd class="sc-kbd">⌘ / Ctrl + N</kbd>
                </div>
                <div class="shortcut-item">
                  <span class="sc-desc">折叠/展开侧栏</span>
                  <kbd class="sc-kbd">⌘ / Ctrl + \</kbd>
                </div>
                <div class="shortcut-item">
                  <span class="sc-desc">保存快照</span>
                  <kbd class="sc-kbd">⌘ / Ctrl + S</kbd>
                </div>
                <div class="shortcut-item">
                  <span class="sc-desc">切换编辑/阅读</span>
                  <kbd class="sc-kbd">⌘ / Ctrl + E</kbd>
                </div>
                <div class="shortcut-item">
                  <span class="sc-desc">列表上下切换</span>
                  <kbd class="sc-kbd">↑ / ↓</kbd>
                </div>
              </div>
            </div>

            <van-button type="primary" icon="plus" round @click="$router.push('/notes/new')">
              创建第一篇笔记
            </van-button>
          </div>
        </div>
      </main>
    </div>

    <!-- ==================== 版本历史与 Diff 对比抽屉 ==================== -->
    <van-popup
      v-model:show="showVersionDrawer"
      position="right"
      :style="{ width: isDesktop ? '600px' : '90%', height: '100%' }"
    >
      <div class="version-drawer-inner">
        <div class="version-drawer-header">
          <div class="drawer-title-box">
            <span class="drawer-title">版本历史快照</span>
            <span class="drawer-count">{{ versionList.length }} 个版本</span>
          </div>
          <van-icon name="cross" size="18" class="drawer-close" @click="showVersionDrawer = false" />
        </div>

        <!-- 差异比对主界面 -->
        <div v-if="selectedVersion" class="version-diff-container">
          <div class="diff-header-bar">
            <button class="back-to-versions-btn" @click="selectedVersion = null">
              <van-icon name="arrow-left" size="12" />
              <span>返回版本列表</span>
            </button>
          </div>
          <DiffViewer
            :old-content="selectedVersion.content"
            :new-content="detailNote?.content"
            :old-time="selectedVersion.createdAt"
            :new-time="detailNote?.updatedAt"
            :restoring="restoringVersion"
            @restore="confirmRestoreVersion(selectedVersion)"
          />
        </div>

        <!-- 版本快照列表 -->
        <div v-else class="version-list-scroll">
          <div v-if="loadingVersions" class="version-loading">
            <van-loading size="24">加载历史版本...</van-loading>
          </div>
          <div v-else-if="versionList.length === 0" class="version-empty">
            <div class="v-empty-icon">🕒</div>
            <p>暂无其他历史版本快照</p>
            <p class="v-empty-sub">每次编辑保存时，系统均会自动归档快照</p>
          </div>
          <div v-else class="version-items-box">
            <div
              v-for="v in versionList"
              :key="v.id"
              class="version-card"
              :class="{ 'is-current': v.isCurrent }"
              @click="inspectVersion(v)"
            >
              <div class="v-card-top">
                <span class="v-time">{{ formatTime(v.createdAt) }}</span>
                <span v-if="v.isCurrent" class="v-badge-current">当前最新</span>
                <span v-else class="v-badge-compare">点击对比差异 →</span>
              </div>
              <div class="v-card-title">{{ v.title || '无标题' }}</div>
              <div class="v-card-preview">{{ v.preview || '暂无内容' }}</div>
            </div>
          </div>
        </div>
      </div>
    </van-popup>

    <!-- 移动端：分类与标签导航抽屉 -->
    <van-popup
      v-model:show="showDrawer"
      position="left"
      :style="{ width: '75%', maxWidth: '320px', height: '100%' }"
    >
      <div class="drawer-inner">
        <div class="drawer-header">
          <span class="drawer-title">分类与标签</span>
          <van-icon name="cross" size="18" @click="showDrawer = false" />
        </div>
        <CategoryNav
          v-model="activeCategoryId"
          v-model:tagModelValue="activeTagId"
          :categories="categories"
          :tags="tags"
        />
      </div>
    </van-popup>
  </div>
</template>

<script setup>
import { ref, onActivated, onMounted, onBeforeUnmount, computed, watch, nextTick } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { showConfirmDialog, showToast } from 'vant'
import http from '../api/http'
import { formatTime } from '../utils/format'
import { resolveNoteColor, getContrastColor, isDarkColor } from '../utils/color'
import { findCategoryById } from '../utils/categoryTree'
import { useAuthStore } from '../stores/auth'
import { useThemeStore } from '../stores/theme'
import { useResponsive } from '../composables/useResponsive'
import CategoryNav from '../components/CategoryNav.vue'
import MarkdownBody from '../components/MarkdownBody.vue'
import MarkdownToc from '../components/MarkdownToc.vue'
import DiffViewer from '../components/DiffViewer.vue'

defineOptions({ name: 'NotesList' })

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const theme = useThemeStore()
const { isDesktop } = useResponsive()

const isDarkEffective = computed(() => theme.isDarkEffective)

const notes = ref([])
const categories = ref([])
const tags = ref([])
const activeCategoryId = ref(null)
const activeTagId = ref(null)

let applyingFromRoute = false
let routeLoadSeq = 0
let lastRouteLoadSeqAtActivation = 0

const viewMode = computed(() => {
  if (activeTagId.value != null) return 'tag'
  if (activeCategoryId.value != null) return 'category'
  return 'home'
})

const showDrawer = ref(false)
const refreshing = ref(false)
const loading = ref(false)
const listLoading = ref(false)
const listFinished = ref(false)
const currentPage = ref(1)
const pageSize = 50

// 桌面端视图偏好设置
const densityMode = ref(localStorage.getItem('notes_density') || 'card') // 'card' | 'compact'
function toggleDensityMode() {
  densityMode.value = densityMode.value === 'card' ? 'compact' : 'card'
  localStorage.setItem('notes_density', densityMode.value)
}

// 排序设置: 'updated' | 'created' | 'title'
const sortOrder = ref(localStorage.getItem('notes_sort') || 'updated')

// 搜索过滤词
const listFilterQuery = ref('')

const filteredNotes = computed(() => {
  let list = [...notes.value]

  // 本地模糊过滤
  const q = listFilterQuery.value.trim().toLowerCase()
  if (q) {
    list = list.filter(
      (n) =>
        (n.title && n.title.toLowerCase().includes(q)) ||
        (n.contentPreview && n.contentPreview.toLowerCase().includes(q)) ||
        (n.tags && n.tags.some((t) => t.toLowerCase().includes(q)))
    )
  }

  // 排序
  if (sortOrder.value === 'created') {
    list.sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime())
  } else if (sortOrder.value === 'title') {
    list.sort((a, b) => (a.title || '').localeCompare(b.title || '', 'zh-CN'))
  } else {
    // 默认更新时间（置顶优先）
    list.sort((a, b) => {
      if (a.isPinned !== b.isPinned) return a.isPinned ? -1 : 1
      return new Date(b.updatedAt).getTime() - new Date(a.updatedAt).getTime()
    })
  }

  return list
})

const currentCategoryName = computed(() => {
  if (activeTagId.value != null) {
    const foundTag = tags.value.find((t) => t.id === activeTagId.value)
    return foundTag ? `#${foundTag.name}` : '标签笔记'
  }
  if (activeCategoryId.value == null) return '全部笔记'
  return findCategoryById(categories.value, activeCategoryId.value)?.name ?? '笔记'
})

const emptyTitle = computed(() => {
  if (listFilterQuery.value) return '未找到匹配的笔记'
  if (viewMode.value === 'home') return '还没有笔记'
  if (viewMode.value === 'tag') return '该标签下暂无笔记'
  return '该分类下暂无笔记'
})
const emptySub = computed(() => {
  if (listFilterQuery.value) return '尝试输入其他关键词过滤'
  if (viewMode.value === 'home') return '置顶的笔记和未分类的笔记会显示在这里'
  return '记录灵感、待办、会议纪要或学习心得'
})

// ==================== 桌面端主舞台状态 ====================
const selectedNoteId = ref(null)
const detailNote = ref(null)
const detailLoading = ref(false)
const editMode = ref(false)
const editTitle = ref('')
const editContent = ref('')
const saving = ref(false)
const lastSavedTimeText = ref('')
const isStageFullscreen = ref(false)
const stageCollapsed = ref(localStorage.getItem('notes_stage_collapsed') === '1')

function toggleStageCollapsed() {
  stageCollapsed.value = !stageCollapsed.value
  localStorage.setItem('notes_stage_collapsed', stageCollapsed.value ? '1' : '0')
}
const showColorDropdown = ref(false)
const contentTextareaRef = ref(null)

let autoSaveTimer = null

const presetColors = [
  '#FFFFFF',
  '#FFF9C4', // 暖黄
  '#C8E6C9', // 薄荷绿
  '#BBDEFB', // 柔蓝
  '#E1BEE7', // 薰衣草紫
  '#FFE0B2', // 暖橙
  '#F5F5F5'  // 浅灰
]

const stageBackgroundStyle = computed(() => {
  if (!detailNote.value?.backgroundColor) return {}
  const bg = detailNote.value.backgroundColor
  return {
    '--stage-accent-bg': bg
  }
})

// 选中并加载笔记
async function selectNote(id, opts = {}) {
  // 桌面端：用户主动选中笔记时，若主舞台处于收起态则自动展开
  // keepCollapsed 用于 loadNotes 的默认选中，避免覆盖用户手动收起的偏好
  if (!opts.keepCollapsed && isDesktop.value && stageCollapsed.value) toggleStageCollapsed()
  if (selectedNoteId.value === id && !opts.edit) {
    editMode.value = false
    return
  }
  selectedNoteId.value = id
  editMode.value = !!opts.edit
  detailLoading.value = true
  try {
    const d = await http.get(`/notes/${id}`)
    detailNote.value = d
    editTitle.value = d.title || ''
    editContent.value = d.content || ''
    lastSavedTimeText.value = formatTime(d.updatedAt)
  } catch {
    detailNote.value = null
  } finally {
    detailLoading.value = false
  }
}

function enterEdit() {
  if (!detailNote.value) return
  editTitle.value = detailNote.value.title || ''
  editContent.value = detailNote.value.content || ''
  editMode.value = true
  nextTick(() => contentTextareaRef.value?.focus())
}

function onContentChanged() {
  clearTimeout(autoSaveTimer)
  autoSaveTimer = setTimeout(() => {
    saveFullEdit(false) // 静默自动保存（不创建版本历史）
  }, 1500)
}

async function saveFullEdit(createVersion = false) {
  if (!detailNote.value || saving.value) return
  const d = detailNote.value
  saving.value = true
  try {
    const tagIds = (d.tags || [])
      .map((name) => tags.value.find((t) => t.name === name)?.id)
      .filter(Boolean)

    await http.put(`/notes/${d.id}`, {
      title: editTitle.value,
      content: editContent.value,
      categoryId: d.categoryId ?? null,
      tagIds,
      backgroundColor: d.backgroundColor ?? null,
      isPinned: d.isPinned ?? false,
      createVersion
    })

    const now = new Date().toISOString()
    const preview = makePreview(editContent.value)
    detailNote.value = { ...d, title: editTitle.value, content: editContent.value, updatedAt: now }
    lastSavedTimeText.value = formatTime(now)

    const listItem = notes.value.find((n) => n.id === d.id)
    if (listItem) {
      listItem.title = editTitle.value
      listItem.contentPreview = preview
      listItem.updatedAt = now
    }

    if (createVersion) {
      showToast('已保存并创建版本快照')
    }
  } catch {
    // 错误由拦截器处理
  } finally {
    saving.value = false
  }
}

// 修改背景色
async function changeNoteColor(colorHex) {
  if (!detailNote.value) return
  detailNote.value.backgroundColor = colorHex
  showColorDropdown.value = false
  await http.put(`/notes/${detailNote.value.id}`, {
    title: detailNote.value.title,
    content: detailNote.value.content,
    categoryId: detailNote.value.categoryId,
    backgroundColor: colorHex,
    isPinned: detailNote.value.isPinned
  })
  const item = notes.value.find((n) => n.id === detailNote.value.id)
  if (item) item.backgroundColor = colorHex
  showToast('已更新背景色')
}

// 格式辅助工具
function insertFormat(prefix, cursorOffset) {
  const el = contentTextareaRef.value
  if (!el) return
  const start = el.selectionStart
  const end = el.selectionEnd
  const val = editContent.value
  editContent.value = val.slice(0, start) + prefix + val.slice(end)
  onContentChanged()
  nextTick(() => {
    el.focus()
    const pos = start + (cursorOffset !== undefined ? cursorOffset : prefix.length)
    el.setSelectionRange(pos, pos)
  })
}

function insertWrap(before, after) {
  const el = contentTextareaRef.value
  if (!el) return
  const start = el.selectionStart
  const end = el.selectionEnd
  const selected = editContent.value.slice(start, end)
  const wrapped = `${before}${selected || '文字'}${after}`
  editContent.value = editContent.value.slice(0, start) + wrapped + editContent.value.slice(end)
  onContentChanged()
  nextTick(() => {
    el.focus()
    el.setSelectionRange(start + before.length, start + before.length + (selected.length || 2))
  })
}

// 键盘快捷键处理
function handleEditorKeydown(e) {
  const isCtrlOrCmd = e.ctrlKey || e.metaKey
  // Ctrl/Cmd + S -> 保存版本快照
  if (isCtrlOrCmd && (e.key === 's' || e.key === 'S')) {
    e.preventDefault()
    saveFullEdit(true)
    return
  }
  // Tab 缩进
  if (e.key === 'Tab') {
    e.preventDefault()
    insertFormat('  ')
  }
}

// 列表区键盘上下键导航
function handleListKeydown(e) {
  if (['INPUT', 'TEXTAREA'].includes(e.target?.tagName)) return
  if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
    e.preventDefault()
    const list = filteredNotes.value
    if (list.length === 0) return
    const curIdx = list.findIndex((n) => n.id === selectedNoteId.value)
    let nextIdx = curIdx
    if (e.key === 'ArrowDown') {
      nextIdx = curIdx < list.length - 1 ? curIdx + 1 : 0
    } else {
      nextIdx = curIdx > 0 ? curIdx - 1 : list.length - 1
    }
    selectNote(list[nextIdx].id)
  }
}

// ==================== 版本历史与对比 ====================
const showVersionDrawer = ref(false)
const versionList = ref([])
const loadingVersions = ref(false)
const selectedVersion = ref(null)
const restoringVersion = ref(false)

async function openVersionDrawer() {
  if (!detailNote.value) return
  showVersionDrawer.value = true
  selectedVersion.value = null
  loadingVersions.value = true
  try {
    const list = await http.get(`/notes/${detailNote.value.id}/versions`)
    versionList.value = list || []
  } catch {
    versionList.value = []
  } finally {
    loadingVersions.value = false
  }
}

async function inspectVersion(v) {
  try {
    const full = await http.get(`/notes/${detailNote.value.id}/versions/${v.id}`)
    selectedVersion.value = full
  } catch {
    showToast('加载历史版本详情失败')
  }
}

async function confirmRestoreVersion(v) {
  try {
    await showConfirmDialog({
      title: '恢复版本',
      message: `确定恢复到 ${formatTime(v.createdAt)} 的版本吗？当前内容将被作为新快照归档。`
    })
    restoringVersion.value = true
    await http.post(`/notes/${detailNote.value.id}/versions/${v.id}/rollback`)
    showToast('已成功恢复至该版本')
    showVersionDrawer.value = false
    selectedVersion.value = null
    // 刷新笔记详情
    await selectNote(detailNote.value.id)
    await loadNotes()
  } catch {
    // 用户取消
  } finally {
    restoringVersion.value = false
  }
}

// 置顶切换
async function onTogglePin(note) {
  const nextPinned = !note.isPinned
  try {
    await http.post(nextPinned ? `/notes/${note.id}/pin` : `/notes/${note.id}/unpin`)
    note.isPinned = nextPinned
    if (detailNote.value && detailNote.value.id === note.id) {
      detailNote.value.isPinned = nextPinned
    }
    const listItem = notes.value.find((n) => n.id === note.id)
    if (listItem) listItem.isPinned = nextPinned
    showToast(nextPinned ? '已置顶' : '已取消置顶')
  } catch {
    // ignore
  }
}

async function onDelete(note) {
  try {
    await showConfirmDialog({ title: '删除笔记', message: `确定删除「${note.title || '无标题'}」吗？` })
    await http.delete(`/notes/${note.id}`)
    notes.value = notes.value.filter((n) => n.id !== note.id)
    if (selectedNoteId.value === note.id) {
      selectedNoteId.value = null
      detailNote.value = null
      editMode.value = false
    }
    showToast('已删除')
  } catch {
    // 取消
  }
}

function goDetail(id) {
  if (isDesktop.value) {
    selectNote(id)
    return
  }
  router.push(`/notes/${id}`)
}

function goEdit(n) {
  const id = typeof n === 'object' ? n.id : n
  if (isDesktop.value) {
    selectNote(id, { edit: true })
    return
  }
  router.push(`/notes/${id}/edit`)
}

function makePreview(content) {
  const text = (content || '')
    .replace(/```[\s\S]*?```/g, ' ')
    .replace(/[#>*`~\-\[\]()!|]/g, ' ')
    .replace(/\s+/g, ' ')
    .trim()
  return text.length > 120 ? text.slice(0, 120) + '…' : text
}

// ==================== 数据拉取 ====================
async function loadCategories() {
  try {
    categories.value = await http.get('/categories')
  } catch {
    categories.value = []
  }
}

async function loadTags() {
  try {
    tags.value = await http.get('/tags')
  } catch {
    tags.value = []
  }
}

async function loadNotes() {
  loading.value = true
  currentPage.value = 1
  try {
    const params = { page: 1, pageSize }
    if (activeCategoryId.value != null) params.categoryId = activeCategoryId.value
    if (activeTagId.value != null) params.tagId = activeTagId.value
    const res = await http.get('/notes', { params })
    notes.value = res.items || []
    listFinished.value = (res.items || []).length < pageSize

    // 桌面端：若当前无选中笔记，默认选中第一篇（keepCollapsed：不因此展开主舞台）
    if (isDesktop.value && !selectedNoteId.value && notes.value.length > 0) {
      selectNote(notes.value[0].id, { keepCollapsed: true })
    }
  } catch {
    notes.value = []
  } finally {
    loading.value = false
  }
}

async function onLoadMore() {
  if (listLoading.value || listFinished.value) return
  listLoading.value = true
  try {
    const nextPage = currentPage.value + 1
    const params = { page: nextPage, pageSize }
    if (activeCategoryId.value != null) params.categoryId = activeCategoryId.value
    if (activeTagId.value != null) params.tagId = activeTagId.value
    const res = await http.get('/notes', { params })
    const items = res.items || []
    notes.value.push(...items)
    currentPage.value = nextPage
    if (items.length < pageSize) listFinished.value = true
  } finally {
    listLoading.value = false
  }
}

function onRefresh() {
  Promise.all([loadCategories(), loadTags(), loadNotes()]).finally(() => {
    refreshing.value = false
  })
}

function cardStyle(note) {
  const bg = resolveNoteColor(note.backgroundColor, auth.user?.defaultNoteColor, isDarkEffective.value)
  const fg = getContrastColor(bg)
  return {
    backgroundColor: bg,
    color: fg
  }
}

function tagStyle(note) {
  const bg = resolveNoteColor(note.backgroundColor, auth.user?.defaultNoteColor, isDarkEffective.value)
  const isDark = isDarkColor(bg)
  return isDark
    ? { backgroundColor: 'rgba(255, 255, 255, 0.15)', color: '#ffffff' }
    : { backgroundColor: 'rgba(0, 0, 0, 0.06)', color: 'inherit' }
}

function timeColor(note) {
  const bg = resolveNoteColor(note.backgroundColor, auth.user?.defaultNoteColor, isDarkEffective.value)
  return getContrastColor(bg)
}

onMounted(() => {
  loadCategories()
  loadTags()
  loadNotes()
  window.addEventListener('keydown', handleStageKeydown)
})

onBeforeUnmount(() => {
  window.removeEventListener('keydown', handleStageKeydown)
})

// 主舞台快捷键：Ctrl/Cmd + Shift + → 展开；桌面端 Esc 收右栏并取消选中
function handleStageKeydown(e) {
  const isCtrlOrCmd = e.ctrlKey || e.metaKey
  if (isCtrlOrCmd && e.shiftKey && e.key === 'ArrowRight') {
    e.preventDefault()
    if (stageCollapsed.value) toggleStageCollapsed()
    return
  }
  // 桌面端按 Esc：收起右栏并取消列表选中
  if (isDesktop.value && e.key === 'Escape') {
    if (!stageCollapsed.value) toggleStageCollapsed()
    if (selectedNoteId.value !== null) {
      selectedNoteId.value = null
      detailNote.value = null
    }
  }
}

watch(
  () => [route.query.categoryId, route.query.tagId],
  ([cat, tag]) => {
    activeCategoryId.value = cat ? parseInt(cat, 10) : null
    activeTagId.value = tag ? parseInt(tag, 10) : null
    loadNotes()
  }
)

// 收起舞台与全屏专注（隐藏中栏）方向相反，二者互斥
watch(stageCollapsed, (v) => {
  if (v) isStageFullscreen.value = false
})
watch(isStageFullscreen, (v) => {
  if (v) stageCollapsed.value = false
})
</script>

<style scoped>
.page {
  min-height: 100vh;
  background: var(--app-bg);
}

.layout {
  min-height: calc(100vh - 46px);
}

/* ==================== 桌面端三栏工作台 ==================== */
@media (min-width: 1024px) {
  .page.is-desktop {
    height: 100vh;
    overflow: hidden;
  }

  .desktop-workbench {
    display: flex;
    height: 100vh;
    overflow: hidden;
  }

  /* 中栏：笔记列表 */
  .workbench-list-pane {
    width: 360px;
    flex-shrink: 0;
    height: 100vh;
    display: flex;
    flex-direction: column;
    background: var(--surface);
    border-right: 1px solid var(--border);
    box-sizing: border-box;
    transition: width 0.22s ease;
  }

  /* 右栏：主工作舞台 */
  .workbench-main-stage {
    flex: 1;
    min-width: 0;
    height: 100vh;
    display: flex;
    flex-direction: column;
    background: var(--app-bg);
    overflow: hidden;
    position: relative;
    transition: flex 0.22s ease, opacity 0.22s ease;
  }

  /* 全屏专注态 */
  .page.is-stage-fullscreen .workbench-list-pane {
    display: none;
  }

  /* 收起右栏主舞台：收缩为 0 */
  .page.is-stage-collapsed .workbench-main-stage {
    flex: 0 0 0;
    min-width: 0;
    opacity: 0;
    overflow: hidden;
    pointer-events: none;
  }

  /* 收起后：中栏列表铺满整个可用空间 */
  .page.is-stage-collapsed .workbench-list-pane {
    flex: 1 1 auto;
    width: 100%;
  }

  /* ==== 中栏与舞台之间的分隔手柄 ==== */
  .stage-divider {
    flex: 0 0 10px;
    min-width: 0;
    display: flex;
    align-items: center;
    justify-content: center;
    cursor: pointer;
    background: transparent;
    transition: flex-basis 0.22s ease, background 0.15s ease;
  }

  .stage-divider:hover {
    background: var(--surface-2);
  }

  .divider-grip {
    width: 2px;
    height: 44px;
    border-radius: 2px;
    background: var(--border);
    opacity: 0.85;
    transition: background 0.15s ease, opacity 0.15s ease;
  }

  .stage-divider:hover .divider-grip {
    background: var(--color-primary);
    opacity: 1;
  }

  /* 收起态：收起按钮(展开胶囊)不显示，分隔手柄彻底隐藏 */
  .page.is-stage-collapsed .stage-divider {
    flex: 0 0 0;
    overflow: hidden;
    pointer-events: none;
  }

  .page.is-stage-collapsed .divider-grip {
    display: none;
  }

  .page.is-stage-collapsed .expand-capsule {
    display: none;
  }

  .expand-capsule {
    width: 30px;
    height: 30px;
    border-radius: 50%;
    background: var(--surface);
    border: 1px solid var(--border-strong);
    box-shadow: var(--shadow-sm);
    display: flex;
    align-items: center;
    justify-content: center;
    cursor: pointer;
    transition: transform 0.15s ease, box-shadow 0.15s ease;
  }

  .expand-capsule::after {
    content: '';
    width: 7px;
    height: 7px;
    border-top: 2px solid var(--color-primary);
    border-right: 2px solid var(--color-primary);
    transform: rotate(45deg);
    margin-left: -2px;
  }

  .stage-divider:hover .expand-capsule {
    transform: scale(1.1);
    box-shadow: var(--shadow-md);
  }
}

/* ==================== 列表工具栏 ==================== */
.list-toolbar {
  padding: 16px 14px 10px;
  border-bottom: 1px solid var(--border);
  display: flex;
  flex-direction: column;
  gap: 10px;
  background: var(--surface);
}

.list-toolbar-top {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.list-title-wrap {
  display: flex;
  align-items: center;
  gap: 8px;
}

.list-title {
  font-size: 16px;
  font-weight: 700;
  color: var(--text-primary);
  margin: 0;
}

.list-count-badge {
  font-size: 11px;
  font-weight: 600;
  padding: 1px 7px;
  border-radius: 12px;
  background: var(--surface-2);
  color: var(--text-tertiary);
  border: 1px solid var(--border);
}

.list-toolbar-actions {
  display: flex;
  align-items: center;
  gap: 6px;
}

.toolbar-btn {
  width: 28px;
  height: 28px;
  border-radius: 6px;
  border: 1px solid var(--border);
  background: var(--surface);
  color: var(--text-secondary);
  display: inline-flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  transition: all 0.15s ease;
}

.toolbar-btn:hover {
  background: var(--surface-2);
  color: var(--color-primary);
  border-color: var(--color-primary);
}

/* 展开主舞台：常态即主色 + 文字标签，与相邻图标按钮区分开 */
.toolbar-btn.is-accent {
  width: auto;
  padding: 0 10px;
  gap: 4px;
  color: var(--color-primary);
  border-color: var(--color-primary);
}

.toolbar-btn-text {
  font-size: 12px;
  font-weight: 500;
  white-space: nowrap;
}

.toolbar-new-btn {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 5px 10px;
  border-radius: 6px;
  background: var(--color-primary);
  color: #fff;
  font-size: 12px;
  font-weight: 600;
  text-decoration: none;
  transition: opacity 0.15s ease;
}

.toolbar-new-btn:hover {
  opacity: 0.9;
}

/* 列表搜索过滤输入框 */
.list-search-wrap {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px 10px;
  border-radius: 8px;
  background: var(--surface-2);
  border: 1px solid var(--border);
}

.list-search-icon {
  font-size: 14px;
  color: var(--text-tertiary);
}

.list-search-input {
  flex: 1;
  min-width: 0;
  border: none;
  background: transparent;
  outline: none;
  font-size: 13px;
  color: var(--text-primary);
}

.list-search-input::placeholder {
  color: var(--text-tertiary);
}

.list-search-clear {
  border: none;
  background: transparent;
  color: var(--text-tertiary);
  cursor: pointer;
  padding: 0;
}

/* ==================== 列表滚动容器 ==================== */
.list-scroll-area {
  flex: 1;
  overflow-y: auto;
  outline: none;
}

/* ==================== 移动端：列表区收敛为内部滚动 ==================== */
@media (max-width: 1023px) {
  .page {
    height: 100dvh;
    min-height: 0;
    display: flex;
    flex-direction: column;
    overflow: hidden;
  }

  .layout {
    flex: 1;
    min-height: 0;
    display: flex;
    flex-direction: column;
    overflow: hidden;
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
    min-height: 0;
    -webkit-overflow-scrolling: touch;
    overscroll-behavior: contain;
    padding-bottom: calc(var(--van-tabbar-height, 50px) + env(safe-area-inset-bottom));
  }
}

/* 紧凑列表样式 */
.compact-list-wrap {
  padding: 6px 8px;
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.compact-list-row {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 9px 10px;
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.15s ease;
  position: relative;
  border: 1px solid transparent;
}

.compact-list-row:hover {
  background: var(--surface-2);
}

.compact-list-row.is-selected {
  background: rgba(59, 130, 246, 0.1);
  border-color: rgba(59, 130, 246, 0.3);
}

:global(body.dark) .compact-list-row.is-selected {
  background: rgba(56, 189, 248, 0.14);
}

.row-color-indicator {
  width: 4px;
  height: 24px;
  border-radius: 2px;
  flex-shrink: 0;
}

.row-main {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 3px;
}

.row-title-line {
  display: flex;
  align-items: center;
  gap: 4px;
}

.row-pin-star {
  color: #f59e0b;
  font-size: 12px;
}

.row-title {
  font-size: 13.5px;
  font-weight: 600;
  color: var(--text-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.row-sub-line {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 11.5px;
  color: var(--text-tertiary);
}

.row-cat {
  color: var(--color-primary);
}

.row-actions {
  display: none;
  align-items: center;
  gap: 2px;
}

.compact-list-row:hover .row-actions {
  display: flex;
}

.row-action-btn {
  width: 22px;
  height: 22px;
  border-radius: 4px;
  border: none;
  background: transparent;
  color: var(--text-tertiary);
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  justify-content: center;
}

.row-action-btn:hover {
  color: var(--text-primary);
  background: var(--surface-3);
}

.row-action-btn.row-del:hover {
  color: #ef4444;
}

/* 卡片列表样式 */
.card-list-wrap {
  padding: 10px;
  display: flex;
  flex-direction: column;
  gap: 10px;
}

/* 收起右栏铺满态：卡片以响应式网格铺满整个空间 */
.page.is-stage-collapsed .card-list-wrap {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(240px, 1fr));
  gap: 14px;
  padding: 16px;
  align-content: start;
}

.desktop-note-card-item {
  padding: 14px;
  border-radius: 10px;
  border: 1px solid var(--border);
  box-shadow: var(--shadow-xs);
  cursor: pointer;
  transition: all 0.18s ease;
  position: relative;
}

.desktop-note-card-item:hover {
  box-shadow: var(--shadow-sm);
  transform: translateY(-1px);
  border-color: var(--border-strong);
}

.desktop-note-card-item.is-selected {
  box-shadow: 0 0 0 2px var(--color-primary);
  border-color: var(--color-primary);
}

/* ==================== 右侧主工作舞台 ==================== */
.stage-loading {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
}

.stage-container {
  display: flex;
  flex-direction: column;
  height: 100vh;
  background: var(--surface);
  box-sizing: border-box;
}

/* 顶部操作条 */
.stage-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 10px 20px;
  border-bottom: 1px solid var(--border);
  background: var(--surface);
  gap: 12px;
  flex-shrink: 0;
}

.stage-header-left {
  display: flex;
  align-items: center;
  gap: 14px;
}

.mode-toggle-group {
  display: inline-flex;
  border-radius: 8px;
  background: var(--surface-2);
  padding: 2px;
  border: 1px solid var(--border);
}

.mode-btn {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  padding: 5px 12px;
  border-radius: 6px;
  border: none;
  background: transparent;
  color: var(--text-secondary);
  font-size: 12.5px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.15s ease;
}

.mode-btn.is-active {
  background: var(--surface);
  color: var(--color-primary);
  font-weight: 600;
  box-shadow: var(--shadow-xs);
}

.stage-save-status {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  color: var(--text-tertiary);
}

.status-dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: var(--text-tertiary);
}

.status-dot.is-saving {
  background: #f59e0b;
  animation: pulse-dot 1s infinite;
}

.status-dot.is-saved {
  background: #10b981;
}

@keyframes pulse-dot {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.3; }
}

.stage-header-actions {
  display: flex;
  align-items: center;
  gap: 6px;
}

.stage-action-btn {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  height: 30px;
  padding: 0 9px;
  border-radius: 7px;
  border: 1px solid var(--border);
  background: var(--surface);
  color: var(--text-secondary);
  cursor: pointer;
  font-size: 12px;
  transition: all 0.15s ease;
}

.stage-action-btn:hover {
  background: var(--surface-2);
  color: var(--text-primary);
  border-color: var(--border-strong);
}

.stage-action-btn.is-starred {
  color: #f59e0b;
}

.stage-action-btn.btn-danger:hover {
  background: rgba(239, 68, 68, 0.12);
  color: #ef4444;
  border-color: #ef4444;
}

/* 收起主舞台：主色图标常驻，避免与相邻中性按钮混淆 */
.stage-action-btn.stage-collapse-btn {
  color: var(--color-primary);
}

.stage-action-btn.stage-collapse-btn:hover {
  background: var(--surface-2);
  color: var(--color-primary);
  border-color: var(--color-primary);
}

/* 调色气泡 */
.color-picker-wrapper {
  position: relative;
}

.color-dropdown-bubble {
  position: absolute;
  top: 36px;
  right: 0;
  z-index: 100;
  padding: 8px;
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 10px;
  box-shadow: 0 10px 30px rgba(0, 0, 0, 0.15);
  display: flex;
  align-items: center;
  gap: 6px;
}

.color-dot-btn {
  width: 22px;
  height: 22px;
  border-radius: 50%;
  border: 1px solid rgba(0, 0, 0, 0.15);
  cursor: pointer;
  transition: transform 0.12s ease;
}

.color-dot-btn:hover {
  transform: scale(1.18);
}

.color-dot-btn.is-current {
  box-shadow: 0 0 0 2px var(--color-primary);
}

.color-reset-btn {
  border: 1px solid var(--border);
  background: var(--surface-2);
  color: var(--text-secondary);
  padding: 3px 8px;
  border-radius: 5px;
  font-size: 11px;
  cursor: pointer;
}

/* ==================== 1. 阅读模式排版 ==================== */
.stage-view-mode {
  flex: 1;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

.reading-scroll-viewport {
  flex: 1;
  overflow-y: auto;
  padding: 32px 40px 60px;
  display: flex;
  justify-content: center;
  gap: 40px;
}

.reading-article-content {
  flex: 1;
  max-width: 760px;
  min-width: 0;
}

.reading-title {
  font-size: 28px;
  font-weight: 800;
  color: var(--text-primary);
  line-height: 1.35;
  margin: 0 0 16px;
  word-break: break-word;
}

.reading-meta-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 12px;
  padding-bottom: 20px;
  margin-bottom: 24px;
  border-bottom: 1px solid var(--border);
}

.meta-chips-wrap {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}

.meta-chip {
  font-size: 11.5px;
  font-weight: 500;
  padding: 2px 8px;
  border-radius: 6px;
  background: var(--surface-2);
  color: var(--text-secondary);
}

.meta-chip.chip-cat {
  background: rgba(59, 130, 246, 0.12);
  color: var(--color-primary);
  font-weight: 600;
}

.meta-extra-info {
  font-size: 12px;
  color: var(--text-tertiary);
  display: flex;
  align-items: center;
  gap: 6px;
}

.meta-divider {
  opacity: 0.5;
}

.reading-body {
  font-size: 15px;
  line-height: 1.75;
  color: var(--text-primary);
}

.reading-toc-aside {
  width: 220px;
  flex-shrink: 0;
}

/* ==================== 2. 分屏编辑模式排版 ==================== */
.stage-edit-mode {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.edit-formatting-toolbar {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 8px 16px;
  background: var(--surface-2);
  border-bottom: 1px solid var(--border);
  flex-wrap: wrap;
}

.fmt-btn {
  height: 26px;
  padding: 0 8px;
  border-radius: 5px;
  border: 1px solid var(--border);
  background: var(--surface);
  color: var(--text-secondary);
  font-size: 12px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.12s ease;
}

.fmt-btn:hover {
  background: var(--surface-3);
  color: var(--color-primary);
  border-color: var(--color-primary);
}

.fmt-divider {
  width: 1px;
  height: 16px;
  background: var(--border);
  margin: 0 4px;
}

.fmt-right-actions {
  margin-left: auto;
}

.split-editor-grid {
  flex: 1;
  display: grid;
  grid-template-columns: 1fr 1fr;
  min-height: 0;
}

.editor-pane-left {
  display: flex;
  flex-direction: column;
  border-right: 1px solid var(--border);
  padding: 16px 20px;
  min-height: 0;
  background: var(--surface);
}

.editor-title-input {
  border: none;
  outline: none;
  font-size: 20px;
  font-weight: 700;
  color: var(--text-primary);
  background: transparent;
  padding: 0 0 12px;
  margin-bottom: 12px;
  border-bottom: 1px solid var(--border);
}

.editor-title-input::placeholder {
  color: var(--text-tertiary);
}

.editor-content-textarea {
  flex: 1;
  border: none;
  outline: none;
  background: transparent;
  color: var(--text-primary);
  font-size: 14.5px;
  line-height: 1.7;
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
  resize: none;
  padding: 0;
}

.editor-pane-right {
  display: flex;
  flex-direction: column;
  padding: 16px 24px;
  overflow-y: auto;
  min-height: 0;
  background: var(--app-bg);
}

.preview-header-label {
  font-size: 11px;
  font-weight: 700;
  text-transform: uppercase;
  color: var(--text-tertiary);
  letter-spacing: 0.6px;
  margin-bottom: 14px;
}

.preview-rendered-title {
  font-size: 22px;
  font-weight: 800;
  color: var(--text-primary);
  margin: 0 0 16px;
}

/* ==================== 桌面欢迎占位区 ==================== */
.stage-empty-placeholder {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 40px;
}

.empty-hero {
  max-width: 460px;
  display: flex;
  flex-direction: column;
  align-items: center;
  text-align: center;
}

.hero-logo-box {
  width: 68px;
  height: 68px;
  border-radius: 16px;
  overflow: hidden;
  box-shadow: 0 8px 24px rgba(15, 169, 140, 0.25);
  margin-bottom: 18px;
}

.hero-logo-img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.hero-title {
  font-size: 20px;
  font-weight: 800;
  color: var(--text-primary);
  margin: 0 0 6px;
}

.hero-subtitle {
  font-size: 13.5px;
  color: var(--text-tertiary);
  margin: 0 0 24px;
}

.shortcuts-card {
  width: 100%;
  padding: 18px;
  border-radius: 12px;
  background: var(--surface);
  border: 1px solid var(--border);
  box-shadow: var(--shadow-sm);
  margin-bottom: 24px;
  text-align: left;
}

.shortcuts-card-title {
  font-size: 12px;
  font-weight: 700;
  color: var(--text-tertiary);
  text-transform: uppercase;
  letter-spacing: 0.5px;
  margin-bottom: 12px;
}

.shortcuts-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 10px;
}

.shortcut-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  font-size: 12px;
}

.sc-desc {
  color: var(--text-secondary);
}

.sc-kbd {
  font-size: 10px;
  padding: 2px 6px;
  border-radius: 4px;
  background: var(--surface-2);
  border: 1px solid var(--border);
  color: var(--text-tertiary);
  font-family: inherit;
}

/* ==================== 版本抽屉样式 ==================== */
.version-drawer-inner {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: var(--surface);
}

.version-drawer-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 16px 20px;
  border-bottom: 1px solid var(--border);
}

.drawer-title-box {
  display: flex;
  align-items: center;
  gap: 8px;
}

.drawer-title {
  font-size: 16px;
  font-weight: 700;
  color: var(--text-primary);
}

.drawer-count {
  font-size: 11px;
  padding: 1px 7px;
  border-radius: 10px;
  background: var(--surface-2);
  color: var(--text-tertiary);
}

.drawer-close {
  cursor: pointer;
  color: var(--text-tertiary);
}

.version-diff-container {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-height: 0;
}

.diff-header-bar {
  padding: 10px 16px;
  background: var(--surface-2);
  border-bottom: 1px solid var(--border);
}

.back-to-versions-btn {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  border: none;
  background: transparent;
  color: var(--color-primary);
  font-size: 13px;
  font-weight: 500;
  cursor: pointer;
}

.version-list-scroll {
  flex: 1;
  overflow-y: auto;
  padding: 16px;
}

.version-loading,
.version-empty {
  text-align: center;
  padding: 60px 20px;
  color: var(--text-tertiary);
}

.v-empty-icon {
  font-size: 36px;
  margin-bottom: 10px;
}

.v-empty-sub {
  font-size: 12px;
  margin-top: 4px;
}

.version-items-box {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.version-card {
  padding: 14px;
  border-radius: 10px;
  border: 1px solid var(--border);
  background: var(--surface);
  cursor: pointer;
  transition: all 0.15s ease;
}

.version-card:hover {
  border-color: var(--color-primary);
  background: var(--surface-2);
}

.version-card.is-current {
  border-color: rgba(59, 130, 246, 0.4);
  background: rgba(59, 130, 246, 0.04);
}

.v-card-top {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 6px;
}

.v-time {
  font-size: 12px;
  color: var(--text-tertiary);
}

.v-badge-current {
  font-size: 10.5px;
  padding: 1px 6px;
  border-radius: 4px;
  background: rgba(16, 185, 129, 0.12);
  color: #10b981;
  font-weight: 600;
}

.v-badge-compare {
  font-size: 11px;
  color: var(--color-primary);
}

.v-card-title {
  font-size: 14px;
  font-weight: 600;
  color: var(--text-primary);
  margin-bottom: 4px;
}

.v-card-preview {
  font-size: 12.5px;
  color: var(--text-secondary);
  line-height: 1.5;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

/* 基础公共组件与移动端样式 */
.empty-wrap {
  text-align: center;
  padding: 40px 16px;
}
.empty-icon-box {
  font-size: 40px;
  margin-bottom: 12px;
}
.empty-title {
  font-size: 15px;
  font-weight: 600;
  color: var(--text-primary);
  margin: 0 0 6px;
}
.empty-sub {
  font-size: 12.5px;
  color: var(--text-tertiary);
  margin: 0 0 16px;
}

.notes-grid {
  padding: 8px 16px 16px;
}
.notes-grid-item {
  margin-bottom: 12px;
  border-radius: 12px;
  overflow: hidden;
  box-shadow: var(--shadow-xs);
  border: 1px solid var(--border);
  position: relative;
}
.note-card {
  padding: 16px;
  position: relative;
  min-height: 110px;
  display: flex;
  flex-direction: column;
  justify-content: space-between;
}
.pinned-badge {
  display: inline-flex;
  align-items: center;
  gap: 3px;
  position: absolute;
  top: 10px;
  right: 10px;
  background: linear-gradient(135deg, #fbbf24, #f59e0b);
  color: #78350f;
  font-size: 10px;
  font-weight: 700;
  padding: 2px 6px;
  border-radius: 10px;
  z-index: 2;
}
.note-title-row {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 8px;
  margin-bottom: 6px;
  padding-right: 36px;
}
.note-title {
  font-size: 15px;
  font-weight: 700;
  line-height: 1.4;
  word-break: break-word;
  min-width: 0;
  flex: 1;
}
.card-hover-actions {
  position: absolute;
  top: 8px;
  right: 8px;
  display: flex;
  align-items: center;
  gap: 3px;
  opacity: 0;
  pointer-events: none;
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 6px;
  padding: 2px;
  box-shadow: 0 2px 6px rgba(0, 0, 0, 0.08);
  transition: opacity 0.15s ease;
  z-index: 3;
}
.desktop-note-card-item:hover .card-hover-actions {
  opacity: 1;
  pointer-events: auto;
}
.card-action-btn {
  width: 24px;
  height: 24px;
  border-radius: 5px;
  border: none;
  background: rgba(0, 0, 0, 0.08);
  color: inherit;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
}
:global(body.dark) .card-action-btn {
  background: rgba(255, 255, 255, 0.12);
}
.card-action-btn:hover {
  background: rgba(25, 137, 250, 0.16);
  color: #1989fa;
}
:global(body.dark) .card-action-btn:hover {
  background: rgba(25, 137, 250, 0.3);
  color: #4fc3f7;
}
.card-action-btn.btn-danger:hover {
  background: rgba(239, 68, 68, 0.2);
  color: #ef4444;
}
.note-preview {
  font-size: 13px;
  line-height: 1.5;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
  margin-bottom: 10px;
  opacity: 0.78;
}
.note-meta {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 6px;
}
.tags-row {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
}
.meta-tag {
  font-size: 10.5px;
  padding: 2px 6px;
  border-radius: 5px;
  background: rgba(0, 0, 0, 0.05);
  color: inherit;
}
.meta-tag.cat-tag {
  background: rgba(59, 130, 246, 0.12);
  color: var(--color-primary);
  font-weight: 600;
}
.note-time {
  font-size: 11px;
  opacity: 0.7;
}
.swipe-action-btn {
  height: 100%;
}
.drawer-inner {
  padding: 16px;
  height: 100%;
  display: flex;
  flex-direction: column;
}
.drawer-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 4px 14px;
  border-bottom: 1px solid var(--border);
  margin-bottom: 12px;
}
</style>
