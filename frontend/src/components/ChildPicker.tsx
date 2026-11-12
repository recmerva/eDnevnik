import { useState, type FormEvent } from 'react'
import { Plus } from 'lucide-react'
import { api, errorMessage } from '../api/client'
import { useApp } from '../AppContext'
import type { UserProfile } from '../types'

// Roditelj bira dete čije podatke gleda i može da poveže još jedno dete kodom iz škole.
export function ChildPicker({ onDone }: { onDone: () => void }) {
  const { user, setUser, selectedStudentId, selectStudent, refreshNotifications } = useApp()
  const [error, setError] = useState('')
  const [saving, setSaving] = useState(false)

  async function linkChild(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = event.currentTarget
    setSaving(true)
    setError('')
    try {
      const studentCode = String(new FormData(form).get('studentCode') ?? '')
      const profile = await api<UserProfile>('/api/auth/children', { method: 'POST', body: { studentCode } })
      const added = profile.children.find((child) => !user.children.some((existing) => existing.id === child.id))
      setUser(profile)
      if (added) selectStudent(added.id)
      refreshNotifications()
      form.reset()
      onDone()
    } catch (linkError) {
      setError(errorMessage(linkError, 'Dete nije povezano.'))
    } finally {
      setSaving(false)
    }
  }

  return (
    <>
      <span className="eyebrow">DECA NA NALOGU</span>
      {user.children.map((child) => (
        <button
          key={child.id}
          type="button"
          className={`popover-item ${child.id === selectedStudentId ? 'is-current' : ''}`}
          onClick={() => { selectStudent(child.id); onDone() }}
        >
          <strong>{child.fullName}</strong>
          <small>{child.className}{child.id === selectedStudentId ? ' · trenutno izabrano' : ''}</small>
        </button>
      ))}
      <form className="popover-form" onSubmit={linkChild}>
        <label>Poveži još jedno dete<input name="studentCode" placeholder="Kod iz škole, npr. K7MR-Q4TB" autoComplete="off" maxLength={32} required /></label>
        {error && <p className="auth-error" role="alert">{error}</p>}
        <button className="button button-secondary" type="submit" disabled={saving}><Plus size={15} /> {saving ? 'Povezivanje...' : 'Poveži dete'}</button>
      </form>
    </>
  )
}
