import axios from 'axios'
import { showToast } from 'vant'
import router from '../router'

const http = axios.create({
  baseURL: '/api',
  timeout: 15000
})

http.interceptors.request.use(
  (config) => {
    const method = (config.method || 'get').toUpperCase()

    // 离线时拦截一切写操作：不发起请求，直接提示。
    // 读操作(GET)交给 Service Worker 的离线缓存处理（见 vite.config.js runtimeCaching）。
    if (
      ['POST', 'PUT', 'DELETE', 'PATCH'].includes(method) &&
      typeof navigator !== 'undefined' &&
      navigator.onLine === false
    ) {
      showToast({ message: '当前处于离线状态，无法保存，请恢复网络后重试', type: 'fail' })
      const err = new Error('离线状态，写操作被拦截')
      err._offlineBlocked = true
      return Promise.reject(err)
    }

    const token = localStorage.getItem('token')
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
      localStorage.removeItem('token')
      if (router.currentRoute.value.name !== 'login') {
        router.replace({
          name: 'login',
          query: { redirect: router.currentRoute.value.fullPath }
        })
      }
    } else {
      showToast(message)
    }

    return Promise.reject(error)
  }
)

export default http
