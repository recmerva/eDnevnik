import { useState, type FormEvent } from 'react'
import { Pencil, Plus, Trash2 } from 'lucide-react'
import { api } from '../../api/client'
import { useApp } from '../../AppContext'
import { Modal } from '../../components/Modal'
import { EmptyState, PanelHeading } from '../../components/ui'
import { plural } from '../../lib/format'
import type { ClassRoom, Subject } from '../../types'
import { useAdminAction } from './useAdminAction'

// Šifarnici škole: odeljenja i predmeti. Brisanje je dozvoljeno samo kad nisu u upotrebi.
export function CatalogSection() {
  const { classes, subjects, reloadCatalog } = useApp()
  const run = useAdminAction()
  const [editingClass, setEditingClass] = useState<ClassRoom | null>(null)
  const [editingSubject, setEditingSubject] = useState<Subject | null>(null)

  async function submitForm(event: FormEvent<HTMLFormElement>, path: string, body: (data: FormData) => object, fallback: string) {
    event.preventDefault()
    const form = event.currentTarget
    if (await run(() => api(path, { method: 'POST', body: body(new FormData(form)) }), fallback)) {
      form.reset()
      reloadCatalog()
    }
  }

  const classBody = (data: FormData) => ({ name: String(data.get('name')).trim(), gradeLevel: Number(data.get('gradeLevel')) })
  const subjectBody = (data: FormData) => ({ name: String(data.get('name')).trim(), usesDescriptiveScale: data.get('descriptive') === 'on' })

  async function removeClass(item: ClassRoom) {
    if (await run(() => api(`/api/admin/classes/${item.id}`, { method: 'DELETE' }), 'Odeljenje nije obrisano.', `Obrisati odeljenje ${item.name}?`)) reloadCatalog()
  }

  async function removeSubject(item: Subject) {
    if (await run(() => api(`/api/admin/subjects/${item.id}`, { method: 'DELETE' }), 'Predmet nije obrisan.', `Obrisati predmet ${item.name}?`)) reloadCatalog()
  }

  return (
    <div className="admin-layout">
      <section className="panel data-panel">
        <PanelHeading eyebrow="ODELJENJA" title={`Odeljenja (${classes.length})`} />
        <form className="admin-form inline-form" onSubmit={(event) => submitForm(event, '/api/admin/classes', classBody, 'Odeljenje nije dodato.')}>
          <label>Naziv<input name="name" placeholder="Npr. V-1" maxLength={20} required /></label>
          <label>Razred<select name="gradeLevel" defaultValue={5}>{[1, 2, 3, 4, 5, 6, 7, 8].map((level) => <option key={level} value={level}>{level}.</option>)}</select></label>
          <button className="button button-primary" type="submit"><Plus size={16} /> Dodaj</button>
        </form>
        <div className="staff-list">
          {classes.map((item) => (
            <div className="staff-row" key={item.id}>
              <span className="teacher-avatar">{item.gradeLevel}.</span>
              <span><strong>{item.name}</strong><small>{item.studentCount} {plural(item.studentCount, 'učenik', 'učenika', 'učenika')} · razredni: {item.homeroomTeacher ?? 'nije određen'}</small></span>
              <button className="icon-button" aria-label={`Izmeni odeljenje ${item.name}`} title="Izmeni" onClick={() => setEditingClass(item)}><Pencil size={15} /></button>
              <button className="icon-button" aria-label={`Obriši odeljenje ${item.name}`} title="Obriši" onClick={() => removeClass(item)}><Trash2 size={15} /></button>
            </div>
          ))}
          {classes.length === 0 && <EmptyState>Još nema odeljenja.</EmptyState>}
        </div>
      </section>

      <section className="panel data-panel">
        <PanelHeading eyebrow="NASTAVNI PLAN" title={`Predmeti (${subjects.length})`} />
        <form className="admin-form inline-form" onSubmit={(event) => submitForm(event, '/api/admin/subjects', subjectBody, 'Predmet nije dodat.')}>
          <label>Naziv<input name="name" placeholder="Npr. Građansko vaspitanje" minLength={2} maxLength={80} required /></label>
          <label className="checkbox-label"><input type="checkbox" name="descriptive" /> Opisno ocenjivanje</label>
          <button className="button button-primary" type="submit"><Plus size={16} /> Dodaj</button>
        </form>
        <div className="staff-list">
          {subjects.map((item) => (
            <div className="staff-row" key={item.id}>
              <span><strong>{item.name}</strong><small>{item.usesDescriptiveScale ? 'Opisno ocenjivanje, ne ulazi u prosek' : 'Brojčano ocenjivanje 1–5'}</small></span>
              <button className="icon-button" aria-label={`Izmeni predmet ${item.name}`} title="Izmeni" onClick={() => setEditingSubject(item)}><Pencil size={15} /></button>
              <button className="icon-button" aria-label={`Obriši predmet ${item.name}`} title="Obriši" onClick={() => removeSubject(item)}><Trash2 size={15} /></button>
            </div>
          ))}
        </div>
      </section>

      {editingClass && (
        <Modal eyebrow="IZMENA ODELJENJA" title={editingClass.name} submitLabel="Sačuvaj" onClose={() => setEditingClass(null)}
          onSubmit={async (data) => { await api(`/api/admin/classes/${editingClass.id}`, { method: 'PUT', body: classBody(data) }); reloadCatalog() }}>
          <label>Naziv<input name="name" defaultValue={editingClass.name} maxLength={20} required /></label>
          <label>Razred<select name="gradeLevel" defaultValue={editingClass.gradeLevel}>{[1, 2, 3, 4, 5, 6, 7, 8].map((level) => <option key={level} value={level}>{level}.</option>)}</select></label>
        </Modal>
      )}
      {editingSubject && (
        <Modal eyebrow="IZMENA PREDMETA" title={editingSubject.name} submitLabel="Sačuvaj" onClose={() => setEditingSubject(null)}
          onSubmit={async (data) => { await api(`/api/admin/subjects/${editingSubject.id}`, { method: 'PUT', body: subjectBody(data) }); reloadCatalog() }}>
          <label>Naziv<input name="name" defaultValue={editingSubject.name} minLength={2} maxLength={80} required /></label>
          <label className="checkbox-label"><input type="checkbox" name="descriptive" defaultChecked={editingSubject.usesDescriptiveScale} /> Opisno ocenjivanje</label>
        </Modal>
      )}
    </div>
  )
}
