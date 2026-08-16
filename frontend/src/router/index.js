import { createRouter, createWebHistory } from 'vue-router'

const routes = [
  { path: '/', redirect: '/notes' },
  {
    path: '/login',
    name: 'login',
    component: () => import('../views/Login.vue'),
    meta: { public: true, tabbar: false }
  },
  {
    path: '/register',
    name: 'register',
    component: () => import('../views/Register.vue'),
    meta: { public: true, tabbar: false }
  },
  { path: '/notes', name: 'notes', component: () => import('../views/NotesList.vue') },
  { path: '/timeline', name: 'timeline', component: () => import('../views/Timeline.vue') },
  {
    path: '/notes/new',
    name: 'note-new',
    component: () => import('../views/NoteEdit.vue'),
    meta: { tabbar: false }
  },
  {
    path: '/notes/:id',
    name: 'note-detail',
    component: () => import('../views/NoteDetail.vue'),
    meta: { tabbar: false }
  },
  {
    path: '/notes/:id/edit',
    name: 'note-edit',
    component: () => import('../views/NoteEdit.vue'),
    meta: { tabbar: false }
  },
  { path: '/search', name: 'search', component: () => import('../views/Search.vue') },
  { path: '/categories', name: 'categories', component: () => import('../views/Categories.vue') },
  { path: '/tags', name: 'tags', component: () => import('../views/Tags.vue') },
  { path: '/settings', name: 'settings', component: () => import('../views/Settings.vue') }
]

const router = createRouter({
  history: createWebHistory(),
  routes,
  scrollBehavior() {
    return { top: 0 }
  }
})

router.beforeEach((to) => {
  const token = localStorage.getItem('token')
  if (!to.meta.public && !token) {
    return { name: 'login', query: { redirect: to.fullPath } }
  }
})

export default router
