<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useApi } from '~/composables/useApi'

const { fetchApi } = useApi()
const assignments = ref<any[]>([])
const loading = ref(true)

const loadData = async () => {
  try {
    const res = await fetchApi<any[]>('/review/my-assignments')
    assignments.value = res
  } catch (err) {
    console.error('Lỗi tải danh sách phân công:', err)
  } finally {
    loading.value = false
  }
}

const getStatusColor = (status: number) => {
  const map: Record<number, string> = {
    0: 'bg-yellow-100 text-yellow-800', // Assigned
    1: 'bg-blue-100 text-blue-800',    // InProgress
    2: 'bg-green-100 text-green-800'   // Completed
  }
  return map[status] || 'bg-gray-100 text-gray-800'
}

const getStatusText = (status: number) => {
  const map: Record<number, string> = {
    0: 'Mới giao',
    1: 'Đang xử lý',
    2: 'Hoàn thành'
  }
  return map[status] || 'Không xác định'
}

onMounted(loadData)
</script>

<template>
  <div>
    <h1 class="text-2xl font-bold text-gray-900 mb-6">Nhiệm vụ Review</h1>
    
    <div v-if="loading" class="text-center py-20 text-gray-500">Đang tải dữ liệu...</div>
    
    <div v-else-if="assignments.length === 0" class="bg-white rounded-lg shadow-sm border border-gray-200 p-10 text-center text-gray-500">
      Hiện tại bạn chưa được phân công lô tài liệu nào.
    </div>
    
    <div v-else class="bg-white rounded-lg shadow-sm border border-gray-200 overflow-hidden">
      <ul class="divide-y divide-gray-200">
        <li v-for="item in assignments" :key="item.id">
          <NuxtLink :to="`/batches/${item.batchId}/review`" class="block hover:bg-gray-50 transition-colors duration-150">
            <div class="px-4 py-4 sm:px-6 flex items-center justify-between">
              <div class="flex flex-col">
                <p class="text-sm font-medium text-primary-600 truncate">Lô: {{ item.batchName }}</p>
                <p class="text-sm text-gray-500 mt-1">Dự án: {{ item.projectName }}</p>
              </div>
              <div class="flex items-center space-x-4">
                <div class="text-sm text-gray-500 flex flex-col text-right">
                  <span>Tiến độ: {{ item.reviewedDocuments }} / {{ item.totalDocuments }}</span>
                  <span class="text-xs text-gray-400 mt-1">{{ new Date(item.createdAt).toLocaleDateString('vi-VN') }}</span>
                </div>
                <span :class="['inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium', getStatusColor(item.status)]">
                  {{ getStatusText(item.status) }}
                </span>
                <svg class="h-5 w-5 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7" />
                </svg>
              </div>
            </div>
          </NuxtLink>
        </li>
      </ul>
    </div>
  </div>
</template>
