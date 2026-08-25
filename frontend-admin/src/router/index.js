import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '../stores/auth'

const routes = [
  {
    path: '/login',
    name: 'login',
    component: () => import('../views/Login.vue'),
    meta: { public: true }
  },
  {
    path: '/',
    component: () => import('../layout/AdminLayout.vue'),
    redirect: '/dashboard',
    children: [
      {
        path: 'dashboard',
        name: 'dashboard',
        component: () => import('../views/Dashboard.vue'),
        meta: { title: '仪表盘' }
      },
      {
        path: 'users',
        name: 'users',
        component: () => import('../views/UserManage.vue'),
        meta: { title: '用户管理' }
      },
      {
        path: 'notes',
        name: 'notes',
        component: () => import('../views/NoteManage.vue'),
        meta: { title: '笔记管理' }
      },
      {
        path: 'categories',
        name: 'categories',
        component: () => import('../views/CategoryManage.vue'),
        meta: { title: '分类管理' }
      },
      {
        path: 'tags',
        name: 'tags',
        component: () => import('../views/TagManage.vue'),
        meta: { title: '标签管理' }
      },
      {
        path: 'attachments',
        name: 'attachments',
        component: () => import('../views/AttachmentManage.vue'),
        meta: { title: '附件管理' }
      }
    ]
  },
  { path: '/:pathMatch(.*)*', redirect: '/dashboard' }
]

const router = createRouter({
  history: createWebHistory('/admin/'),
  routes
})

router.beforeEach((to) => {
  const auth = useAuthStore()
  if (to.meta.public) {
    // 已登录管理员访问登录页则跳首页
    if (auth.isLoggedIn && auth.isAdmin) return { name: 'dashboard' }
    return true
  }
  if (!auth.isLoggedIn || !auth.isAdmin) {
    return { name: 'login' }
  }
  return true
})

export default router