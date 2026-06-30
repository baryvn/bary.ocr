<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useCookie } from '#app'
import { useApi } from '~/composables/useApi'

definePageMeta({
  layout: 'auth'
})

const username = ref('admin')
const password = ref('Admin@123')
const errorMsg = ref('')
const loading = ref(false)
const router = useRouter()
const token = useCookie('auth_token')
const { fetchApi } = useApi()

const handleLogin = async () => {
  loading.value = true
  errorMsg.value = ''
  try {
    const res = await fetchApi<{ accessToken: string }>('/auth/login', {
      method: 'POST',
      body: { username: username.value, password: password.value }
    })
    token.value = res.accessToken
    router.push('/')
  } catch (err: any) {
    errorMsg.value = err.data?.message || 'Đăng nhập thất bại'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="sm:mx-auto sm:w-full sm:max-w-md">
    <h2 class="mt-6 text-center text-3xl font-extrabold text-gray-900">Đăng nhập BARY OCR</h2>
  </div>

  <div class="mt-8 sm:mx-auto sm:w-full sm:max-w-md">
    <div class="bg-white py-8 px-4 shadow sm:rounded-lg sm:px-10">
      <form class="space-y-6" @submit.prevent="handleLogin">
        <div>
          <label for="username" class="block text-sm font-medium text-gray-700">Tên đăng nhập</label>
          <div class="mt-1">
            <input id="username" v-model="username" name="username" type="text" required class="appearance-none block w-full px-3 py-2 border border-gray-300 rounded-md shadow-sm focus:outline-none focus:ring-primary-500 focus:border-primary-500 sm:text-sm" />
          </div>
        </div>

        <div>
          <label for="password" class="block text-sm font-medium text-gray-700">Mật khẩu</label>
          <div class="mt-1">
            <input id="password" v-model="password" name="password" type="password" required class="appearance-none block w-full px-3 py-2 border border-gray-300 rounded-md shadow-sm focus:outline-none focus:ring-primary-500 focus:border-primary-500 sm:text-sm" />
          </div>
        </div>

        <div v-if="errorMsg" class="text-red-500 text-sm">{{ errorMsg }}</div>

        <div>
          <button type="submit" :disabled="loading" class="w-full flex justify-center py-2 px-4 border border-transparent rounded-md shadow-sm text-sm font-medium text-white bg-primary-600 hover:bg-primary-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-primary-500 disabled:opacity-50">
            {{ loading ? 'Đang đăng nhập...' : 'Đăng nhập' }}
          </button>
        </div>
      </form>
    </div>
  </div>
</template>
