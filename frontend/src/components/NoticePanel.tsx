import { useState } from 'react'
import { Bell, ChevronRight, Plus, Trash2 } from 'lucide-react'
import { api, errorMessage } from '../api/client'
import { useApp } from '../AppContext'
import { formatDateTime } from '../lib/format'
import { useApi } from '../lib/useApi'
import type { Announcement } from '../types'
import { Modal } from './Modal'
import { PanelHeading } from './ui'

// Najnovije obaveštenje škole ili odeljenja; osoblje odavde objavljuje nova.
export function NoticePanel() {
  const { user, isStaff, classes, selectedClass, setPage, showError, refreshNotifications } = useApp()
  const announcements = useApi<Announcement[]>(selectedClass ? `/api/announcements?classId=${selectedClass.id}` : '/api/announcements', [])
  const [open, setOpen] = useState(false)
  const latest = announcements.data[0]

  async function publish(form: FormData) {
    const classValue = String(form.get('classId'))
    const saved = await api<Announcement>('/api/announcements', {
      method: 'POST',
      body: { classId: classValue === 'school' ? null : Number(classValue), content: String(form.get('content')).trim() },
    })
    if (saved.classId === null || saved.classId === selectedClass?.id) announcements.setData((current) => [saved, ...current])
    refreshNotifications()
  }

  async function remove(announcement: Announcement) {
    if (!window.confirm('Ukloniti ovo obaveštenje?')) return
    try {
      await api(`/api/announcements/${announcement.id}`, { method: 'DELETE' })
      announcements.setData((current) => current.filter((item) => item.id !== announcement.id))
    } catch (error) {
      showError(errorMessage(error, 'Obaveštenje nije uklonjeno.'))
    }
  }

  return (
    <section className="panel notice-panel">
      <PanelHeading eyebrow="OBAVEŠTENJE" title="Iz škole"><span className="notice-mark"><Bell size={16} /></span></PanelHeading>
      {latest ? <>
        <p>{latest.content}</p>
        <small>{latest.authorName} · {latest.className ?? 'cela škola'} · {formatDateTime(latest.createdAt)}</small>
      </> : <p>Trenutno nema obaveštenja za {selectedClass ? `odeljenje ${selectedClass.name}` : 'vašu školu'}.</p>}
      <div className="notice-actions">
        {isStaff
          ? <button className="text-link" onClick={() => setOpen(true)}><Plus size={15} /> Novo obaveštenje</button>
          : <button className="text-link" onClick={() => setPage('Poruke')}>Otvori poruke <ChevronRight size={15} /></button>}
        {latest?.canDelete && <button className="text-link" onClick={() => remove(latest)}><Trash2 size={14} /> Ukloni</button>}
      </div>

      {open && (
        <Modal eyebrow="KOMUNIKACIJA SA RODITELJIMA" title="Novo obaveštenje" submitLabel="Objavi" onClose={() => setOpen(false)} onSubmit={publish}>
          <label>Za koga
            <select name="classId" defaultValue={selectedClass?.id ?? 'school'}>
              {user.role === 'Administrator' && <option value="school">Cela škola</option>}
              {classes.map((item) => <option key={item.id} value={item.id}>Odeljenje {item.name}</option>)}
            </select>
          </label>
          <label>Tekst obaveštenja<textarea name="content" rows={5} minLength={5} maxLength={2000} placeholder="Npr. Roditeljski sastanak održaće se u četvrtak u 18 časova." required /></label>
        </Modal>
      )}
    </section>
  )
}
