import { useState, type FormEvent } from 'react'
import { Pencil, Plus, Trash2 } from 'lucide-react'
import { api } from '../../api/client'
import { useApp } from '../../AppContext'
import { Modal } from '../../components/Modal'
import { EmptyState, PanelHeading } from '../../components/ui'
import { initials, plural } from '../../lib/format'
import { useApi } from '../../lib/useApi'
import type { AdminUser, Role } from '../../types'
import { useAdminAction } from './useAdminAction'

const roleFilters: { value: Role | ''; label: string }[] = [
  { value: '', label: 'Svi nalozi' },
  { value: 'Nastavnik', label: 'Nastavnici' },
  { value: 'Roditelj', label: 'Roditelji' },
  { value: 'Administrator', label: 'Administratori' },
]

function accountDetail(account: AdminUser) {
  if (account.role === 'Roditelj') return `Roditelj · ${account.childCount} ${plural(account.childCount, 'dete', 'deteta', 'dece')}`
  if (account.role === 'Nastavnik') return `Nastavnik · ${account.classCount} ${plural(account.classCount, 'odeljenje', 'odeljenja', 'odeljenja')}`
  return 'Administrator'
}

export function AccountsSection() {
  const { user } = useApp()
  const run = useAdminAction()
  const [role, setRole] = useState<Role | ''>('')
  const users = useApi<AdminUser[]>(`/api/admin/users${role ? `?role=${role}` : ''}`, [])
  const [editing, setEditing] = useState<AdminUser | null>(null)

  async function create(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = event.currentTarget
    const data = new FormData(form)
    const ok = await run(() => api('/api/admin/users', {
      method: 'POST',
      body: { fullName: String(data.get('fullName')).trim(), email: String(data.get('email')).trim(), password: data.get('password'), role: data.get('role') },
    }), 'Nalog nije kreiran.')
    if (ok) { form.reset(); users.reload() }
  }

  async function update(data: FormData) {
    const password = String(data.get('newPassword'))
    await api(`/api/admin/users/${editing!.id}`, {
      method: 'PUT',
      body: {
        fullName: String(data.get('fullName')).trim(),
        email: String(data.get('email')).trim(),
        role: data.get('role') ?? editing!.role,
        newPassword: password || null,
      },
    })
    users.reload()
  }

  async function remove(account: AdminUser) {
    const consequence = account.role === 'Roditelj' ? ' Deca dobijaju nove kodove za registraciju.' : account.role === 'Nastavnik' ? ' Uklanjaju se i sve dodele odeljenjima.' : ''
    if (await run(() => api(`/api/admin/users/${account.id}`, { method: 'DELETE' }), 'Nalog nije obrisan.', `Obrisati nalog ${account.fullName}?${consequence}`)) users.reload()
  }

  return (
    <div className="admin-layout">
      <section className="panel data-panel">
        <PanelHeading eyebrow="NOVI NALOG" title="Nastavnici i administratori" />
        <form className="admin-form" onSubmit={create}>
          <label>Ime i prezime<input name="fullName" placeholder="Npr. Selma Hadžić" minLength={2} maxLength={120} required /></label>
          <label>Email<input name="email" type="email" placeholder="nastavnik@skola.rs" maxLength={160} required /></label>
          <label>Privremena lozinka<input name="password" type="password" minLength={10} maxLength={128} autoComplete="new-password" placeholder="Najmanje 10 karaktera" required /></label>
          <label>Uloga
            <select name="role" defaultValue="Nastavnik">
              <option value="Nastavnik">Nastavnik</option>
              <option value="Administrator">Administrator</option>
            </select>
          </label>
          <button className="button button-primary" type="submit"><Plus size={16} /> Kreiraj nalog</button>
        </form>
        <p className="table-note">Roditelji sami otvaraju nalog kodom učenika koji dobiju od škole (sekcija „Učenici i kodovi”).</p>
      </section>

      <section className="panel data-panel">
        <PanelHeading eyebrow="KORISNIČKI NALOZI" title="Svi nalozi">
          <select className="class-select" value={role} onChange={(event) => setRole(event.target.value as Role | '')} aria-label="Filtriraj po ulozi">
            {roleFilters.map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}
          </select>
        </PanelHeading>
        <div className="staff-list">
          {users.data.map((account) => (
            <div className="staff-row" key={account.id}>
              <span className="teacher-avatar">{initials(account.fullName)}</span>
              <span><strong>{account.fullName}</strong><small>{account.email}</small></span>
              <span className="class-label">{accountDetail(account)}</span>
              <button className="icon-button" aria-label={`Izmeni nalog ${account.fullName}`} title="Izmeni nalog" onClick={() => setEditing(account)}><Pencil size={15} /></button>
              {account.id !== user.id && (
                <button className="icon-button" aria-label={`Obriši nalog ${account.fullName}`} title="Obriši nalog" onClick={() => remove(account)}><Trash2 size={15} /></button>
              )}
            </div>
          ))}
          {!users.loading && users.data.length === 0 && <EmptyState>Nema naloga za izabrani filter.</EmptyState>}
        </div>
      </section>

      {editing && (
        <Modal eyebrow="IZMENA NALOGA" title={editing.fullName} submitLabel="Sačuvaj" onClose={() => setEditing(null)} onSubmit={update}>
          <label>Ime i prezime<input name="fullName" defaultValue={editing.fullName} minLength={2} maxLength={120} required /></label>
          <label>Email<input name="email" type="email" defaultValue={editing.email} maxLength={160} required /></label>
          {editing.role !== 'Roditelj' && editing.id !== user.id && (
            <label>Uloga
              <select name="role" defaultValue={editing.role}>
                <option value="Nastavnik">Nastavnik</option>
                <option value="Administrator">Administrator (uklanja dodele odeljenjima)</option>
              </select>
            </label>
          )}
          <label>Nova lozinka<input name="newPassword" type="password" minLength={10} maxLength={128} autoComplete="new-password" placeholder="Ostavite prazno da se lozinka ne menja" /></label>
        </Modal>
      )}
    </div>
  )
}
