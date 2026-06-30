<script setup lang="ts">
import { ref, onMounted, watch } from 'vue'
import { useApi } from '~/composables/useApi'
import { useProjectStore } from '~/stores/project'
import { useToast } from '~/composables/useToast'

const props = defineProps({
  projectId: { type: String, required: true }
})

const { fetchApi } = useApi()
const projectStore = useProjectStore()
const toast = useToast()

const loading = ref(false)
const saving = ref(false)
const previewLoading = ref(false)

const form = ref({
  namingRule: '{OriginalFileName}',
  folderRule: '{Year}/{Month}'
})

const previewResult = ref({
  folderPath: '',
  fileName: '',
  fullPath: ''
})

const availableVariables = [
  { key: '{ProjectName}', desc: 'Tên dự án' },
  { key: '{LabelCode}', desc: 'Mã nhãn (VD: HDLD)' },
  { key: '{Year}', desc: 'Năm hiện tại (VD: 2026)' },
  { key: '{Month}', desc: 'Tháng hiện tại (VD: 06)' },
  { key: '{Day}', desc: 'Ngày hiện tại (VD: 24)' },
  { key: '{OriginalFileName}', desc: 'Tên file gốc' },
  { key: '{AutoIncrement}', desc: 'Số thứ tự tăng dần (VD: 001)' },
]

const loadRules = async () => {
  loading.value = true
  try {
    const res = await fetchApi<any>(`/projects/${props.projectId}/naming-rule`)
    if (res.namingRule) form.value.namingRule = res.namingRule
    if (res.folderRule) form.value.folderRule = res.folderRule
    await loadPreview()
  } catch (err) {
    console.error('Lỗi tải cấu hình đặt tên:', err)
  } finally {
    loading.value = false
  }
}

const loadPreview = async () => {
  previewLoading.value = true
  try {
    const res = await fetchApi<any>(`/projects/${props.projectId}/naming-rule/preview`, {
      method: 'POST',
      body: {
        namingRule: form.value.namingRule,
        folderRule: form.value.folderRule,
        labelId: null, // Mock
        mockMetadata: {
          "SoHopDong": "HD-12345"
        }
      }
    })
    previewResult.value = res
  } catch (err) {
    console.error('Lỗi preview:', err)
  } finally {
    previewLoading.value = false
  }
}

let timeoutId: any = null
watch(form, () => {
  if (timeoutId) clearTimeout(timeoutId)
  timeoutId = setTimeout(() => {
    loadPreview()
  }, 500)
}, { deep: true })

const saveRules = async () => {
  saving.value = true
  try {
    await fetchApi(`/projects/${props.projectId}/naming-rule`, {
      method: 'PUT',
      body: form.value
    })
    toast.success('Đã lưu cấu hình đặt tên thành công!')
  } catch (err: any) {
    toast.error(err.data?.message || 'Có lỗi khi lưu cấu hình.')
  } finally {
    saving.value = false
  }
}

const insertVariable = (variable: string, target: 'name' | 'folder') => {
  if (target === 'name') {
    form.value.namingRule += (form.value.namingRule && !form.value.namingRule.endsWith('_') ? '_' : '') + variable
  } else {
    form.value.folderRule += (form.value.folderRule && !form.value.folderRule.endsWith('/') ? '/' : '') + variable
  }
}

onMounted(() => {
  loadRules()
})
</script>

<template>
  <div class="bg-white rounded-lg shadow-sm border border-gray-200">
    <div class="p-6">
      <div class="sm:flex sm:items-center sm:justify-between mb-6 border-b border-gray-200 pb-4">
        <div class="sm:flex-auto">
          <h2 class="text-xl font-semibold text-gray-900">Quy tắc Đặt tên (Naming & Folder Rules)</h2>
          <p class="mt-2 text-sm text-gray-700">Cấu hình tự động đổi tên file và phân bổ thư mục sau khi hệ thống bóc tách xong.</p>
        </div>
        <div class="mt-4 sm:mt-0 flex items-center space-x-4">
          <button @click="saveRules" :disabled="saving" class="inline-flex items-center justify-center rounded-md border border-transparent bg-primary-600 px-4 py-2 text-sm font-medium text-white shadow-sm hover:bg-primary-700 disabled:bg-primary-400">
            {{ saving ? 'Đang lưu...' : 'Lưu cấu hình' }}
          </button>
        </div>
      </div>

      <div class="grid grid-cols-1 lg:grid-cols-3 gap-8">
        <!-- Cột trái: Form nhập -->
        <div class="lg:col-span-2 space-y-8">
          
          <!-- Folder Rule -->
          <div class="bg-gray-50 p-4 rounded-lg border border-gray-200">
            <h3 class="text-lg font-medium text-gray-900 mb-4">Cấu trúc thư mục (Folder Rule)</h3>
            <div>
              <label class="block text-sm font-medium text-gray-700 mb-1">Mẫu thư mục</label>
              <div class="flex">
                <input v-model="form.folderRule" type="text" class="block w-full rounded-md border-gray-300 shadow-sm focus:border-primary-500 focus:ring-primary-500 sm:text-sm" placeholder="{Year}/{Month}" />
              </div>
              <p class="mt-2 text-xs text-gray-500">Sử dụng dấu <code>/</code> để tạo các thư mục con.</p>
            </div>
            <div class="mt-4">
              <p class="text-xs font-medium text-gray-700 mb-2">Thêm nhanh biến (Click để chèn):</p>
              <div class="flex flex-wrap gap-2">
                <button v-for="v in availableVariables" :key="'f_'+v.key" @click="insertVariable(v.key, 'folder')" type="button" class="inline-flex items-center rounded bg-white px-2 py-1 text-xs font-medium text-gray-700 shadow-sm ring-1 ring-inset ring-gray-300 hover:bg-gray-50" :title="v.desc">
                  {{ v.key }}
                </button>
              </div>
            </div>
          </div>

          <!-- Naming Rule -->
          <div class="bg-gray-50 p-4 rounded-lg border border-gray-200">
            <h3 class="text-lg font-medium text-gray-900 mb-4">Quy tắc tên file (File Name Rule)</h3>
            <div>
              <label class="block text-sm font-medium text-gray-700 mb-1">Mẫu tên file</label>
              <div class="flex items-center">
                <input v-model="form.namingRule" type="text" class="block w-full rounded-md border-gray-300 shadow-sm focus:border-primary-500 focus:ring-primary-500 sm:text-sm" placeholder="{LabelCode}_{Year}_{AutoIncrement}" />
                <span class="ml-3 text-gray-500 font-medium">.pdf</span>
              </div>
              <p class="mt-2 text-xs text-gray-500">Hệ thống sẽ tự động gán phần mở rộng (.pdf, .jpg, v.v.) dựa theo file gốc.</p>
            </div>
            <div class="mt-4">
              <p class="text-xs font-medium text-gray-700 mb-2">Thêm nhanh biến (Click để chèn):</p>
              <div class="flex flex-wrap gap-2">
                <button v-for="v in availableVariables" :key="'n_'+v.key" @click="insertVariable(v.key, 'name')" type="button" class="inline-flex items-center rounded bg-white px-2 py-1 text-xs font-medium text-gray-700 shadow-sm ring-1 ring-inset ring-gray-300 hover:bg-gray-50" :title="v.desc">
                  {{ v.key }}
                </button>
              </div>
              <p class="mt-3 text-xs text-blue-600">Mẹo: Bạn có thể sử dụng trực tiếp Mã trường dữ liệu (VD: <code>{SoHopDong}</code>) để tự động điền giá trị bóc tách vào tên file.</p>
            </div>
          </div>

        </div>

        <!-- Cột phải: Preview -->
        <div class="lg:col-span-1">
          <div class="sticky top-6 rounded-lg border-2 border-primary-100 bg-primary-50 p-5">
            <h3 class="text-base font-semibold text-primary-900 mb-4 flex items-center">
              <svg class="h-5 w-5 mr-2 text-primary-600" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" /><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" /></svg>
              Xem trước kết quả
            </h3>
            
            <div v-if="previewLoading" class="text-sm text-gray-500 italic">Đang cập nhật...</div>
            <div v-else class="space-y-4">
              <div>
                <p class="text-xs font-medium text-gray-500 uppercase tracking-wider mb-1">Thư mục đầu ra</p>
                <div class="bg-white p-2 rounded border border-primary-200 text-sm font-mono text-gray-800 break-all">
                  {{ projectStore.currentProject?.outputPath || '/data/out' }}/<span class="text-primary-700 font-bold">{{ previewResult.folderPath }}</span>
                </div>
              </div>
              <div>
                <p class="text-xs font-medium text-gray-500 uppercase tracking-wider mb-1">Tên file</p>
                <div class="bg-white p-2 rounded border border-primary-200 text-sm font-mono text-gray-800 break-all">
                  <span class="text-primary-700 font-bold">{{ previewResult.fileName }}</span>
                </div>
              </div>
              <div class="pt-3 border-t border-primary-200">
                <p class="text-xs font-medium text-gray-500 uppercase tracking-wider mb-1">Đường dẫn đầy đủ</p>
                <div class="bg-gray-800 text-green-400 p-3 rounded text-xs font-mono break-all leading-relaxed">
                  {{ previewResult.fullPath }}
                </div>
              </div>
            </div>
            
            <div class="mt-6 text-xs text-gray-500">
              * Dữ liệu mô phỏng dựa trên thời gian hiện tại và metadata mẫu (<code>{SoHopDong}: "HD-12345"</code>).
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
