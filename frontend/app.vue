<script setup lang="ts">
import { onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import { useCookie } from '#app'
import { useApi } from '~/composables/useApi'
import { useToast } from '~/composables/useToast'

const router = useRouter()
const { fetchApi } = useApi()
const token = useCookie('auth_token')
const toast = useToast()

const INACTIVITY_TIMEOUT = 30 * 60 * 1000 // 30 minutes
const REFRESH_THRESHOLD = 5 * 60 * 1000 // 5 minutes before expiry

let lastActivityTime = Date.now()
let intervalId: any = null

const updateActivity = () => {
  lastActivityTime = Date.now()
}

const parseJwt = (t: string) => {
  try {
    return JSON.parse(atob(t.split('.')[1]))
  } catch (e) {
    return null
  }
}

const checkSession = async () => {
  if (!token.value) return

  // Check inactivity
  if (Date.now() - lastActivityTime > INACTIVITY_TIMEOUT) {
    token.value = null
    toast.warning('Phiên đăng nhập đã hết hạn do không có tương tác.')
    router.push('/login')
    return
  }

  // Check token expiry
  const decoded = parseJwt(token.value)
  if (decoded && decoded.exp) {
    const timeToExpiry = decoded.exp * 1000 - Date.now()
    if (timeToExpiry > 0 && timeToExpiry < REFRESH_THRESHOLD) {
      try {
        const res = await fetchApi<any>('/auth/refresh', { method: 'GET' })
        if (res && res.accessToken) {
          token.value = res.accessToken
        }
      } catch (err) {
        console.error('Không thể refresh token:', err)
      }
    } else if (timeToExpiry <= 0) {
      // Token expired
      token.value = null
      toast.warning('Phiên đăng nhập đã hết hạn.')
      router.push('/login')
    }
  }
}

onMounted(() => {
  if (process.client) {
    window.addEventListener('mousemove', updateActivity)
    window.addEventListener('keydown', updateActivity)
    window.addEventListener('click', updateActivity)
    window.addEventListener('scroll', updateActivity)
    
    // Check every minute
    intervalId = setInterval(checkSession, 60 * 1000)
  }
})

onUnmounted(() => {
  if (process.client) {
    window.removeEventListener('mousemove', updateActivity)
    window.removeEventListener('keydown', updateActivity)
    window.removeEventListener('click', updateActivity)
    window.removeEventListener('scroll', updateActivity)
    if (intervalId) clearInterval(intervalId)
  }
})
</script>

<template>
  <div>
    <Head>
      <Title>BARY OCR</Title>
    </Head>
    <NuxtLayout>
      <NuxtPage />
    </NuxtLayout>
  </div>
</template>
