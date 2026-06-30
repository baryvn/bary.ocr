<script setup lang="ts">
import { computed } from 'vue'

const props = defineProps({
  status: String,
  type: { type: String, default: 'status' } // 'status' or 'role'
})

const badgeClass = computed(() => {
  if (props.type === 'role') {
    switch (props.status) {
      case 'Admin': return 'bg-purple-100 text-purple-800'
      case 'Manager': return 'bg-blue-100 text-blue-800'
      case 'Reviewer': return 'bg-green-100 text-green-800'
      default: return 'bg-gray-100 text-gray-800'
    }
  } else {
    // True/False string for active status, or other project status
    if (props.status === 'true' || props.status === 'Active') return 'bg-green-100 text-green-800'
    if (props.status === 'false' || props.status === 'Archived') return 'bg-gray-100 text-gray-800'
    return 'bg-gray-100 text-gray-800'
  }
})

const displayValue = computed(() => {
  if (props.type !== 'role') {
    if (props.status === 'true') return 'Hoạt động'
    if (props.status === 'false') return 'Vô hiệu hóa'
  }
  return props.status
})
</script>

<template>
  <span :class="['inline-flex items-center rounded-md px-2 py-1 text-xs font-medium ring-1 ring-inset ring-opacity-20', badgeClass]">
    {{ displayValue }}
  </span>
</template>
