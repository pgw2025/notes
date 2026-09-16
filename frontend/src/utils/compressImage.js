// 浏览器端图片压缩：在上传前缩小图片，节省服务器带宽与存储。
// 规则：GIF 动图不动、透明 PNG 保持透明、只有压缩后更小才替换原图。
const DEFAULT_MAX_EDGE = 1920 // 最长边上限(px)
const DEFAULT_QUALITY = 0.8 // JPEG/WebP 压缩质量(0~1)
const SMALL_FILE_THRESHOLD = 300 * 1024 // 小于该体积且无需缩小则直接返回原图

/**
 * 将图片文件解码为可绘制的画布源（ImageBitmap 优先，自动应用 EXIF 方向；不支持时回退到 <img>）。
 */
function loadImage(file) {
  if (typeof createImageBitmap === 'function') {
    return createImageBitmap(file, { imageOrientation: 'from-image' }).catch(() =>
      loadViaImageElement(file)
    )
  }
  return loadViaImageElement(file)
}

function loadViaImageElement(file) {
  return new Promise((resolve, reject) => {
    const url = URL.createObjectURL(file)
    const img = new Image()
    img.onload = () => {
      URL.revokeObjectURL(url)
      resolve({ width: img.naturalWidth, height: img.naturalHeight, source: img })
    }
    img.onerror = () => {
      URL.revokeObjectURL(url)
      reject(new Error('图片解码失败'))
    }
    img.src = url
  })
}

/**
 * 缩小采样检测图片是否存在透明像素（PNG/WebP 判断是否需要保留 alpha 通道）。
 */
function hasAlpha(bmp, source) {
  const width = bmp.width || source.naturalWidth
  const height = bmp.height || source.naturalHeight
  const tw = Math.max(1, width >> 4) // 采样缩小至 1/16，速度优先
  const th = Math.max(1, height >> 4)
  const t = document.createElement('canvas')
  t.width = tw
  t.height = th
  const ctx = t.getContext('2d', { willReadFrequently: true })
  ctx.clearRect(0, 0, tw, th)
  ctx.drawImage(bmp || source, 0, 0, tw, th)
  const data = ctx.getImageData(0, 0, tw, th).data
  for (let i = 3; i < data.length; i += 4) {
    if (data[i] < 255) return true
  }
  return false
}

function canvasToBlob(canvas, type, quality) {
  return new Promise((resolve) => canvas.toBlob(resolve, type, quality))
}

/**
 * 压缩图片。
 * @param {File} file 原始图片文件
 * @param {{maxEdge?:number, quality?:number}} [opts]
 * @returns {Promise<File>} 压缩后的文件；无需压缩或压缩失败时返回原文件
 */
export async function compressImage(file, opts = {}) {
  if (!file || !file.type.startsWith('image/') || file.type === 'image/gif') return file

  const maxEdge = opts.maxEdge || DEFAULT_MAX_EDGE
  const quality = opts.quality ?? DEFAULT_QUALITY

  try {
    const decoded = await loadImage(file)
    const width = decoded.width || decoded.source?.naturalWidth
    const height = decoded.height || decoded.source?.naturalHeight
    if (!width || !height) return file

    const scale = Math.min(1, maxEdge / Math.max(width, height))
    // 无需缩小且体积不大：直接返回原图，避免不必要的重编码损耗
    if (scale >= 1 && file.size < SMALL_FILE_THRESHOLD) {
      decoded.close?.()
      return file
    }

    const outW = Math.max(1, Math.round(width * scale))
    const outH = Math.max(1, Math.round(height * scale))

    // 有透明像素时强制保留 alpha（用 PNG 重编码），否则填白底转 webp/jpeg
    const transparent =
      (file.type === 'image/png' || file.type === 'image/webp') && hasAlpha(decoded, decoded.source)
    const type = transparent ? 'image/png' : 'image/webp'

    const canvas = document.createElement('canvas')
    canvas.width = outW
    canvas.height = outH
    const ctx = canvas.getContext('2d')
    if (transparent) {
      ctx.clearRect(0, 0, outW, outH)
    } else {
      ctx.fillStyle = '#fff'
      ctx.fillRect(0, 0, outW, outH)
    }
    ctx.drawImage(decoded, 0, 0, outW, outH)
    decoded.close?.()

    let outType = type
    // WebP 编码失败时回退到 JPEG
    let blob = await canvasToBlob(canvas, type, quality)
    if (type === 'image/webp' && (!blob || !blob.type.startsWith('image/webp'))) {
      outType = 'image/jpeg'
      blob = await canvasToBlob(canvas, 'image/jpeg', quality)
    }
    if (blob && blob.size && blob.size < file.size) {
      return new File([blob], file.name, { type: outType })
    }
    return file // 压缩后反而更大（小图/截图），丢弃压缩结果
  } catch {
    return file // 压缩失败不阻断上传，走原始文件
  }
}