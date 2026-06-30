<script setup lang="ts">
import { useConfirm } from '~/composables/useConfirm'

const { state, handleConfirm, handleCancel } = useConfirm()
</script>

<template>
  <Modal v-model="state.isOpen" :title="state.title" @close="handleCancel">
    <div class="mt-2 text-sm text-gray-500 whitespace-pre-line">
      {{ state.message }}
    </div>
    <div class="mt-5 sm:mt-6 sm:grid sm:grid-cols-2 sm:gap-3 sm:grid-flow-row-dense">
      <button type="button" @click="handleConfirm" class="w-full inline-flex justify-center rounded-md border border-transparent shadow-sm px-4 py-2 bg-primary-600 text-base font-medium text-white hover:bg-primary-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-primary-500 sm:col-start-2 sm:text-sm">
        Đồng ý
      </button>
      <button type="button" @click="handleCancel" class="mt-3 w-full inline-flex justify-center rounded-md border border-gray-300 shadow-sm px-4 py-2 bg-white text-base font-medium text-gray-700 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-primary-500 sm:mt-0 sm:col-start-1 sm:text-sm">
        Hủy bỏ
      </button>
    </div>
  </Modal>
</template>
