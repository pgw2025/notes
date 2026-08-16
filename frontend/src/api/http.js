import axios from 'axios'
import { showToast } from 'vant'
import router from '../router'

const http = axios.create({
  baseURL: '/api',
  timeout: 15000
})

http.interceptors.request.use(
  (config) => {
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
