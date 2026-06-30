<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRoute } from 'vue-router'
import { useApi } from '~/composables/useApi'
import { useToast } from '~/composables/useToast'

const route = useRoute()
const documentId = route.params.id as string
const { fetchApi } = useApi()
const toast = useToast()

const document = ref<any>(null)
const labels = ref<any[]>([])
const loading = ref(true)

const pdfMode = ref<'ocr' | 'original'>('ocr')

const extractedData = computed(() => {
  if (!document.value?.extractedMetadata) return null
  try {
    return JSON.parse(document.value.extractedMetadata)
  } catch (e) {
    return null
  }
})

const labelFields = ref<any[]>([])
const formData = ref<Record<string, any>>({})
const savingMetadata = ref(false)

const getOptions = (optionsStr: string) => {
  if (!optionsStr) return []
  try { return JSON.parse(optionsStr) } catch { return [] }
}

const loadFields = async () => {
  if (!document.value?.labelId) {
    labelFields.value = []
    formData.value = {}
    return
  }
  try {
    const res = await fetchApi<any[]>(`/labels/${document.value.labelId}/fields`)
    labelFields.value = res || []
    
    const ext = extractedData.value || {}
    const rev = document.value?.reviewedMetadata ? JSON.parse(document.value.reviewedMetadata) : null
    
    const initialData: Record<string, any> = {}
    for (const f of labelFields.value) {
      let val = rev ? rev[f.fieldKey] : ext[f.fieldKey]
      if (f.fieldType === 4) { // MultiSelect
        if (typeof val === 'string') {
          try { val = JSON.parse(val) } catch { val = val ? [val] : [] }
        }
        initialData[f.fieldKey] = Array.isArray(val) ? val : []
      } else {
        initialData[f.fieldKey] = val || ''
      }
    }
    formData.value = initialData
  } catch (err) {
    console.error('Lỗi tải trường dữ liệu:', err)
  }
}

const saveMetadata = async () => {
  savingMetadata.value = true
  try {
    await fetchApi(`/documents/${documentId}/metadata`, {
      method: 'PUT',
      body: formData.value
    })
    document.value.reviewedMetadata = JSON.stringify(formData.value)
    toast.success('Lưu dữ liệu thành công!')
  } catch (err: any) {
    toast.error(err.data?.message || 'Lỗi lưu dữ liệu')
  } finally {
    savingMetadata.value = false
  }
}

const loadData = async () => {
  try {
    const res = await fetchApi<any>(`/documents/${documentId}`)
    document.value = res

    if (document.value?.projectId) {
      const labelRes = await fetchApi<any[]>(`/projects/${document.value.projectId}/labels`)
      const flattenLabels = (list: any[], prefix = ''): any[] => {
        let result: any[] = []
        for (const l of list) {
          result.push({ ...l, displayName: prefix + l.name })
          if (l.children && l.children.length > 0) {
            result = result.concat(flattenLabels(l.children, prefix + '-- '))
          }
        }
        return result
      }
      labels.value = flattenLabels(labelRes)
    }

    if (document.value?.labelId) {
      await loadFields()
    }
  } catch (err) {
    console.error('Lỗi tải chi tiết tài liệu:', err)
  } finally {
    loading.value = false
  }
}

const updateLabel = async (event: any) => {
  const newLabelId = event.target.value
  try {
    await fetchApi(`/documents/${documentId}/label`, {
      method: 'PUT',
      body: newLabelId ? `"${newLabelId}"` : null
    })
    document.value.labelId = newLabelId || null
    await loadFields()
    toast.success('Cập nhật nhãn thành công')
  } catch (err: any) {
    toast.error(err.data?.message || 'Lỗi cập nhật nhãn')
    event.target.value = document.value?.labelId || ''
  }
}

const reclassify = async () => {
  try {
    await fetchApi(`/documents/${documentId}/reclassify`, { method: 'POST' })
    toast.success('Đã gửi yêu cầu phân loại lại.')
    loadData()
  } catch (err: any) {
    toast.error(err.data?.message || 'Lỗi khi yêu cầu phân loại lại.')
  }
}

const reExtract = async () => {
  try {
    await fetchApi(`/documents/${documentId}/re-extract`, { method: 'POST' })
    toast.success('Đã gửi yêu cầu bóc tách lại.')
    loadData()
  } catch (err: any) {
    toast.error(err.data?.message || 'Lỗi khi yêu cầu bóc tách lại.')
  }
}

const getStatusColor = (status: string) => {
  const map: Record<string, string> = {
    'Pending': 'bg-gray-100 text-gray-800',
    'Importing': 'bg-blue-100 text-blue-800',
    'OcrProcessing': 'bg-yellow-100 text-yellow-800',
    'OcrDone': 'bg-indigo-100 text-indigo-800',
    'Classifying': 'bg-purple-100 text-purple-800',
    'Classified': 'bg-teal-100 text-teal-800',
    'Extracting': 'bg-purple-100 text-purple-800',
    'Extracted': 'bg-teal-100 text-teal-800',
    'ReadyForReview': 'bg-orange-100 text-orange-800',
    'Reviewing': 'bg-pink-100 text-pink-800',
    'Approved': 'bg-green-100 text-green-800',
    'Error': 'bg-red-100 text-red-800'
  }
  return map[status] || 'bg-gray-100 text-gray-800'
}

onMounted(() => {
  loadData()
})
</script>

<template>
  <div class="h-[calc(100vh-64px)] flex flex-col">
    <!-- Header -->
    <div class="bg-white border-b border-gray-200 px-4 py-4 sm:px-6 flex items-center justify-between flex-shrink-0">
      <div class="flex items-center space-x-4">
        <button @click="$router.back()" class="text-gray-400 hover:text-gray-500">
          <svg class="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10 19l-7-7m0 0l7-7m-7 7h18" /></svg>
        </button>
        <div>
          <h1 class="text-xl font-semibold text-gray-900">{{ document?.originalFilename || 'Tài liệu' }}</h1>
          <div class="flex items-center mt-1 space-x-2 text-sm text-gray-500">
            <span v-if="document?.status" :class="['inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium', getStatusColor(document.status)]">
              {{ document.status }}
            </span>
            <span>|</span>
            <span>Số trang: {{ document?.pageCount || 0 }}</span>
          </div>
        </div>
      </div>
      
      <div class="flex space-x-4 items-center">
        <!-- Reclassify Button -->
        <button @click="reclassify" class="text-sm text-primary-600 hover:text-primary-800 font-medium" title="Phân loại tự động">
          Auto Classify
        </button>

        <!-- Re-Extract Button -->
        <button @click="reExtract" class="text-sm text-indigo-600 hover:text-indigo-800 font-medium" title="Bóc tách tự động">
          Auto Extract
        </button>

        <!-- PDF Mode Toggle -->
        <div class="flex rounded-md shadow-sm">
          <button 
            @click="pdfMode = 'original'" 
            :class="['px-4 py-2 text-sm font-medium border border-gray-300 rounded-l-md hover:bg-gray-50 focus:z-10 focus:ring-1 focus:ring-primary-500 focus:border-primary-500', pdfMode === 'original' ? 'bg-gray-100 text-gray-900' : 'bg-white text-gray-700']"
          >
            File gốc
          </button>
          <button 
            @click="pdfMode = 'ocr'" 
            :class="['px-4 py-2 text-sm font-medium border border-l-0 border-gray-300 rounded-r-md hover:bg-gray-50 focus:z-10 focus:ring-1 focus:ring-primary-500 focus:border-primary-500', pdfMode === 'ocr' ? 'bg-gray-100 text-gray-900' : 'bg-white text-gray-700']"
          >
            PDF (Searchable)
          </button>
        </div>
      </div>
    </div>

    <!-- Viewer Area -->
    <div class="flex-1 overflow-hidden p-4 bg-gray-50 flex space-x-4">
      
      <!-- PDF Viewer (Left Side) -->
      <div class="flex-1 bg-white shadow-sm rounded-lg overflow-hidden border border-gray-200">
        <DocumentViewer :document-id="documentId" :mode="pdfMode" />
      </div>

      <!-- Properties / Extracted Data (Right Side) -->
      <div class="w-[450px] bg-white shadow-sm rounded-lg border border-gray-200 flex flex-col">
        <div class="px-4 py-3 border-b border-gray-200 bg-gray-50">
          <h3 class="text-sm font-medium text-gray-900">Phân loại & Bóc tách</h3>
        </div>
        
        <div class="p-4 flex-1 overflow-y-auto space-y-6">
          
          <!-- Label Selection -->
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Loại tài liệu (Nhãn)</label>
            <select 
              :value="document?.labelId || ''" 
              @change="updateLabel"
              class="mt-1 block w-full pl-3 pr-10 py-2 text-base border-gray-300 focus:outline-none focus:ring-primary-500 focus:border-primary-500 sm:text-sm rounded-md"
            >
              <option value="">-- Chọn nhãn --</option>
              <option v-for="label in labels" :key="label.id" :value="label.id">
                {{ label.displayName }}
              </option>
            </select>
            <div v-if="document?.classificationConfidence !== null" class="mt-1 text-xs text-gray-500">
              Độ tự tin (LLM): <span :class="document?.classificationConfidence > 0.8 ? 'text-green-600' : 'text-orange-600'">{{ Math.round(document?.classificationConfidence * 100) }}%</span>
            </div>
          </div>

          <!-- Metadata Fields -->
          <div>
            <div class="flex items-center justify-between mb-3 border-b pb-2">
              <h4 class="text-sm font-medium text-gray-900">Kết quả bóc tách</h4>
              <button v-if="labelFields.length > 0" @click="saveMetadata" :disabled="savingMetadata" class="inline-flex items-center px-3 py-1 border border-transparent text-xs font-medium rounded shadow-sm text-white bg-primary-600 hover:bg-primary-700">
                {{ savingMetadata ? 'Đang lưu...' : 'Lưu thay đổi' }}
              </button>
            </div>
            
            <div v-if="labelFields.length > 0" class="space-y-4">
              <div v-for="field in labelFields" :key="field.id">
                <label class="block text-xs font-semibold text-gray-700 uppercase tracking-wider mb-1">
                  {{ field.fieldName }} <span v-if="field.isRequired" class="text-red-500">*</span>
                </label>
                
                <select v-if="field.fieldType === 3" v-model="formData[field.fieldKey]" class="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-primary-500 focus:ring-primary-500 sm:text-sm">
                  <option value="">-- Chọn --</option>
                  <option v-for="opt in getOptions(field.options)" :key="opt" :value="opt">{{ opt }}</option>
                </select>
                
                <div v-else-if="field.fieldType === 4" class="mt-1 flex flex-wrap gap-2">
                  <label v-for="opt in getOptions(field.options)" :key="opt" class="inline-flex items-center">
                    <input type="checkbox" :value="opt" v-model="formData[field.fieldKey]" class="rounded border-gray-300 text-primary-600 focus:ring-primary-500">
                    <span class="ml-2 text-sm text-gray-700">{{ opt }}</span>
                  </label>
                </div>
                
                <input v-else-if="field.fieldType === 2" type="date" v-model="formData[field.fieldKey]" class="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-primary-500 focus:ring-primary-500 sm:text-sm" />
                
                <input v-else-if="field.fieldType === 1" type="number" v-model="formData[field.fieldKey]" class="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-primary-500 focus:ring-primary-500 sm:text-sm" />
                
                <input v-else type="text" v-model="formData[field.fieldKey]" class="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-primary-500 focus:ring-primary-500 sm:text-sm" />
              </div>
            </div>
            
            <div v-else-if="extractedData" class="space-y-3">
              <div v-for="(val, key) in extractedData" :key="key" class="bg-gray-50 p-3 rounded border border-gray-200">
                <div class="text-xs font-semibold text-gray-500 uppercase tracking-wider mb-1">{{ key }}</div>
                <div :class="['text-sm', val ? 'text-gray-900' : 'text-red-500 italic']">
                  {{ val || 'Không bóc tách được (Null/Empty)' }}
                </div>
              </div>
            </div>
            <div v-else class="text-sm text-gray-500 text-center py-4 border-t border-gray-100">
              Chưa có dữ liệu bóc tách hoặc nhãn này chưa cấu hình trường.
            </div>
          </div>
          
        </div>
      </div>
      
    </div>
  </div>
</template>
