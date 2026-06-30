<script setup lang="ts">
import { ref, onMounted, watch } from 'vue'
import { useApi } from '~/composables/useApi'
import { useToast } from '~/composables/useToast'
import { useConfirm } from '~/composables/useConfirm'

const props = defineProps({
  projectId: { type: String, required: true }
})

const { fetchApi } = useApi()
const toast = useToast()
const { showConfirm } = useConfirm()
const treeData = ref([])
const loading = ref(false)
const selectedNode = ref<any>(null)

// Form state
const isEditing = ref(false)
const showModal = ref(false)
const modalMode = ref('add') // 'add_root', 'add_child', 'edit'
const form = ref({
  id: '',
  parentId: null as string | null,
  name: '',
  code: '',
  description: '',
  llmPrompt: '',
  isActive: true
})

const selectedNodeFields = ref<any[]>([])
const loadingFields = ref(false)

const fieldTypeMap: Record<number, string> = {
  0: 'Text',
  1: 'Number',
  2: 'Date',
  3: 'Select',
  4: 'MultiSelect'
}

watch(() => selectedNode.value, async (newVal) => {
  if (newVal && newVal.id) {
    loadingFields.value = true
    try {
      const res = await fetchApi(`/labels/${newVal.id}/fields`)
      selectedNodeFields.value = res || []
    } catch (e) {
      selectedNodeFields.value = []
    } finally {
      loadingFields.value = false
    }
  } else {
    selectedNodeFields.value = []
  }
})

const loadTree = async () => {
  loading.value = true
  try {
    const res = await fetchApi<any>(`/projects/${props.projectId}/labels`)
    treeData.value = res || []
  } catch (err) {
    console.error('Lỗi tải cây nhãn:', err)
  } finally {
    loading.value = false
  }
}

const handleSelect = (node: any) => {
  selectedNode.value = node
}

const handleAddRoot = () => {
  modalMode.value = 'add_root'
  form.value = { id: '', parentId: null, name: '', code: '', description: '', llmPrompt: '', isActive: true }
  showModal.value = true
}

const handleAddChild = (parentNode: any) => {
  modalMode.value = 'add_child'
  form.value = { id: '', parentId: parentNode.id, name: '', code: '', description: '', llmPrompt: '', isActive: true }
  showModal.value = true
}

const handleEdit = (node: any) => {
  modalMode.value = 'edit'
  form.value = { 
    id: node.id, 
    parentId: node.parentId, 
    name: node.name, 
    code: node.code || '', 
    description: node.description || '', 
    llmPrompt: node.llmPrompt || '', 
    isActive: node.isActive 
  }
  showModal.value = true
}

const submitForm = () => {
  const btn = document.getElementById('submitLabelBtn')
  if (btn) btn.click()
}

const saveLabel = async () => {
  try {
    if (modalMode.value === 'edit') {
      await fetchApi(`/labels/${form.value.id}`, {
        method: 'PUT',
        body: form.value
      })
      // Update selected node locally or reload tree
      if (selectedNode.value?.id === form.value.id) {
        selectedNode.value = { ...selectedNode.value, ...form.value }
      }
    } else {
      await fetchApi(`/projects/${props.projectId}/labels`, {
        method: 'POST',
        body: {
          projectId: props.projectId,
          parentId: form.value.parentId,
          name: form.value.name,
          code: form.value.code,
          description: form.value.description,
          llmPrompt: form.value.llmPrompt
        }
      })
    }
    showModal.value = false
    await loadTree()
  } catch (err: any) {
    toast.error(err.data?.message || 'Có lỗi xảy ra khi lưu nhãn.')
  }
}

const handleDelete = async (node: any) => {
  if (await showConfirm(`Bạn có chắc chắn muốn xóa nhãn "${node.name}"?\n(Toàn bộ nhãn con cũng sẽ bị xóa)`)) {
    try {
      await fetchApi(`/labels/${node.id}`, { method: 'DELETE' })
      if (selectedNode.value?.id === node.id) {
        selectedNode.value = null
      }
      toast.success('Đã xóa nhãn.')
      await loadTree()
    } catch (err: any) {
      toast.error(err.data?.message || 'Lỗi khi xóa nhãn.')
    }
  }
}

onMounted(() => {
  loadTree()
})
</script>

<template>
  <div class="flex h-[600px] gap-6">
    <!-- Cây nhãn (Trái) -->
    <div class="w-1/3 min-w-[300px]">
      <div v-if="loading" class="text-center py-4">Đang tải...</div>
      <TreeView 
        v-else
        :treeData="treeData" 
        :selectedId="selectedNode?.id"
        @select="handleSelect"
        @add-root="handleAddRoot"
        @add-child="handleAddChild"
        @edit="handleEdit"
        @delete="handleDelete"
      />
    </div>

    <!-- Chi tiết nhãn (Phải) -->
    <div class="flex-1 bg-white rounded-lg shadow-sm border border-gray-200 overflow-y-auto">
      <div v-if="!selectedNode" class="flex items-center justify-center h-full text-gray-500">
        Chọn một nhãn bên trái để xem chi tiết
      </div>
      <div v-else class="p-6">
        <div class="flex justify-between items-start mb-6 border-b border-gray-200 pb-4">
          <div>
            <h2 class="text-xl font-bold text-gray-900">{{ selectedNode.name }}</h2>
            <p class="text-sm text-gray-500 mt-1">Cấp độ: {{ selectedNode.level }} | Mã: {{ selectedNode.code || 'N/A' }}</p>
          </div>
          <StatusBadge :status="selectedNode.isActive ? 'true' : 'false'" type="status" />
        </div>

        <dl class="grid grid-cols-1 gap-x-4 gap-y-6 sm:grid-cols-2">
          <div class="sm:col-span-2">
            <dt class="text-sm font-medium text-gray-500">Mô tả</dt>
            <dd class="mt-1 text-sm text-gray-900 bg-gray-50 p-3 rounded-md">{{ selectedNode.description || 'Không có mô tả' }}</dd>
          </div>
          <div class="sm:col-span-2">
            <dt class="text-sm font-medium text-gray-500">LLM Prompt (Hướng dẫn phân loại AI)</dt>
            <dd class="mt-1 text-sm text-gray-900 bg-gray-50 p-3 rounded-md whitespace-pre-wrap">{{ selectedNode.llmPrompt || 'Chưa cấu hình prompt AI cho nhãn này.' }}</dd>
          </div>
        </dl>

        <div class="mt-8 pt-4 border-t border-gray-200 flex space-x-3">
          <button @click="handleEdit(selectedNode)" class="px-4 py-2 border border-gray-300 rounded-md shadow-sm text-sm font-medium text-gray-700 bg-white hover:bg-gray-50">Sửa thông tin</button>
          <button v-if="selectedNode.level < 3" @click="handleAddChild(selectedNode)" class="px-4 py-2 border border-transparent rounded-md shadow-sm text-sm font-medium text-white bg-primary-600 hover:bg-primary-700">Thêm nhãn con</button>
        </div>

        <div v-if="selectedNodeFields.length > 0" class="mt-8 pt-4 border-t border-gray-200">
          <h3 class="text-sm font-bold text-gray-700 mb-4">Các trường dữ liệu (Metadata)</h3>
          <div class="overflow-hidden shadow ring-1 ring-black ring-opacity-5 rounded-lg border border-gray-200">
            <table class="min-w-full divide-y divide-gray-300">
              <thead class="bg-gray-50">
                <tr>
                  <th scope="col" class="py-3 pl-4 pr-3 text-left text-xs font-semibold text-gray-900 uppercase">Tên trường</th>
                  <th scope="col" class="px-3 py-3 text-left text-xs font-semibold text-gray-900 uppercase">Key</th>
                  <th scope="col" class="px-3 py-3 text-left text-xs font-semibold text-gray-900 uppercase">Loại</th>
                  <th scope="col" class="px-3 py-3 text-center text-xs font-semibold text-gray-900 uppercase">Bắt buộc</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-gray-200 bg-white">
                <tr v-for="field in selectedNodeFields" :key="field.id">
                  <td class="whitespace-nowrap py-3 pl-4 pr-3 text-sm font-medium text-gray-900">{{ field.fieldName }}</td>
                  <td class="whitespace-nowrap px-3 py-3 text-sm text-gray-500">
                    <span class="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-gray-100 text-gray-800">{{ field.fieldKey }}</span>
                  </td>
                  <td class="whitespace-nowrap px-3 py-3 text-sm text-gray-500">{{ fieldTypeMap[field.fieldType] }}</td>
                  <td class="whitespace-nowrap px-3 py-3 text-sm text-center">
                    <svg v-if="field.isRequired" class="h-4 w-4 text-green-500 mx-auto" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" /></svg>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </div>

    <!-- Modal Form -->
    <Modal v-model="showModal" :title="modalMode === 'edit' ? 'Sửa nhãn' : (modalMode === 'add_child' ? 'Thêm nhãn con' : 'Thêm nhãn gốc')">
      <form @submit.prevent="saveLabel" class="space-y-4">
        <div>
          <label class="block text-sm font-bold text-gray-700 mb-1">Tên nhãn <span class="text-red-500">*</span></label>
          <input v-model="form.name" required type="text" class="block w-full rounded-md border-gray-300 shadow-sm focus:border-primary-500 focus:ring-primary-500 sm:text-sm px-3 py-2" />
        </div>
        <div>
          <label class="block text-sm font-bold text-gray-700 mb-1">Mã nhãn (Code)</label>
          <input v-model="form.code" type="text" class="block w-full rounded-md border-gray-300 shadow-sm focus:border-primary-500 focus:ring-primary-500 sm:text-sm px-3 py-2" />
        </div>
        <div>
          <label class="block text-sm font-bold text-gray-700 mb-1">Mô tả</label>
          <textarea v-model="form.description" rows="2" class="block w-full rounded-md border-gray-300 shadow-sm focus:border-primary-500 focus:ring-primary-500 sm:text-sm px-3 py-2"></textarea>
        </div>
        
        <div class="bg-gray-100 border border-gray-200 rounded-lg p-4 mt-4">
          <label class="block text-sm font-bold text-gray-700 mb-2">LLM Prompt (Hướng dẫn AI phân loại)</label>
          <textarea v-model="form.llmPrompt" rows="3" class="block w-full rounded-md border-gray-300 shadow-sm focus:border-primary-500 focus:ring-primary-500 sm:text-sm px-3 py-2" placeholder="Ví dụ: Đây là hóa đơn tài chính của công ty..."></textarea>
        </div>
        <div v-if="modalMode === 'edit'" class="flex items-center">
          <input id="isActive" v-model="form.isActive" type="checkbox" class="h-4 w-4 rounded border-gray-300 text-primary-600 focus:ring-primary-500" />
          <label for="isActive" class="ml-2 block text-sm text-gray-900">Hoạt động</label>
        </div>

        <div class="hidden">
          <button type="submit" id="submitLabelBtn">Submit</button>
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
