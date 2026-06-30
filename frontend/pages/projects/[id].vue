<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useApi } from '~/composables/useApi'
import { useProjectStore } from '~/stores/project'
import { useToast } from '~/composables/useToast'
import { useConfirm } from '~/composables/useConfirm'

definePageMeta({
  middleware: ['auth']
})

const route = useRoute()
const router = useRouter()
const { fetchApi, downloadFile } = useApi()
const projectStore = useProjectStore()
const toast = useToast()
const { showConfirm } = useConfirm()

const projectId = route.params.id as string
const project = ref<any>(null)
const loading = ref(true)

const tabs = [
  { id: 'overview', name: 'Tổng quan' },
  { id: 'labels', name: 'Nhãn (Labels)' },
  { id: 'metadata', name: 'Trường thông tin' },
  { id: 'naming', name: 'Quy tắc Đặt tên' },
  { id: 'batches', name: 'Lô tài liệu' },
  { id: 'reports', name: 'Báo cáo' }
]
const activeTab = ref('overview')

const loadProject = async () => {
  loading.value = true
  try {
    project.value = await fetchApi(`/projects/${projectId}`)
    projectStore.setCurrentProject(project.value)
  } catch (err) {
    console.error('Lỗi tải chi tiết dự án:', err)
  } finally {
    loading.value = false
  }
}

const exportExcel = async () => {
  try {
    toast.info('Đang xử lý xuất báo cáo...')
    await downloadFile(`/projects/${projectId}/reports/export`, `ProjectReport_${projectId}.xlsx`)
    toast.success('Xuất báo cáo thành công.')
  } catch (err) {
    console.error('Lỗi xuất báo cáo:', err)
    toast.error('Có lỗi xảy ra khi xuất báo cáo.')
  }
}

const deleteProject = async () => {
  if (await showConfirm('Bạn có chắc chắn muốn xóa dự án này? Toàn bộ dữ liệu sẽ bị mất.')) {
    try {
      await fetchApi(`/projects/${projectId}`, { method: 'DELETE' })
      toast.success('Đã xóa dự án thành công.')
      router.push('/projects')
    } catch (err: any) {
      toast.error(err.data?.message || 'Lỗi khi xóa dự án.')
    }
  }
}

onMounted(() => {
  if (projectStore.currentProject && projectStore.currentProject.id === projectId) {
    project.value = projectStore.currentProject
    loading.value = false
  } else {
    loadProject()
  }
})
</script>

<template>
  <div v-if="loading" class="py-10 text-center">Đang tải...</div>
  <div v-else-if="!project" class="py-10 text-center text-red-500">Không tìm thấy dự án</div>
  <div v-else>
    <div class="mb-6 flex items-center justify-between">
      <div class="flex items-center gap-4">
        <button @click="router.push('/projects')" class="text-gray-500 hover:text-gray-900">
          <svg class="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" d="M10.5 19.5L3 12m0 0l7.5-7.5M3 12h18" /></svg>
        </button>
        <h1 class="text-2xl font-semibold text-gray-900">{{ project.name }}</h1>
        <StatusBadge :status="project.status === 0 ? 'Active' : 'Archived'" type="status" />
      </div>
      <div class="flex items-center space-x-3">
        <button @click="exportExcel" class="inline-flex items-center px-4 py-2 border border-gray-300 shadow-sm text-sm font-medium rounded-md text-gray-700 bg-white hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-primary-500">
          <svg class="h-5 w-5 mr-2 text-green-600" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 10v6m0 0l-3-3m3 3l3-3m2 8H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" /></svg>
          Xuất Báo cáo Excel
        </button>
        <button @click="deleteProject" class="inline-flex items-center px-4 py-2 border border-transparent shadow-sm text-sm font-medium rounded-md text-white bg-red-600 hover:bg-red-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-red-500">
          <svg class="h-5 w-5 mr-2" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" /></svg>
          Xóa
        </button>
      </div>
    </div>

    <!-- Tabs -->
    <div class="border-b border-gray-200">
      <nav class="-mb-px flex space-x-8" aria-label="Tabs">
        <button 
          v-for="tab in tabs" :key="tab.id"
          @click="activeTab = tab.id"
          :class="[activeTab === tab.id ? 'border-primary-500 text-primary-600' : 'border-transparent text-gray-500 hover:border-gray-300 hover:text-gray-700', 'whitespace-nowrap border-b-2 py-4 px-1 text-sm font-medium']"
        >
          {{ tab.name }}
        </button>
      </nav>
    </div>

    <!-- Tab Content -->
    <div class="mt-6">
      <div v-if="activeTab === 'overview'" class="bg-white shadow overflow-hidden sm:rounded-lg">
        <div class="px-4 py-5 sm:px-6">
          <h3 class="text-lg leading-6 font-medium text-gray-900">Thông tin dự án</h3>
        </div>
        <div class="border-t border-gray-200 px-4 py-5 sm:p-0">
          <dl class="sm:divide-y sm:divide-gray-200">
            <div class="py-4 sm:grid sm:grid-cols-3 sm:gap-4 sm:py-5 sm:px-6">
              <dt class="text-sm font-medium text-gray-500">Mô tả</dt>
              <dd class="mt-1 text-sm text-gray-900 sm:col-span-2 sm:mt-0">{{ project.description || 'N/A' }}</dd>
            </div>
            <div class="py-4 sm:grid sm:grid-cols-3 sm:gap-4 sm:py-5 sm:px-6">
              <dt class="text-sm font-medium text-gray-500">Thư mục nguồn (Source)</dt>
              <dd class="mt-1 text-sm text-gray-900 sm:col-span-2 sm:mt-0">{{ project.sourcePath || 'N/A' }}</dd>
            </div>
            <div class="py-4 sm:grid sm:grid-cols-3 sm:gap-4 sm:py-5 sm:px-6">
              <dt class="text-sm font-medium text-gray-500">Thư mục kết quả (Output)</dt>
              <dd class="mt-1 text-sm text-gray-900 sm:col-span-2 sm:mt-0">{{ project.outputPath || 'N/A' }}</dd>
            </div>
            <div class="py-4 sm:grid sm:grid-cols-3 sm:gap-4 sm:py-5 sm:px-6">
              <dt class="text-sm font-medium text-gray-500">System Prompt Phân loại</dt>
              <dd class="mt-1 text-sm text-gray-900 sm:col-span-2 sm:mt-0 whitespace-pre-wrap">{{ project.classificationPrompt || 'Mặc định' }}</dd>
            </div>
            <div class="py-4 sm:grid sm:grid-cols-3 sm:gap-4 sm:py-5 sm:px-6">
              <dt class="text-sm font-medium text-gray-500">System Prompt Bóc tách</dt>
              <dd class="mt-1 text-sm text-gray-900 sm:col-span-2 sm:mt-0 whitespace-pre-wrap">{{ project.extractionPrompt || 'Mặc định' }}</dd>
            </div>
          </dl>
        </div>
      </div>

      <div v-else-if="activeTab === 'labels'">
        <ProjectLabels :projectId="projectId" />
      </div>

      <div v-else-if="activeTab === 'metadata'">
        <ProjectMetadata :projectId="projectId" />
      </div>

      <div v-else-if="activeTab === 'naming'">
        <ProjectNaming :projectId="projectId" />
      </div>

      <div v-else-if="activeTab === 'batches'">
        <ProjectBatches :projectId="projectId" />
      </div>

      <div v-else-if="activeTab === 'reports'">
        <ProjectReports :projectId="projectId" />
      </div>
    </div>
  </div>
</template>
