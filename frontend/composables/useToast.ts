import { ref } from 'vue'

export type ToastType = 'success' | 'error' | 'info' | 'warning'

export interface ToastMessage {
  id: number
  message: string
  type: ToastType
}

const toasts = ref<ToastMessage[]>([])
let nextId = 1

export const useToast = () => {
  const show = (message: string, type: ToastType = 'info', duration = 5000) => {
    const id = nextId++
    toasts.value.push({ id, message, type })
    setTimeout(() => {
      remove(id)
    }, duration)
  }

  const success = (message: string, duration?: number) => show(message, 'success', duration)
  const error = (message: string, duration?: number) => show(message, 'error', duration)
  const info = (message: string, duration?: number) => show(message, 'info', duration)
  const warning = (message: string, duration?: number) => show(message, 'warning', duration)

  const remove = (id: number) => {
    const index = toasts.value.findIndex(t => t.id === id)
    if (index !== -1) {
      toasts.value.splice(index, 1)
    }
  }

  return {
    toasts,
    show,
    success,
    error,
    info,
    warning,
    remove
  }
}
