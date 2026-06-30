<script setup lang="ts">
import { ref, watch, onMounted, computed } from 'vue'
import { useApi } from '~/composables/useApi'

const props = defineProps<{
  labelId: string | null
  labels: any[]
  initialData: Record<string, any>
  documentId: string
  userRole?: string
}>()

const emit = defineEmits(['update:labelId', 'save', 'approve', 'reject'])

const { fetchApi } = useApi()
const fields = ref<any[]>([])
const formData = ref<Record<string, any>>({})
const loading = ref(false)
const note = ref('')

const showHistory = ref(false)
const historyLogs = ref<any[]>([])

const loadHistory = async () => {
  if (!props.documentId) return
  try {
    historyLogs.value = await fetchApi<any[]>(`/documents/${props.documentId}/history`)
  } catch (err) {
    console.error('Lỗi tải lịch sử:', err)
  }
}

watch(showHistory, (newVal) => {
  if (newVal) {
    loadHistory()
  }
})

const flatLabels = computed(() => {
  const result: any[] = []
  const flatten = (items: any[], level: number = 0) => {
    for (const item of items) {
      result.push({
        ...item,
        displayName: (level > 0 ? '-- '.repeat(level) : '') + item.name
      })
      if (item.children && item.children.length > 0) {
        flatten(item.children, level + 1)
      }
    }
  }
  if (props.labels) {
    flatten(props.labels)
  }
  return result
})

const loadFields = async () => {
  if (!props.labelId) {
    fields.value = []
    return
  }
  loading.value = true
  try {
    fields.value = await fetchApi<any[]>(`/labels/${props.labelId}/fields/inherited`)
    // Khởi tạo formData từ initialData
    const newForm: Record<string, any> = {}
    fields.value.forEach(f => {
      newForm[f.fieldKey] = props.initialData[f.fieldKey] || ''
    })
    formData.value = newForm
  } catch (err) {
    console.error('Lỗi tải metadata fields:', err)
  } finally {
    loading.value = false
  }
}

watch(() => props.labelId, loadFields)

watch(() => props.initialData, (newVal) => {
  if (fields.value.length > 0) {
    const newForm: Record<string, any> = {}
    fields.value.forEach(f => {
      newForm[f.fieldKey] = newVal[f.fieldKey] || ''
    })
    formData.value = newForm
  }
}, { deep: true })

onMounted(loadFields)

// Bắt sự kiện phím tắt
onMounted(() => {
  const handleKeyDown = (e: KeyboardEvent) => {
    // Chỉ kích hoạt nếu không đang focus vào textarea/input (tuỳ ý, hoặc bắt luôn)
    // Ctrl + S
    if (e.ctrlKey && e.key === 's') {
      e.preventDefault()
      handleSave()
    }
    // Enter (nếu không ở textarea hoặc có Ctrl)
    if (e.ctrlKey && e.key === 'Enter') {
      e.preventDefault()
      if (props.userRole === 'Manager') {
        handleApprove()
      }
    }
  }
  window.addEventListener('keydown', handleKeyDown)
  // return () => window.removeEventListener('keydown', handleKeyDown) => vue3 onUnmounted
})

const prepareFormData = () => {
  const data = { ...formData.value }
  fields.value.forEach(f => {
    if (f.fieldType === 'Number' || f.fieldType === 1) {
      const val = data[f.fieldKey]
      if (val !== undefined && val !== null && val !== '') {
        data[f.fieldKey] = Number(val)
      }
    } else if (f.fieldType === 'MultiSelect' || f.fieldType === 4) {
      const val = data[f.fieldKey]
      if (typeof val === 'string') {
        data[f.fieldKey] = val.split(',').map((s: string) => s.trim()).filter((s: string) => s)
      }
    }
  })
  return data
}

const handleSave = () => {
  emit('save', prepareFormData())
}

const handleApprove = () => {
  emit('approve', prepareFormData())
}

const handleReject = () => {
  const reason = prompt('Lý do từ chối:')
  if (reason !== null) {
    emit('reject', reason)
  }
}

</script>

<template>
  <div class="flex flex-col h-full bg-white">
    <div class="p-4 border-b border-gray-200 bg-gray-50 flex justify-between items-center">
      <h3 class="text-sm font-medium text-gray-900">Biểu mẫu Dữ liệu</h3>
      <div class="flex items-center space-x-3">
        <button @click="showHistory = true" type="button" class="text-xs px-2 py-1 bg-gray-200 hover:bg-gray-300 rounded text-gray-700 font-medium">Lịch sử</button>
        <div class="text-xs text-gray-500">Ctrl+S: Lưu | Ctrl+Enter: Duyệt</div>
      </div>
    </div>

    <div class="p-4 border-b border-gray-200">
      <label class="block text-sm font-medium text-gray-700 mb-1">Loại tài liệu (Nhãn)</label>
      <select 
        :value="labelId || ''" 
        @change="$emit('update:labelId', ($event.target as HTMLSelectElement).value)"
        class="mt-1 block w-full pl-3 pr-10 py-2 text-base border-gray-300 focus:outline-none focus:ring-primary-500 focus:border-primary-500 sm:text-sm rounded-md"
      >
        <option value="" disabled>-- Chọn loại tài liệu --</option>
        <option v-for="l in flatLabels" :key="l.id" :value="l.id">{{ l.displayName }}</option>
      </select>
    </div>

    <div class="flex-1 p-4 overflow-y-auto">
      <div v-if="loading" class="text-center text-sm text-gray-500 py-4">Đang tải biểu mẫu...</div>
      
      <div v-else-if="!labelId" class="text-center text-sm text-gray-500 py-4">
        Tài liệu chưa được gắn nhãn. Vui lòng chọn nhãn để hiển thị biểu mẫu.
      </div>
      
      <div v-else-if="fields.length === 0" class="text-center text-sm text-gray-500 py-4">
        Nhãn này chưa được cấu hình trường dữ liệu (Metadata Fields).
      </div>

      <div v-else class="space-y-4">
        <div v-for="field in fields" :key="field.id">
          <label :for="field.fieldKey" class="block text-sm font-medium text-gray-700">
            {{ field.fieldName }}
            <span v-if="field.isRequired" class="text-red-500">*</span>
          </label>
          
          <div class="mt-1">
            <!-- Text/Number -->
            <input 
              v-if="field.fieldType === 'Text' || field.fieldType === 0 || field.fieldType === 'Number' || field.fieldType === 1"
              :type="(field.fieldType === 'Number' || field.fieldType === 1) ? 'number' : 'text'"
              :id="field.fieldKey"
              v-model="formData[field.fieldKey]"
              class="shadow-sm focus:ring-primary-500 focus:border-primary-500 block w-full sm:text-sm border-gray-300 rounded-md py-2 px-3 border"
              :required="field.isRequired"
            />
            
            <!-- Date -->
            <input 
              v-else-if="field.fieldType === 'Date' || field.fieldType === 2"
              type="date"
              :id="field.fieldKey"
              v-model="formData[field.fieldKey]"
              class="shadow-sm focus:ring-primary-500 focus:border-primary-500 block w-full sm:text-sm border-gray-300 rounded-md py-2 px-3 border"
              :required="field.isRequired"
            />
            
            <!-- Select -->
            <select
              v-else-if="field.fieldType === 'Select' || field.fieldType === 3"
              :id="field.fieldKey"
              v-model="formData[field.fieldKey]"
              class="shadow-sm focus:ring-primary-500 focus:border-primary-500 block w-full sm:text-sm border-gray-300 rounded-md py-2 px-3 border"
              :required="field.isRequired"
            >
              <option value="">-- Chọn --</option>
              <option v-for="opt in JSON.parse(field.options || '[]')" :key="opt" :value="opt">{{ opt }}</option>
            </select>
            
            <!-- MultiSelect (Tạm dùng dạng text input cho đơn giản, hoặc custom component) -->
            <input 
              v-else-if="field.fieldType === 'MultiSelect' || field.fieldType === 4"
              type="text"
              :id="field.fieldKey"
              v-model="formData[field.fieldKey]"
              placeholder="Ngăn cách bởi dấu phẩy"
              class="shadow-sm focus:ring-primary-500 focus:border-primary-500 block w-full sm:text-sm border-gray-300 rounded-md py-2 px-3 border"
            />
          </div>
        </div>
      </div>
    </div>

    <!-- Actions Footer -->
    <div class="p-4 border-t border-gray-200 bg-gray-50 flex space-x-3">
      <button 
        @click="handleSave"
        class="flex-1 bg-primary-600 py-2 px-4 border border-transparent rounded-md shadow-sm text-sm font-medium text-white hover:bg-primary-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-primary-500"
      >
        Lưu dữ liệu
      </button>
      <button 
        v-if="userRole === 'Manager'"
        @click="handleReject"
        class="flex-1 bg-red-600 py-2 px-4 border border-transparent rounded-md shadow-sm text-sm font-medium text-white hover:bg-red-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-red-500"
      >
        Từ chối
      </button>
      <button 
        v-if="userRole === 'Manager'"
        @click="handleApprove"
        class="flex-1 bg-green-600 py-2 px-4 border border-transparent rounded-md shadow-sm text-sm font-medium text-white hover:bg-green-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-green-500"
      >
        Duyệt
      </button>
    </div>

    <!-- Modal Lịch sử -->
    <Modal v-model="showHistory" title="Lịch sử chỉnh sửa" size="lg">
      <div class="overflow-x-auto">
        <table class="min-w-full divide-y divide-gray-200 text-sm">
          <thead class="bg-gray-50">
            <tr>
              <th scope="col" class="px-3 py-2 text-left font-medium text-gray-500">Thời gian</th>
              <th scope="col" class="px-3 py-2 text-left font-medium text-gray-500">Người thực hiện</th>
              <th scope="col" class="px-3 py-2 text-left font-medium text-gray-500">Hành động</th>
              <th scope="col" class="px-3 py-2 text-left font-medium text-gray-500">Chi tiết</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-gray-200 bg-white">
            <tr v-for="log in historyLogs" :key="log.id">
              <td class="px-3 py-2 whitespace-nowrap">{{ new Date(log.createdAt).toLocaleString('vi-VN') }}</td>
              <td class="px-3 py-2 whitespace-nowrap">{{ log.userName }}</td>
              <td class="px-3 py-2 whitespace-nowrap font-medium">
                <span v-if="log.action === 'UpdateMetadata'" class="text-blue-600">Sửa Metadata</span>
                <span v-else-if="log.action === 'ApproveDocument'" class="text-green-600">Duyệt</span>
                <span v-else-if="log.action === 'RejectDocument'" class="text-red-600">Từ chối</span>
                <span v-else>{{ log.action }}</span>
              </td>
              <td class="px-3 py-2 text-xs text-gray-600 break-all max-w-md">
                <div v-if="log.newValues" class="max-h-20 overflow-y-auto" title="Dữ liệu mới">
                  {{ log.newValues }}
                </div>
              </td>
            </tr>
            <tr v-if="historyLogs.length === 0">
              <td colspan="4" class="px-3 py-4 text-center text-gray-500">Chưa có lịch sử thay đổi</td>
            </tr>
          </tbody>
        </table>
      </div>
    </Modal>
  </div>
</template>
