<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useApi } from '~/composables/useApi'
import { useToast } from '~/composables/useToast'

const route = useRoute()
const router = useRouter()
const batchId = route.params.id as string
const { fetchApi } = useApi()
const toast = useToast()

const batch = ref<any>(null)
const documents = ref<any[]>([])
const currentIndex = ref(0)
const loading = ref(true)
const authUser = ref<any>(null)
const labels = ref<any[]>([])

const pdfMode = ref<'ocr' | 'original'>('ocr')

const loadBatch = async () => {
  try {
    const res = await fetchApi<any>(`/batches/${batchId}`)
    batch.value = res
    
    // Fetch labels for project
    if (res.projectId) {
      labels.value = await fetchApi<any[]>(`/projects/${res.projectId}/labels`)
    }
    
    // Fetch documents
    const docRes = await fetchApi<any>(`/batches/${batchId}/documents?page=1&pageSize=1000`)
    const docs = docRes.data || []
    documents.value = docs.filter((d: any) => 
      ['Classified', 'Extracted', 'ReadyForReview', 'Reviewing', 'Rejected'].includes(d.status)
    )
    
    if (documents.value.length === 0) {
      toast.info('Không có tài liệu nào cần duyệt trong lô này.')
      router.push(`/batches/${batchId}`)
    }
  } catch (err) {
    console.error('Lỗi tải lô tài liệu:', err)
  } finally {
    loading.value = false
  }
}

const currentDocument = computed(() => documents.value[currentIndex.value] || null)
const currentDocumentDetail = ref<any>(null)

const loadDocumentDetail = async () => {
  if (!currentDocument.value) return
  loading.value = true
  try {
    currentDocumentDetail.value = await fetchApi<any>(`/documents/${currentDocument.value.id}`)
  } catch (err) {
    console.error('Lỗi tải chi tiết tài liệu:', err)
  } finally {
    loading.value = false
  }
}

watch(currentIndex, loadDocumentDetail)
watch(documents, () => {
  if (documents.value.length > 0) {
    loadDocumentDetail()
  }
}, { immediate: true })

const initialData = computed(() => {
  const doc = currentDocumentDetail.value
  if (!doc) return {}
  const jsonStr = doc.reviewedMetadata || doc.extractedMetadata || '{}'
  try {
    return JSON.parse(jsonStr)
  } catch {
    return {}
  }
})

const next = () => {
  if (currentIndex.value < documents.value.length - 1) {
    currentIndex.value++
  } else {
    toast.info('Đã duyệt hết danh sách.')
    router.push(`/batches/${batchId}`)
  }
}

const prev = () => {
  if (currentIndex.value > 0) {
    currentIndex.value--
  }
}

const handleLabelChange = async (newLabelId: string) => {
  if (!currentDocument.value) return
  try {
    await fetchApi(`/documents/${currentDocument.value.id}/info`, {
      method: 'PUT',
      body: { labelId: newLabelId }
    })
    currentDocument.value.labelId = newLabelId
    if (currentDocumentDetail.value) {
      currentDocumentDetail.value.labelId = newLabelId
    }
  } catch (err: any) {
    toast.error(err.data?.message || 'Lỗi khi cập nhật nhãn.')
  }
}

const handleSave = async (formData: any) => {
  if (!currentDocument.value) return
  try {
    await fetchApi(`/documents/${currentDocument.value.id}/metadata`, {
      method: 'PUT',
      body: formData
    })
    // Cập nhật local
    if (currentDocumentDetail.value) {
      currentDocumentDetail.value.reviewedMetadata = JSON.stringify(formData)
    }
    toast.success('Đã lưu dữ liệu.')
  } catch (err: any) {
    toast.error(err.data?.message || 'Lỗi khi lưu dữ liệu.')
  }
}

const handleApprove = async (formData: any) => {
  if (!currentDocument.value) return
  try {
    // 1. Lưu metadata mới nhất trước khi duyệt
    await fetchApi(`/documents/${currentDocument.value.id}/metadata`, {
      method: 'PUT',
      body: formData
    })
    
    // 2. Approve
    await fetchApi(`/documents/${currentDocument.value.id}/approve`, {
      method: 'POST'
    })
    
    currentDocument.value.status = 'Approved'
    next()
  } catch (err: any) {
    toast.error(err.data?.message || 'Lỗi khi duyệt tài liệu.')
  }
}

const handleReject = async (reason: string) => {
  if (!currentDocument.value) return
  try {
    await fetchApi(`/documents/${currentDocument.value.id}/reject`, {
      method: 'POST',
      body: `"${reason}"`
    })
    currentDocument.value.status = 'Rejected'
    currentDocument.value.errorMessage = reason
    next()
  } catch (err: any) {
    toast.error(err.data?.message || 'Lỗi khi từ chối tài liệu.')
  }
}

onMounted(async () => {
  try {
    authUser.value = await fetchApi('/auth/me')
  } catch (err) {
    // ignore
  }
  loadBatch()
})
</script>

<template>
  <div class="h-[calc(100vh-64px)] flex flex-col bg-gray-100">
    <!-- Header: Toolbar Review -->
    <div class="bg-white border-b border-gray-200 px-4 py-3 flex items-center justify-between flex-shrink-0 shadow-sm z-10">
      <div class="flex items-center space-x-4">
        <button @click="$router.push(`/batches/${batchId}`)" class="text-gray-500 hover:text-gray-700 bg-gray-100 p-2 rounded-full transition-colors">
          <svg class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10 19l-7-7m0 0l7-7m-7 7h18" /></svg>
        </button>
        <div>
          <h1 class="text-lg font-bold text-gray-900">Review Lô: {{ batch?.name || '...' }}</h1>
          <div class="text-sm text-gray-500 mt-0.5" v-if="documents.length > 0">
            Tài liệu <span class="font-semibold text-gray-900">{{ currentIndex + 1 }}</span> / {{ documents.length }}
            <span class="mx-2">|</span>
            <span class="truncate max-w-xs inline-block align-bottom">{{ currentDocument?.originalFilename }}</span>
          </div>
        </div>
      </div>
      
      <div class="flex space-x-4 items-center">
        <!-- PDF Mode Toggle -->
        <div class="flex rounded-md shadow-sm mr-4">
          <button 
            @click="pdfMode = 'original'" 
            :class="['px-3 py-1.5 text-xs font-medium border border-gray-300 rounded-l-md hover:bg-gray-50 focus:z-10 focus:ring-1 focus:ring-primary-500 focus:border-primary-500', pdfMode === 'original' ? 'bg-gray-100 text-gray-900' : 'bg-white text-gray-700']"
          >
            File gốc
          </button>
          <button 
            @click="pdfMode = 'ocr'" 
            :class="['px-3 py-1.5 text-xs font-medium border border-l-0 border-gray-300 rounded-r-md hover:bg-gray-50 focus:z-10 focus:ring-1 focus:ring-primary-500 focus:border-primary-500', pdfMode === 'ocr' ? 'bg-gray-100 text-gray-900' : 'bg-white text-gray-700']"
          >
            Bản OCR
          </button>
        </div>

        <div class="flex space-x-2">
          <button @click="prev" :disabled="currentIndex === 0" :class="['px-4 py-2 border border-gray-300 rounded-md text-sm font-medium shadow-sm focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-primary-500', currentIndex === 0 ? 'bg-gray-100 text-gray-400 cursor-not-allowed' : 'bg-white text-gray-700 hover:bg-gray-50']">
            Trước
          </button>
          <button @click="next" :disabled="currentIndex === documents.length - 1" :class="['px-4 py-2 border border-gray-300 rounded-md text-sm font-medium shadow-sm focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-primary-500', currentIndex === documents.length - 1 ? 'bg-gray-100 text-gray-400 cursor-not-allowed' : 'bg-white text-gray-700 hover:bg-gray-50']">
            Tiếp
          </button>
        </div>
      </div>
    </div>

    <!-- Viewer Area (Split View) -->
    <div class="flex-1 overflow-hidden p-4 flex space-x-4" v-if="currentDocument">
      
      <!-- PDF Viewer (Left Side 60%) -->
      <div class="w-3/5 bg-white shadow-md rounded-xl overflow-hidden border border-gray-200 flex flex-col">
        <DocumentViewer :document-id="currentDocument.id" :mode="pdfMode" :key="currentDocument.id" />
      </div>

      <!-- Metadata Form (Right Side 40%) -->
      <div class="w-2/5 shadow-md rounded-xl overflow-hidden border border-gray-200">
        <MetadataForm 
          :label-id="currentDocument.labelId" 
          :labels="labels"
          :initial-data="initialData"
          :document-id="currentDocument.id"
          :user-role="authUser?.role"
          @update:label-id="handleLabelChange"
          @save="handleSave"
          @approve="handleApprove"
          @reject="handleReject"
          :key="currentDocument.id"
        />
      </div>
      
    </div>
    <div v-else-if="!loading" class="flex-1 flex items-center justify-center text-gray-500">
      Không có tài liệu nào để hiển thị.
    </div>
  </div>
</template>
