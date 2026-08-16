<template>
  <router-view v-slot="{ Component }">
    <keep-alive include="NotesList">
      <component :is="Component" />
    </keep-alive>
  </router-view>

  <van-tabbar v-if="showTabbar" route safe-area-inset-bottom>
    <van-tabbar-item to="/notes" icon="notes-o">笔记</van-tabbar-item>
    <van-tabbar-item to="/search" icon="search">搜索</van-tabbar-item>
    <van-tabbar-item to="/categories" icon="apps-o">分类</van-tabbar-item>
    <van-tabbar-item to="/settings" icon="contact">我的</van-tabbar-item>
  </van-tabbar>
</template>

<script setup>
import { computed } from 'vue'
import { useRoute } from 'vue-router'

const route = useRoute()
const showTabbar = computed(() => route.meta.tabbar !== false)
</script>
