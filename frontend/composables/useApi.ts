import { useRuntimeConfig, useCookie } from '#app'

export const useApi = () => {
  const config = useRuntimeConfig()
  const token = useCookie('auth_token')

  const fetchApi = async <T>(endpoint: string, options: any = {}) => {
    const headers = {
      ...options.headers,
    }

    if (token.value) {
      headers.Authorization = `Bearer ${token.value}`
    }

    try {
      return await $fetch<T>(`${config.public.apiBase}${endpoint}`, {
        ...options,
        headers,
      })
    } catch (error: any) {
      // Handle global error if needed (like 401 redirect to login)
      throw error
    }
  }

  const downloadFile = async (endpoint: string, filename: string) => {
    try {
      const headers: any = {}
      if (token.value) {
        headers.Authorization = `Bearer ${token.value}`
      }
      
      const res: any = await $fetch(`${config.public.apiBase}${endpoint}`, {
        method: 'GET',
        headers,
        responseType: 'blob'
      })
      
      const url = window.URL.createObjectURL(res as Blob)
      const link = document.createElement('a')
      link.href = url
      link.setAttribute('download', filename)
      document.body.appendChild(link)
      link.click()
      link.parentNode?.removeChild(link)
      window.URL.revokeObjectURL(url)
    } catch (error: any) {
      throw error
    }
  }

  return { fetchApi, downloadFile }
}
