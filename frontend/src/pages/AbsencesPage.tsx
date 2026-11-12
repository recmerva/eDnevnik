import { useState } from 'react'
import { Check, Clock3, MessageCircle, Plus, RotateCcw, Send, Trash2 } from 'lucide-react'
import { api, errorMessage } from '../api/client'
import { useApp } from '../AppContext'
import { Modal } from '../components/Modal'
import { ChooseStudent, EmptyState, PanelHeading, StudentSelect } from '../components/ui'
import { formatDate, formatDateTime, lessonHours, plural, todayIso } from '../lib/format'
import { useApi } from '../lib/useApi'
import type { AbsenceRecord, ComplaintRecord } from '../types'

export function AbsencesPage() {
  const { isStaff, isTeacher, selectedClass, selectedStudentId, selectedStudentName, showError, refreshNotifications } = useApp()
  // Izostanke opravdava i na žalbe odgovara razredni starešina (i administrator).
  const isHomeroom = selectedClass?.isMyHomeroom ?? false
  const studentPath = selectedStudentId ? `/api/students/${selectedStudentId}` : null
  const absences = useApi<AbsenceRecord[]>(studentPath && `${studentPath}/absences`, [])
  const complaints = useApi<ComplaintRecord[]>(studentPath && `${studentPath}/complaints`, [])
  const [absenceOpen, setAbsenceOpen] = useState(false)
  const [complaintOpen, setComplaintOpen] = useState(false)
  const [answering, setAnswering] = useState<ComplaintRecord | null>(null)

  const totalHours = absences.data.reduce((sum, item) => sum + item.lessonHours, 0)
  const excusedHours = absences.data.filter((item) => item.isExcused).reduce((sum, item) => sum + item.lessonHours, 0)

  async function addAbsence(form: FormData) {
    const saved = await api<AbsenceRecord>(`${studentPath}/absences`, {
      method: 'POST',
      body: {
        date: String(form.get('date')),
        lessonHours: Number(form.get('lessonHours')),
        isExcused: form.get('isExcused') === 'true',
        note: String(form.get('note') ?? '').trim() || null,
      },
    })
    absences.setData((current) => [saved, ...current].sort((first, second) => second.date.localeCompare(first.date)))
  }

  async function toggleExcused(absence: AbsenceRecord) {
    try {
      const saved = await api<AbsenceRecord>(`/api/absences/${absence.id}`, { method: 'PUT', body: { ...absence, isExcused: !absence.isExcused } })
      absences.setData((current) => current.map((item) => item.id === saved.id ? saved : item))
    } catch (error) {
      showError(errorMessage(error, 'Izostanak nije izmenjen.'))
    }
  }

  async function removeAbsence(absence: AbsenceRecord) {
    if (!window.confirm(`Obrisati izostanak od ${formatDate(absence.date)}?`)) return
    try {
      await api(`/api/absences/${absence.id}`, { method: 'DELETE' })
      absences.setData((current) => current.filter((item) => item.id !== absence.id))
    } catch (error) {
      showError(errorMessage(error, 'Izostanak nije obrisan.'))
    }
  }

  async function submitComplaint(form: FormData) {
    const saved = await api<ComplaintRecord>(`${studentPath}/complaints`, { method: 'POST', body: { content: String(form.get('complaint')).trim() } })
    complaints.setData((current) => [saved, ...current])
  }

  async function answer(form: FormData) {
    const saved = await api<ComplaintRecord>(`/api/complaints/${answering!.id}`, { method: 'PUT', body: { response: String(form.get('response')).trim() } })
    complaints.setData((current) => current.map((item) => item.id === saved.id ? saved : item))
    refreshNotifications()
  }

  if (isStaff && !selectedStudentId) return <ChooseStudent eyebrow="EVIDENCIJA PRISUSTVA" title="Izostanci i opravdanja" />

  return (
    <section className="panel data-panel">
      <PanelHeading eyebrow="EVIDENCIJA PRISUSTVA" title="Izostanci i opravdanja">
        {isStaff
          ? <><StudentSelect />{isTeacher
            ? <button className="button button-primary" onClick={() => setAbsenceOpen(true)} disabled={!selectedStudentId}><Plus size={16} /> Evidentiraj izostanak</button>
            : <span className="class-label">Samo pregled</span>}</>
          : <button className="button button-primary" onClick={() => setComplaintOpen(true)} disabled={!selectedStudentId}><Plus size={16} /> Nova žalba</button>}
      </PanelHeading>

      <div className="absence-summary">
        <span className="absence-number">{totalHours}</span>
        <span>
          <strong>{plural(totalHours, 'čas izostanka', 'časa izostanka', 'časova izostanaka')} ukupno</strong>
          <small>{excusedHours} opravdano · {totalHours - excusedHours} neopravdano · {absences.data.length} {plural(absences.data.length, 'dan', 'dana', 'dana')}</small>
        </span>
      </div>
      <div className="absence-list">
        {absences.data.map((absence) => (
          <div className="absence-row" key={absence.id}>
            <span className="absence-icon"><Clock3 size={17} /></span>
            <span className="absence-copy">
              <strong>{selectedStudentName}</strong>
              <small>{formatDate(absence.date)} · {lessonHours(absence.lessonHours)}{absence.note ? ` · ${absence.note}` : ''}</small>
            </span>
            <span className={`absence-status ${absence.isExcused ? 'is-excused' : 'is-pending'}`}>{absence.isExcused ? 'Opravdan' : 'Neopravdan'}</span>
            {isHomeroom && (
              <span className="grade-actions">
                {isHomeroom && <button className="icon-button" onClick={() => toggleExcused(absence)} title={absence.isExcused ? 'Vrati na neopravdan' : 'Opravdaj'} aria-label={absence.isExcused ? 'Vrati na neopravdan' : 'Opravdaj izostanak'}>
                  {absence.isExcused ? <RotateCcw size={14} /> : <Check size={14} />}
                </button>}
                {isHomeroom && <button className="icon-button" onClick={() => removeAbsence(absence)} title="Obriši" aria-label="Obriši izostanak"><Trash2 size={14} /></button>}
              </span>
            )}
          </div>
        ))}
        {!absences.loading && absences.data.length === 0 && <EmptyState>Nema evidentiranih izostanaka.</EmptyState>}
      </div>

      <div className="chart-title"><strong>Žalbe i napomene roditelja</strong><span>{complaints.data.filter((item) => !item.response).length} čeka odgovor</span></div>
      {complaints.data.map((complaint) => (
        <div className="complaint-status" key={complaint.id}>
          {complaint.response ? <Check size={17} /> : <Clock3 size={17} />}
          <span>
            <strong>{complaint.status} · {complaint.parentName} · {formatDateTime(complaint.submittedAt)}</strong>
            <small>{complaint.content}</small>
            {complaint.response && <small><b>Odgovor ({complaint.respondedByName}):</b> {complaint.response}</small>}
            {isHomeroom && !complaint.response && (
              <button className="text-link" onClick={() => setAnswering(complaint)}><MessageCircle size={14} /> Odgovori i zatvori</button>
            )}
          </span>
        </div>
      ))}
      {!complaints.loading && complaints.data.length === 0 && <EmptyState>Nema poslatih žalbi.</EmptyState>}

      {absenceOpen && (
        <Modal eyebrow="EVIDENCIJA PRISUSTVA" title={`Novi izostanak · ${selectedStudentName}`} submitLabel="Sačuvaj izostanak" onClose={() => setAbsenceOpen(false)} onSubmit={addAbsence}>
          <label>Datum<input name="date" type="date" max={todayIso()} defaultValue={todayIso()} required /></label>
          <div className={isHomeroom ? 'form-grid' : undefined}>
            <label>Broj časova<input name="lessonHours" type="number" min="1" max="7" defaultValue="1" required /></label>
            {isHomeroom ? <label>Status
              <select name="isExcused" defaultValue="false">
                <option value="false">Neopravdan / čeka opravdanje</option>
                <option value="true">Opravdan</option>
              </select>
            </label> : <input type="hidden" name="isExcused" value="false" />}
          </div>
          <label>Napomena<input name="note" maxLength={500} placeholder="Opciono, npr. lekarsko opravdanje" /></label>
        </Modal>
      )}
      {complaintOpen && (
        <Modal eyebrow="KOMUNIKACIJA SA ŠKOLOM" title="Nova žalba / napomena" submitLabel="Pošalji razrednom" submitIcon={<Send size={16} />} onClose={() => setComplaintOpen(false)} onSubmit={submitComplaint}>
          <label>Poruka<textarea name="complaint" rows={5} minLength={5} maxLength={4000} placeholder="Opišite razlog obraćanja..." required /></label>
        </Modal>
      )}
      {answering && (
        <Modal eyebrow="ODGOVOR RODITELJU" title="Odgovor na žalbu" submitLabel="Pošalji odgovor" submitIcon={<Send size={16} />} onClose={() => setAnswering(null)} onSubmit={answer}>
          <p className="muted">{answering.parentName}: „{answering.content}”</p>
          <label>Odgovor<textarea name="response" rows={5} minLength={2} maxLength={4000} placeholder="Napišite odgovor roditelju..." required /></label>
        </Modal>
      )}
    </section>
  )
}
