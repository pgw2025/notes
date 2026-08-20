<template>
  <div class="page" :class="{ 'page--fullscreen': isFullscreen }">
    <van-nav-bar :title="navTitle" left-arrow @click-left="onBack">
      <template #right>
        <!-- 桌面端单行布局：按钮在标题右侧 -->
        <div class="nav-actions nav-actions--desktop">
          <van-icon :name="form.isPinned ? 'star' : 'star-o'" size="20" :color="form.isPinned ? '#ff976a' : undefined"
            @click="form.isPinned = !form.isPinned" />
          <!-- 编辑/预览 模式切换（单按钮双向切换，替代原双Tabs） -->
          <span class="nav-icon nav-char-btn" @click="mode = mode === 'edit' ? 'preview' : 'edit'"
            :title="mode === 'edit' ? '切换到预览' : '切换到编辑'">
            <template v-if="mode === 'edit'">👁</template>
            <template v-else>✎</template>
          </span>
          <!-- P2-1 大纲按钮 -->
          <span class="nav-icon nav-char-btn" title="大纲 (Ctrl+Shift+O)" @click="showOutline = !showOutline">☰</span>
          <!-- P2-2 搜索按钮 -->
          <span class="nav-icon nav-char-btn" title="搜索替换 (Ctrl+F)" @click="openSearch">🔍</span>
          <!-- P2-3 全屏按钮 -->
          <span class="nav-icon nav-char-btn" :title="isFullscreen ? '退出全屏 (Esc)' : '全屏编辑'" @click="toggleFullscreen">{{
            isFullscreen ? '⤢' : '⛶' }}</span>
          <van-icon name="edit" size="20" class="color-icon" @click="showColorPicker = true" />
          <van-button size="mini" type="primary" :loading="saving" @click="onSave">保存</van-button>
        </div>
      </template>
    </van-nav-bar>

    <!-- 保存状态条：显示保存中/已保存/保存失败，移动端桌面端都显示 -->
    <div class="save-status" :class="[`save-status--${saveStatus}`, { 'is-clickable': saveStatus === 'error' }]"
      @click="saveStatus === 'error' && onSave()">
      <span class="save-status__dot"></span>
      <span class="save-status__text">
        <template v-if="saveStatus === 'saving'">保存中…</template>
        <template v-else-if="saveStatus === 'saved'">已保存 · {{ saveTimeText }}</template>
        <template v-else-if="saveStatus === 'error'">保存失败，点击重试</template>
        <template v-else>&nbsp;</template>
      </span>
    </div>

    <!-- 移动端第二行工具栏：按钮独占一行，和标题完全不重叠 -->
    <div class="nav-actions nav-actions--mobile">
      <van-icon :name="form.isPinned ? 'star' : 'star-o'" size="20" :color="form.isPinned ? '#ff976a' : undefined"
        title="置顶" @click="form.isPinned = !form.isPinned" />
      <!-- 编辑/预览 模式切换（替代原 van-tabs 双tab） -->
      <span class="nav-icon nav-char-btn" @click="mode = mode === 'edit' ? 'preview' : 'edit'"
        :title="mode === 'edit' ? '切换到预览' : '切换到编辑'">
        <template v-if="mode === 'edit'">👁</template>
        <template v-else>✎</template>
      </span>
      <span class="nav-icon nav-char-btn" title="大纲" @click="showOutline = !showOutline">☰</span>
      <span class="nav-icon nav-char-btn" title="搜索替换" @click="openSearch">🔍</span>
      <span class="nav-icon nav-char-btn" :title="isFullscreen ? '退出全屏' : '全屏'" @click="toggleFullscreen">{{ isFullscreen
        ? '⤢' : '⛶' }}</span>
      <van-icon name="edit" size="20" class="color-icon" title="背景色" @click="showColorPicker = true" />
      <van-button size="mini" type="primary" :loading="saving" @click="onSave">保存</van-button>
    </div>

    <div class="editor" :style="editorStyle">
      <van-field v-model="form.title" placeholder="标题" class="title-field" :style="{ background: 'transparent' }"
        maxlength="200" />

      <!-- 方案A：分类/标签 Chips 化（label + pill + 移除× + 添加） -->
      <div class="meta-row meta-row--chips" :style="{ borderTopColor: metaBorderColor, borderColor: metaBorderColor }">
        <!-- 分类行 -->
        <div class="meta-chip-row">
          <span class="meta-label" :style="{ color: textColor }">分类</span>
          <div class="meta-chips">
            <span v-if="selectedCategory" class="chip chip--cat" :style="chipStyle.cat" @click="showCategoryPicker = true"
              :title="'点击切换分类：' + selectedCategory.name">
              <span class="chip-icon">📁</span>
              <span class="chip-text">{{ selectedCategory.name }}</span>
              <span class="chip-x" @click.stop="form.categoryId = null" title="移除分类">×</span>
            </span>
            <span v-else class="chip chip--add" @click="showCategoryPicker = true" :style="chipStyle.add" title="选择分类">＋
              选分类</span>
          </div>
        </div>

        <!-- 标签行 -->
        <div class="meta-chip-row" :style="{ borderTopColor: metaBorderColor }">
          <span class="meta-label" :style="{ color: textColor }">标签</span>
          <div class="meta-chips">
            <span v-for="t in selectedTags" :key="t.id" class="chip chip--tag" :style="chipStyle.tag"
              :title="'点击移除标签：' + t.name">
              <span class="chip-icon">#</span>
              <span class="chip-text">{{ t.name }}</span>
              <span class="chip-x" @click.stop="removeTag(t.id)">×</span>
            </span>
            <span class="chip chip--add" @click="showTagPicker = true" :style="chipStyle.add" title="添加标签">＋ 添加标签</span>
          </div>
        </div>
      </div>

      <div class="editor-body">
        <div v-show="mode === 'edit'" class="edit-area" :class="{ 'drag-over': dragOver }" :style="areaStyle"
          @dragover.prevent.stop="onDragOver" @dragleave.prevent.stop="onDragLeave" @drop.prevent.stop="onDrop">
          <!-- 桌面端：工具栏位于 textarea 上方 -->
          <div v-if="isDesktop" class="toolbar desktop-toolbar">
            <template v-for="(b, bi) in toolbarButtons" :key="'dt-'+bi">
              <van-button v-if="!b.divider" size="small" plain :type="b.primary ? 'primary' : undefined" @click="b.action"
                :title="b.title">
                <span v-html="b.label" />
              </van-button>
              <div v-else class="toolbar-divider" />
            </template>
            <input ref="fileInput" type="file" accept="image/*" style="display: none" @change="onFileChange" />
          </div>

          <div class="textarea-wrap">
            <textarea ref="textareaRef" v-model="form.content" class="content-area" :style="textareaDynamicStyle"
              placeholder="开始记录... 支持 Markdown 语法" @select="onTextSelect" @mouseup="onTextSelect" @touchend="onTouchEnd"
              @focus="onTextareaFocus" @scroll="onTextareaScroll" @blur="onTextareaBlur"
              @paste.capture="onPaste"></textarea>

            <!-- 字数统计 -->
            <div class="word-count" :style="{ color: textColor }">
              {{ charCount }} 字 · {{ lineCount }} 行
            </div>

            <!-- =============== P1-5 选中文本浮动工具栏 =============== -->
            <transition name="float-fade">
              <div v-show="floatingBarVisible" class="floating-toolbar" :style="floatingBarStyle" @mousedown.prevent
                @touchstart.stop.passive>
                <button class="ft-btn" type="button" title="粗体 (Ctrl+B)" @click="applyFloat('bold')">
                  <b>B</b>
                </button>
                <button class="ft-btn" type="button" title="斜体 (Ctrl+I)" @click="applyFloat('italic')">
                  <i>I</i>
                </button>
                <button class="ft-btn" type="button" title="删除线" @click="applyFloat('strike')">
                  <s>S</s>
                </button>
                <button class="ft-btn" type="button" title="行内代码" @click="applyFloat('code')">
                  <span style="font-family:monospace;font-size:13px">&lt;/&gt;</span>
                </button>
                <button class="ft-btn" type="button" title="链接 (Ctrl+K)" @click="applyFloat('link')">🔗</button>
              </div>
            </transition>

            <!-- =============== P2-2 搜索 & 替换面板（可拖动） =============== -->
            <transition name="float-fade">
              <div v-show="showSearch" ref="searchPanelRef" class="search-panel"
                :class="{ 'is-dragging': searchDragDragging }" :style="searchPanelStyle" @mousedown.stop>
                <div class="search-row">
                  <!-- 拖拽把手：6 点阵，移动/鼠标都能拖 -->
                  <span class="sp-drag-handle" title="拖动移动面板" @pointerdown="onSearchDragStart">
                    <svg viewBox="0 0 14 14" width="12" height="12" aria-hidden="true">
                      <circle cx="3" cy="3" r="1.3" fill="currentColor" />
                      <circle cx="7" cy="3" r="1.3" fill="currentColor" />
                      <circle cx="11" cy="3" r="1.3" fill="currentColor" />
                      <circle cx="3" cy="7" r="1.3" fill="currentColor" />
                      <circle cx="7" cy="7" r="1.3" fill="currentColor" />
                      <circle cx="11" cy="7" r="1.3" fill="currentColor" />
                      <circle cx="3" cy="11" r="1.3" fill="currentColor" />
                      <circle cx="7" cy="11" r="1.3" fill="currentColor" />
                      <circle cx="11" cy="11" r="1.3" fill="currentColor" />
                    </svg>
                  </span>
                  <input v-model="searchKeyword" class="search-input" type="text" placeholder="查找 (支持正则)"
                    @input="ensureMatchesComputed" @keydown.enter.prevent="searchNext"
                    @keydown.esc.prevent="closeSearch" />
                  <span class="search-count" :title="`共 ${matches.length} 处`">
                    {{ matches.length === 0 ? '0/0' : `${currentMatchIdx + 1}/${matches.length}` }}
                  </span>
                  <button type="button" class="sp-btn" title="上一个 (Shift+Enter)" @click="searchPrev">▲</button>
                  <button type="button" class="sp-btn" title="下一个 (Enter)" @click="searchNext">▼</button>
                  <button type="button" class="sp-btn sp-close" title="关闭 (Esc)" @click="closeSearch">✕</button>
                </div>
                <div class="search-row search-row-2">
                  <button type="button" class="sp-toggle" :class="{ active: useRegex }" title="正则模式"
                    @click="useRegex = !useRegex">.*</button>
                  <button type="button" class="sp-toggle" :class="{ active: matchCase }" title="区分大小写"
                    @click="matchCase = !matchCase">Aa</button>
                  <input v-model="replaceText" class="search-input replace-input" type="text" placeholder="替换为"
                    @keydown.enter.prevent="doReplace" />
                  <button type="button" class="sp-btn sp-primary" :disabled="matches.length === 0"
                    @click="doReplace">替换</button>
                  <button type="button" class="sp-btn sp-primary" :disabled="matches.length === 0"
                    @click="doReplaceAll">全部</button>
                </div>
              </div>
            </transition>
          </div>
        </div>

        <div v-show="mode === 'preview'" class="preview-area" :style="areaStyle">
          <markdown-body :content="form.content || '*暂无内容*'" :style="{ color: textColor }" />
        </div>
      </div>

      <!-- 移动端：工具栏吸底常驻（动态 bottom: 跟随软键盘高度） -->
      <div v-if="!isDesktop" class="toolbar mobile-toolbar" :style="mobileToolbarStyle">
        <div class="mobile-toolbar-scroll">
          <template v-for="(b, bi) in toolbarButtons" :key="'mt-'+bi">
            <van-button v-if="!b.divider" size="small" plain :type="b.primary ? 'primary' : undefined" @click="b.action">
              <span v-html="b.label" />
            </van-button>
          </template>
          <input ref="mobileFileInput" type="file" accept="image/*" style="display: none" @change="onFileChange" />
        </div>
      </div>
    </div>

    <!-- =============== P2-1 大纲面板（右侧抽屉） =============== -->
    <transition name="slide-right">
      <div v-show="showOutline" class="outline-mask" @click.self="showOutline = false">
        <aside class="outline-panel" :style="{ background: effectiveBg, color: textColor }" @mousedown.stop>
          <div class="outline-header" :style="{ borderColor: metaBorderColor }">
            <span class="outline-title">📑 大纲</span>
            <button type="button" class="sp-btn sp-close" @click="showOutline = false">✕</button>
          </div>
          <div v-if="outlineList.length === 0" class="outline-empty" :style="{ color: subTextColor }">
            暂无标题，使用 <b># / ## / ###</b> 来组织内容
          </div>
          <ul v-else class="outline-list">
            <li v-for="(h, i) in outlineList" :key="'h-' + i" class="outline-item" :class="[
              'outline-level-' + h.level,
              { active: i === activeHeadingIdx }
            ]" :style="(i === activeHeadingIdx ? { borderColor: '#1989fa', color: '#1989fa' } : {})"
              @click="scrollToHeading(i)">
              <span class="outline-dot"
                :style="(i === activeHeadingIdx ? { background: '#1989fa' } : { background: subTextColor })"></span>
              <span class="outline-text">{{ h.text || '(空标题)' }}</span>
            </li>
          </ul>
        </aside>
      </div>
    </transition>

    <!-- 分类选择：含内联新建输入 -->
    <van-popup v-model:show="showCategoryPicker" position="bottom" round>
      <div class="cat-picker">
        <van-nav-bar title="选择分类">
          <template #right>
            <van-button size="mini" type="primary" plain @click="showCategoryPicker = false">完成</van-button>
          </template>
        </van-nav-bar>
        <div class="inline-create">
          <van-field v-model="newCategoryName" placeholder="输入新分类，回车立即创建" clearable maxlength="20" :border="false"
            :loading="creatingCategory" @keydown.enter.prevent="createCategoryInline">
            <template #left-icon>
              <span class="ic-plus">＋</span>
            </template>
            <template #button>
              <van-button size="small" type="primary" :disabled="!newCategoryName.trim()" :loading="creatingCategory"
                @click="createCategoryInline">创建</van-button>
            </template>
          </van-field>
        </div>
        <van-picker :columns="categoryColumns" :default-index="categoryDefaultIndex" @confirm="onCategoryConfirm"
          @cancel="showCategoryPicker = false" />
      </div>
    </van-popup>

    <!-- 标签选择：含内联新建输入 -->
    <van-popup v-model:show="showTagPicker" position="bottom" round style="height: 60%">
      <div class="tag-picker">
        <van-nav-bar title="选择标签">
          <template #right>
            <van-button size="mini" type="primary" @click="showTagPicker = false">完成</van-button>
          </template>
        </van-nav-bar>
        <div class="inline-create">
          <van-field v-model="newTagName" placeholder="输入新标签，回车立即创建并勾选" clearable maxlength="20" :border="false"
            :loading="creatingTag" @keydown.enter.prevent="createTagInline">
            <template #left-icon>
              <span class="ic-plus">＋</span>
            </template>
            <template #button>
              <van-button size="small" type="primary" :disabled="!newTagName.trim()" :loading="creatingTag"
                @click="createTagInline">创建</van-button>
            </template>
          </van-field>
        </div>
        <div class="tag-list">
          <van-checkbox-group v-model="form.tagIds">
            <van-cell v-for="t in tags" :key="t.id" :title="t.name"
              :label="t.noteCount != null ? `包含 ${t.noteCount} 篇笔记` : '新建标签'" clickable @click="toggleTag(t.id)">
              <template #right-icon>
                <van-checkbox shape="square" :name="t.id" />
              </template>
            </van-cell>
          </van-checkbox-group>
          <div v-if="tags.length === 0" class="empty-tags">
            <van-empty description="还没有标签，在上方输入框直接创建" image-size="80" />
          </div>
        </div>
      </div>
    </van-popup>

    <!-- 颜色选择 -->
    <ColorPicker v-model:show="showColorPicker" v-model="form.backgroundColor" />
  </div>
</template>

<script setup>
import { ref, reactive, computed, onMounted, onUnmounted, nextTick, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { showToast } from 'vant'
import http from '../api/http'
import MarkdownBody from '../components/MarkdownBody.vue'
import ColorPicker from '../components/ColorPicker.vue'
import { useResponsive } from '../composables/useResponsive'
import { useAuthStore } from '../stores/auth'
import { useThemeStore } from '../stores/theme'
import { resolveNoteColor, getContrastColor, isDarkColor } from '../utils/color'

const route = useRoute()
const router = useRouter()
const { isDesktop } = useResponsive()
const auth = useAuthStore()
const theme = useThemeStore()

const noteId = computed(() => route.params.id)
const isEdit = computed(() => !!noteId.value)

// =============== P1-8: 临时 noteId（新建模式下，上传图片前先建空壳，把 id 暂存在这） ===============
const temporaryNoteId = ref(null)
/** 上传附件 / 正式保存时的"真实 note id"，优先使用临时值（路由还没跳转前） */
const effectiveNoteId = computed(() => temporaryNoteId.value || noteId.value || null)
const isReallyEdit = computed(() => !!effectiveNoteId.value)

const form = reactive({
  title: '',
  content: '',
  categoryId: null,
  tagIds: [],
  backgroundColor: null,
  isPinned: false
})
const categories = ref([])
const tags = ref([])
const mode = ref('edit')
const saving = ref(false)
const showCategoryPicker = ref(false)
const showTagPicker = ref(false)
// 内联新建（方案A：弹窗内直接创建，不再跳 /tags /categories 页）
const newTagName = ref('')
const newCategoryName = ref('')
const creatingTag = ref(false)
const creatingCategory = ref(false)
const showColorPicker = ref(false)
const textareaRef = ref(null)
const fileInput = ref(null)
const mobileFileInput = ref(null)
// ========== 搜索面板拖动 ==========
const searchPanelRef = ref(null)
const searchDragX = ref(null)
const searchDragY = ref(null)
const searchDragDragging = ref(false)
let searchDragOffsetX = 0
let searchDragOffsetY = 0
let searchDragPointerId = null
const searchPanelStyle = computed(() => {
  if (searchDragX.value == null || searchDragY.value == null) return {}
  return {
    left: `${searchDragX.value}px`,
    top: `${searchDragY.value}px`,
    right: 'auto'
  }
})
function onSearchDragStart(e) {
  const panel = searchPanelRef.value
  if (!panel) return
  const panelRect = panel.getBoundingClientRect()
  searchDragDragging.value = true
  searchDragOffsetX = e.clientX - panelRect.left
  searchDragOffsetY = e.clientY - panelRect.top
  searchDragPointerId = e.pointerId
  try { e.target.setPointerCapture?.(e.pointerId) } catch { /* ignore */ }
  e.preventDefault()
  e.stopPropagation()
  document.addEventListener('pointermove', onSearchDragMove, true)
  document.addEventListener('pointerup', onSearchDragEnd, true)
  document.addEventListener('pointercancel', onSearchDragEnd, true)
}
function onSearchDragMove(e) {
  if (!searchDragDragging.value) return
  const panel = searchPanelRef.value
  const container = panel?.parentElement
  if (!panel || !container) return
  const cRect = container.getBoundingClientRect()
  const panelW = panel.offsetWidth
  const panelH = panel.offsetHeight
  let nx = e.clientX - cRect.left - searchDragOffsetX
  let ny = e.clientY - cRect.top - searchDragOffsetY
  const padX = 6
  const padY = 6
  nx = Math.max(padX, Math.min(nx, Math.max(padX, cRect.width - panelW - padX)))
  ny = Math.max(padY, Math.min(ny, Math.max(padY, cRect.height - panelH - padY)))
  searchDragX.value = nx
  searchDragY.value = ny
  e.preventDefault()
}
function onSearchDragEnd(e) {
  if (searchDragPointerId != null) {
    try { e.target.releasePointerCapture?.(searchDragPointerId) } catch { /* ignore */ }
  }
  searchDragDragging.value = false
  searchDragPointerId = null
  document.removeEventListener('pointermove', onSearchDragMove, true)
  document.removeEventListener('pointerup', onSearchDragEnd, true)
  document.removeEventListener('pointercancel', onSearchDragEnd, true)
}

// ========== 自动保存（方案A：服务器 debounce 保存） ==========
// 保存状态机：idle / saving / saved / error
const saveStatus = ref('idle')
const saveErrorMsg = ref('')
const lastSavedAt = ref(null)
// 防抖计时器（支持 flush：手动保存/返回时立即取消并立刻执行）
let autoSaveTimer = null
// 当前是否处于真正的请求中，防止并发（手动 flush 时和 debounced 同时撞车）
let autoSaveInFlight = false
// 是否有待保存的变更（上一次保存成功之后表单又变了）
let pendingChanges = false

function cancelAutoSaveTimer() {
  if (autoSaveTimer) {
    clearTimeout(autoSaveTimer)
    autoSaveTimer = null
  }
}

const saveTimeText = computed(() => {
  const t = lastSavedAt.value
  if (!t) return ''
  try {
    const d = new Date(t)
    const pad = (n) => n.toString().padStart(2, '0')
    return `${pad(d.getHours())}:${pad(d.getMinutes())}`
  } catch { return '' }
})

function buildPayload() {
  return {
    title: form.title.trim(),
    content: form.content,
    categoryId: form.categoryId,
    tagIds: form.tagIds,
    backgroundColor: form.backgroundColor || '',
    isPinned: form.isPinned
  }
}

async function doServerSave({ silentIfBlank = true } = {}) {
  // 标题+内容同时为空：不调用接口（接口会拒绝）
  const blank = !form.title.trim() && !form.content.trim()
  if (blank) {
    pendingChanges = false
    saveStatus.value = 'idle'
    return { ok: true, blank: true }
  }
  autoSaveInFlight = true
  saveStatus.value = 'saving'
  saving.value = true
  try {
    const payload = buildPayload()
    if (isReallyEdit.value) {
      await http.put(`/notes/${effectiveNoteId.value}`, payload)
    } else {
      const created = await http.post('/notes', payload)
      // 新建笔记创建成功：URL replace 到编辑页，表单不变
      await router.replace(`/notes/${created.id}/edit`)
    }
    lastSavedAt.value = Date.now()
    saveStatus.value = 'saved'
    pendingChanges = false
    updateDocumentTitle()
    return { ok: true }
  } catch (e) {
    saveStatus.value = 'error'
    saveErrorMsg.value = e?.message || '未知错误'
    updateDocumentTitle()
    return { ok: false, error: e }
  } finally {
    autoSaveInFlight = false
    saving.value = false
  }
}

function scheduleAutoSave() {
  pendingChanges = true
  saveStatus.value = pendingChanges ? 'idle' : 'saved'
  updateDocumentTitle()
  cancelAutoSaveTimer()
  autoSaveTimer = setTimeout(() => {
    autoSaveTimer = null
    // 已经在请求中 → 等这次请求结束会再被触发一次（见 finally 块兜底）
    if (autoSaveInFlight) return
    doServerSave()
  }, 2000)
}

// flush：立即取消防抖计时器并强制保存一次，返回 Promise<ok>
async function flushAutoSave({ forceEvenIfNotPending = false } = {}) {
  cancelAutoSaveTimer()
  if (!pendingChanges && !forceEvenIfNotPending) {
    return { ok: true }
  }
  // 如果已经在请求中：等它完再立刻补一次，取最后结果
  if (autoSaveInFlight) {
    // 轮询等待 inFlight 结束（最多 20s）
    const t0 = Date.now()
    while (autoSaveInFlight && Date.now() - t0 < 20000) {
      await new Promise((r) => setTimeout(r, 50))
    }
  }
  return await doServerSave({ silentIfBlank: false })
}

function updateDocumentTitle() {
  const base = isEdit.value ? '编辑笔记' : '新建笔记'
  const dirty = pendingChanges || saveStatus.value === 'saving' || saveStatus.value === 'error'
  document.title = dirty ? `● ${base}` : base
}

// 监听表单变化 → 启动 2s 防抖
watch(
  [() => form.title, () => form.content, () => form.categoryId, () => form.tagIds, () => form.backgroundColor, () => form.isPinned],
  () => { scheduleAutoSave() },
  { deep: true }
)

// ========== P0-4 字数统计 ==========
const charCount = computed(() => form.content ? form.content.length : 0)
const lineCount = computed(() => {
  if (!form.content) return 0
  return form.content.split('\n').length
})

// ========== 工具栏按钮 ==========
const toolbarButtons = computed(() => [
  { label: 'H', title: '一级标题', action: () => insert('# ', '', '标题') },
  { label: 'H2', title: '二级标题', action: () => insert('## ', '', '二级标题') },
  { label: 'H3', title: '三级标题', action: () => insert('### ', '', '三级标题') },
  { divider: true },
  { label: '<b>B</b>', title: '粗体', action: () => insert('**', '**', '粗体') },
  { label: '<i>I</i>', title: '斜体', action: () => insert('*', '*', '斜体') },
  { label: '<s>S</s>', title: '删除线', action: () => insert('~~', '~~', '删除线') },
  { label: '<code>&lt;/&gt;</code>', title: '行内代码', action: () => insert('`', '`', 'code') },
  { divider: true },
  { label: '•', title: '无序列表', action: () => insert('- ', '', '列表项') },
  { label: '1.', title: '有序列表', action: () => insert('1. ', '', '列表项') },
  { label: '&quot;', title: '引用', action: () => insert('> ', '', '引用文字') },
  { label: '---', title: '分割线', action: () => insert('\n---\n', '', '') },
  { divider: true },
  { label: '{ }', title: '代码块', action: () => insert('\n```\n', '\n```\n', '代码') },
  { label: '$', title: '行内公式', action: () => insert('$', '$', '公式') },
  { label: '$$', title: '块级公式', action: () => insert('\n$$\n', '\n$$\n', '公式') },
  { label: '链接', title: '链接', action: () => insert('[', '](https://)', '链接文本') },
  { label: '图片', title: '插入图片', primary: true, action: triggerUpload },
  { divider: true },
  { label: '格式化', title: '格式化公式', action: formatFormulas }
])

const categoryColumns = computed(() => [
  { text: '无分类', value: null },
  ...categories.value.map((c) => ({ text: c.name, value: c.id }))
])
// Picker 默认选中当前分类（方案A补齐：van-picker default-index）
const categoryDefaultIndex = computed(() => {
  const cols = categoryColumns.value
  const idx = cols.findIndex((x) => x.value === form.categoryId)
  return idx >= 0 ? idx : 0
})

const selectedCategoryName = computed(() => {
  const c = categories.value.find((x) => x.id === form.categoryId)
  return c?.name || ''
})
// 方案A：已选分类对象（用于 Chip 展示，null 则渲染 ＋选分类 占位）
const selectedCategory = computed(() => categories.value.find((x) => x.id === form.categoryId) || null)

const selectedTagNames = computed(() => {
  const names = tags.value
    .filter((t) => form.tagIds.includes(t.id))
    .map((t) => t.name)
  return names.join('、')
})
// 方案A：已选标签数组（v-for 渲染 Pill，每个带 × 移除）
const selectedTags = computed(() => tags.value.filter((t) => form.tagIds.includes(t.id)))

// 方案A：Chip 动态样式 —— 根据笔记背景色深浅自动切换 chip 填充色，保证对比度
// 原则：chip 背景选浅色系 + 文字深色；在深色笔记背景下也用浅色 chip 填充，
// 只有 chip--add 用半透明描边更柔和，和整体配色协同
const chipStyle = computed(() => {
  const dark = isDarkColor(effectiveBg.value)
  if (dark) {
    // 深色笔记背景：chip 用柔和半透明 fill，保证可辨识
    return {
      cat: { background: 'rgba(106,92,255,0.18)', color: '#c4bfff', borderColor: 'transparent' },
      tag: { background: 'rgba(52,199,89,0.15)', color: '#9fe6b4', borderColor: 'transparent' },
      add: { background: 'transparent', color: 'rgba(255,255,255,0.55)', borderStyle: 'dashed' }
    }
  }
  // 浅色背景（默认）
  return {
    cat: { background: '#eef0ff', color: '#5d4fff', borderColor: 'transparent' },
    tag: { background: '#eaf8ee', color: '#1f7a3a', borderColor: 'transparent' },
    add: { background: 'transparent', color: '#86909c', borderStyle: 'dashed' }
  }
})

const effectiveBg = computed(() => resolveNoteColor(form.backgroundColor, auth.user?.defaultNoteColor, theme.isDarkEffective))
const textColor = computed(() => getContrastColor(effectiveBg.value))
const subTextColor = computed(() => isDarkColor(effectiveBg.value) ? 'rgba(255,255,255,0.6)' : 'var(--text-tertiary)')
const metaBorderColor = computed(() => isDarkColor(effectiveBg.value) ? 'rgba(255,255,255,0.15)' : 'var(--border)')

const navTitle = computed(() => (isEdit.value ? '编辑笔记' : '新建笔记'))

const editorStyle = computed(() => ({
  background: effectiveBg.value,
  color: textColor.value
}))

const areaStyle = computed(() => ({
  background: 'transparent',
  color: textColor.value
}))

// =============== P1-6: 软键盘高度 & viewport 适配（移动端） ===============
const keyboardHeight = ref(0)
const dragOver = ref(false)

function onViewportChange() {
  if (!window.visualViewport) return
  // 视口可见高度 vs 真实窗口高度之差 ≈ 键盘高度（额外排除一些安全区误差）
  const kh = Math.max(0, window.innerHeight - window.visualViewport.height - window.visualViewport.offsetTop)
  keyboardHeight.value = kh > 30 ? kh : 0 // 忽略 <30px 的微小差异（例如地址栏收起）
}

// =============== P1-5: 选中文本浮动工具栏 ===============
const floatingBarVisible = ref(false)
const floatingX = ref(0)
const floatingY = ref(0)

function getSelectionRange() {
  const ta = textareaRef.value
  if (!ta) return null
  const s = ta.selectionStart
  const e = ta.selectionEnd
  if (s === e) return null
  return { start: s, end: e }
}

/**
 * 计算 textarea 中某个字符 offset 对应的（相对 textarea-wrap 的）像素位置
 * 用"镜像 div 克隆 + span 插入"法：将 textarea 的样式复刻到一个脱离文档流的 div，
 * 把 [0, offset) 文本放进去，最后塞一个零宽 <span id="marker">，测量其位置。
 */
let _mirror = null
function getCaretPixel(offset) {
  const ta = textareaRef.value
  if (!ta) return { top: 0, left: 0 }
  const wrap = ta.parentElement // .textarea-wrap
  if (!_mirror) {
    _mirror = document.createElement('div')
    const cs = window.getComputedStyle(ta)
    // 复制所有影响排版的样式
    const copyProps = [
      'boxSizing', 'width', 'height', 'overflowX', 'overflowY',
      'borderTopWidth', 'borderRightWidth', 'borderBottomWidth', 'borderLeftWidth',
      'paddingTop', 'paddingRight', 'paddingBottom', 'paddingLeft',
      'fontStyle', 'fontVariant', 'fontWeight', 'fontStretch', 'fontSize',
      'fontSizeAdjust', 'lineHeight', 'fontFamily',
      'textAlign', 'textTransform', 'textIndent', 'textDecoration',
      'letterSpacing', 'wordSpacing', 'tabSize', 'MozTabSize', 'whiteSpace',
      'wordWrap', 'wordBreak'
    ]
    copyProps.forEach(p => { _mirror.style[p] = cs[p] })
    _mirror.style.position = 'absolute'
    _mirror.style.top = '0'
    _mirror.style.left = '-9999px'
    _mirror.style.zIndex = '-1'
    _mirror.style.visibility = 'hidden'
    _mirror.style.whiteSpace = 'pre-wrap'
    document.body.appendChild(_mirror)
  }
  const text = ta.value.substring(0, offset)
  _mirror.textContent = ''
  const pre = document.createTextNode(text)
  const marker = document.createElement('span')
  marker.textContent = '\u200b'
  _mirror.appendChild(pre)
  _mirror.appendChild(marker)
  // 同步滚动，避免测量位置错位
  _mirror.scrollTop = ta.scrollTop
  _mirror.scrollLeft = ta.scrollLeft
  const taRect = ta.getBoundingClientRect()
  const mRect = marker.getBoundingClientRect()
  const wrapRect = wrap.getBoundingClientRect()
  // 转换为相对 textarea-wrap 的坐标
  return {
    top: mRect.top - wrapRect.top,
    left: mRect.left - wrapRect.left,
    taTopInWrap: taRect.top - wrapRect.top,
    taLeftInWrap: taRect.left - wrapRect.left
  }
}

function updateFloatingBarPosition() {
  const range = getSelectionRange()
  if (!range) { hideFloatingBar(); return }
  const pos = getCaretPixel(Math.min(range.start, range.end))
  const endPos = getCaretPixel(Math.max(range.start, range.end))
  const wrap = textareaRef.value?.parentElement
  if (!wrap) return
  // 水平居中于选区中间；垂直在选区上方
  const barWidth = 220 // 5 个按钮预估宽度
  let left = (pos.left + endPos.left) / 2 - barWidth / 2
  const wrapWidth = wrap.clientWidth
  left = Math.max(8, Math.min(left, wrapWidth - barWidth - 8))
  // 在选区上方，bar 高度约 40px
  let top = Math.min(pos.top, endPos.top) - 48
  if (top < 8) top = Math.max(pos.top, endPos.top) + 24 // 上方不够 → 放选区下方
  floatingX.value = left
  floatingY.value = top
  floatingBarVisible.value = true
}

function hideFloatingBar() {
  floatingBarVisible.value = false
}

let hideFloatTimer = null
function onTextSelect() {
  if (hideFloatTimer) clearTimeout(hideFloatTimer)
  hideFloatTimer = setTimeout(() => {
    const range = getSelectionRange()
    if (range) updateFloatingBarPosition()
    else hideFloatingBar()
  }, 30)
}
function onTouchEnd() {
  // 移动端 touchend 后稍等片刻（等 selection 稳定）再计算
  if (hideFloatTimer) clearTimeout(hideFloatTimer)
  hideFloatTimer = setTimeout(onTextSelect, 80)
}
function onTextareaBlur() {
  // blur 时可能是点击浮动工具栏（mousedown preventDefault 阻止了 blur）
  // 所以延迟隐藏，给工具栏 click 留时间
  setTimeout(hideFloatingBar, 120)
}
function onTextareaFocus() {
  // =============== P1-6: 聚焦时把光标滚动到可视区中央，避免被软键盘遮挡 ===============
  nextTick(() => {
    const ta = textareaRef.value
    if (!ta) return
    try {
      ta.scrollIntoView({ block: 'center', behavior: 'smooth' })
    } catch { }
  })
}

const floatingBarStyle = computed(() => ({
  left: `${floatingX.value}px`,
  top: `${floatingY.value}px`
}))

function applyFloat(type) {
  switch (type) {
    case 'bold': insert('**', '**', '粗体文字'); break
    case 'italic': insert('*', '*', '斜体文字'); break
    case 'strike': insert('~~', '~~', '删除线文字'); break
    case 'code': insert('`', '`', 'code'); break
    case 'link': insert('[', '](https://)', '链接文本'); break
  }
  nextTick(() => {
    // 插入后重新定位（如果还有选区）
    const r = getSelectionRange()
    if (r) updateFloatingBarPosition()
    else hideFloatingBar()
  })
}

// =============== P1-6 动态样式（软键盘高度影响 textarea padding 与吸底工具栏位置） ===============
const textareaDynamicStyle = computed(() => {
  if (isDesktop.value) return {}
  return {
    paddingBottom: `${90 + keyboardHeight.value}px`
  }
})

const mobileToolbarStyle = computed(() => {
  if (isDesktop.value) return {}
  const safeBottom = typeof window !== 'undefined'
    ? (parseFloat(getComputedStyle(document.documentElement).getPropertyValue('--safe-area-bottom')) || 0)
    : 0
  return {
    bottom: `${keyboardHeight.value}px`,
    paddingBottom: `calc(8px + env(safe-area-inset-bottom, ${safeBottom}px) + ${keyboardHeight.value > 0 ? 4 : 0}px)`
  }
})

// =============== P2-1 大纲导航 ===============
const showOutline = ref(false)
const activeHeadingIdx = ref(0)

/**
 * 解析 Markdown 标题（ATX 风格：在行首匹配 1~6 个 # 后接空格）
 * 排除出现在代码块或引用块中的 #。实现：逐行扫描 + inFence/inBlockquote 状态机，
 * 只接受"真正的行首"（之前是换行/文本起始）、非 ``` 代码块内、且非 > 引用行首的标题。
 */
const outlineList = computed(() => {
  const text = form.content || ''
  const result = []
  let inFence = false
  const lines = text.split(/\r?\n/)
  let offset = 0 // 累积字符 offset（包含换行），用于后续 scrollToHeading 精确定位
  for (let i = 0; i < lines.length; i++) {
    const line = lines[i]
    const lineStartOffset = offset
    // 代码块围栏检测（``` 或 ~~~）
    const fenceMatch = line.match(/^\s*(```|~~~)/)
    if (fenceMatch && !line.slice(fenceMatch[0].length).includes(fenceMatch[1])) {
      inFence = !inFence
    }
    if (!inFence) {
      const m = line.match(/^(#{1,6})(?:\s+)(.*)$/)
      if (m) {
        const level = m[1].length
        const rawText = m[2].replace(/\s*#+\s*$/, '').trim() // 去掉闭合 #
        result.push({
          level,
          text: rawText,
          offset: lineStartOffset,
          line: i
        })
      }
    }
    offset += line.length + 1 // +1 表示换行符
  }
  return result
})

function scrollToHeading(idx) {
  const ta = textareaRef.value
  if (!ta) return
  const h = outlineList.value[idx]
  if (!h) return
  activeHeadingIdx.value = idx
  // 用镜像 div 算出该标题行首的像素位置 → 滚动到顶部再留一点上边距
  const px = getCaretPixel(h.offset)
  const targetTop = (px.taTopInWrap || 0) + px.top - 24
  ta.scrollTo({ top: Math.max(0, targetTop), behavior: 'smooth' })
  ta.focus()
  ta.setSelectionRange(h.offset, h.offset)
}

function updateActiveHeading() {
  const ta = textareaRef.value
  if (!ta || outlineList.value.length === 0) return
  const viewTop = ta.scrollTop
  // 找最后一个「标题像素位置 <= viewport 顶部 + 40」的标题作为当前
  let bestIdx = 0
  for (let i = 0; i < outlineList.value.length; i++) {
    const px = getCaretPixel(outlineList.value[i].offset)
    const rel = (px.taTopInWrap || 0) + px.top
    // 相对 textarea 的内容可视区顶部：px.top 是相对 wrap，但 textarea 的 scroll 把上方内容挤出可视区
    // 简化做法：offset 对应字符 offset，scrollTop 近似正比；用镜像 + scrollTop 同步后取 marker.top - ta.top
    const taRect = ta.getBoundingClientRect()
    const wrapRect = ta.parentElement.getBoundingClientRect()
    const absTop = (px.taTopInWrap || 0) + px.top - (taRect.top - wrapRect.top) + ta.scrollTop
    if (absTop <= viewTop + 36) bestIdx = i
    else break
  }
  activeHeadingIdx.value = bestIdx
}

function onTextareaScroll() {
  hideFloatingBar()
  // P2-1: 滚动时更新大纲高亮（节流 30ms）
  if (_scrollTimer) clearTimeout(_scrollTimer)
  _scrollTimer = setTimeout(updateActiveHeading, 30)
}
let _scrollTimer = null

// =============== P2-2 搜索 & 替换 ===============
const showSearch = ref(false)
const searchKeyword = ref('')
const replaceText = ref('')
const useRegex = ref(false)
const matchCase = ref(false)
const currentMatchIdx = ref(0)

/** 把 matches 作为副作用触发计算（Vue 计算属性在模板中访问时会懒求值，
 *  这里把"匹配结果数组"和"当前 index"拆包出来，以便搜索框输入时实时高亮定位） */
const matches = computed(() => {
  const kw = searchKeyword.value
  const text = form.content || ''
  if (!kw) return []
  let re
  try {
    const flags = (matchCase.value ? '' : 'i') + 'g'
    if (useRegex.value) re = new RegExp(kw, flags)
    else {
      // 非正则模式：转义特殊字符
      const escaped = kw.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
      re = new RegExp(escaped, flags)
    }
  } catch { return [] }
  const res = []
  let m
  while ((m = re.exec(text)) !== null) {
    res.push({ start: m.index, end: m.index + m[0].length, text: m[0] })
    if (m[0].length === 0) re.lastIndex++ // 避免零宽死循环
  }
  return res
})

function ensureMatchesComputed() {
  // 仅为了触发 matches computed 在输入时立即重算（不依赖模板访问时机）
  void matches.value
  if (matches.value.length > 0) {
    // 若当前索引超出，重置到最后一个
    if (currentMatchIdx.value >= matches.value.length) {
      currentMatchIdx.value = matches.value.length - 1
    }
    scrollToMatch(currentMatchIdx.value)
  }
}

function openSearch() {
  showSearch.value = true
  nextTick(() => {
    const el = document.querySelector('.search-input')
    if (el) { el.focus(); (el).select() }
  })
}
function closeSearch() {
  showSearch.value = false
  currentMatchIdx.value = 0
  searchKeyword.value = ''
  replaceText.value = ''
  // 关闭后重置位置，下一次打开回到默认的右上角
  searchDragX.value = null
  searchDragY.value = null
}

function scrollToMatch(idx) {
  const ta = textareaRef.value
  if (!ta || !matches.value[idx]) return
  const { start, end } = matches.value[idx]
  // 把当前 match 滚到可视区中上位置
  const px = getCaretPixel(start)
  const taRect = ta.getBoundingClientRect()
  const wrapRect = ta.parentElement.getBoundingClientRect()
  const contentY = (px.taTopInWrap || 0) + px.top - (taRect.top - wrapRect.top) + ta.scrollTop
  ta.scrollTo({ top: Math.max(0, contentY - ta.clientHeight / 3), behavior: 'smooth' })
  ta.focus()
  nextTick(() => ta.setSelectionRange(start, end))
}
function searchNext() {
  if (matches.value.length === 0) return
  currentMatchIdx.value = (currentMatchIdx.value + 1) % matches.value.length
  scrollToMatch(currentMatchIdx.value)
}
function searchPrev() {
  if (matches.value.length === 0) return
  currentMatchIdx.value = (currentMatchIdx.value - 1 + matches.value.length) % matches.value.length
  scrollToMatch(currentMatchIdx.value)
}

function doReplace() {
  if (matches.value.length === 0) return
  const m = matches.value[currentMatchIdx.value]
  if (!m) return
  const before = form.content.substring(0, m.start)
  const after = form.content.substring(m.end)
  form.content = before + replaceText.value + after
  nextTick(() => {
    ensureMatchesComputed()
    if (matches.value.length > 0) {
      // 跳到下一个（若当前已经是末尾则重置到第 0 个）
      if (currentMatchIdx.value >= matches.value.length) currentMatchIdx.value = 0
      scrollToMatch(currentMatchIdx.value)
    }
  })
  showToast('已替换 1 处')
}

function doReplaceAll() {
  const total = matches.value.length
  if (total === 0) return
  const kw = searchKeyword.value
  try {
    const flags = (matchCase.value ? '' : 'i') + 'g'
    const re = useRegex.value ? new RegExp(kw, flags) : new RegExp(kw.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'), flags)
    form.content = form.content.replace(re, replaceText.value)
    showToast(`已替换 ${total} 处`)
    currentMatchIdx.value = 0
  } catch {
    showToast('正则语法错误')
  }
}

// =============== P2-3 全屏沉浸式编辑 ===============
const isFullscreen = ref(false)

function toggleFullscreen() {
  const doc = document
  if (!doc.fullscreenElement) {
    const target = doc.documentElement
    const req = target.requestFullscreen || (target).webkitRequestFullscreen || (target).msRequestFullscreen
    if (req) req.call(target).catch(() => showToast('当前浏览器不支持全屏'))
    else showToast('当前浏览器不支持全屏')
  } else {
    const exit = doc.exitFullscreen || (doc).webkitExitFullscreen || (doc).msExitFullscreen
    if (exit) exit.call(doc)
  }
}
function onFullscreenChange() {
  isFullscreen.value = !!document.fullscreenElement
}

// =============== P1-7 键盘快捷键（桌面端） — 扩展 P2 的快捷键 ===============
function onKeyDown(e) {
  const ta = textareaRef.value
  if (!ta) return
  const mod = e.metaKey || e.ctrlKey
  const k = e.key.toLowerCase()

  // Esc 优先处理：关闭搜索/大纲或退出全屏
  if (e.key === 'Escape') {
    if (showSearch.value) { closeSearch(); e.preventDefault(); return }
    if (showOutline.value) { showOutline.value = false; e.preventDefault(); return }
    if (isFullscreen.value) {
      const doc = document
      if (doc.fullscreenElement) {
        const exit = doc.exitFullscreen || (doc).webkitExitFullscreen || (doc).msExitFullscreen
        if (exit) exit.call(doc)
      }
      e.preventDefault()
      return
    }
  }

  if (mod) {
    if (k === 'f') {
      // 打开搜索（Ctrl/Cmd+F 拦截浏览器默认页内搜索）
      e.preventDefault()
      openSearch()
      return
    }
    if (k === 's' && !e.shiftKey) {
      e.preventDefault()
      onSave()
      return
    }
    if (e.shiftKey && k === 'o') {
      // Ctrl+Shift+O 切换大纲
      e.preventDefault()
      showOutline.value = !showOutline.value
      return
    }
    if (k === 'b') { e.preventDefault(); applyFloat('bold'); return }
    if (k === 'i') { e.preventDefault(); applyFloat('italic'); return }
    if (k === 'k') { e.preventDefault(); applyFloat('link'); return }
    if (k === 's' && e.shiftKey) { e.preventDefault(); applyFloat('strike'); return }
  }

  // 搜索面板内部：Shift+Enter → 上一个；Enter → 下一个
  if (showSearch.value) {
    if (e.key === 'Enter' && e.shiftKey) {
      e.preventDefault()
      searchPrev()
      return
    }
  }
}

async function loadData() {
  const [cats, tgs] = await Promise.all([http.get('/categories'), http.get('/tags')])
  categories.value = cats
  tags.value = tgs

  if (!auth.user) {
    try { await auth.fetchUser() } catch { /* ignore */ }
  }

  if (isEdit.value) {
    const note = await http.get(`/notes/${noteId.value}`)
    form.title = note.title
    form.content = note.content
    form.categoryId = note.categoryId
    form.isPinned = note.isPinned || false
    form.tagIds = (note.tags || [])
      .map((name) => tags.value.find((t) => t.name === name)?.id)
      .filter(Boolean)
    form.backgroundColor = note.backgroundColor || null
    // 刚从服务器取回来的内容视为已保存基准
    lastSavedAt.value = note.updatedAt ? new Date(note.updatedAt).getTime() : null
    saveStatus.value = 'saved'
    pendingChanges = false
    updateDocumentTitle()
  } else {
    saveStatus.value = 'idle'
    pendingChanges = false
    updateDocumentTitle()
  }
}

function onCategoryConfirm({ selectedValues }) {
  form.categoryId = selectedValues[0] ?? null
  showCategoryPicker.value = false
}

function toggleTag(id) {
  const i = form.tagIds.indexOf(id)
  if (i >= 0) form.tagIds.splice(i, 1)
  else form.tagIds.push(id)
}

// 方案A：Chip × 点击移除标签
function removeTag(id) {
  const i = form.tagIds.indexOf(id)
  if (i >= 0) form.tagIds.splice(i, 1)
}

// 方案A：标签弹窗 — 内联新建标签（回车/按钮触发），创建成功自动勾选并加入 tags 列表
async function createTagInline() {
  const name = newTagName.value.trim()
  if (!name) { showToast('请输入标签名'); return }
  // 已存在则直接勾选并清空输入
  const exist = tags.value.find((t) => t.name.toLowerCase() === name.toLowerCase())
  if (exist) {
    if (!form.tagIds.includes(exist.id)) form.tagIds.push(exist.id)
    newTagName.value = ''
    showToast('已勾选')
    return
  }
  creatingTag.value = true
  try {
    const created = await http.post('/tags', { name })
    // 后端可能返回 { id, name, noteCount } 或 { name }，兜底兼容
    const record = created && created.id ? created : { id: created?.id, name, noteCount: 0 }
    // 兼容后端只返回 name/空对象的情况：再刷新一次列表拿真实 id
    if (!record.id) {
      const fresh = await http.get('/tags')
      tags.value = fresh
      const just = fresh.find((t) => t.name === name)
      if (just) {
        record.id = just.id
        record.noteCount = just.noteCount ?? 0
      }
    } else {
      tags.value.push(record)
    }
    if (record.id && !form.tagIds.includes(record.id)) form.tagIds.push(record.id)
    newTagName.value = ''
    showToast('已创建并勾选')
  } catch {
    // http 全局拦截器会弹错误 toast
  } finally {
    creatingTag.value = false
  }
}

// 方案A：分类弹窗 — 内联新建分类（回车/按钮触发），创建成功自动设为当前分类
async function createCategoryInline() {
  const name = newCategoryName.value.trim()
  if (!name) { showToast('请输入分类名'); return }
  const exist = categories.value.find((c) => c.name.toLowerCase() === name.toLowerCase())
  if (exist) {
    form.categoryId = exist.id
    newCategoryName.value = ''
    showToast('已选择')
    return
  }
  creatingCategory.value = true
  try {
    const created = await http.post('/categories', { name })
    const record = created && created.id ? created : null
    let finalId = record?.id
    if (!finalId) {
      // 后端没返回 id：刷新拿真实 id
      const fresh = await http.get('/categories')
      categories.value = fresh
      const just = fresh.find((c) => c.name === name)
      if (just) finalId = just.id
    } else {
      categories.value.push(record)
    }
    if (finalId) form.categoryId = finalId
    newCategoryName.value = ''
    showToast('已创建并应用')
  } catch {
    // 由全局拦截器提示错误
  } finally {
    creatingCategory.value = false
  }
}

function insert(before, after = '', placeholder = '') {
  const ta = textareaRef.value
  if (!ta) return
  const start = ta.selectionStart
  const end = ta.selectionEnd
  const sel = form.content.substring(start, end) || placeholder
  form.content =
    form.content.substring(0, start) + before + sel + after + form.content.substring(end)
  nextTick(() => {
    ta.focus()
    const pos = start + before.length + sel.length
    ta.setSelectionRange(pos, pos)
  })
}

function formatFormulas() {
  let text = form.content
  if (!text || !text.trim()) {
    showToast('没有内容可格式化')
    return
  }
  const original = text
  text = text.replace(/\$\$([\s\S]*?)\$\$/g, (match, inner, offset, full) => {
    const formula = inner.trim()
    const beforeChar = offset > 0 ? full[offset - 1] : ''
    const afterChar = offset + match.length < full.length ? full[offset + match.length] : ''
    const needNewlineBefore = beforeChar && beforeChar !== '\n'
    const needNewlineAfter = afterChar && afterChar !== '\n'
    return `${needNewlineBefore ? '\n' : ''}$$\n${formula}\n$$${needNewlineAfter ? '\n' : ''}`
  })
  text = text.replace(/(?<![\$\\])\$(?!\$)([^\n]+?)(?<!\$)\$(?!\$)/g, (match, _content, offset, full) => {
    const beforeChar = offset > 0 ? full[offset - 1] : ''
    const afterChar = offset + match.length < full.length ? full[offset + match.length] : ''
    const needPrefix = beforeChar && !/\s/.test(beforeChar)
    const needSuffix = afterChar && !/\s/.test(afterChar)
    return `${needPrefix ? ' ' : ''}${match}${needSuffix ? ' ' : ''}`
  })
  if (text !== original) {
    form.content = text
    showToast('已格式化公式')
  } else {
    showToast('无需格式化')
  }
}

// =============== P1-8: 确保当前笔记有 id（新建模式下，上传图片前先 POST 建空壳） ===============
async function ensureNoteId() {
  if (isReallyEdit.value) return true
  // 如果刚在请求中：等它完再决定；如果已成功保存 isReallyEdit 会变 true（因为 URL replace）
  if (autoSaveInFlight) {
    const t0 = Date.now()
    while (autoSaveInFlight && Date.now() - t0 < 20000) {
      await new Promise((r) => setTimeout(r, 50))
    }
    if (isReallyEdit.value) return true
  }
  // 仍为新建态：强制 POST 一条空壳笔记（标题或内容给一个空格兜底），保证附件上传有 id
  autoSaveInFlight = true
  saveStatus.value = 'saving'
  saving.value = true
  try {
    const payload = {
      title: form.title.trim() || ' ',
      content: form.content || ' ',
      categoryId: form.categoryId,
      tagIds: form.tagIds,
      backgroundColor: form.backgroundColor || '',
      isPinned: form.isPinned
    }
    const created = await http.post('/notes', payload)
    cancelAutoSaveTimer()
    lastSavedAt.value = Date.now()
    saveStatus.value = 'saved'
    pendingChanges = false
    updateDocumentTitle()
    await router.replace(`/notes/${created.id}/edit`)
    showToast('已自动创建笔记')
    return true
  } catch (err) {
    saveStatus.value = 'error'
    saveErrorMsg.value = err?.message || '未知错误'
    showToast('创建笔记失败')
    return false
  } finally {
    autoSaveInFlight = false
    saving.value = false
  }
}

async function triggerUpload() {
  if (!isReallyEdit.value) {
    const ok = await ensureNoteId()
    if (!ok) return
  }
  const target = isDesktop.value ? fileInput.value : mobileFileInput.value
  target?.click()
}

async function handleImageFile(file) {
  if (!file || !file.type?.startsWith('image/')) return
  if (!isReallyEdit.value) {
    const ok = await ensureNoteId()
    if (!ok) return
  }
  try {
    const fd = new FormData()
    fd.append('file', file)
    const res = await http.post(`/notes/${effectiveNoteId.value}/attachments`, fd)
    insert(`\n![${res.fileName || '图片'}](/api/attachments/${res.id})\n`)
    showToast('图片已插入')
  } catch {
    // 拦截器提示
  }
}

async function onFileChange(e) {
  const file = e.target.files?.[0]
  if (!file) return
  await handleImageFile(file)
  e.target.value = ''
}

// =============== P1-8 粘贴上传图片 ===============
function onPaste(e) {
  if (!e.clipboardData) return
  const items = e.clipboardData.items
  if (!items || !items.length) return
  for (const it of items) {
    if (it.type?.startsWith('image/')) {
      const file = it.getAsFile?.()
      if (file) {
        e.preventDefault()
        handleImageFile(file)
        return
      }
    }
  }
}

// =============== P1-8 拖拽上传图片 ===============
function onDragOver(e) {
  if (!e.dataTransfer?.types?.includes('Files')) return
  dragOver.value = true
}
function onDragLeave(e) {
  dragOver.value = false
}
function onDrop(e) {
  dragOver.value = false
  const files = e.dataTransfer?.files
  if (!files || !files.length) return
  for (const f of files) {
    if (f.type?.startsWith('image/')) {
      handleImageFile(f)
      break
    }
  }
}

async function onSave() {
  const res = await flushAutoSave({ forceEvenIfNotPending: true })
  if (!res.ok) {
    showToast('保存失败：' + (saveErrorMsg.value || ''))
    return
  }
  if (res.blank) {
    showToast('标题和内容不能同时为空')
    return
  }
  showToast('已保存')
}

async function onBack() {
  // 完全空白：直接离开
  const blank = !form.title.trim() && !form.content.trim()
  if (blank && !pendingChanges) {
    cancelAutoSaveTimer()
    router.back()
    return
  }
  // 有内容/有改动：flush 保存成功后返回；失败阻止离开
  const res = await flushAutoSave({ forceEvenIfNotPending: false })
  if (!res.ok) {
    showToast('保存失败，未离开')
    return
  }
  // 新建但空白：flushAutoSave silentIfBlank=false 会尝试保存，但 POST/PUT 可能被拒
  if (res.blank) {
    // 服务器拒绝空白笔记，直接离开不报错
  }
  cancelAutoSaveTimer()
  router.back()
}

// =============== 生命周期 & 全局事件 ===============
let _keydownHandler = null
let _docKeydownHandler = null

onMounted(() => {
  updateDocumentTitle()
  loadData()

  // P1-7 + P2：textarea 范围内快捷键（处理修饰键组合等）
  nextTick(() => {
    const ta = textareaRef.value
    if (ta) {
      _keydownHandler = onKeyDown
      ta.addEventListener('keydown', _keydownHandler)
    }
  })

  // P2：文档级快捷键拦截（Ctrl+F / Esc / Ctrl+Shift+O 可以不在 textarea focus 时触发）
  _docKeydownHandler = (e) => {
    const mod = e.metaKey || e.ctrlKey
    const k = e.key.toLowerCase()
    if (e.key === 'Escape') {
      // 交给 onKeyDown，但需要手动调用一次来处理面板关闭/退出全屏（当焦点不在 textarea）
      const ta = textareaRef.value
      if (document.activeElement !== ta) onKeyDown(e)
    }
    if (mod && k === 'f') {
      // 只有当焦点不在浏览器原生搜索输入时拦截
      const tag = (document.activeElement?.tagName || '').toLowerCase()
      if (tag === 'input' || tag === 'textarea') return // 由元素上监听器处理
      e.preventDefault()
      openSearch()
    }
    if (mod && e.shiftKey && k === 'o') {
      e.preventDefault()
      showOutline.value = !showOutline.value
    }
  }
  document.addEventListener('keydown', _docKeydownHandler)

  // P1-6 软键盘高度监听
  if (window.visualViewport) {
    window.visualViewport.addEventListener('resize', onViewportChange)
    window.visualViewport.addEventListener('scroll', onViewportChange)
  }
  window.addEventListener('resize', onViewportChange)

  // 点击外部关闭浮动工具栏 / 大纲面板 / 搜索面板
  document.addEventListener('mousedown', (e) => {
    const ta = textareaRef.value
    const wrap = ta?.parentElement
    if (wrap && !wrap.contains(e.target)) hideFloatingBar()

    const outline = document.querySelector('.outline-panel')
    if (showOutline.value && outline && !outline.contains(e.target) && !(e.target).closest?.('.nav-icon')) {
      // 不在这里直接关，避免和按钮的 click 冲突：交给各自 click self mask 处理
    }
  })

  // P2-3 全屏事件
  document.addEventListener('fullscreenchange', onFullscreenChange)
  document.addEventListener('webkitfullscreenchange', onFullscreenChange)
  document.addEventListener('msfullscreenchange', onFullscreenChange)
})

onUnmounted(() => {
  cancelAutoSaveTimer()
  document.title = '笔记'

  // 清理浮动栏的镜像 div
  if (_mirror) {
    try { document.body.removeChild(_mirror) } catch { }
    _mirror = null
  }
  // 清理快捷键
  if (_keydownHandler && textareaRef.value) {
    textareaRef.value.removeEventListener('keydown', _keydownHandler)
    _keydownHandler = null
  }
  if (_docKeydownHandler) {
    document.removeEventListener('keydown', _docKeydownHandler)
    _docKeydownHandler = null
  }
  // 清理键盘高度监听
  if (window.visualViewport) {
    window.visualViewport.removeEventListener('resize', onViewportChange)
    window.visualViewport.removeEventListener('scroll', onViewportChange)
  }
  window.removeEventListener('resize', onViewportChange)
  // 清理全屏监听
  document.removeEventListener('fullscreenchange', onFullscreenChange)
  document.removeEventListener('webkitfullscreenchange', onFullscreenChange)
  document.removeEventListener('msfullscreenchange', onFullscreenChange)
  dragOver.value = false
  if (_scrollTimer) clearTimeout(_scrollTimer)
})
</script>

<style scoped src="./NoteEdit.css">
</style>
