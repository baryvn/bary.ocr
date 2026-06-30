<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useApi } from '~/composables/useApi'
import { useToast } from '~/composables/useToast'
import { useConfirm } from '~/composables/useConfirm'

const props = defineProps({
  projectId: { type: String, required: true }
})

const { fetchApi } = useApi()
const toast = useToast()
const { showConfirm } = useConfirm()
const labels = ref<any[]>([])
const selectedLabelId = ref<string>('')
const flatLabels = ref<any[]>([])
const fields = ref<any[]>([])
const loading = ref(false)

const flattenLabels = (list: any[], prefix = ''): any[] => {
  let result: any[] = []
  for (const l of list) {
    const isLeaf = !l.children || l.children.length === 0
    result.push({ ...l, displayName: prefix + l.name, isLeafNode: isLeaf })
    if (!isLeaf) {
      result = result.concat(flattenLabels(l.children, prefix + '-- '))
    }
  }
  return result
}

const loadLabels = async () => {
  try {
    const res = await fetchApi<any[]>(`/projects/${props.projectId}/labels`)
    flatLabels.value = flattenLabels(res || [])
    
    if (!selectedLabelId.value && flatLabels.value.length > 0) {
      selectedLabelId.value = flatLabels.value[0].id
      await loadFields()
    }
  } catch (err) {
    console.error('Lỗi tải danh sách nhãn:', err)
  }
}

const loadFields = async () => {
  if (!selectedLabelId.value) return
  loading.value = true
  try {
    const res = await fetchApi<any>(`/labels/${selectedLabelId.value}/fields`)
    fields.value = res || []
  } catch (err) {
    console.error('Lỗi tải trường dữ liệu:', err)
  } finally {
    loading.value = false
  }
}

const onLabelChange = () => {
  if (selectedLabelId.value) {
    loadFields()
  } else {
    fields.value = []
  }
}

const fieldTypeMap: Record<number, string> = {
  0: 'Text',
  1: 'Number',
  2: 'Date',
  3: 'Select',
  4: 'MultiSelect'
}

// Modal state
const showModal = ref(false)
const isEditing = ref(false)
const form = ref({
  id: '',
  fieldName: '',
  fieldKey: '',
  fieldType: 0,
  options: '',
  llmExtractionPrompt: '',
  isRequired: false,
  sortOrder: 0
})

const openAddModal = () => {
  if (!selectedLabelId.value) {
    toast.warning('Vui lòng chọn một nhãn để thêm trường dữ liệu.')
    return
  }
  isEditing.value = false
  form.value = {
    id: '',
    fieldName: '',
    fieldKey: '',
    fieldType: 0,
    options: '',
    llmExtractionPrompt: '',
    isRequired: false,
    sortOrder: 0
  }
  showModal.value = true
}

const openEditModal = (field: any) => {
  isEditing.value = true
  form.value = {
    id: field.id,
    fieldName: field.fieldName,
    fieldKey: field.fieldKey,
    fieldType: field.fieldType,
    options: field.options || '',
    llmExtractionPrompt: field.llmExtractionPrompt || '',
    isRequired: field.isRequired,
    sortOrder: field.sortOrder
  }
  showModal.value = true
}

const submitForm = () => {
  const btn = document.getElementById('submitFieldBtn')
  if (btn) btn.click()
}

const saveField = async () => {
  try {
    if (isEditing.value) {
      await fetchApi(`/fields/${form.value.id}`, {
        method: 'PUT',
        body: form.value
      })
    } else {
      await fetchApi(`/labels/${selectedLabelId.value}/fields`, {
        method: 'POST',
        body: form.value
      })
    }
    showModal.value = false
    await loadFields()
  } catch (err: any) {
    toast.error(err.data?.message || 'Có lỗi xảy ra khi lưu trường dữ liệu.')
  }
}

const deleteField = async (field: any) => {
  if (await showConfirm(`Bạn có chắc chắn muốn xóa trường "${field.fieldName}"?`)) {
    try {
      await fetchApi(`/fields/${field.id}`, { method: 'DELETE' })
      toast.success('Đã xóa trường dữ liệu.')
      await loadFields()
    } catch (err: any) {
      toast.error(err.data?.message || 'Lỗi khi xóa trường dữ liệu.')
    }
  }
}

onMounted(() => {
  loadLabels()
})
</script>

<template>
  <div class="bg-white rounded-lg shadow-sm border border-gray-200">
    <div class="p-6">
      <div class="sm:flex sm:items-center sm:justify-between mb-6">
        <div class="sm:flex-auto">
          <h2 class="text-xl font-semibold text-gray-900">Trường thông tin (Metadata)</h2>
          <p class="mt-2 text-sm text-gray-700">Cấu hình các trường dữ liệu cần bóc tách cho từng loại tài liệu.</p>
        </div>
        
        <div class="mt-4 sm:mt-0 flex items-center space-x-4">
          <select v-model="selectedLabelId" @change="onLabelChange" class="block w-64 rounded-md border-gray-300 py-2 pl-3 pr-10 text-base focus:border-primary-500 focus:outline-none focus:ring-primary-500 sm:text-sm">
            <option value="">-- Chọn nhãn tài liệu --</option>
            <option v-for="l in flatLabels" :key="l.id" :value="l.id">
              {{ l.displayName }}
            </option>
          </select>

          <button @click="openAddModal" :disabled="!selectedLabelId" class="inline-flex items-center justify-center rounded-md border border-transparent bg-primary-600 px-4 py-2 text-sm font-medium text-white shadow-sm hover:bg-primary-700 disabled:bg-gray-300 disabled:cursor-not-allowed">
            Thêm trường
          </button>
        </div>
      </div>

      <!-- Bảng danh sách trường -->
      <div v-if="!selectedLabelId" class="text-center py-10 text-gray-500 border border-dashed rounded-lg">
        Vui lòng chọn một nhãn ở dropdown phía trên.
      </div>
      <div v-else-if="loading" class="text-center py-10 text-gray-500">Đang tải...</div>
      <div v-else-if="fields.length === 0" class="text-center py-10 text-gray-500 border border-dashed rounded-lg">
        Chưa có trường dữ liệu nào được cấu hình cho nhãn này.
      </div>
      <div v-else class="mt-4 flex flex-col">
        <div class="-my-2 -mx-4 overflow-x-auto sm:-mx-6 lg:-mx-8">
          <div class="inline-block min-w-full py-2 align-middle md:px-6 lg:px-8">
            <div class="overflow-hidden shadow ring-1 ring-black ring-opacity-5 md:rounded-lg">
              <table class="min-w-full divide-y divide-gray-300">
                <thead class="bg-gray-50">
                  <tr>
                    <th scope="col" class="py-3.5 pl-4 pr-3 text-left text-sm font-semibold text-gray-900 sm:pl-6">Tên trường</th>
                    <th scope="col" class="px-3 py-3.5 text-left text-sm font-semibold text-gray-900">Key</th>
                    <th scope="col" class="px-3 py-3.5 text-left text-sm font-semibold text-gray-900">Loại (Type)</th>
                    <th scope="col" class="px-3 py-3.5 text-center text-sm font-semibold text-gray-900">Bắt buộc</th>
                    <th scope="col" class="relative py-3.5 pl-3 pr-4 sm:pr-6"><span class="sr-only">Actions</span></th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-gray-200 bg-white">
                  <tr v-for="field in fields" :key="field.id">
                    <td class="whitespace-nowrap py-4 pl-4 pr-3 text-sm font-medium text-gray-900 sm:pl-6">
                      {{ field.fieldName }}
                    </td>
                    <td class="whitespace-nowrap px-3 py-4 text-sm text-gray-500">
                      <span class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-gray-100 text-gray-800">
                        {{ field.fieldKey }}
                      </span>
                    </td>
                    <td class="whitespace-nowrap px-3 py-4 text-sm text-gray-500">{{ fieldTypeMap[field.fieldType] }}</td>
                    <td class="whitespace-nowrap px-3 py-4 text-sm text-center">
                      <svg v-if="field.isRequired" class="h-5 w-5 text-green-500 mx-auto" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" /></svg>
                      <svg v-else class="h-5 w-5 text-gray-300 mx-auto" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" /></svg>
                    </td>
                    <td class="relative whitespace-nowrap py-4 pl-3 pr-4 text-right text-sm font-medium sm:pr-6 space-x-2">
                      <button @click="openEditModal(field)" class="text-primary-600 hover:text-primary-900">Sửa</button>
                      <button @click="deleteField(field)" class="text-red-600 hover:text-red-900">Xóa</button>
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Modal Form -->
    <Modal v-model="showModal" :title="isEditing ? 'Sửa trường dữ liệu' : 'Thêm trường dữ liệu'">
      <form @submit.prevent="saveField" class="space-y-4">
        <div class="grid grid-cols-2 gap-4">
          <div>
            <label class="block text-sm font-bold text-gray-700 mb-1">Tên trường (Hiển thị) <span class="text-red-500">*</span></label>
            <input v-model="form.fieldName" required type="text" class="block w-full rounded-md border-gray-300 shadow-sm focus:border-primary-500 focus:ring-primary-500 sm:text-sm px-3 py-2" placeholder="VD: Ngày tháng năm sinh" />
          </div>
          <div>
            <label class="block text-sm font-bold text-gray-700 mb-1">Mã trường (Key) <span class="text-red-500">*</span></label>
            <input v-model="form.fieldKey" required type="text" class="block w-full rounded-md border-gray-300 shadow-sm focus:border-primary-500 focus:ring-primary-500 sm:text-sm px-3 py-2 disabled:bg-gray-100 disabled:text-gray-500" placeholder="VD: dob" :disabled="isEditing" />
            <p v-if="isEditing" class="mt-2 text-xs text-gray-500 italic">Không thể đổi Key sau khi tạo.</p>
          </div>
        </div>
        
        <div class="grid grid-cols-2 gap-4">
          <div>
            <label class="block text-sm font-bold text-gray-700 mb-1">Loại dữ liệu (Type)</label>
            <select v-model="form.fieldType" class="block w-full rounded-md border-gray-300 shadow-sm focus:border-primary-500 focus:ring-primary-500 sm:text-sm px-3 py-2">
              <option :value="0">Text</option>
              <option :value="1">Number</option>
              <option :value="2">Date</option>
              <option :value="3">Select (1 lựa chọn)</option>
              <option :value="4">MultiSelect (Nhiều lựa chọn)</option>
            </select>
          </div>
          <div class="flex items-center mt-6">
            <input id="isRequired" v-model="form.isRequired" type="checkbox" class="h-4 w-4 rounded border-gray-300 text-primary-600 focus:ring-primary-500" />
            <label for="isRequired" class="ml-2 block text-sm text-gray-900">Trường bắt buộc</label>
          </div>
        </div>

        <div v-if="form.fieldType === 3 || form.fieldType === 4">
          <label class="block text-sm font-bold text-gray-700 mb-1">Tùy chọn (Options)</label>
          <input v-model="form.options" type="text" class="block w-full rounded-md border-gray-300 shadow-sm focus:border-primary-500 focus:ring-primary-500 sm:text-sm px-3 py-2" placeholder='VD: ["Nam", "Nữ", "Khác"]' />
          <p class="mt-2 text-xs text-gray-500 italic">Nhập danh sách tùy chọn dưới dạng JSON array (VD: ["A", "B"]).</p>
        </div>

        <div class="bg-gray-100 border border-gray-200 rounded-lg p-4 mt-4">
          <label class="block text-sm font-bold text-gray-700 mb-2">LLM Prompt (Hướng dẫn AI bóc tách)</label>
          <textarea v-model="form.llmExtractionPrompt" rows="3" class="block w-full rounded-md border-gray-300 shadow-sm focus:border-primary-500 focus:ring-primary-500 sm:text-sm px-3 py-2" placeholder="VD: Hãy trích xuất ngày sinh theo định dạng DD/MM/YYYY."></textarea>
        </div>

        <div class="hidden">
          <button type="submit" id="submitFieldBtn">Submit</button>
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
