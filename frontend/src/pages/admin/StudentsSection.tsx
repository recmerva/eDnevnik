import { useState } from 'react'
import { Copy, Pencil, Plus, RefreshCw, Trash2, Unlink } from 'lucide-react'
import { api } from '../../api/client'
import { useApp } from '../../AppContext'
import { Modal } from '../../components/Modal'
import { EmptyState, PanelHeading } from '../../components/ui'
import { formatCode } from '../../lib/format'
import { useApi } from '../../lib/useApi'
import type { AdminStudent } from '../../types'
import { useAdminAction } from './useAdminAction'

export function StudentsSection() {
  const { classes, showError, reloadCatalog } = useApp()
  const run = useAdminAction()
  const [classId, setClassId] = useState<number | ''>(classes[0]?.id ?? '')
  const [search, setSearch] = useState('')
  const students = useApi<AdminStudent[]>(`/api/admin/students${classId ? `?classId=${classId}` : ''}`, [])
  // undefined = zatvoren modal, null = novi učenik.
  const [editing, setEditing] = useState<AdminStudent | null | undefined>(undefined)
  const [copied, setCopied] = useState<number | null>(null)

  const term = search.trim().toLowerCase()
  const shown = students.data.filter((row) => !term || row.fullName.toLowerCase().includes(term) || row.parentName?.toLowerCase().includes(term))
  const linked = students.data.filter((row) => row.parentName).length

  function changed() {
    students.reload()
    reloadCatalog()
  }

  async function save(data: FormData) {
    const body = { fullName: String(data.get('fullName')).trim(), classId: Number(data.get('classId')) }
    if (editing) await api(`/api/admin/students/${editing.studentId}`, { method: 'PUT', body })
    else await api('/api/admin/students', { method: 'POST', body })
    changed()
  }

  async function copyCode(row: AdminStudent) {
    try {
      await navigator.clipboard.writeText(formatCode(row.registrationCode))
      setCopied(row.studentId)
      window.setTimeout(() => setCopied(null), 2000)
    } catch {
      showError('Kopiranje nije dozvoljeno u ovom pregledaču. Označite kod i kopirajte ga ručno.')
    }
  }

  async function regenerate(row: AdminStudent) {
    if (await run(() => api(`/api/admin/students/${row.studentId}/registration-code`, { method: 'POST' }), 'Novi kod nije izdat.',
      `Izdati novi kod za učenika ${row.fullName}? Stari kod prestaje da važi.`)) students.reload()
  }

  async function unlink(row: AdminStudent) {
    if (await run(() => api(`/api/admin/students/${row.studentId}/parent`, { method: 'DELETE' }), 'Roditelj nije odvezan.',
      `Odvezati roditelja ${row.parentName} od učenika ${row.fullName}? Biće izdat novi kod za registraciju.`)) students.reload()
  }

  async function remove(row: AdminStudent) {
    if (await run(() => api(`/api/admin/students/${row.studentId}`, { method: 'DELETE' }), 'Učenik nije obrisan.',
      `Trajno obrisati učenika ${row.fullName} sa svim ocenama, izostancima i porukama? Ovo se ne može poništiti.`)) changed()
  }

  return (
    <section className="panel data-panel">
      <PanelHeading eyebrow="UČENICI I REGISTRACIJA RODITELJA" title="Učenici i kodovi">
        <span className="muted">{linked} od {students.data.length} povezano</span>
        <input className="class-select" type="search" value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Pretraga po imenu" aria-label="Pretraga učenika" />
        <select className="class-select" value={classId} onChange={(event) => setClassId(event.target.value ? Number(event.target.value) : '')} aria-label="Filtriraj po odeljenju">
          <option value="">Sva odeljenja</option>
          {classes.map((schoolClass) => <option key={schoolClass.id} value={schoolClass.id}>{schoolClass.name}</option>)}
        </select>
        <button className="button button-primary" onClick={() => setEditing(null)} disabled={classes.length === 0}><Plus size={16} /> Novi učenik</button>
      </PanelHeading>
      <div className="table-scroll">
        <table>
          <thead><tr><th>UČENIK</th><th>ODELJENJE</th><th>KOD / RODITELJ</th><th>AKCIJA</th></tr></thead>
          <tbody>
            {shown.map((row) => (
              <tr key={row.studentId}>
                <td><strong>{row.fullName}</strong></td>
                <td>{row.className}</td>
                <td>{row.parentName ? `${row.parentName} · ${row.parentEmail}` : <code className="code-chip">{formatCode(row.registrationCode)}</code>}</td>
                <td>
                  <span className="row-actions">
                    {row.parentName
                      ? <button className="text-link" onClick={() => unlink(row)}><Unlink size={14} /> Odveži</button>
                      : <>
                        <button className="text-link" onClick={() => copyCode(row)}><Copy size={14} /> {copied === row.studentId ? 'Kopirano' : 'Kopiraj'}</button>
                        <button className="text-link" onClick={() => regenerate(row)}><RefreshCw size={14} /> Novi kod</button>
                      </>}
                    <button className="text-link" onClick={() => setEditing(row)}><Pencil size={14} /> Izmeni</button>
                    <button className="text-link danger-link" onClick={() => remove(row)}><Trash2 size={14} /> Obriši</button>
                  </span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        {!students.loading && shown.length === 0 && <EmptyState>Nema učenika.</EmptyState>}
      </div>

      {editing !== undefined && (
        <Modal eyebrow="EVIDENCIJA UČENIKA" title={editing ? `Izmena · ${editing.fullName}` : 'Novi učenik'} submitLabel="Sačuvaj" onClose={() => setEditing(undefined)} onSubmit={save}>
          <label>Ime i prezime<input name="fullName" defaultValue={editing?.fullName} minLength={2} maxLength={120} required /></label>
          <label>Odeljenje
            <select name="classId" defaultValue={editing?.classId ?? (classId || classes[0]?.id)}>
              {classes.map((schoolClass) => <option key={schoolClass.id} value={schoolClass.id}>{schoolClass.name}</option>)}
            </select>
          </label>
          {!editing && <p className="table-note">Učenik dobija kod za registraciju roditelja, koji se prikazuje u tabeli.</p>}
        </Modal>
      )}
    </section>
  )
}
