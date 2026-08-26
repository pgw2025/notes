// PWA 图标生成脚本
// 用法：node scripts/generate-icons.mjs
// 输出到 public/，供 vite-plugin-pwa 的 manifest 使用。
// 源图形与现有 favicon（品牌紫 #863bff + 白/浅紫云笔符号）一致。
import { mkdir, writeFile } from 'node:fs/promises'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import sharp from 'sharp'

const __dirname = dirname(fileURLToPath(import.meta.url))
const outDir = join(__dirname, '..', 'public')

const brand = '#863bff'
const brandDeep = '#5d1fe0'
const soft = '#ede6ff'
const glow = '#b9a6ff'

// 常规图标：图形尽量占满，适合普通安装场景（圆角由系统裁切）。
function regularSvg() {
  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512" width="512" height="512">
  <defs>
    <linearGradient id="bg" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0" stop-color="${brand}"/>
      <stop offset="1" stop-color="${brandDeep}"/>
    </linearGradient>
  </defs>
  <rect width="512" height="512" rx="112" fill="url(#bg)"/>
  <g>
    <path transform="translate(150 120) scale(1.35) translate(-160 -86)" fill="${soft}" fill-opacity="0.22" d="M70 50 C70 22 92 0 120 0 C141 0 158 12 166 29 C171 28 176 27 181 27 C205 27 224 46 224 70 C224 88 213 104 197 111 L120 111 C92 111 70 89 70 61 Z"/>
    <path transform="translate(340 96) scale(1.1)" fill="${glow}" fill-opacity="0.35" d="M20 30 C20 22 27 15 35 15 C42 15 47 20 49 26 C51 26 53 25 55 25 C63 25 69 31 69 39 C69 45 65 50 59 52 L35 52 C27 52 20 45 20 37 Z"/>
  </g>
  <g transform="translate(96 140)">
    <rect x="0" y="0" width="320" height="300" rx="32" fill="#ffffff"/>
    <rect x="64" y="80" width="192" height="16" rx="8" fill="${soft}"/>
    <rect x="64" y="126" width="192" height="16" rx="8" fill="${soft}"/>
    <rect x="64" y="172" width="142" height="16" rx="8" fill="${soft}"/>
    <rect x="234" y="172" width="22" height="16" rx="8" fill="${soft}"/>
  </g>
</svg>`
}

// Maskable 图标：图形内容控制在中心 60% 以内，避免被系统安全区裁掉。
function maskableSvg(contentScale) {
  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512" width="512" height="512">
  <defs>
    <linearGradient id="bgm" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0" stop-color="${brand}"/>
      <stop offset="1" stop-color="${brandDeep}"/>
    </linearGradient>
  </defs>
  <rect width="512" height="512" fill="url(#bgm)"/>
  <g transform="translate(${256 - 320 * contentScale / 2} ${256 - 300 * contentScale / 2}) scale(${contentScale})">
    <path transform="translate(150 120) scale(1.35) translate(-160 -86)" fill="${soft}" fill-opacity="0.22" d="M70 50 C70 22 92 0 120 0 C141 0 158 12 166 29 C171 28 176 27 181 27 C205 27 224 46 224 70 C224 88 213 104 197 111 L120 111 C92 111 70 89 70 61 Z"/>
    <path transform="translate(340 96) scale(1.1)" fill="${glow}" fill-opacity="0.35" d="M20 30 C20 22 27 15 35 15 C42 15 47 20 49 26 C51 26 53 25 55 25 C63 25 69 31 69 39 C69 45 65 50 59 52 L35 52 C27 52 20 45 20 37 Z"/>
  </g>
  <g transform="translate(${96 * contentScale} ${140 * contentScale}) scale(${contentScale})">
    <rect x="0" y="0" width="320" height="300" rx="32" fill="#ffffff"/>
    <rect x="64" y="80" width="192" height="16" rx="8" fill="${soft}"/>
    <rect x="64" y="126" width="192" height="16" rx="8" fill="${soft}"/>
    <rect x="64" y="172" width="142" height="16" rx="8" fill="${soft}"/>
    <rect x="234" y="172" width="22" height="16" rx="8" fill="${soft}"/>
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

console.log('全部图标已生成到 public/')