<script setup lang="ts">
import { ref, computed } from 'vue'

const props = defineProps({
  node: Object as () => any,
  selectedId: String,
  depth: { type: Number, default: 1 }
})

const emit = defineEmits(['select', 'add-child', 'edit', 'delete'])

const expanded = ref(true)

const toggleExpand = (e: Event) => {
  e.stopPropagation()
  expanded.value = !expanded.value
}

const selectNode = () => {
  emit('select', props.node)
}

const isSelected = computed(() => props.selectedId === props.node?.id)

// Bubble up events from children
const onSelect = (node: any) => emit('select', node)
const onAddChild = (node: any) => emit('add-child', node)
const onEdit = (node: any) => emit('edit', node)
const onDelete = (node: any) => emit('delete', node)
</script>

<template>
  <li class="my-1">
    <div 
      @click="selectNode"
      :class="['flex items-center justify-between p-2 rounded-md cursor-pointer transition-colors group', 
               isSelected ? 'bg-primary-50 border border-primary-200' : 'hover:bg-gray-50 border border-transparent']"
    >
      <div class="flex items-center flex-1 min-w-0" :style="{ paddingLeft: `${(depth - 1) * 1.5}rem` }">
        <button v-if="node.children && node.children.length" @click="toggleExpand" class="p-1 text-gray-400 hover:text-gray-600 focus:outline-none">
          <svg v-if="expanded" class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7" /></svg>
          <svg v-else class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7" /></svg>
        </button>
        <div v-else class="w-6"></div> <!-- spacer -->
        
        <span :class="['ml-2 truncate font-medium text-sm', isSelected ? 'text-primary-700' : 'text-gray-700']">
          {{ node.name }}
          <span class="text-xs text-gray-400 font-normal ml-1" v-if="node.code">({{ node.code }})</span>
        </span>
      </div>

      <div class="flex items-center space-x-1 opacity-0 group-hover:opacity-100 transition-opacity">
        <button v-if="depth < 3" @click.stop="() => emit('add-child', node)" title="Thêm nhãn con" class="p-1 text-gray-400 hover:text-green-600">
          <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" /></svg>
        </button>
        <button @click.stop="() => emit('edit', node)" title="Sửa" class="p-1 text-gray-400 hover:text-primary-600">
          <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" /></svg>
        </button>
        <button @click.stop="() => emit('delete', node)" title="Xóa" class="p-1 text-gray-400 hover:text-red-600">
          <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" /></svg>
        </button>
      </div>
    </div>

    <!-- Children -->
    <ul v-if="expanded && node.children && node.children.length" class="mt-1">
      <TreeNode 
        v-for="child in node.children" 
        :key="child.id" 
        :node="child" 
        :selectedId="selectedId" 
        :depth="depth + 1"
        @select="onSelect"
        @add-child="onAddChild"
        @edit="onEdit"
        @delete="onDelete"
      />
    </ul>
  </li>
</template>
