import { useCallback } from 'react'
import { errorMessage } from '../../api/client'
import { useApp } from '../../AppContext'

// Administratorska akcija: greška se prikazuje na vrhu stranice, a uspeh vraća true da bi se liste osvežile.
export function useAdminAction() {
  const { showError } = useApp()
  return useCallback(async (action: () => Promise<unknown>, fallback: string, question?: string) => {
    if (question && !window.confirm(question)) return false
    try {
      await action()
      showError('')
      return true
    } catch (error) {
      showError(errorMessage(error, fallback))
      return false
    }
  }, [showError])
}
