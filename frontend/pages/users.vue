<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useApi } from '~/composables/useApi'
import { useToast } from '~/composables/useToast'
import { useConfirm } from '~/composables/useConfirm'

definePageMeta({
  middleware: ['auth']
})

const { fetchApi } = useApi()
const toast = useToast()
const { showConfirm } = useConfirm()

// Data state
const users = ref([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(10)
const loading = ref(false)

// Form state
const showModal = ref(false)
const form = ref({ id: '', username: '', fullName: '', email: '', role: 2, password: '' })
const isEditing = ref(false)

const columns = [
  { key: 'username', label: 'Tên đăng nhập' },
  { key: 'fullName', label: 'Họ và tên' },
  { key: 'email', label: 'Email' },
  { key: 'role', label: 'Vai trò' },
  { key: 'isActive', label: 'Trạng thái' }
]

const loadUsers = async () => {
  loading.value = true
  try {
    const res = await fetchApi<any>(`/users?page=${page.value}&pageSize=${pageSize.value}`)
    users.value = res.data
    total.value = res.total
  } catch (err) {
    console.error('Lỗi tải danh sách người dùng:', err)
  } finally {
    loading.value = false
  }
}

const handlePageChange = (newPage: number) => {
  page.value = newPage
  loadUsers()
}

const openCreateModal = () => {
  isEditing.value = false
  form.value = { id: '', username: '', fullName: '', email: '', role: 2, password: '' }
  showModal.value = true
}

const openEditModal = (user: any) => {
  isEditing.value = true
  form.value = { 
    id: user.id, 
    username: user.username, 
    fullName: user.fullName, 
    email: user.email || '', 
    role: user.role === 'Admin' ? 0 : (user.role === 'Manager' ? 1 : 2),
    password: ''
  }
  showModal.value = true
}

const submitForm = () => {
  const btn = document.getElementById('submitUserBtn')
  if (btn) btn.click()
}

const saveUser = async () => {
  try {
    if (isEditing.value) {
      await fetchApi(`/users/${form.value.id}`, {
        method: 'PUT',
        body: { 
          fullName: form.value.fullName, 
          email: form.value.email, 
          role: form.value.role 
        }
      })
    } else {
      await fetchApi('/users', {
        method: 'POST',
        body: form.value
      })
    }
    showModal.value = false
    loadUsers()
  } catch (err) {
    console.error('Lỗi lưu người dùng:', err)
    toast.error('Có lỗi xảy ra khi lưu.')
  }
}

const deleteUser = async (user: any) => {
  if (await showConfirm(`Bạn có chắc chắn muốn xóa/vô hiệu hóa user ${user.username}?`)) {
    try {
      await fetchApi(`/users/${user.id}`, { method: 'DELETE' })
      toast.success('Đã xóa người dùng.')
      loadUsers()
    } catch (err) {
      console.error('Lỗi xóa người dùng:', err)
      toast.error('Lỗi xóa người dùng.')
    }
  }
}

const showResetModal = ref(false)
const resetForm = ref({ userId: '', username: '', newPassword: '' })

const openResetPasswordModal = (user: any) => {
  resetForm.value = { userId: user.id, username: user.username, newPassword: '' }
  showResetModal.value = true
}

const submitResetPassword = () => {
  const btn = document.getElementById('submitResetBtn')
  if (btn) btn.click()
}

const resetPassword = async () => {
  try {
    await fetchApi(`/users/${resetForm.value.userId}/reset-password`, {
      method: 'PUT',
      body: { newPassword: resetForm.value.newPassword }
    })
    toast.success(`Đã đổi mật khẩu cho người dùng ${resetForm.value.username}.`)
    showResetModal.value = false
  } catch (err) {
    console.error('Lỗi reset mật khẩu:', err)
    toast.error('Có lỗi xảy ra khi đổi mật khẩu.')
  }
}

onMounted(() => {
  loadUsers()
})
</script>

<template>
  <div>
    <div class="sm:flex sm:items-center">
      <div class="sm:flex-auto">
        <h1 class="text-xl font-semibold text-gray-900">Người dùng</h1>
        <p class="mt-2 text-sm text-gray-700">Quản lý danh sách người dùng trong hệ thống BARY OCR.</p>
      </div>
      <div class="mt-4 sm:ml-16 sm:mt-0 sm:flex-none">
        <button @click="openCreateModal" type="button" class="block rounded-md bg-primary-600 px-3 py-2 text-center text-sm font-semibold text-white shadow-sm hover:bg-primary-500">Thêm người dùng</button>
      </div>
    </div>

    <DataTable 
      :columns="columns" 
      :data="users" 
      :total="total" 
      :page="page" 
      :pageSize="pageSize" 
      :loading="loading"
      @page-change="handlePageChange"
      @edit="openEditModal"
      @delete="deleteUser"
    >
      <template #role="{ item }">
        <StatusBadge :status="item.role" type="role" />
      </template>
      <template #isActive="{ item }">
        <StatusBadge :status="item.isActive ? 'true' : 'false'" type="status" />
      </template>
      <template #actions="{ item }">
        <button @click="openResetPasswordModal(item)" class="text-yellow-600 hover:text-yellow-900 mr-4" title="Reset mật khẩu">
          <svg class="h-5 w-5 inline" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 7a2 2 0 012 2m4 0a6 6 0 01-7.743 5.743L11 17H9v2H7v2H4a1 1 0 01-1-1v-2.586a1 1 0 01.293-.707l5.964-5.964A6 6 0 1121 9z" />
          </svg>
        </button>
        <button @click="openEditModal(item)" class="text-primary-600 hover:text-primary-900 mr-4">Sửa</button>
        <button @click="deleteUser(item)" class="text-red-600 hover:text-red-900">Xóa</button>
      </template>
    </DataTable>

    <Modal v-model="showModal" :title="isEditing ? 'Sửa người dùng' : 'Thêm người dùng mới'">
      <form @submit.prevent="saveUser" class="space-y-4">
        <div>
          <label class="block text-sm font-bold text-gray-700 mb-1">Tên đăng nhập <span class="text-red-500">*</span></label>
          <input v-model="form.username" :disabled="isEditing" required type="text" class="block w-full rounded-md border-gray-300 shadow-sm focus:border-primary-500 focus:ring-primary-500 sm:text-sm px-3 py-2 disabled:bg-gray-100 disabled:text-gray-500" />
        </div>
        <div v-if="!isEditing">
          <label class="block text-sm font-bold text-gray-700 mb-1">Mật khẩu <span class="text-red-500">*</span></label>
          <input v-model="form.password" required type="password" class="block w-full rounded-md border-gray-300 shadow-sm focus:border-primary-500 focus:ring-primary-500 sm:text-sm px-3 py-2" />
        </div>
        <div>
          <label class="block text-sm font-bold text-gray-700 mb-1">Họ và tên <span class="text-red-500">*</span></label>
          <input v-model="form.fullName" required type="text" class="block w-full rounded-md border-gray-300 shadow-sm focus:border-primary-500 focus:ring-primary-500 sm:text-sm px-3 py-2" />
        </div>
        <div>
          <label class="block text-sm font-bold text-gray-700 mb-1">Email</label>
          <input v-model="form.email" type="email" class="block w-full rounded-md border-gray-300 shadow-sm focus:border-primary-500 focus:ring-primary-500 sm:text-sm px-3 py-2" />
        </div>
        <div>
          <label class="block text-sm font-bold text-gray-700 mb-1">Vai trò</label>
          <select v-model="form.role" class="block w-full rounded-md border-gray-300 shadow-sm focus:border-primary-500 focus:ring-primary-500 sm:text-sm px-3 py-2">
            <option :value="0">Admin</option>
            <option :value="1">Manager</option>
            <option :value="2">Reviewer</option>
          </select>
        </div>
        <div class="hidden">
          <button type="submit" id="submitUserBtn">Submit</button>
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

    <!-- Modal Reset Password -->
    <Modal v-model="showResetModal" title="Reset mật khẩu">
      <form @submit.prevent="resetPassword" class="space-y-4">
        <p class="text-sm text-gray-600">
          Đặt lại mật khẩu cho người dùng: <strong>{{ resetForm.username }}</strong>
        </p>
        <div>
          <label class="block text-sm font-bold text-gray-700 mb-1">Mật khẩu mới <span class="text-red-500">*</span></label>
          <input v-model="resetForm.newPassword" required type="password" class="block w-full rounded-md border-gray-300 shadow-sm focus:border-primary-500 focus:ring-primary-500 sm:text-sm px-3 py-2" />
        </div>
        <div class="hidden">
          <button type="submit" id="submitResetBtn">Submit</button>
        </div>
      </form>
      <template #footer>
        <button type="button" @click="showResetModal = false" class="inline-flex w-full justify-center items-center rounded-md bg-white px-4 py-2 text-sm font-medium text-gray-700 shadow-sm border border-gray-300 hover:bg-gray-50 sm:w-auto">
          Hủy
        </button>
        <button type="button" @click="submitResetPassword" class="mt-3 sm:mt-0 inline-flex w-full justify-center items-center rounded-md bg-primary-700 px-4 py-2 text-sm font-medium text-white shadow-sm hover:bg-primary-600 sm:ml-3 sm:w-auto">
          <svg class="-ml-1 mr-2 h-4 w-4 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 7H5a2 2 0 00-2 2v9a2 2 0 002 2h14a2 2 0 002-2V9a2 2 0 00-2-2h-3m-1 4l-3 3m0 0l-3-3m3 3V4" />
          </svg>
          Lưu
        </button>
      </template>
    </Modal>
  </div>
</template>
