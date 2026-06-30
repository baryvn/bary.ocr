<script setup lang="ts">
const props = defineProps({
  treeData: Array as () => Array<any>,
  selectedId: String
})

const emit = defineEmits(['select', 'add-child', 'edit', 'delete', 'add-root'])
</script>

<template>
  <div class="bg-white rounded-lg shadow-sm border border-gray-200 flex flex-col h-full">
    <div class="p-4 border-b border-gray-200 flex justify-between items-center bg-gray-50 rounded-t-lg">
      <h3 class="text-sm font-medium text-gray-900">Cấu trúc Nhãn (Labels)</h3>
      <button @click="emit('add-root')" class="text-primary-600 hover:text-primary-700 text-sm font-medium flex items-center">
        <svg class="h-4 w-4 mr-1" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" /></svg>
        Thêm nhãn gốc
      </button>
    </div>
    
    <div class="flex-1 overflow-y-auto p-4">
      <div v-if="!treeData || treeData.length === 0" class="text-center text-sm text-gray-500 py-10">
        Chưa có nhãn nào. Hãy thêm nhãn gốc đầu tiên.
      </div>
      <ul v-else class="space-y-1">
        <TreeNode 
          v-for="node in treeData" 
          :key="node.id" 
          :node="node" 
          :selectedId="selectedId"
          @select="(n) => emit('select', n)"
          @add-child="(n) => emit('add-child', n)"
          @edit="(n) => emit('edit', n)"
          @delete="(n) => emit('delete', n)"
        />
      </ul>
    </div>
  </div>
</template>
