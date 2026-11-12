import { useEffect, useState, type FormEvent, type ReactNode } from 'react'
import { Check, X } from 'lucide-react'
import { errorMessage } from '../api/client'

type ModalProps = {
  eyebrow: string
  title: string
  submitLabel: string
  submitIcon?: ReactNode
  onClose: () => void
  // Vraća grešku kao izuzetak; modal je prikazuje i ostaje otvoren da korisnik ne izgubi unos.
  onSubmit: (form: FormData) => Promise<void>
  children: ReactNode
}

export function Modal({ eyebrow, title, submitLabel, submitIcon, onClose, onSubmit, children }: ModalProps) {
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')

  useEffect(() => {
    const closeOnEscape = (event: KeyboardEvent) => { if (event.key === 'Escape') onClose() }
    window.addEventListener('keydown', closeOnEscape)
    return () => window.removeEventListener('keydown', closeOnEscape)
  }, [onClose])

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSaving(true)
    setError('')
    try {
      await onSubmit(new FormData(event.currentTarget))
      onClose()
    } catch (submitError) {
      setError(errorMessage(submitError, 'Čuvanje nije uspelo.'))
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="modal-backdrop" onMouseDown={(event) => { if (event.target === event.currentTarget) onClose() }}>
      <form className="complaint-modal" onSubmit={submit} role="dialog" aria-modal="true" aria-label={title}>
        <div className="modal-heading">
          <div><span className="eyebrow">{eyebrow}</span><h2>{title}</h2></div>
          <button type="button" className="icon-button" aria-label="Zatvori" onClick={onClose}><X size={19} /></button>
        </div>
        {children}
        {error && <p className="auth-error" role="alert">{error}</p>}
        <div className="modal-actions">
          <button type="button" className="button button-secondary" onClick={onClose}>Otkaži</button>
          <button type="submit" className="button button-primary" disabled={saving}>
            {submitIcon ?? <Check size={16} />} {saving ? 'Čuvanje...' : submitLabel}
          </button>
        </div>
      </form>
    </div>
  )
}
