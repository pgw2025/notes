import axios from 'axios'
import { ElMessage } from 'element-plus'
import router from '../router'

const http = axios.create({
  baseURL: '/api',
  timeout: 15000
})

/** 解析 JWT payload（不含验签，仅取声明用） */
export function decodeJwt(token) {
  try {
    const payload = token.split('.')[1]
    const base64 = payload.replace(/-/g, '+').replace(/_/g, '/')
    const json = decodeURIComponent(
      atob(base64)
        .split('')
        .map((c) => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2))
        .join('')
    )
    return JSON.parse(json)
  } catch {
    return null
  }
}

/** 从 JWT payload 中提取角色（后端使用 ClaimTypes.Role 标准声明 URI） */
export function getRolesFromToken(token) {
  const claims = decodeJwt(token)
  if (!claims) return []
  const roleClaim =
    claims['http://schemas.microsoft.com/ws/2008/06/identity/claims/role']
  if (Array.isArray(roleClaim)) return roleClaim
  if (typeof roleClaim === 'string') return [roleClaim]
  return []
}

export function isAdminToken(token) {
  return getRolesFromToken(token).includes('Admin')
}

http.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem('admin_token')
    if (token) {
      config.headers.Authorization = `Bearer ${token}`
    }
    return config
  },
  (error) => Promise.reject(error)
)

http.interceptors.response.use(
  (response) => response.data,
  (error) => {
    const status = error.response?.status
    const message =
      error.response?.data?.message || error.message || '请求失败，请稍后重试'

    if (status === 401) {
      localStorage.removeItem('admin_token')
      if (router.currentRoute.value.name !== 'login') {
        router.replace({ name: 'login' })
      }
    } else {
      ElMessage.error(message)
    }

    return Promise.reject(error)
  }
)

export default http