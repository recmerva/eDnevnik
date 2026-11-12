import { useState } from 'react'
import { KeyRound, LogOut } from 'lucide-react'
import { api } from '../api/client'
import { useApp } from '../AppContext'
import { initials } from '../lib/format'
import { ChildPicker } from './ChildPicker'
import { Modal } from './Modal'
import { usePopover } from './usePopover'

export function AccountMenu({ onLogout }: { onLogout: () => void }) {
  const { user } = useApp()
  const { open, setOpen, toggle, anchorRef } = usePopover()
  const [passwordOpen, setPasswordOpen] = useState(false)

  async function changePassword(form: FormData) {
    const newPassword = String(form.get('newPassword'))
    if (newPassword !== String(form.get('confirmPassword'))) throw new Error('Nove lozinke se ne poklapaju.')
    await api('/api/auth/password', { method: 'POST', body: { currentPassword: form.get('currentPassword'), newPassword } })
  }

  return (
    <div className="popover-anchor" ref={anchorRef}>
      <button className="top-avatar" aria-label="Moj nalog" aria-expanded={open} onClick={toggle}>{initials(user.fullName)}</button>
      {open && (
        <div className="popover" role="menu">
          <div className="popover-profile">
            <strong>{user.fullName}</strong>
            <small>{user.email} · {user.role}</small>
          </div>
          {user.role === 'Roditelj' && <ChildPicker onDone={() => setOpen(false)} />}
          <button type="button" className="popover-item popover-action" onClick={() => { setOpen(false); setPasswordOpen(true) }}><KeyRound size={15} /> Promeni lozinku</button>
          <button type="button" className="popover-item popover-action" onClick={onLogout}><LogOut size={15} /> Odjavi se</button>
        </div>
      )}
      {passwordOpen && (
        <Modal eyebrow="BEZBEDNOST NALOGA" title="Promena lozinke" submitLabel="Sačuvaj lozinku" onClose={() => setPasswordOpen(false)} onSubmit={changePassword}>
          <label>Trenutna lozinka<input name="currentPassword" type="password" autoComplete="current-password" required /></label>
          <label>Nova lozinka<input name="newPassword" type="password" autoComplete="new-password" minLength={10} placeholder="Najmanje 10 karaktera" required /></label>
          <label>Ponovite novu lozinku<input name="confirmPassword" type="password" autoComplete="new-password" minLength={10} required /></label>
        </Modal>
      )}
    </div>
  )
}
