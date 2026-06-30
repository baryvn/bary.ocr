<script setup lang="ts">
import { ref } from 'vue'
import { useApi } from '~/composables/useApi'
import { useToast } from '~/composables/useToast'

const props = defineProps({
  batchId: { type: String, default: null }, // Nếu có, upload xong gọi API báo cáo
  prefix: { type: String, default: '' },
  keepExactName: { type: Boolean, default: true }
})

const emit = defineEmits(['uploadSuccess', 'uploadError'])
const { fetchApi } = useApi()
const toast = useToast()

const isDragging = ref(false)
const files = ref<Array<{ file: File, progress: number, status: 'pending' | 'uploading' | 'success' | 'error', key?: string, errorMessage?: string }>>([])
const uploadInput = ref<HTMLInputElement | null>(null)

const onDragOver = (e: DragEvent) => {
  e.preventDefault()
  isDragging.value = true
}

const onDragLeave = (e: DragEvent) => {
  e.preventDefault()
  isDragging.value = false
}

const onDrop = (e: DragEvent) => {
  e.preventDefault()
  isDragging.value = false
  if (e.dataTransfer?.files) {
    handleFiles(Array.from(e.dataTransfer.files))
  }
}

const onFileSelect = (e: Event) => {
  const target = e.target as HTMLInputElement
  if (target.files) {
    handleFiles(Array.from(target.files))
  }
}

const handleFiles = (selectedFiles: File[]) => {
  const pdfFiles = selectedFiles.filter(f => f.type === 'application/pdf' || f.name.toLowerCase().endsWith('.pdf'))
  
  if (pdfFiles.length < selectedFiles.length) {
    toast.warning('Chỉ hỗ trợ upload file PDF.')
  }

  const newFiles = pdfFiles.map(file => ({
    file,
    progress: 0,
    status: 'pending' as const
  }))

  files.value = [...files.value, ...newFiles]
  
  // Auto start upload
  uploadNext()
}

const uploadNext = async () => {
  const fileToUpload = files.value.find(f => f.status === 'pending')
  if (!fileToUpload) return // Done all

  fileToUpload.status = 'uploading'
  fileToUpload.progress = 10

  try {
    // 1. Get presigned URL
    const presignRes = await fetchApi<any>(`/storage/presign-put?fileName=${encodeURIComponent(fileToUpload.file.name)}&prefix=${encodeURIComponent(props.prefix)}&keepExactName=${props.keepExactName}`)
    const { uploadUrl, objectKey } = presignRes
    
    fileToUpload.progress = 30

    // 2. Upload file directly to MinIO using standard fetch (or XMLHttpRequest for better progress, but fetch is simpler for now)
    const response = await fetch(uploadUrl, {
      method: 'PUT',
      body: fileToUpload.file,
      headers: {
        'Content-Type': fileToUpload.file.type || 'application/pdf'
      }
    })

    if (!response.ok) {
      throw new Error('Upload to MinIO failed')
    }

    fileToUpload.progress = 100
    fileToUpload.status = 'success'
    fileToUpload.key = objectKey

    emit('uploadSuccess', { originalName: fileToUpload.file.name, objectKey, size: fileToUpload.file.size })
  } catch (err: any) {
    console.error('Lỗi upload:', err)
    fileToUpload.status = 'error'
    fileToUpload.errorMessage = err.message || 'Lỗi mạng'
    emit('uploadError', { fileName: fileToUpload.file.name, error: err })
  } finally {
    // Recursively upload next
    uploadNext()
  }
}

const removeFile = (index: number) => {
  files.value.splice(index, 1)
}

const triggerSelect = () => {
  uploadInput.value?.click()
}
</script>

<template>
  <div class="space-y-4">
    <!-- Drag Drop Zone -->
    <div 
      @dragover="onDragOver" 
      @dragleave="onDragLeave" 
      @drop="onDrop"
      @click="triggerSelect"
      :class="[
        'mt-1 flex justify-center rounded-md border-2 border-dashed px-6 pt-5 pb-6 cursor-pointer transition-colors',
        isDragging ? 'border-primary-500 bg-primary-50' : 'border-gray-300 hover:border-primary-400 hover:bg-gray-50'
      ]"
    >
      <div class="space-y-1 text-center">
        <svg class="mx-auto h-12 w-12 text-gray-400" stroke="currentColor" fill="none" viewBox="0 0 48 48" aria-hidden="true">
          <path d="M28 8H12a4 4 0 00-4 4v20m32-12v8m0 0v8a4 4 0 01-4 4H12a4 4 0 01-4-4v-4m32-4l-3.172-3.172a4 4 0 00-5.656 0L28 28M8 32l9.172-9.172a4 4 0 015.656 0L28 28m0 0l4 4m4-24h8m-4-4v8m-12 4h.02" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" />
        </svg>
        <div class="flex text-sm text-gray-600 justify-center">
          <span class="relative rounded-md font-medium text-primary-600 focus-within:outline-none focus-within:ring-2 focus-within:ring-primary-500 focus-within:ring-offset-2 hover:text-primary-500">
            Kéo thả hoặc nhấn vào đây để chọn file
          </span>
          <input ref="uploadInput" type="file" multiple accept=".pdf,application/pdf" class="sr-only" @change="onFileSelect" />
        </div>
        <p class="text-xs text-gray-500">Chỉ hỗ trợ file PDF (Tối đa 50MB/file)</p>
      </div>
    </div>

    <!-- File List -->
    <ul v-if="files.length > 0" class="divide-y divide-gray-200 rounded-md border border-gray-200">
      <li v-for="(item, index) in files" :key="index" class="flex items-center justify-between py-3 pl-3 pr-4 text-sm">
        <div class="flex w-0 flex-1 items-center">
          <!-- Icon -->
          <svg class="h-5 w-5 flex-shrink-0 text-gray-400" viewBox="0 0 20 20" fill="currentColor" aria-hidden="true">
            <path fill-rule="evenodd" d="M15.621 4.379a3 3 0 00-4.242 0l-7 7a3 3 0 004.241 4.243h.001l.497-.5a.75.75 0 011.064 1.057l-.498.501-.002.002a4.5 4.5 0 01-6.364-6.364l7-7a4.5 4.5 0 016.368 6.36l-3.455 3.553A2.625 2.625 0 119.52 9.52l3.45-3.451a.75.75 0 111.061 1.06l-3.45 3.451a1.125 1.125 0 001.587 1.595l3.454-3.553a3 3 0 000-4.242z" clip-rule="evenodd" />
          </svg>
          <div class="ml-2 w-full flex-1 truncate">
            <span class="truncate font-medium">{{ item.file.name }}</span>
            
            <!-- Progress bar -->
            <div v-if="item.status === 'uploading'" class="mt-1 w-full bg-gray-200 rounded-full h-1.5">
              <div class="bg-primary-600 h-1.5 rounded-full transition-all duration-300" :style="{ width: item.progress + '%' }"></div>
            </div>
            <div v-else-if="item.status === 'error'" class="mt-1 text-xs text-red-500">
              {{ item.errorMessage }}
            </div>
          </div>
        </div>
        <div class="ml-4 flex-shrink-0 flex items-center space-x-2">
          <span v-if="item.status === 'success'" class="text-green-500">
            <svg class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" /></svg>
          </span>
          <button @click="removeFile(index)" class="font-medium text-gray-400 hover:text-red-500">
            <svg class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" /></svg>
          </button>
        </div>
      </li>
    </ul>
  </div>
</template>
