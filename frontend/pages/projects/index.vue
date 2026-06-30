<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useApi } from '~/composables/useApi'
import { useProjectStore } from '~/stores/project'
import { useToast } from '~/composables/useToast'
import { useConfirm } from '~/composables/useConfirm'

definePageMeta({
  middleware: ['auth']
})

const { fetchApi } = useApi()
const router = useRouter()
const projectStore = useProjectStore()
const toast = useToast()
const { showConfirm } = useConfirm()

const projects = ref([])
const loading = ref(false)
const showModal = ref(false)
const isEditing = ref(false)

const form = ref({
  id: '',
  name: '',
  description: '',
  sourcePath: '',
  outputPath: '',
  classificationPrompt: '',
  extractionPrompt: ''
})

const loadProjects = async () => {
  loading.value = true
  try {
    const res = await fetchApi<any>('/projects')
    // Giả sử API trả về mảng trực tiếp hoặc res.data
    projects.value = res.data || res
  } catch (err) {
    console.error('Lỗi tải danh sách dự án:', err)
  } finally {
    loading.value = false
  }
}

const openCreateModal = () => {
  isEditing.value = false
  form.value = { id: '', name: '', description: '', sourcePath: '', outputPath: '', classificationPrompt: '', extractionPrompt: '' }
  showModal.value = true
}

const openEditModal = (project: any, e: Event) => {
  e.stopPropagation()
  isEditing.value = true
  form.value = { 
    id: project.id, 
    name: project.name, 
    description: project.description || '', 
    sourcePath: project.sourcePath || '', 
    outputPath: project.outputPath || '',
    classificationPrompt: project.classificationPrompt || '',
    extractionPrompt: project.extractionPrompt || ''
  }
  showModal.value = true
}

const submitForm = () => {
  const btn = document.getElementById('submitProjectBtn')
  if (btn) btn.click()
}

const saveProject = async () => {
  try {
    if (isEditing.value) {
      await fetchApi(`/projects/${form.value.id}`, {
        method: 'PUT',
        body: form.value
      })
    } else {
      await fetchApi('/projects', {
        method: 'POST',
        body: form.value
      })
    }
    showModal.value = false
    loadProjects()
  } catch (err) {
    console.error('Lỗi lưu dự án:', err)
    toast.error('Có lỗi xảy ra khi lưu.')
  }
}

const deleteProject = async (project: any, e: Event) => {
  e.stopPropagation()
  if (await showConfirm(`Bạn có chắc chắn muốn lưu trữ dự án ${project.name}?`)) {
    try {
      await fetchApi(`/projects/${project.id}`, { method: 'DELETE' })
      toast.success('Đã lưu trữ dự án.')
      loadProjects()
    } catch (err) {
      console.error('Lỗi lưu trữ dự án:', err)
      toast.error('Lỗi lưu trữ dự án.')
    }
  }
}

const goToProject = (project: any) => {
  projectStore.setCurrentProject(project)
  router.push(`/projects/${project.id}`)
}

const user = ref<any>(null)

onMounted(async () => {
  try {
    user.value = await fetchApi('/auth/me')
  } catch (err) {}
  loadProjects()
})
</script>

<template>
  <div>
    <div class="sm:flex sm:items-center">
      <div class="sm:flex-auto">
        <h1 class="text-xl font-semibold text-gray-900">Dự án</h1>
        <p class="mt-2 text-sm text-gray-700">Quản lý các dự án số hóa tài liệu.</p>
      </div>
      <div class="mt-4 sm:ml-16 sm:mt-0 sm:flex-none">
        <button v-if="user?.role === 'Manager'" @click="openCreateModal" type="button" class="block rounded-md bg-primary-600 px-3 py-2 text-center text-sm font-semibold text-white shadow-sm hover:bg-primary-500">Tạo dự án mới</button>
      </div>
    </div>

    <!-- Grid cards -->
    <div class="mt-8 grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
      <div v-if="loading" class="col-span-full text-center py-10 text-gray-500">Đang tải...</div>
      <div v-else-if="projects.length === 0" class="col-span-full text-center py-10 text-gray-500">Chưa có dự án nào.</div>
      
      <div v-else v-for="project in projects" :key="project.id" @click="goToProject(project)" class="relative flex flex-col justify-between rounded-lg border border-gray-300 bg-white p-6 shadow-sm hover:border-primary-500 hover:ring-1 hover:ring-primary-500 cursor-pointer transition-all duration-200 hover:shadow-md">
        <div>
          <div class="flex items-center justify-between">
            <h3 class="text-lg font-medium text-gray-900 truncate pr-4">{{ project.name }}</h3>
            <StatusBadge :status="project.status === 0 ? 'Active' : 'Archived'" type="status" />
          </div>
          <p class="mt-2 text-sm text-gray-500 line-clamp-2">{{ project.description || 'Không có mô tả' }}</p>
        </div>
        
        <div class="mt-6 flex items-center justify-between border-t border-gray-200 pt-4">
          <div class="text-sm text-gray-500 flex space-x-4">
            <!-- Thống kê mini -->
            <span title="Tổng số tài liệu">📄 {{ project.totalDocuments || 0 }}</span>
            <span title="Hoàn thành">✅ {{ project.completedDocuments || 0 }}</span>
          </div>
          <div class="flex space-x-3" v-if="user?.role === 'Manager'">
            <button @click="openEditModal(project, $event)" class="text-gray-400 hover:text-primary-600">
              <svg class="h-5 w-5" viewBox="0 0 20 20" fill="currentColor" aria-hidden="true"><path d="M2.695 14.763l-1.262 3.152a.5.5 0 00.65.65l3.152-1.262a4 4 0 001.343-.885L17.5 5.5a2.121 2.121 0 00-3-3L3.58 13.42a4 4 0 00-.885 1.343z" /></svg>
            </button>
            <button @click="deleteProject(project, $event)" class="text-gray-400 hover:text-red-600">
              <svg class="h-5 w-5" viewBox="0 0 20 20" fill="currentColor" aria-hidden="true"><path fill-rule="evenodd" d="M8.75 1A2.75 2.75 0 006 3.75v.443c-.795.077-1.584.176-2.365.298a.75.75 0 10.23 1.482l.149-.022.841 10.518A2.75 2.75 0 007.596 19h4.807a2.75 2.75 0 002.742-2.53l.841-10.52.149.023a.75.75 0 00.23-1.482A41.03 41.03 0 0014 4.193V3.75A2.75 2.75 0 0011.25 1h-2.5zM10 4c.84 0 1.673.025 2.5.075V3.75c0-.69-.56-1.25-1.25-1.25h-2.5c-.69 0-1.25.56-1.25 1.25v.325C8.327 4.025 9.16 4 10 4zM8.58 7.72a.75.75 0 00-1.5.06l.3 7.5a.75.75 0 101.5-.06l-.3-7.5zm4.34.06a.75.75 0 10-1.5-.06l-.3 7.5a.75.75 0 101.5.06l.3-7.5z" clip-rule="evenodd" /></svg>
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- Modal Tạo/Sửa Dự án -->
    <Modal v-model="showModal" :title="isEditing ? 'Sửa dự án' : 'Tạo dự án mới'">
      <form @submit.prevent="saveProject" class="space-y-4">
        <div>
          <label class="block text-sm font-bold text-gray-700 mb-1">Tên dự án <span class="text-red-500">*</span></label>
          <input v-model="form.name" required type="text" placeholder="Nhập tên dự án..." class="block w-full rounded-md border-gray-300 shadow-sm focus:border-primary-500 focus:ring-primary-500 sm:text-sm px-3 py-2" />
        </div>
        <div>
          <label class="block text-sm font-bold text-gray-700 mb-1">Mô tả</label>
          <textarea v-model="form.description" rows="2" placeholder="Nhập mô tả dự án..." class="block w-full rounded-md border-gray-300 shadow-sm focus:border-primary-500 focus:ring-primary-500 sm:text-sm px-3 py-2"></textarea>
        </div>
        <div>
          <label class="block text-sm font-bold text-gray-700 mb-1">Đường dẫn thư mục nguồn</label>
          <div class="relative">
            <input v-model="form.sourcePath" type="text" class="block w-full rounded-md border-gray-300 shadow-sm focus:border-primary-500 focus:ring-primary-500 sm:text-sm px-3 py-2 pr-10" placeholder="C:\Projects\Source..." />
            <div class="absolute inset-y-0 right-0 pr-3 flex items-center pointer-events-none">
              <svg class="h-5 w-5 text-gray-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 7v10a2 2 0 002 2h14a2 2 0 002-2V9a2 2 0 00-2-2h-6l-2-2H5a2 2 0 00-2 2z" />
              </svg>
            </div>
          </div>
        </div>
        <div>
          <label class="block text-sm font-bold text-gray-700 mb-1">Đường dẫn thư mục kết quả</label>
          <div class="relative">
            <input v-model="form.outputPath" type="text" class="block w-full rounded-md border-gray-300 shadow-sm focus:border-primary-500 focus:ring-primary-500 sm:text-sm px-3 py-2 pr-10" placeholder="C:\Projects\Output..." />
            <div class="absolute inset-y-0 right-0 pr-3 flex items-center pointer-events-none">
              <svg class="h-5 w-5 text-gray-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 7v10a2 2 0 002 2h14a2 2 0 002-2V9a2 2 0 00-2-2h-6l-2-2H5a2 2 0 00-2 2z" />
              </svg>
            </div>
          </div>
        </div>

        <hr class="border-gray-200 my-4" />
        <h4 class="text-sm font-bold text-primary-700 uppercase mb-4">Cấu hình AI thông minh</h4>

        <div class="bg-gray-100 border border-gray-200 rounded-lg p-4">
          <label class="block text-sm font-bold text-gray-700 mb-2">System Prompt Phân loại</label>
          <textarea v-model="form.classificationPrompt" rows="3" class="block w-full rounded-md border-gray-300 shadow-sm focus:border-primary-500 focus:ring-primary-500 sm:text-sm px-3 py-2" placeholder="Nhập ngữ cảnh bổ sung cho AI khi phân loại tài liệu..."></textarea>
          <p class="mt-2 text-xs text-gray-500 italic">Quy định cách AI nhận diện các loại văn bản: Hợp đồng, hóa đơn, CV...</p>
        </div>
        <div class="bg-gray-100 border border-gray-200 rounded-lg p-4">
          <label class="block text-sm font-bold text-gray-700 mb-2">System Prompt Bóc tách</label>
          <textarea v-model="form.extractionPrompt" rows="3" class="block w-full rounded-md border-gray-300 shadow-sm focus:border-primary-500 focus:ring-primary-500 sm:text-sm px-3 py-2" placeholder="Nhập ngữ cảnh bổ sung cho AI khi bóc tách dữ liệu..."></textarea>
          <p class="mt-2 text-xs text-gray-500 italic">Quy định các trường thông tin cần lấy và định dạng dữ liệu đầu ra.</p>
        </div>
        
        <div class="hidden">
          <button type="submit" id="submitProjectBtn">Submit</button>
        </div>
      </form>

      <template #footer>
        <button type="button" @click="showModal = false" class="inline-flex w-full justify-center items-center rounded-md bg-white px-4 py-2 text-sm font-medium text-gray-700 shadow-sm border border-gray-300 hover:bg-gray-50 sm:w-auto">
          Hủy
        </button>
        <button type="button" @click="submitForm" class="mt-3 sm:mt-0 inline-flex w-full justify-center items-center rounded-md bg-primary-700 px-4 py-2 text-sm font-medium text-white shadow-sm hover:bg-primary-600 sm:ml-3 sm:w-auto">
          <svg class="-ml-1 mr-2 h-4 w-4 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 7H5a2 2 0 00-2 2v9a2 2 0 002 2h14a2 2 0 002-2V9a2 2 0 00-2-2h-3m-1 4l-3 3m0 0l-3-3m3 3V4" />
          </svg>
          Lưu
        </button>
      </template>
    </Modal>
  </div>
</template>
