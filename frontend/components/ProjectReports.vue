<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useApi } from '~/composables/useApi'
import { useToast } from '~/composables/useToast'

const props = defineProps({
  projectId: { type: String, required: true }
})

const { fetchApi, downloadFile } = useApi()
const toast = useToast()
const stats = ref<any>(null)
const loading = ref(true)

const loadReports = async () => {
  loading.value = true
  try {
    stats.value = await fetchApi(`/projects/${props.projectId}/reports/summary`)
  } catch (err) {
    console.error('Lỗi tải báo cáo dự án:', err)
  } finally {
    loading.value = false
  }
}

const exportExcel = async () => {
  try {
    toast.info('Đang xử lý xuất báo cáo...')
    await downloadFile(`/projects/${props.projectId}/reports/export`, `ProjectReport_${props.projectId}.xlsx`)
    toast.success('Xuất báo cáo thành công.')
  } catch (err) {
    console.error('Lỗi xuất báo cáo:', err)
    toast.error('Có lỗi xảy ra khi xuất báo cáo.')
  }
}

onMounted(loadReports)
</script>

<template>
  <div class="space-y-6">
    <div class="flex justify-between items-center">
      <h3 class="text-lg leading-6 font-medium text-gray-900">Thống kê Dự án</h3>
      <button @click="exportExcel" class="inline-flex items-center px-4 py-2 border border-transparent shadow-sm text-sm font-medium rounded-md text-white bg-green-600 hover:bg-green-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-green-500">
        <svg class="h-5 w-5 mr-2" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 10v6m0 0l-3-3m3 3l3-3m2 8H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" /></svg>
        Tải xuống Excel
      </button>
    </div>

    <div v-if="loading" class="text-center py-10 text-gray-500">Đang tải báo cáo...</div>

    <div v-else-if="stats">
      <div class="grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-4 mb-8">
        <div class="bg-white overflow-hidden shadow rounded-lg border border-gray-200">
          <div class="px-4 py-5 sm:p-6 text-center">
            <dt class="text-sm font-medium text-gray-500 truncate">Tổng số Tài liệu</dt>
            <dd class="mt-1 text-3xl font-semibold text-gray-900">{{ stats.totalDocuments }}</dd>
          </div>
        </div>
        <div class="bg-white overflow-hidden shadow rounded-lg border border-gray-200">
          <div class="px-4 py-5 sm:p-6 text-center">
            <dt class="text-sm font-medium text-gray-500 truncate">Độ tin cậy TB (Phân loại)</dt>
            <dd class="mt-1 text-3xl font-semibold text-primary-600">{{ stats.averageConfidence ? Math.round(stats.averageConfidence * 100) + '%' : 'N/A' }}</dd>
          </div>
        </div>
      </div>

      <div class="grid grid-cols-1 lg:grid-cols-2 gap-8">
        <div class="bg-white shadow rounded-lg p-6 border border-gray-200">
          <h4 class="text-base font-medium text-gray-900 mb-4">Tài liệu theo Trạng thái</h4>
          <ul class="divide-y divide-gray-200">
            <li v-for="item in stats.documentsByStatus" :key="item.status" class="py-3 flex justify-between">
              <span class="text-sm text-gray-600">{{ item.status }}</span>
              <span class="text-sm font-medium text-gray-900">{{ item.count }}</span>
            </li>
            <li v-if="!stats.documentsByStatus || stats.documentsByStatus.length === 0" class="py-3 text-sm text-gray-500">Chưa có dữ liệu</li>
          </ul>
        </div>

        <div class="bg-white shadow rounded-lg p-6 border border-gray-200">
          <h4 class="text-base font-medium text-gray-900 mb-4">Tài liệu theo Nhãn (Label)</h4>
          <ul class="divide-y divide-gray-200">
            <li v-for="item in stats.documentsByLabel" :key="item.label" class="py-3 flex justify-between">
              <span class="text-sm text-gray-600">{{ item.label }}</span>
              <span class="text-sm font-medium text-gray-900">{{ item.count }}</span>
            </li>
            <li v-if="!stats.documentsByLabel || stats.documentsByLabel.length === 0" class="py-3 text-sm text-gray-500">Chưa có dữ liệu phân loại</li>
          </ul>
        </div>
      </div>
    </div>
  </div>
</template>
