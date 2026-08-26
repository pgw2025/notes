/**
 * 后端 API 地址配置
 * 前后端同域部署，直接使用当前站点 origin
 */
export const API_BASE_URL = typeof window !== 'undefined' ? window.location.origin : ''
