// PWA 图标生成脚本
// 用法：node scripts/generate-icons.mjs
// 输出到 public/，供 vite-plugin-pwa 的 manifest 使用。
// 源图形与 favicon 一致（青瓷信笺：青瓷绿底 + 米色信笺 + 朱丝栏 + 祥云 + 朱印）。
import { mkdir, writeFile } from 'node:fs/promises'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import sharp from 'sharp'

const __dirname = dirname(fileURLToPath(import.meta.url))
const outDir = join(__dirname, '..', 'public')

// 青瓷信笺配色
const celadon = '#0FA98C'      // 青瓷绿（底）
const celadonDeep = '#128065'  // 深青瓷（祥云线条）
const paper = '#F8F4E9'        // 米色信笺纸
const ink = '#A9CFC6'          // 淡青（信笺文字行）
const vermilion = '#E34D59'    // 朱砂红（朱丝栏边框 + 印章）

// 信笺主体内容（纸张 + 朱丝栏 + 祥云 + 文字行 + 朱印）
// 供常规图标与 maskable 图标复用；maskable 由外层做 0.6 缩放并全出血铺底。
function paperContent() {
  return `
  <!-- 信笺纸 -->
  <rect x="136" y="96" width="240" height="320" rx="20" fill="${paper}"/>
  <!-- 朱丝栏（信笺红色内框） -->
  <rect x="158" y="118" width="196" height="276" rx="12" fill="none" stroke="${vermilion}" stroke-opacity="0.5" stroke-width="5"/>
  <!-- 祥云卷纹 -->
  <path fill="none" stroke="${celadonDeep}" stroke-width="16" stroke-linecap="round"
        d="M254 280 C218 280 188 250 188 214 C188 178 218 148 254 148 C288 148 316 174 316 208 C316 234 296 254 270 254 C248 254 231 238 231 216 C231 198 245 184 263 184"/>
  <!-- 文字行 -->
  <rect x="170" y="312" width="140" height="12" rx="6" fill="${ink}"/>
  <rect x="170" y="336" width="96" height="12" rx="6" fill="${ink}"/>
  <!-- 朱印（白纹祥云印章） -->
  <rect x="296" y="340" width="48" height="48" rx="8" fill="${vermilion}"/>
  <path fill="none" stroke="#FFFFFF" stroke-width="5" stroke-linecap="round"
        d="M328 372 C320 372 313 365 313 357 C313 350 318 344 325 344 C331 344 336 349 336 355"/>
  `
}

// 常规图标：圆角由系统裁切，图形尽量占满。
function regularSvg() {
  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512" width="512" height="512">
  <rect width="512" height="512" rx="112" fill="${celadon}"/>
  ${paperContent()}
</svg>`
}

// Maskable 图标：全出血铺底，图形内容控制在中心 60% 以内，避免被系统安全区裁掉。
function maskableSvg(contentScale) {
  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512" width="512" height="512">
  <rect width="512" height="512" fill="${celadon}"/>
  <g transform="translate(256 256) scale(${contentScale}) translate(-256 -256)">
    ${paperContent()}
  </g>
</svg>`
}

async function renderSvg(src, size, dest) {
  await sharp(Buffer.from(src))
    .resize(size, size)
    .png()
    .toFile(join(outDir, dest))
  console.log('✓', dest)
}

await mkdir(outDir, { recursive: true })

await renderSvg(regularSvg(), 192, 'pwa-192x192.png')
await renderSvg(regularSvg(), 512, 'pwa-512x512.png')
await renderSvg(maskableSvg(0.6), 192, 'pwa-maskable-192x192.png')
await renderSvg(maskableSvg(0.6), 512, 'pwa-maskable-512x512.png')
await renderSvg(regularSvg(), 180, 'apple-touch-icon-180x180.png')

// favicon.svg 与应用图标同源，保证浏览器标签页与安装图标一致
await writeFile(join(outDir, 'favicon.svg'), regularSvg(), 'utf8')
console.log('✓ favicon.svg')

console.log('全部图标已生成到 public/')
