<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import { useApi } from '~/composables/useApi'
import { useProjectStore } from '~/stores/project'
import { useToast } from '~/composables/useToast'
import { useConfirm } from '~/composables/useConfirm'

const props = defineProps({
  projectId: { type: String, required: true }
})

const router = useRouter()
const { fetchApi } = useApi()
const projectStore = useProjectStore()
const toast = useToast()
const { showConfirm } = useConfirm()

const batches = ref<any[]>([])
const loading = ref(false)
const showCreateModal = ref(false)
const isEditing = ref(false)
const authUser = ref<any>(null)

const form = ref({
  id: '',
  name: '',
  mode: 'upload' as 'upload' | 'folder',
  sourceFolder: '',
  uploadedFileKeys: [] as string[]
})

let pollingInterval: any = null

const loadBatches = async () => {
  if (loading.value && batches.value.length === 0) return
  loading.value = true
  try {
    const res = await fetchApi<any>(`/projects/${props.projectId}/batches?page=1&pageSize=50`)
    batches.value = res.data || res.items || []
  } catch (err) {
    console.error('Lỗi tải danh sách lô:', err)
  } finally {
    loading.value = false
  }
}

const openCreateModal = () => {
  isEditing.value = false
  form.value = {
    id: '',
    name: `Lô_${new Date().toISOString().slice(0, 10).replace(/-/g, '')}_001`,
    mode: 'upload',
    sourceFolder: '',
    uploadedFileKeys: []
  }
  showCreateModal.value = true
}

const openEditModal = (batch: any, e: Event) => {
  e.stopPropagation()
  isEditing.value = true
  form.value = {
    id: batch.id,
    name: batch.name,
    mode: 'folder',
    sourceFolder: batch.sourceFolder || '',
    uploadedFileKeys: []
  }
  showCreateModal.value = true
}

const handleUploadSuccess = (data: any) => {
  form.value.uploadedFileKeys.push(data.objectKey)
}

const submitForm = () => {
  const btn = document.getElementById('submitBatchBtn')
  if (btn) btn.click()
}

const createBatch = async () => {
  if (!isEditing.value) {
    if (form.value.mode === 'upload' && form.value.uploadedFileKeys.length === 0) {
      toast.warning('Vui lòng chọn hoặc kéo thả ít nhất 1 file PDF.')
      return
    }
    if (form.value.mode === 'folder' && !form.value.sourceFolder) {
      toast.warning('Vui lòng nhập đường dẫn thư mục nguồn.')
      return
    }
  }

  try {
    if (isEditing.value) {
      await fetchApi(`/batches/${form.value.id}`, {
        method: 'PUT',
        body: {
          name: form.value.name,
          sourceFolder: form.value.mode === 'folder' ? form.value.sourceFolder : null
        }
      })
    } else {
      await fetchApi(`/projects/${props.projectId}/batches`, {
        method: 'POST',
        body: {
          name: form.value.name,
          sourceFolder: form.value.mode === 'folder' ? form.value.sourceFolder : null,
          uploadedFileKeys: form.value.mode === 'upload' ? form.value.uploadedFileKeys : []
        }
      })
    }
    showCreateModal.value = false
    loadBatches()
  } catch (err: any) {
    toast.error(err.data?.message || 'Có lỗi khi lưu lô.')
  }
}

const deleteBatch = async (batch: any, e: Event) => {
  e.stopPropagation()
  if (await showConfirm(`Bạn có chắc chắn muốn xóa lô "${batch.name}"? TẤT CẢ TÀI LIỆU bên trong sẽ bị xóa và không thể khôi phục!`)) {
    try {
      await fetchApi(`/batches/${batch.id}`, { method: 'DELETE' })
      loadBatches()
    } catch (err: any) {
      toast.error(err.data?.message || 'Lỗi khi xóa lô.')
    }
  }
}

const goToBatch = (batchId: string) => {
  router.push(`/batches/${batchId}`)
}

const showAssignModal = ref(false)
const assignBatchId = ref<string>('')
const reviewers = ref<any[]>([])
const selectedReviewerId = ref<string>('')

const openAssignModal = async (batch: any, e: Event) => {
  e.stopPropagation()
  assignBatchId.value = batch.id
  selectedReviewerId.value = ''
  
  try {
    reviewers.value = await fetchApi('/users/reviewers')
    showAssignModal.value = true
  } catch (err: any) {
    toast.error(err.data?.message || 'Không thể tải danh sách Reviewer')
  }
}

const assignBatch = async () => {
  if (!selectedReviewerId.value) {
    toast.warning('Vui lòng chọn Reviewer')
    return
  }
  
  try {
    await fetchApi(`/batches/${assignBatchId.value}/assign?reviewerId=${selectedReviewerId.value}`, {
      method: 'POST'
    })
    toast.success('Phân công thành công')
    showAssignModal.value = false
    loadBatches()
  } catch (err: any) {
    toast.error(err.data?.message || 'Có lỗi khi phân công.')
  }
}

const getStatusColor = (status: string) => {
  const map: Record<string, string> = {
    'Created': 'bg-gray-100 text-gray-800',
    'Importing': 'bg-blue-100 text-blue-800',
    'Processing': 'bg-yellow-100 text-yellow-800',
    'Completed': 'bg-green-100 text-green-800'
  }
  return map[status] || 'bg-gray-100 text-gray-800'
}

onMounted(async () => {
  try {
    authUser.value = await fetchApi('/auth/me')
  } catch (err) {
    // ignore
  }
  loadBatches()
  // Tự động làm mới danh sách mỗi 5 giây
  pollingInterval = setInterval(() => {
    loadBatches()
  }, 5000)
})

onUnmounted(() => {
  if (pollingInterval) clearInterval(pollingInterval)
})
</script>

<template>
  <div class="bg-white rounded-lg shadow-sm border border-gray-200">
    <div class="p-6">
      <div class="sm:flex sm:items-center sm:justify-between mb-6 border-b border-gray-200 pb-4">
        <div class="sm:flex-auto">
          <h2 class="text-xl font-semibold text-gray-900">Danh sách Lô tài liệu</h2>
          <p class="mt-2 text-sm text-gray-700">Quản lý các lô tài liệu đã import vào hệ thống.</p>
        </div>
        <div class="mt-4 sm:mt-0 flex items-center" v-if="authUser?.role === 'Manager'">
          <button @click="openCreateModal" class="inline-flex items-center justify-center rounded-md border border-transparent bg-primary-600 px-4 py-2 text-sm font-medium text-white shadow-sm hover:bg-primary-700">
            <svg class="-ml-1 mr-2 h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 6v6m0 0v6m0-6h6m-6 0H6" /></svg>
            Tạo Lô mới
          </button>
        </div>
      </div>

      <!-- Bảng danh sách lô -->
      <div v-if="loading && batches.length === 0" class="text-center py-10 text-gray-500">Đang tải danh sách...</div>
      <div v-else-if="batches.length === 0" class="text-center py-10 text-gray-500 border border-dashed rounded-lg">
        Chưa có lô tài liệu nào. Bấm "Tạo Lô mới" để bắt đầu.
      </div>
      <div v-else class="mt-4 flex flex-col">
        <div class="-my-2 -mx-4 overflow-x-auto sm:-mx-6 lg:-mx-8">
          <div class="inline-block min-w-full py-2 align-middle md:px-6 lg:px-8">
            <div class="overflow-hidden shadow ring-1 ring-black ring-opacity-5 md:rounded-lg">
              <table class="min-w-full divide-y divide-gray-300">
                <thead class="bg-gray-50">
                  <tr>
                    <th scope="col" class="py-3.5 pl-4 pr-3 text-left text-sm font-semibold text-gray-900 sm:pl-6">Tên lô</th>
                    <th scope="col" class="px-3 py-3.5 text-left text-sm font-semibold text-gray-900">Nguồn</th>
                    <th scope="col" class="px-3 py-3.5 text-left text-sm font-semibold text-gray-900">Tiến độ</th>
                    <th scope="col" class="px-3 py-3.5 text-left text-sm font-semibold text-gray-900">Trạng thái</th>
                    <th scope="col" class="px-3 py-3.5 text-left text-sm font-semibold text-gray-900">Reviewer</th>
                    <th scope="col" class="px-3 py-3.5 text-left text-sm font-semibold text-gray-900">Ngày tạo</th>
                    <th scope="col" class="relative py-3.5 pl-3 pr-4 sm:pr-6"><span class="sr-only">Actions</span></th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-gray-200 bg-white">
                  <tr v-for="batch in batches" :key="batch.id" class="hover:bg-gray-50">
                    <td class="whitespace-nowrap py-4 pl-4 pr-3 text-sm font-medium text-gray-900 sm:pl-6 cursor-pointer" @click="goToBatch(batch.id)">
                      <div class="text-primary-600 hover:text-primary-900">{{ batch.name }}</div>
                    </td>
                    <td class="whitespace-nowrap px-3 py-4 text-sm text-gray-500">
                      {{ batch.sourceFolder ? batch.sourceFolder : 'Upload thủ công' }}
                    </td>
                    <td class="whitespace-nowrap px-3 py-4 text-sm text-gray-500">
                      <div class="flex items-center space-x-2">
                        <div class="w-full bg-gray-200 rounded-full h-1.5 w-24">
                          <div class="bg-primary-600 h-1.5 rounded-full" :style="{ width: (batch.totalFiles > 0 ? (batch.processedFiles / batch.totalFiles) * 100 : 0) + '%' }"></div>
                        </div>
                        <span class="text-xs">{{ batch.processedFiles }}/{{ batch.totalFiles }}</span>
                      </div>
                    </td>
                    <td class="whitespace-nowrap px-3 py-4 text-sm">
                      <span :class="['inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium', getStatusColor(batch.status)]">
                        {{ batch.status }}
                      </span>
                    </td>
                    <td class="whitespace-nowrap px-3 py-4 text-sm text-gray-500">
                      {{ batch.reviewerName || 'Chưa phân công' }}
                    </td>
                    <td class="whitespace-nowrap px-3 py-4 text-sm text-gray-500">
                      {{ new Date(batch.createdAt).toLocaleString('vi-VN') }}
                    </td>
                    <td class="relative whitespace-nowrap py-4 pl-3 pr-4 text-right text-sm font-medium sm:pr-6">
                      <div class="flex justify-end space-x-3">
                        <button @click="goToBatch(batch.id)" class="text-primary-600 hover:text-primary-900">Chi tiết</button>
                        <button v-if="authUser?.role === 'Manager'" @click="openAssignModal(batch, $event)" class="text-gray-400 hover:text-green-600" title="Phân công">
                          <svg class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M18 9v3m0 0v3m0-3h3m-3 0h-3m-2-5a4 4 0 11-8 0 4 4 0 018 0zM3 20a6 6 0 0112 0v1H3v-1z" /></svg>
                        </button>
                        <button v-if="authUser?.role === 'Manager'" @click="openEditModal(batch, $event)" class="text-gray-400 hover:text-primary-600" title="Sửa Lô">
                          <svg class="h-5 w-5" viewBox="0 0 20 20" fill="currentColor" aria-hidden="true"><path d="M2.695 14.763l-1.262 3.152a.5.5 0 00.65.65l3.152-1.262a4 4 0 001.343-.885L17.5 5.5a2.121 2.121 0 00-3-3L3.58 13.42a4 4 0 00-.885 1.343z" /></svg>
                        </button>
                        <button v-if="authUser?.role === 'Manager'" @click="deleteBatch(batch, $event)" class="text-gray-400 hover:text-red-600" title="Xóa Lô">
                          <svg class="h-5 w-5" viewBox="0 0 20 20" fill="currentColor" aria-hidden="true"><path fill-rule="evenodd" d="M8.75 1A2.75 2.75 0 006 3.75v.443c-.795.077-1.584.176-2.365.298a.75.75 0 10.23 1.482l.149-.022.841 10.518A2.75 2.75 0 007.596 19h4.807a2.75 2.75 0 002.742-2.53l.841-10.52.149.023a.75.75 0 00.23-1.482A41.03 41.03 0 0014 4.193V3.75A2.75 2.75 0 0011.25 1h-2.5zM10 4c.84 0 1.673.025 2.5.075V3.75c0-.69-.56-1.25-1.25-1.25h-2.5c-.69 0-1.25.56-1.25 1.25v.325C8.327 4.025 9.16 4 10 4zM8.58 7.72a.75.75 0 00-1.5.06l.3 7.5a.75.75 0 101.5-.06l-.3-7.5zm4.34.06a.75.75 0 10-1.5-.06l-.3 7.5a.75.75 0 101.5.06l.3-7.5z" clip-rule="evenodd" /></svg>
                        </button>
                      </div>
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Modal Tạo/Sửa Lô -->
    <Modal v-model="showCreateModal" :title="isEditing ? 'Sửa Lô tài liệu' : 'Tạo Lô tài liệu mới'">
      <form @submit.prevent="createBatch" class="space-y-4">
        <div>
          <label class="block text-sm font-bold text-gray-700 mb-1">Tên lô <span class="text-red-500">*</span></label>
          <input v-model="form.name" required type="text" class="block w-full rounded-md border-gray-300 shadow-sm focus:border-primary-500 focus:ring-primary-500 sm:text-sm px-3 py-2" />
        </div>

        <div>
          <label class="block text-sm font-bold text-gray-700 mb-2">Chế độ Import</label>
          <div class="flex items-center space-x-4">
            <label class="flex items-center">
              <input type="radio" v-model="form.mode" value="upload" class="h-4 w-4 text-primary-600 focus:ring-primary-500 border-gray-300" />
              <span class="ml-2 text-sm text-gray-700">Upload từ máy tính</span>
            </label>
            <label class="flex items-center">
              <input type="radio" v-model="form.mode" value="folder" class="h-4 w-4 text-primary-600 focus:ring-primary-500 border-gray-300" />
              <span class="ml-2 text-sm text-gray-700">Quét thư mục máy chủ</span>
            </label>
          </div>
        </div>

        <div v-if="form.mode === 'upload'" class="mt-4 p-4 border border-gray-200 rounded-lg bg-gray-50">
          <FileUploader @uploadSuccess="handleUploadSuccess" :prefix="projectStore.currentProject?.sourcePath || ''" />
          <p class="mt-2 text-sm text-gray-600">Đã upload: {{ form.uploadedFileKeys.length }} file</p>
        </div>

        <div v-if="form.mode === 'folder'" class="mt-4">
          <label class="block text-sm font-bold text-gray-700 mb-1">Đường dẫn thư mục (Tuyệt đối) <span class="text-red-500">*</span></label>
          <input v-model="form.sourceFolder" type="text" class="block w-full rounded-md border-gray-300 shadow-sm focus:border-primary-500 focus:ring-primary-500 sm:text-sm px-3 py-2" placeholder="/data/in/PhongNS/HoSo" />
          <p class="mt-2 text-xs text-gray-500 italic">Hệ thống Worker (Python) sẽ quét thư mục này để import file tự động.</p>
        </div>

        <div class="hidden">
          <button type="submit" id="submitBatchBtn">Submit</button>
        </div>
      </form>
      <template #footer>
        <button type="button" @click="showCreateModal = false" class="inline-flex w-full justify-center items-center rounded-md bg-white px-4 py-2 text-sm font-medium text-gray-700 shadow-sm border border-gray-300 hover:bg-gray-50 sm:w-auto">
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

    <!-- Modal Phân công Reviewer -->
    <Modal v-model="showAssignModal" title="Phân công Reviewer">
      <div class="space-y-4">
        <p class="text-sm text-gray-500">Chọn một người dùng có vai trò Reviewer để phân công lô tài liệu này.</p>
        <div>
          <label class="block text-sm font-medium text-gray-700">Reviewer</label>
          <select v-model="selectedReviewerId" class="mt-1 block w-full pl-3 pr-10 py-2 text-base border-gray-300 focus:outline-none focus:ring-primary-500 focus:border-primary-500 sm:text-sm rounded-md">
            <option value="" disabled>-- Chọn Reviewer --</option>
            <option v-for="user in reviewers" :key="user.id" :value="user.id">
              {{ user.username }} ({{ user.fullName }})
            </option>
          </select>
        </div>
      </div>
      <template #footer>
        <button type="button" @click="showAssignModal = false" class="inline-flex w-full justify-center items-center rounded-md bg-white px-4 py-2 text-sm font-medium text-gray-700 shadow-sm border border-gray-300 hover:bg-gray-50 sm:w-auto">
          Hủy
        </button>
        <button type="button" @click="assignBatch" class="mt-3 sm:mt-0 inline-flex w-full justify-center items-center rounded-md bg-green-600 px-4 py-2 text-sm font-medium text-white shadow-sm hover:bg-green-500 sm:ml-3 sm:w-auto">
          <svg class="-ml-1 mr-2 h-4 w-4 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
          </svg>
          Phân công
        </button>
      </template>
    </Modal>
  </div>
</template>
