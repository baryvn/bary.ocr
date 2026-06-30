import { defineStore } from 'pinia'
import { ref } from 'vue'

export const useProjectStore = defineStore('project', () => {
  const currentProject = ref<any>(null)
  
  const setCurrentProject = (project: any) => {
    currentProject.value = project
    // Save to local storage or cookie if needed
    if (process.client) {
      localStorage.setItem('adms_current_project', JSON.stringify(project))
    }
  }

  const loadCurrentProject = () => {
    if (process.client) {
      const saved = localStorage.getItem('adms_current_project')
      if (saved) {
        try {
          currentProject.value = JSON.parse(saved)
        } catch (e) {}
      }
    }
  }

  return {
    currentProject,
    setCurrentProject,
    loadCurrentProject
  }
})
