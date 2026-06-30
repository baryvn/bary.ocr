<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useCookie } from '#app'
import { useApi } from '~/composables/useApi'

definePageMeta({
  middleware: ['auth']
})

const { fetchApi } = useApi()
const router = useRouter()
const token = useCookie('auth_token')

const stats = ref<any>(null)
const loading = ref(true)
const user = ref<any>(null)

const loadDashboard = async () => {
  try {
    stats.value = await fetchApi('/dashboard/stats')
  } catch (err) {
    console.error('Lỗi tải thống kê:', err)
  } finally {
    loading.value = false
  }
}

const loadProfile = async () => {
  try {
    user.value = await fetchApi('/auth/me')
    if (user.value.role === 'Admin') {
      router.replace('/users')
    } else if (user.value.role === 'Manager') {
      loadDashboard()
    } else {
      router.replace('/review')
    }
  } catch (e) {
    token.value = null
    router.push('/login')
  }
}

onMounted(() => {
  loadProfile()
})
</script>

<template>
  <div>
    <div class="flex justify-between items-center mb-6">
      <h1 class="text-2xl font-semibold text-gray-900">Dashboard Quản lý</h1>
    </div>
    
    <div v-if="loading" class="text-center py-20 text-gray-500">Đang tải số liệu...</div>
    
    <div v-else-if="stats">
      <!-- Top Cards -->
      <div class="grid grid-cols-1 gap-5 sm:grid-cols-3 mb-8">
        <div class="bg-white overflow-hidden shadow rounded-lg">
          <div class="px-4 py-5 sm:p-6">
            <dt class="text-sm font-medium text-gray-500 truncate">Tổng số Dự án</dt>
            <dd class="mt-1 text-3xl font-semibold text-gray-900">{{ stats.totalProjects }}</dd>
          </div>
        </div>
        <div class="bg-white overflow-hidden shadow rounded-lg">
          <div class="px-4 py-5 sm:p-6">
            <dt class="text-sm font-medium text-gray-500 truncate">Tổng số Lô tài liệu</dt>
            <dd class="mt-1 text-3xl font-semibold text-gray-900">{{ stats.totalBatches }}</dd>
          </div>
        </div>
        <div class="bg-white overflow-hidden shadow rounded-lg">
          <div class="px-4 py-5 sm:p-6">
            <dt class="text-sm font-medium text-gray-500 truncate">Tổng số Tài liệu</dt>
            <dd class="mt-1 text-3xl font-semibold text-gray-900">{{ stats.totalDocuments }}</dd>
          </div>
        </div>
      </div>

      <div class="grid grid-cols-1 lg:grid-cols-2 gap-8">
        <!-- Trạng thái tài liệu -->
        <div class="bg-white shadow rounded-lg p-6">
          <h3 class="text-lg leading-6 font-medium text-gray-900 mb-4">Trạng thái Tài liệu</h3>
          <ul class="divide-y divide-gray-200">
            <li v-for="item in stats.documentsByStatus" :key="item.status" class="py-3 flex justify-between">
              <span class="text-sm font-medium text-gray-600">{{ item.status }}</span>
              <span class="text-sm font-semibold text-gray-900">{{ item.count }}</span>
            </li>
            <li v-if="!stats.documentsByStatus || stats.documentsByStatus.length === 0" class="py-3 text-sm text-gray-500">Chưa có dữ liệu</li>
          </ul>
        </div>

        <!-- Hoạt động gần đây -->
        <div class="bg-white shadow rounded-lg p-6">
          <h3 class="text-lg leading-6 font-medium text-gray-900 mb-4">Hoạt động gần đây (Audit Logs)</h3>
          <div class="flow-root">
            <ul class="-mb-8">
              <li v-for="(act, idx) in stats.recentActivities" :key="idx">
                <div class="relative pb-8">
                  <span v-if="idx !== stats.recentActivities.length - 1" class="absolute top-4 left-4 -ml-px h-full w-0.5 bg-gray-200" aria-hidden="true"></span>
                  <div class="relative flex space-x-3">
                    <div>
                      <span class="h-8 w-8 rounded-full bg-primary-100 flex items-center justify-center ring-8 ring-white">
                        <svg class="h-4 w-4 text-primary-600" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>
                      </span>
                    </div>
                    <div class="min-w-0 flex-1 pt-1.5 flex justify-between space-x-4">
                      <div>
                        <p class="text-sm text-gray-500">
                          <span class="font-medium text-gray-900">{{ act.user }}</span> {{ act.action }} <span class="font-medium text-gray-900">{{ act.entityType }}</span>
                        </p>
                      </div>
                      <div class="text-right text-sm whitespace-nowrap text-gray-500">
                        <time :datetime="act.createdAt">{{ new Date(act.createdAt).toLocaleString('vi-VN') }}</time>
                      </div>
                    </div>
                  </div>
                </div>
              </li>
            </ul>
          </div>
        </div>
      </div>
      
    </div>
  </div>
</template>
