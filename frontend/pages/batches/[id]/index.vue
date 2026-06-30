<script setup lang="ts">
import { ref, onMounted, onUnmounted, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useApi } from '~/composables/useApi'
import { useToast } from '~/composables/useToast'
import { useConfirm } from '~/composables/useConfirm'

const route = useRoute()
const router = useRouter()
const batchId = route.params.id as string
const { fetchApi, downloadFile } = useApi()
const toast = useToast()
const { showConfirm } = useConfirm()

const batch = ref<any>(null)
const progress = ref<any>(null)
const documents = ref<any[]>([])
const loading = ref(true)
const authUser = ref<any>(null)

let pollingInterval: any = null

const loadData = async () => {
  try {
    const [batchRes, progRes, docsRes] = await Promise.all([
      fetchApi<any>(`/batches/${batchId}`),
      fetchApi<any>(`/batches/${batchId}/progress`),
      fetchApi<any>(`/batches/${batchId}/documents?page=1&pageSize=100`)
    ])
    
    batch.value = batchRes
    progress.value = progRes
    documents.value = docsRes.data || docsRes.items || []
  } catch (err) {
    console.error('Lỗi tải chi tiết lô:', err)
  } finally {
    loading.value = false
  }
}

const getStatusColor = (status: string) => {
  const map: Record<string, string> = {
    'Pending': 'bg-gray-100 text-gray-800',
    'Importing': 'bg-blue-100 text-blue-800',
    'OcrProcessing': 'bg-yellow-100 text-yellow-800',
    'Classifying': 'bg-indigo-100 text-indigo-800',
    'Extracting': 'bg-purple-100 text-purple-800',
    'ReadyForReview': 'bg-orange-100 text-orange-800',
    'Reviewing': 'bg-pink-100 text-pink-800',
    'Approved': 'bg-green-100 text-green-800',
    'Error': 'bg-red-100 text-red-800'
  }
  return map[status] || 'bg-gray-100 text-gray-800'
}

const progressPercentage = computed(() => {
  if (!progress.value || progress.value.totalFiles === 0) return 0
  return Math.round((progress.value.processedFiles / progress.value.totalFiles) * 100)
})

const processBatch = async () => {
  try {
    await fetchApi(`/batches/${batchId}/process`, { method: 'POST' })
    toast.success('Đã gửi yêu cầu xử lý OCR cho toàn bộ lô.')
    loadData()
  } catch (err: any) {
    toast.error(err.data?.message || 'Lỗi khi bắt đầu xử lý.')
  }
}

const retryDocument = async (id: string) => {
  try {
    await fetchApi(`/documents/${id}/retry`, { method: 'POST' })
    toast.success('Đã gửi lại yêu cầu xử lý OCR.')
    loadData()
  } catch (err: any) {
    toast.error(err.data?.message || 'Lỗi khi gửi lại yêu cầu.')
  }
}

const deleteDocument = async (id: string) => {
  if (await showConfirm('Bạn có chắc chắn muốn xóa tài liệu này khỏi hệ thống? Hành động này không thể khôi phục.')) {
    try {
      await fetchApi(`/documents/${id}`, { method: 'DELETE' })
      toast.success('Đã xóa tài liệu.')
      loadData()
    } catch (err: any) {
      toast.error(err.data?.message || 'Lỗi khi xóa tài liệu.')
    }
  }
}

const approveDocument = async (id: string) => {
  if (await showConfirm('Bạn có chắc chắn muốn duyệt tài liệu này?')) {
    try {
      await fetchApi(`/documents/${id}/approve`, { method: 'POST' })
      toast.success('Đã duyệt tài liệu.')
      loadData()
    } catch (err: any) {
      toast.error(err.data?.message || 'Lỗi khi duyệt tài liệu.')
    }
  }
}

// Assign Reviewer Modal Logic
const showAssignModal = ref(false)
const reviewers = ref<any[]>([])
const selectedReviewerId = ref('')

const loadReviewers = async () => {
  try {
    // Gọi API lấy danh sách user có role Reviewer/Manager (Tạm lấy tất cả users vì chưa có API filter role, ta có thể dùng filter local nếu API trả về role)
    const res = await fetchApi<any[]>('/users')
    reviewers.value = res.filter((u: any) => u.role === 'Reviewer' || u.role === 'Manager' || u.role === 'Admin')
  } catch (err) {
    console.error('Lỗi tải danh sách reviewer:', err)
  }
}

const assignReviewer = async () => {
  if (!selectedReviewerId.value) {
    toast.warning('Vui lòng chọn người duyệt.')
    return
  }
  try {
    await fetchApi(`/batches/${batchId}/assign?reviewerId=${selectedReviewerId.value}`, { method: 'POST' })
    toast.success('Phân công thành công.')
    showAssignModal.value = false
    loadData()
  } catch (err: any) {
    toast.error(err.data?.message || 'Lỗi khi phân công.')
  }
}

const exportExcel = async () => {
  try {
    toast.info('Đang xử lý xuất báo cáo...')
    await downloadFile(`/batches/${batchId}/reports/export`, `BatchReport_${batchId}.xlsx`)
    toast.success('Xuất báo cáo thành công.')
  } catch (err) {
    console.error('Lỗi xuất báo cáo:', err)
    toast.error('Có lỗi xảy ra khi xuất báo cáo.')
  }
}

// Upload Modal Logic
const showUploadModal = ref(false)
const uploadedFileKeys = ref<string[]>([])
const isUploading = ref(false)

const openUploadModal = () => {
  uploadedFileKeys.value = []
  showUploadModal.value = true
}

const handleUploadSuccess = (data: any) => {
  uploadedFileKeys.value.push(data.objectKey)
}

const submitUpload = async () => {
  if (uploadedFileKeys.value.length === 0) {
    toast.warning('Vui lòng chọn hoặc kéo thả ít nhất 1 file PDF.')
    return
  }
  
  isUploading.value = true
  try {
    await fetchApi(`/batches/${batchId}/documents/upload`, {
      method: 'POST',
      body: { uploadedFileKeys: uploadedFileKeys.value }
    })
    toast.success('Đã thêm tài liệu vào lô thành công.')
    showUploadModal.value = false
    loadData()
  } catch (err: any) {
    toast.error(err.data?.message || 'Lỗi khi thêm tài liệu.')
  } finally {
    isUploading.value = false
  }
}

onMounted(async () => {
  try {
    authUser.value = await fetchApi('/auth/me')
  } catch (err) {
    // ignore
  }
  loadData()
  loadReviewers()
  pollingInterval = setInterval(() => {
    loadData()
  }, 5000)
})

onUnmounted(() => {
  if (pollingInterval) clearInterval(pollingInterval)
})
</script>

<template>
  <div>
    <div class="mb-6">
      <NuxtLink :to="`/projects/${batch?.projectId}`" class="text-sm text-primary-600 hover:text-primary-800 font-medium flex items-center">
        <svg class="mr-1 h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10 19l-7-7m0 0l7-7m-7 7h18" /></svg>
        Quay lại Dự án
      </NuxtLink>
    </div>

    <div v-if="loading && !batch" class="text-center py-20 text-gray-500">Đang tải dữ liệu...</div>
    
    <div v-else>
      <!-- Header Lô -->
      <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-6 mb-6">
        <div class="flex flex-col md:flex-row md:items-center md:justify-between">
          <div>
            <h1 class="text-2xl font-bold text-gray-900">{{ batch?.name }}</h1>
            <div class="mt-2 flex items-center text-sm text-gray-500 space-x-4">
              <span>Ngày tạo: {{ new Date(batch?.createdAt).toLocaleString('vi-VN') }}</span>
              <span>Nguồn: {{ batch?.sourceFolder || 'Upload' }}</span>
              <span :class="['inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium', getStatusColor(batch?.status)]">
                {{ batch?.status }}
              </span>
            </div>
          </div>
          <div class="mt-4 flex md:mt-0 space-x-3">
            <button @click="exportExcel" class="inline-flex items-center px-4 py-2 border border-gray-300 shadow-sm text-sm font-medium rounded-md text-gray-700 bg-white hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-primary-500">
              <svg class="h-5 w-5 mr-2 text-green-600" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 10v6m0 0l-3-3m3 3l3-3m2 8H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" /></svg>
              Xuất Báo cáo
            </button>
            <button @click="openUploadModal" class="inline-flex items-center px-4 py-2 border border-gray-300 shadow-sm text-sm font-medium rounded-md text-gray-700 bg-white hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-primary-500">
              <svg class="-ml-1 mr-2 h-5 w-5 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12" />
              </svg>
              Upload thêm tài liệu
            </button>
            <button v-if="batch?.status === 'Completed' && (authUser?.role === 'Manager')" @click="showAssignModal = true" class="inline-flex items-center px-4 py-2 border border-gray-300 shadow-sm text-sm font-medium rounded-md text-gray-700 bg-white hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-primary-500">
              <svg class="-ml-1 mr-2 h-5 w-5 text-gray-400" viewBox="0 0 20 20" fill="currentColor"><path d="M13 6a3 3 0 11-6 0 3 3 0 016 0zM18 8a2 2 0 11-4 0 2 2 0 014 0zM14 15a4 4 0 00-8 0v3h8v-3zM6 8a2 2 0 11-4 0 2 2 0 014 0zM16 18v-3a5.972 5.972 0 00-.75-2.906A3.005 3.005 0 0119 15v3h-3zM4.75 12.094A5.973 5.973 0 004 15v3H1v-3a3 3 0 013.75-2.906z" /></svg>
              Phân công Reviewer
            </button>
            <button v-if="['Created', 'Completed', 'Importing'].includes(batch?.status) && authUser?.role === 'Manager'" @click="processBatch" class="inline-flex items-center px-4 py-2 border border-transparent shadow-sm text-sm font-medium rounded-md text-white bg-primary-600 hover:bg-primary-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-primary-500">
              <svg class="-ml-1 mr-2 h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M14.752 11.168l-3.197-2.132A1 1 0 0010 9.87v4.263a1 1 0 001.555.832l3.197-2.132a1 1 0 000-1.664z" />
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
              </svg>
              Bắt đầu xử lý OCR
            </button>
          </div>
        </div>

        <!-- Progress Overview -->
        <div class="mt-6 border-t border-gray-200 pt-6">
          <h3 class="text-sm font-medium text-gray-900 mb-2">Tiến độ xử lý chung</h3>
          <div class="flex items-center">
            <div class="w-full bg-gray-200 rounded-full h-2.5 mr-4">
              <div class="bg-primary-600 h-2.5 rounded-full transition-all duration-500" :style="{ width: progressPercentage + '%' }"></div>
            </div>
            <span class="text-sm font-medium text-gray-700">{{ progressPercentage }}%</span>
          </div>
          
          <div class="mt-4 grid grid-cols-2 md:grid-cols-6 gap-4 text-center">
            <div class="bg-gray-50 p-3 rounded-lg border border-gray-100">
              <p class="text-xs text-gray-500 font-medium">Tổng số file</p>
              <p class="mt-1 text-xl font-semibold text-gray-900">{{ progress?.totalFiles }}</p>
            </div>
            <div class="bg-blue-50 p-3 rounded-lg border border-blue-100">
              <p class="text-xs text-blue-600 font-medium">Đã bóc tách</p>
              <p class="mt-1 text-xl font-semibold text-blue-900">{{ progress?.processedFiles }}</p>
            </div>
            <div class="bg-indigo-50 p-3 rounded-lg border border-indigo-100">
              <p class="text-xs text-indigo-600 font-medium">Phân loại</p>
              <p class="mt-1 text-xl font-semibold text-indigo-900">{{ progress?.classifying }}</p>
            </div>
            <div class="bg-orange-50 p-3 rounded-lg border border-orange-100">
              <p class="text-xs text-orange-600 font-medium">Chờ duyệt</p>
              <p class="mt-1 text-xl font-semibold text-orange-900">{{ progress?.reviewing }}</p>
            </div>
            <div class="bg-green-50 p-3 rounded-lg border border-green-100">
              <p class="text-xs text-green-600 font-medium">Đã duyệt</p>
              <p class="mt-1 text-xl font-semibold text-green-900">{{ progress?.approvedFiles }}</p>
            </div>
            <div class="bg-red-50 p-3 rounded-lg border border-red-100">
              <p class="text-xs text-red-600 font-medium">Lỗi</p>
              <p class="mt-1 text-xl font-semibold text-red-900">{{ progress?.errors }}</p>
            </div>
          </div>
        </div>
      </div>

      <!-- Document List -->
      <div class="bg-white rounded-lg shadow-sm border border-gray-200 overflow-hidden">
        <div class="px-4 py-4 sm:px-6 flex justify-between items-center bg-gray-50 border-b border-gray-200">
          <h3 class="text-lg leading-6 font-medium text-gray-900">Danh sách Tài liệu</h3>
          <div class="flex space-x-3">
            <button v-if="batch?.status === 'Completed' && (authUser?.role === 'Admin' || authUser?.role === 'Manager')" @click="showAssignModal = true" class="inline-flex items-center px-4 py-2 border border-gray-300 shadow-sm text-sm font-medium rounded-md text-gray-700 bg-white hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-primary-500">
              Phân công Review
            </button>
            <NuxtLink :to="`/batches/${batchId}/review`" class="inline-flex items-center px-4 py-2 border border-transparent shadow-sm text-sm font-medium rounded-md text-white bg-primary-600 hover:bg-primary-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-primary-500">
              Bắt đầu Duyệt (Review)
            </NuxtLink>
          </div>
        </div>
        
        <div class="-my-2 overflow-x-auto sm:-mx-6 lg:-mx-8">
          <div class="py-2 align-middle inline-block min-w-full sm:px-6 lg:px-8">
            <div class="overflow-hidden border-t border-gray-200">
              <table class="min-w-full divide-y divide-gray-200">
                <thead class="bg-gray-50">
                  <tr>
                    <th scope="col" class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Tên file</th>
                    <th scope="col" class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Trạng thái</th>
                    <th scope="col" class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Nhãn (Label)</th>
                    <th scope="col" class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Độ tin cậy</th>
                    <th scope="col" class="relative px-6 py-3"><span class="sr-only">Hành động</span></th>
                  </tr>
                </thead>
                <tbody class="bg-white divide-y divide-gray-200">
                  <tr v-for="doc in documents" :key="doc.id" class="hover:bg-gray-50">
                    <td class="px-6 py-4 whitespace-nowrap">
                      <div class="flex items-center">
                        <svg class="h-5 w-5 text-red-500 mr-2" fill="currentColor" viewBox="0 0 20 20"><path fill-rule="evenodd" d="M4 4a2 2 0 012-2h4.586A2 2 0 0112 2.586L15.414 6A2 2 0 0116 7.414V16a2 2 0 01-2 2H6a2 2 0 01-2-2V4zm2 6a1 1 0 011-1h6a1 1 0 110 2H7a1 1 0 01-1-1zm1 3a1 1 0 100 2h6a1 1 0 100-2H7z" clip-rule="evenodd" /></svg>
                        <div class="text-sm font-medium text-gray-900">{{ doc.originalFilename }}</div>
                      </div>
                      <div v-if="doc.errorMessage" class="text-xs text-red-500 mt-1 max-w-xs truncate" :title="doc.errorMessage">{{ doc.errorMessage }}</div>
                    </td>
                    <td class="px-6 py-4 whitespace-nowrap">
                      <span :class="['inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium', getStatusColor(doc.status)]">
                        {{ doc.status }}
                      </span>
                      <div v-if="doc.processingMessage" class="mt-1 text-xs text-gray-500 font-medium">
                        {{ doc.processingMessage }}
                      </div>
                    </td>
                    <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
                      {{ doc.labelName || '---' }}
                    </td>
                    <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
                      <div v-if="doc.classificationConfidence !== null" class="flex items-center">
                        <span :class="doc.classificationConfidence > 0.8 ? 'text-green-600' : 'text-orange-600'">
                          {{ Math.round(doc.classificationConfidence * 100) }}%
                        </span>
                      </div>
                      <span v-else>---</span>
                    </td>
                    <td class="px-6 py-4 whitespace-nowrap text-right text-sm font-medium space-x-3">
                      <button v-if="doc.status !== 'Approved' && authUser?.role === 'Manager'" @click="approveDocument(doc.id)" class="text-green-600 hover:text-green-900">Duyệt</button>
                      <button v-if="['Error', 'Pending', 'Created'].includes(doc.status) && authUser?.role === 'Manager'" @click="retryDocument(doc.id)" class="text-orange-600 hover:text-orange-900">Xử lý OCR</button>
                      <button v-if="authUser?.role === 'Manager'" @click="deleteDocument(doc.id)" class="text-red-600 hover:text-red-900">Xóa</button>
                      <NuxtLink :to="`/documents/${doc.id}`" class="text-primary-600 hover:text-primary-900">Xem</NuxtLink>
                    </td>
                  </tr>
                  <tr v-if="documents.length === 0">
                    <td colspan="5" class="px-6 py-10 text-center text-sm text-gray-500">
                      Chưa có tài liệu nào trong lô này.
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </div>
      </div>
    </div>
    <!-- Assign Modal -->
    <Modal v-model="showAssignModal" title="Phân công Review">
      <div class="mt-2">
        <label class="block text-sm font-medium text-gray-700">Chọn người duyệt (Reviewer)</label>
        <select v-model="selectedReviewerId" class="mt-1 block w-full pl-3 pr-10 py-2 text-base border-gray-300 focus:outline-none focus:ring-primary-500 focus:border-primary-500 sm:text-sm rounded-md border">
          <option value="">-- Chọn người duyệt --</option>
          <option v-for="user in reviewers" :key="user.id" :value="user.id">
            {{ user.fullName }} ({{ user.username }})
          </option>
        </select>
      </div>
      <div class="mt-5 sm:mt-6 sm:grid sm:grid-cols-2 sm:gap-3 sm:grid-flow-row-dense">
        <button type="button" @click="assignReviewer" class="w-full inline-flex justify-center rounded-md border border-transparent shadow-sm px-4 py-2 bg-primary-600 text-base font-medium text-white hover:bg-primary-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-primary-500 sm:col-start-2 sm:text-sm">
          Lưu phân công
        </button>
        <button type="button" @click="showAssignModal = false" class="mt-3 w-full inline-flex justify-center rounded-md border border-gray-300 shadow-sm px-4 py-2 bg-white text-base font-medium text-gray-700 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-primary-500 sm:mt-0 sm:col-start-1 sm:text-sm">
          Hủy
        </button>
      </div>
    </Modal>

    <!-- Upload Modal -->
    <Modal v-model="showUploadModal" title="Upload thêm tài liệu" width="max-w-3xl">
      <div class="mt-4">
        <FileUploader 
          :batchId="batch?.id" 
          @uploadSuccess="handleUploadSuccess"
        />
        
        <div v-if="uploadedFileKeys.length > 0" class="mt-4 p-4 bg-green-50 border border-green-200 rounded-md">
          <p class="text-sm text-green-700 font-medium">Đã tải lên {{ uploadedFileKeys.length }} file thành công. Nhấn Lưu để thêm vào lô.</p>
        </div>
      </div>
      
      <div class="mt-5 sm:mt-6 sm:flex sm:flex-row-reverse">
        <button 
          type="button" 
          @click="submitUpload" 
          :disabled="isUploading || uploadedFileKeys.length === 0"
          class="w-full inline-flex justify-center rounded-md border border-transparent shadow-sm px-4 py-2 bg-primary-600 text-base font-medium text-white hover:bg-primary-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-primary-500 sm:ml-3 sm:w-auto sm:text-sm disabled:opacity-50 disabled:cursor-not-allowed">
          <svg v-if="isUploading" class="animate-spin -ml-1 mr-2 h-5 w-5 text-white" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
            <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
            <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
          </svg>
          Lưu tài liệu
        </button>
        <button 
          type="button" 
          @click="showUploadModal = false" 
          :disabled="isUploading"
          class="mt-3 w-full inline-flex justify-center rounded-md border border-gray-300 shadow-sm px-4 py-2 bg-white text-base font-medium text-gray-700 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-primary-500 sm:mt-0 sm:w-auto sm:text-sm disabled:opacity-50">
          Hủy
        </button>
      </div>
    </Modal>
  </div>
</template>
