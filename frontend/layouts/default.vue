<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useCookie } from '#app'
import { useApi } from '~/composables/useApi'
import { useToast } from '~/composables/useToast'

const router = useRouter()
const { fetchApi } = useApi()
const toast = useToast()
const user = ref<any>(null)
const showUserMenu = ref(false)

const handleLogout = () => {
  const token = useCookie('auth_token')
  token.value = null
  router.push('/login')
}

const showChangePasswordModal = ref(false)
const changePasswordForm = ref({ oldPassword: '', newPassword: '' })

const openChangePasswordModal = () => {
  changePasswordForm.value = { oldPassword: '', newPassword: '' }
  showUserMenu.value = false
  showChangePasswordModal.value = true
}

const submitChangePassword = () => {
  const btn = document.getElementById('submitChangePasswordBtn')
  if (btn) btn.click()
}

const changePassword = async () => {
  try {
    await fetchApi('/auth/password', {
      method: 'PUT',
      body: changePasswordForm.value
    })
    toast.success('Đã đổi mật khẩu thành công.')
    showChangePasswordModal.value = false
  } catch (err: any) {
    console.error('Lỗi đổi mật khẩu:', err)
    toast.error(err.data?.message || 'Lỗi đổi mật khẩu.')
  }
}

onMounted(async () => {
  try {
    user.value = await fetchApi('/auth/me')
  } catch (err) {
    // ignore
  }
})
</script>

<template>
  <div class="h-screen flex overflow-hidden bg-gray-100">
    <!-- Sidebar -->
    <div class="hidden md:flex md:flex-shrink-0">
      <div class="flex flex-col w-64 bg-gray-800 border-r border-gray-200">
        <div class="flex items-center justify-center h-16 bg-gray-900 border-b border-gray-700">
          <span class="text-white text-lg font-bold">BARY OCR</span>
        </div>
        <div class="flex-1 flex flex-col overflow-y-auto">
          <nav class="flex-1 px-2 py-4 space-y-1">
            <NuxtLink v-if="user?.role === 'Manager'" to="/" class="text-gray-300 hover:bg-gray-700 hover:text-white group flex items-center px-2 py-2 text-sm font-medium rounded-md">
              Dashboard
            </NuxtLink>
            <NuxtLink v-if="user?.role === 'Manager'" to="/projects" class="text-gray-300 hover:bg-gray-700 hover:text-white group flex items-center px-2 py-2 text-sm font-medium rounded-md">
              Dự án
            </NuxtLink>
            <NuxtLink v-if="user?.role === 'Reviewer' || user?.role === 'Manager'" to="/review" class="text-gray-300 hover:bg-gray-700 hover:text-white group flex items-center px-2 py-2 text-sm font-medium rounded-md">
              Nhiệm vụ Review
            </NuxtLink>
            <NuxtLink v-if="user?.role === 'Admin'" to="/users" class="text-gray-300 hover:bg-gray-700 hover:text-white group flex items-center px-2 py-2 text-sm font-medium rounded-md">
              Người dùng
            </NuxtLink>
          </nav>
        </div>
      </div>
    </div>

    <!-- Main content -->
    <div class="flex flex-col w-0 flex-1 overflow-hidden">
      <div class="relative z-10 flex-shrink-0 flex h-16 bg-white shadow">
        <div class="flex-1 px-4 flex justify-between">
          <div class="flex-1 flex items-center">
            <span class="text-gray-500 font-medium">BARY OCR</span>
          </div>
          <div class="ml-4 flex items-center md:ml-6 space-x-4">
            <!-- Project Selector -->
            <select v-if="$route.path.includes('/projects/')" class="block w-48 pl-3 pr-10 py-2 text-base border-gray-300 focus:outline-none focus:ring-primary-500 focus:border-primary-500 sm:text-sm rounded-md">
              <option>Dự án hiện tại...</option>
            </select>
            <!-- Profile dropdown -->
            <div class="relative">
              <button @click="showUserMenu = !showUserMenu" class="max-w-xs bg-white flex items-center text-sm rounded-full focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-primary-500" id="user-menu-button" aria-expanded="false" aria-haspopup="true">
                <span class="sr-only">Open user menu</span>
                <div class="h-8 w-8 rounded-full bg-gray-200 flex items-center justify-center text-gray-500 hover:bg-gray-300 transition-colors">
                  <svg class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" /></svg>
                </div>
              </button>

              <div v-if="showUserMenu" class="origin-top-right absolute right-0 mt-2 w-48 rounded-md shadow-lg py-1 bg-white ring-1 ring-black ring-opacity-5 focus:outline-none" role="menu" aria-orientation="vertical" aria-labelledby="user-menu-button" tabindex="-1">
                <div class="px-4 py-2 border-b border-gray-100">
                  <p class="text-sm text-gray-500">Đăng nhập với tên</p>
                  <p class="text-sm font-medium text-gray-900 truncate">{{ user?.username || 'User' }}</p>
                </div>
                <button @click="openChangePasswordModal" class="block w-full text-left px-4 py-2 text-sm text-gray-700 hover:bg-gray-100" role="menuitem" tabindex="-1">
                  Đổi mật khẩu
                </button>
                <button @click="handleLogout" class="block w-full text-left px-4 py-2 text-sm text-red-600 hover:bg-gray-100" role="menuitem" tabindex="-1">
                  Đăng xuất
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>

      <main class="flex-1 relative z-0 overflow-y-auto focus:outline-none bg-gray-50">
        <div class="py-6">
          <div class="px-4 sm:px-6 lg:px-8">
            <slot />
          </div>
        </div>
      </main>
    </div>
    <ToastContainer />
    <ConfirmDialog />

    <!-- Modal Change Password -->
    <Modal v-model="showChangePasswordModal" title="Đổi mật khẩu">
      <form @submit.prevent="changePassword" class="space-y-4">
        <div>
          <label class="block text-sm font-bold text-gray-700 mb-1">Mật khẩu cũ <span class="text-red-500">*</span></label>
          <input v-model="changePasswordForm.oldPassword" required type="password" class="block w-full rounded-md border-gray-300 shadow-sm focus:border-primary-500 focus:ring-primary-500 sm:text-sm px-3 py-2" />
        </div>
        <div>
          <label class="block text-sm font-bold text-gray-700 mb-1">Mật khẩu mới <span class="text-red-500">*</span></label>
          <input v-model="changePasswordForm.newPassword" required type="password" class="block w-full rounded-md border-gray-300 shadow-sm focus:border-primary-500 focus:ring-primary-500 sm:text-sm px-3 py-2" />
        </div>
        <div class="hidden">
          <button type="submit" id="submitChangePasswordBtn">Submit</button>
        </div>
      </form>
      <template #footer>
        <button type="button" @click="showChangePasswordModal = false" class="inline-flex w-full justify-center items-center rounded-md bg-white px-4 py-2 text-sm font-medium text-gray-700 shadow-sm border border-gray-300 hover:bg-gray-50 sm:w-auto">
          Hủy
        </button>
        <button type="button" @click="submitChangePassword" class="mt-3 sm:mt-0 inline-flex w-full justify-center items-center rounded-md bg-primary-700 px-4 py-2 text-sm font-medium text-white shadow-sm hover:bg-primary-600 sm:ml-3 sm:w-auto">
          <svg class="-ml-1 mr-2 h-4 w-4 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 7H5a2 2 0 00-2 2v9a2 2 0 002 2h14a2 2 0 002-2V9a2 2 0 00-2-2h-3m-1 4l-3 3m0 0l-3-3m3 3V4" />
          </svg>
          Lưu
        </button>
      </template>
    </Modal>
  </div>
</template>
