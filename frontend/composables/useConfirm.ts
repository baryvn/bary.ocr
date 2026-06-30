import { ref } from 'vue'

interface ConfirmState {
  isOpen: boolean
  message: string
  title: string
  resolve: ((value: boolean) => void) | null
}

const state = ref<ConfirmState>({
  isOpen: false,
  message: '',
  title: 'Xác nhận',
  resolve: null
})

export const useConfirm = () => {
  const showConfirm = (message: string, title = 'Xác nhận') => {
    return new Promise<boolean>((resolve) => {
      state.value = {
        isOpen: true,
        message,
        title,
        resolve
      }
    })
  }

  const handleConfirm = () => {
    if (state.value.resolve) {
      state.value.resolve(true)
    }
    close()
  }

  const handleCancel = () => {
    if (state.value.resolve) {
      state.value.resolve(false)
    }
    close()
  }

  const close = () => {
    state.value.isOpen = false
  }

  return {
    state,
    showConfirm,
    handleConfirm,
    handleCancel
  }
}
