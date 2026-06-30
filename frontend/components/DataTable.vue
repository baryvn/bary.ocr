<script setup lang="ts">
import { ref, computed } from 'vue'

const props = defineProps({
  columns: Array as () => Array<{ key: string, label: string }>,
  data: Array as () => Array<any>,
  total: Number,
  page: Number,
  pageSize: Number,
  loading: Boolean
})

const emit = defineEmits(['page-change', 'edit', 'delete'])

const totalPages = computed(() => Math.ceil((props.total || 0) / (props.pageSize || 10)))

const nextPage = () => {
  if (props.page! < totalPages.value) emit('page-change', props.page! + 1)
}

const prevPage = () => {
  if (props.page! > 1) emit('page-change', props.page! - 1)
}
</script>

<template>
  <div class="mt-8 flow-root">
    <div class="-mx-4 -my-2 overflow-x-auto sm:-mx-6 lg:-mx-8">
      <div class="inline-block min-w-full py-2 align-middle sm:px-6 lg:px-8">
        <div class="overflow-hidden shadow ring-1 ring-black ring-opacity-5 sm:rounded-lg">
          <table class="min-w-full divide-y divide-gray-300">
            <thead class="bg-gray-50">
              <tr>
                <th v-for="col in columns" :key="col.key" scope="col" class="py-3.5 pl-4 pr-3 text-left text-sm font-semibold text-gray-900 sm:pl-6">
                  {{ col.label }}
                </th>
                <th scope="col" class="relative py-3.5 pl-3 pr-4 sm:pr-6">
                  <span class="sr-only">Hành động</span>
                </th>
              </tr>
            </thead>
            <tbody class="divide-y divide-gray-200 bg-white">
              <tr v-if="loading">
                <td :colspan="columns?.length! + 1" class="py-10 text-center text-sm text-gray-500">Đang tải dữ liệu...</td>
              </tr>
              <tr v-else-if="!data || data.length === 0">
                <td :colspan="columns?.length! + 1" class="py-10 text-center text-sm text-gray-500">Không có dữ liệu</td>
              </tr>
              <tr v-else v-for="(row, idx) in data" :key="row.id || idx">
                <td v-for="col in columns" :key="col.key" class="whitespace-nowrap py-4 pl-4 pr-3 text-sm text-gray-900 sm:pl-6">
                  <slot :name="col.key" :item="row">{{ row[col.key] }}</slot>
                </td>
                <td class="relative whitespace-nowrap py-4 pl-3 pr-4 text-right text-sm font-medium sm:pr-6">
                  <slot name="actions" :item="row">
                    <button @click="emit('edit', row)" class="text-primary-600 hover:text-primary-900 mr-4">Sửa</button>
                    <button @click="emit('delete', row)" class="text-red-600 hover:text-red-900">Xóa</button>
                  </slot>
                </td>
              </tr>
            </tbody>
          </table>
          
          <!-- Pagination -->
          <div class="flex items-center justify-between border-t border-gray-200 bg-white px-4 py-3 sm:px-6">
            <div class="hidden sm:flex sm:flex-1 sm:items-center sm:justify-between">
              <div>
                <p class="text-sm text-gray-700">
                  Hiển thị từ <span class="font-medium">{{ ((page! - 1) * pageSize!) + 1 }}</span> đến <span class="font-medium">{{ Math.min(page! * pageSize!, total || 0) }}</span> trong <span class="font-medium">{{ total }}</span> kết quả
                </p>
              </div>
              <div>
                <nav class="isolate inline-flex -space-x-px rounded-md shadow-sm" aria-label="Pagination">
                  <button @click="prevPage" :disabled="page === 1" class="relative inline-flex items-center rounded-l-md px-2 py-2 text-gray-400 ring-1 ring-inset ring-gray-300 hover:bg-gray-50 disabled:opacity-50">
                    <span class="sr-only">Previous</span>
                    <svg class="h-5 w-5" viewBox="0 0 20 20" fill="currentColor" aria-hidden="true">
                      <path fill-rule="evenodd" d="M12.79 5.23a.75.75 0 01-.02 1.06L8.832 10l3.938 3.71a.75.75 0 11-1.04 1.08l-4.5-4.25a.75.75 0 010-1.08l4.5-4.25a.75.75 0 011.06.02z" clip-rule="evenodd" />
                    </svg>
                  </button>
                  <span class="relative inline-flex items-center px-4 py-2 text-sm font-semibold text-gray-900 ring-1 ring-inset ring-gray-300 focus:z-20 focus:outline-offset-0">
                    Trang {{ page }} / {{ totalPages }}
                  </span>
                  <button @click="nextPage" :disabled="page === totalPages || totalPages === 0" class="relative inline-flex items-center rounded-r-md px-2 py-2 text-gray-400 ring-1 ring-inset ring-gray-300 hover:bg-gray-50 disabled:opacity-50">
                    <span class="sr-only">Next</span>
                    <svg class="h-5 w-5" viewBox="0 0 20 20" fill="currentColor" aria-hidden="true">
                      <path fill-rule="evenodd" d="M7.21 14.77a.75.75 0 01.02-1.06L11.168 10 7.23 6.29a.75.75 0 111.04-1.08l4.5 4.25a.75.75 0 010 1.08l-4.5 4.25a.75.75 0 01-1.06-.02z" clip-rule="evenodd" />
                    </svg>
                  </button>
                </nav>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
