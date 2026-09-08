import { defineStore } from 'pinia'
import http, { isAdminToken } from '../api/http'

export const useAuthStore = defineStore('auth', {
  state: () => ({
    token: localStorage.getItem('admin_token') || '',
    email: localStorage.getItem('admin_email') || '',
    displayName: localStorage.getItem('admin_displayName') || ''
  }),

  getters: {
    isLoggedIn: (state) => !!state.token,
    isAdmin: (state) => isAdminToken(state.token)
  },

  actions: {
    setSession(token, email, displayName) {
      this.token = token
      this.email = email
      this.displayName = displayName
      localStorage.setItem('admin_token', token)
      localStorage.setItem('admin_email', email || '')
      localStorage.setItem('admin_displayName', displayName || '')
    },

    async login(account, password) {
      const res = await http.post('/auth/login', { account, password })
      // 校验是否为管理员，非管理员不入库并抛错
      if (!isAdminToken(res.token)) {
        throw new Error('该账号不是管理员，无权访问后台')
      }
      this.setSession(res.token, res.email, res.displayName)
    },

    logout() {
      this.token = ''
      this.email = ''
      this.displayName = ''
      localStorage.removeItem('admin_token')
      localStorage.removeItem('admin_email')
      localStorage.removeItem('admin_displayName')
    }
  }
})