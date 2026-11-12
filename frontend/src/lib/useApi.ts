import { useEffect, useRef, useState } from 'react'
import { api, errorMessage } from '../api/client'
import { useApp } from '../AppContext'

// Učitava podatke sa API-ja kad se putanja promeni; null znači "nema šta da se učita".
export function useApi<T>(path: string | null, initial: T, refreshEveryMs?: number) {
  const { showError } = useApp()
  const initialRef = useRef(initial)
  const [data, setData] = useState<T>(initial)
  const [loading, setLoading] = useState(path !== null)
  const [version, setVersion] = useState(0)

  useEffect(() => {
    if (path === null) {
      setData(initialRef.current)
      setLoading(false)
      return
    }
    let active = true
    setLoading(true)
    api<T>(path)
      .then((result) => { if (active) setData(result) })
      .catch((error) => { if (active) showError(errorMessage(error, 'Podaci nisu učitani.')) })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [path, version, showError])

  useEffect(() => {
    if (!refreshEveryMs || path === null) return
    const timer = window.setInterval(() => setVersion((value) => value + 1), refreshEveryMs)
    return () => window.clearInterval(timer)
  }, [path, refreshEveryMs])

  return { data, setData, loading, reload: () => setVersion((value) => value + 1) }
}
