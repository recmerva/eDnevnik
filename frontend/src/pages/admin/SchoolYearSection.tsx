import { type FormEvent } from 'react'
import { Check } from 'lucide-react'
import { api } from '../../api/client'
import { useApp } from '../../AppContext'
import { PanelHeading } from '../../components/ui'
import { formatLongDate } from '../../lib/format'
import { useAdminAction } from './useAdminAction'

export function SchoolYearSection() {
  const { school, reloadCatalog } = useApp()
  const run = useAdminAction()

  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const data = new FormData(event.currentTarget)
    const body = Object.fromEntries(['name', 'firstTermStart', 'firstTermEnd', 'secondTermStart', 'secondTermEnd'].map((key) => [key, String(data.get(key)).trim()]))
    if (await run(() => api('/api/admin/school-year', { method: 'PUT', body }), 'Školska godina nije sačuvana.')) reloadCatalog()
  }

  return (
    <div className="admin-layout">
      <section className="panel data-panel">
        <PanelHeading eyebrow="ŠKOLSKA GODINA" title={`Školska ${school.schoolYear}`} />
        <form className="admin-form" onSubmit={save} key={school.schoolYear + school.secondTermStart}>
          <label>Naziv<input name="name" defaultValue={school.schoolYear} placeholder="Npr. 2026/27." minLength={4} maxLength={20} required /></label>
          <div className="form-grid">
            <label>Početak I polugodišta<input name="firstTermStart" type="date" defaultValue={school.firstTermStart} required /></label>
            <label>Kraj I polugodišta<input name="firstTermEnd" type="date" defaultValue={school.firstTermEnd} required /></label>
            <label>Početak II polugodišta<input name="secondTermStart" type="date" defaultValue={school.secondTermStart} required /></label>
            <label>Kraj II polugodišta<input name="secondTermEnd" type="date" defaultValue={school.secondTermEnd} required /></label>
          </div>
          <button className="button button-primary" type="submit"><Check size={16} /> Sačuvaj</button>
        </form>
      </section>
      <section className="panel data-panel">
        <PanelHeading eyebrow="TEKUĆE STANJE" title={`${school.currentTerm}. polugodište`} />
        <div className="test-list">
          <div className="test-row"><div><strong>I polugodište</strong><small>{formatLongDate(school.firstTermStart)} – {formatLongDate(school.firstTermEnd)}</small></div></div>
          <div className="test-row"><div><strong>II polugodište</strong><small>{formatLongDate(school.secondTermStart)} – {formatLongDate(school.secondTermEnd)}</small></div></div>
        </div>
        <p className="table-note">Polugodište određuje za koji period nastavnici zaključuju ocene i kako se računa opšti uspeh.</p>
      </section>
    </div>
  )
}
