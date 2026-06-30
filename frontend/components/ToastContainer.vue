<script setup lang="ts">
import { useToast } from '~/composables/useToast'

const { toasts, remove } = useToast()

const getIconClass = (type: string) => {
  switch (type) {
    case 'success': return 'text-green-400'
    case 'error': return 'text-red-400'
    case 'warning': return 'text-yellow-400'
    default: return 'text-blue-400'
  }
}

const getBgClass = (type: string) => {
  switch (type) {
    case 'success': return 'bg-green-50 border-green-200'
    case 'error': return 'bg-red-50 border-red-200'
    case 'warning': return 'bg-yellow-50 border-yellow-200'
    default: return 'bg-blue-50 border-blue-200'
  }
}
</script>

<template>
  <div aria-live="assertive" class="fixed inset-0 flex items-end px-4 py-6 pointer-events-none sm:p-6 sm:items-start z-50">
    <div class="w-full flex flex-col items-center space-y-4 sm:items-end">
      <TransitionGroup name="toast" tag="div" class="flex flex-col gap-2 w-full max-w-sm">
        <div v-for="toast in toasts" :key="toast.id" :class="['pointer-events-auto rounded-lg shadow-lg ring-1 ring-black ring-opacity-5 overflow-hidden border', getBgClass(toast.type)]">
          <div class="p-4">
            <div class="flex items-start">
              <div class="flex-shrink-0">
                <!-- Icon -->
                <svg v-if="toast.type === 'success'" class="h-6 w-6" :class="getIconClass(toast.type)" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
                </svg>
                <svg v-else-if="toast.type === 'error'" class="h-6 w-6" :class="getIconClass(toast.type)" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                </svg>
                <svg v-else class="h-6 w-6" :class="getIconClass(toast.type)" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                </svg>
              </div>
              <div class="ml-3 w-0 flex-1 pt-0.5">
                <p class="text-sm font-medium text-gray-900">{{ toast.message }}</p>
              </div>
              <div class="ml-4 flex-shrink-0 flex">
                <button @click="remove(toast.id)" class="bg-transparent rounded-md inline-flex text-gray-400 hover:text-gray-500 focus:outline-none">
                  <span class="sr-only">Close</span>
                  <svg class="h-5 w-5" viewBox="0 0 20 20" fill="currentColor">
                    <path fill-rule="evenodd" d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z" clip-rule="evenodd" />
                  </svg>
                </button>
              </div>
            </div>
          </div>
        </div>
      </TransitionGroup>
    </div>
  </div>
</template>

<style scoped>
.toast-enter-active,
.toast-leave-active {
  transition: all 0.3s ease;
}
.toast-enter-from {
  opacity: 0;
  transform: translateX(30px);
}
.toast-leave-to {
  opacity: 0;
  transform: translateX(30px);
}
</style>
