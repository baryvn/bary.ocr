<script setup lang="ts">
import { ref, watch, onMounted, onUnmounted } from 'vue'
import { useCookie } from '#app'
import { useRuntimeConfig } from '#app'

const props = defineProps({
  documentId: { type: String, required: true },
  mode: { type: String, default: 'ocr' } // 'original' or 'ocr'
})

const config = useRuntimeConfig()
const apiUrl = config.public.apiBase

const pdfUrl = ref<string>('')
const loading = ref(false)
const error = ref<string | null>(null)

const loadPdf = async () => {
  if (!props.documentId) return
  
  loading.value = true
  error.value = null

  if (pdfUrl.value) {
    URL.revokeObjectURL(pdfUrl.value)
    pdfUrl.value = ''
  }

  try {
    const token = useCookie('auth_token').value
    const endpoint = `${apiUrl}/documents/${props.documentId}/${props.mode === 'ocr' ? 'ocr-pdf' : 'original'}`
    const response = await fetch(endpoint, {
      headers: {
        'Authorization': `Bearer ${token}`
      }
    })

    if (!response.ok) {
      if (response.status === 404) {
        throw new Error('Không tìm thấy file PDF (có thể chưa OCR xong)')
      }
      throw new Error(`Lỗi tải file: ${response.statusText}`)
    }

    const blob = await response.blob()
    pdfUrl.value = URL.createObjectURL(blob)
  } catch (err: any) {
    console.error('Lỗi load PDF:', err)
    error.value = err.message
  } finally {
    loading.value = false
  }
}

watch(() => props.mode, () => {
  loadPdf()
})

onMounted(() => {
  loadPdf()
})

onUnmounted(() => {
  if (pdfUrl.value) {
    URL.revokeObjectURL(pdfUrl.value)
  }
})
</script>

<template>
  <div class="h-full w-full bg-gray-100 rounded-lg overflow-hidden border border-gray-300 relative">
    <div v-if="loading" class="absolute inset-0 flex items-center justify-center bg-white bg-opacity-75 z-10">
      <div class="text-primary-600 font-medium">Đang tải PDF...</div>
    </div>
    
    <div v-if="error" class="absolute inset-0 flex items-center justify-center bg-white z-10">
      <div class="text-red-500 font-medium text-center">
        <svg class="mx-auto h-8 w-8 mb-2" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>
        {{ error }}
      </div>
    </div>

    <iframe v-if="pdfUrl" :src="pdfUrl" class="w-full h-full border-0" title="PDF Viewer"></iframe>
  </div>
</template>
