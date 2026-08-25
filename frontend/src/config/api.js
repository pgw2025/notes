/**
 * 后端 API 地址配置
 *
 * - 开发环境：与 vite.config.js 中 proxy '/api' 的 target 保持一致
 *   （修改后端端口时，这里和 vite.config.js 需要同步更新）
 * - 生产环境：前后端同域部署（nginx 将 /api 反向代理到后端），
 *   直接使用当前站点地址
 */
export const API_BASE_URL = import.meta.env.DEV
  ? 'http://localhost:5062'
  : window.location.origin
