/**
 * 颜色工具：亮度判断 + 预设色板
 */

/** 预设色板（10 个常用色，前 7 个浅色，后 3 个深色） */
export const PRESET_COLORS = [
  { name: '经典白', hex: '#FFFFFF' },
  { name: '米黄',   hex: '#FFF9C4' },
  { name: '暖橙',   hex: '#FFE0B2' },
  { name: '浅粉',   hex: '#FFCDD2' },
  { name: '薄荷绿', hex: '#C8E6C9' },
  { name: '天蓝',   hex: '#BBDEFB' },
  { name: '淡紫',   hex: '#E1BEE7' },
  { name: '深蓝',   hex: '#1A237E' },
  { name: '深绿',   hex: '#1B5E20' },
  { name: '墨黑',   hex: '#212121' }
]

/** 系统默认色（用户没设默认色、笔记也没设色时用） */
export const DEFAULT_NOTE_COLOR = '#FFFFFF'

/**
 * 校验是否为合法的 HEX 颜色字符串（#RRGGBB）
 */
export function isValidHex(s) {
  if (typeof s !== 'string' || s.length !== 7 || s[0] !== '#') return false
  for (let i = 1; i < 7; i++) {
    const c = s.charCodeAt(i)
    const isDigit = c >= 48 && c <= 57      // 0-9
    const isLower = c >= 97 && c <= 102     // a-f
    const isUpper = c >= 65 && c <= 70      // A-F
    if (!isDigit && !isLower && !isUpper) return false
  }
  return true
}

/**
 * 解析 HEX 颜色为 { r, g, b }（0-255）。非法格式返回 null。
 */
function parseHex(hex) {
  if (!isValidHex(hex)) return null
  return {
    r: parseInt(hex.slice(1, 3), 16),
    g: parseInt(hex.slice(3, 5), 16),
    b: parseInt(hex.slice(5, 7), 16)
  }
}

/**
 * 根据背景色亮度返回适配的文字色（WCAG YIQ 公式）。
 * - 浅色背景 → 深色文字 #1a1a1a
 * - 深色背景 → 白色文字 #FFFFFF
 *
 * @param {string|null|undefined} bgHex 背景色（#RRGGBB），为空则用默认白色
 * @returns {string} 文字色 HEX
 */
export function getContrastColor(bgHex) {
  const hex = bgHex && isValidHex(bgHex) ? bgHex : DEFAULT_NOTE_COLOR
  const { r, g, b } = parseHex(hex)
  // YIQ 亮度公式
  const yiq = (r * 299 + g * 587 + b * 114) / 1000
  return yiq >= 128 ? '#1a1a1a' : '#FFFFFF'
}

/**
 * 判断背景色是否为深色（用于决定 Tag 用什么样式）
 */
export function isDarkColor(bgHex) {
  const hex = bgHex && isValidHex(bgHex) ? bgHex : DEFAULT_NOTE_COLOR
  const { r, g, b } = parseHex(hex)
  const yiq = (r * 299 + g * 587 + b * 114) / 1000
  return yiq < 128
}

/**
 * 解析笔记的有效背景色：
 * 1. 优先用笔记自身色
 * 2. 否则用用户默认色
 * 3. 都没有则用系统白色
 */
export function resolveNoteColor(noteColor, userDefaultColor) {
  if (noteColor && isValidHex(noteColor)) return noteColor
  if (userDefaultColor && isValidHex(userDefaultColor)) return userDefaultColor
  return DEFAULT_NOTE_COLOR
}
