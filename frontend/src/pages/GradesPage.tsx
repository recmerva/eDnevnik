import { useState } from 'react'
import { Award, Lock, NotebookPen, Pencil, Plus, Trash2 } from 'lucide-react'
import { api, errorMessage } from '../api/client'
import { useApp } from '../AppContext'
import { Modal } from '../components/Modal'
import { ChooseStudent, EmptyState, PanelHeading, StudentSelect } from '../components/ui'
import { formatDate, formatDateTime, plural, todayIso } from '../lib/format'
import { averageOf, descriptiveScale, finalGrade, formatAverage, gradeTypes, overallSuccess, teachableSubjects } from '../lib/grades'
import { useApi } from '../lib/useApi'
import type { FinalGrade, Grade, StudentNote, Subject } from '../types'

type FinalTarget = { subject: Subject; suggested: number | null; current: FinalGrade | undefined }

const finalLabel = (value: number, descriptive: boolean) => descriptive ? descriptiveScale[value - 1] : String(value)

export function GradesPage() {
  const { isStaff, isTeacher, school, subjects, selectedClass, selectedStudentId, selectedStudentName, showError } = useApp()
  const studentPath = selectedStudentId ? `/api/students/${selectedStudentId}` : null
  const grades = useApi<Grade[]>(studentPath && `${studentPath}/grades`, [])
  const finals = useApi<FinalGrade[]>(studentPath && `${studentPath}/final-grades`, [])
  const notes = useApi<StudentNote[]>(studentPath && `${studentPath}/notes`, [])
  // undefined = zatvoren modal, null = nova ocena, Grade = izmena postojeće.
  const [editing, setEditing] = useState<Grade | null | undefined>(undefined)
  const [finalTarget, setFinalTarget] = useState<FinalTarget | null>(null)
  const [noteOpen, setNoteOpen] = useState(false)

  const term = school.currentTerm
  const mySubjects = teachableSubjects(subjects, selectedClass)
  const canManage = (subjectId: number) => isTeacher && mySubjects.some((subject) => subject.id === subjectId)
  // Predmetni nastavnik vidi samo svoje predmete; razredni starešina, učitelj, administrator i roditelj vide sve.
  const seesAll = !isTeacher || (selectedClass?.isMyHomeroom ?? false)
  const success = overallSuccess(grades.data, finals.data, term)
  const shownSubjects = subjects.filter((subject) =>
    grades.data.some((grade) => grade.subjectId === subject.id) || finals.data.some((final) => final.subjectId === subject.id)
    || (isStaff && mySubjects.some((item) => item.id === subject.id)))
  const finalOf = (subjectId: number, forTerm: number) => finals.data.find((final) => final.subjectId === subjectId && final.term === forTerm)
  const recordedThisTerm = finals.data.filter((final) => final.term === term).length

  async function run(action: () => Promise<void>, fallback: string) {
    try { await action() } catch (error) { showError(errorMessage(error, fallback)) }
  }

  function removeGrade(grade: Grade) {
    if (!window.confirm(`Obrisati ocenu ${grade.value} iz predmeta ${grade.subject}?`)) return
    run(async () => {
      await api(`/api/grades/${grade.id}`, { method: 'DELETE' })
      grades.setData((current) => current.filter((item) => item.id !== grade.id))
    }, 'Ocena nije obrisana.')
  }

  function removeFinal(final: FinalGrade) {
    if (!window.confirm(`Poništiti zaključnu ocenu iz predmeta ${final.subject} (${final.term}. polugodište)?`)) return
    run(async () => {
      await api(`/api/final-grades/${final.id}`, { method: 'DELETE' })
      finals.setData((current) => current.filter((item) => item.id !== final.id))
    }, 'Zaključna ocena nije poništena.')
  }

  function removeNote(note: StudentNote) {
    if (!window.confirm('Obrisati belešku?')) return
    run(async () => {
      await api(`/api/notes/${note.id}`, { method: 'DELETE' })
      notes.setData((current) => current.filter((item) => item.id !== note.id))
    }, 'Beleška nije obrisana.')
  }

  function savedGrade(grade: Grade) {
    grades.setData((current) => [grade, ...current.filter((item) => item.id !== grade.id)]
      .sort((first, second) => second.gradedOn.localeCompare(first.gradedOn)))
  }

  async function saveFinal(form: FormData) {
    const saved = await api<FinalGrade>(`${studentPath}/final-grades`, {
      method: 'PUT',
      body: { subjectId: finalTarget!.subject.id, term, value: Number(form.get('value')) },
    })
    finals.setData((current) => [...current.filter((item) => item.id !== saved.id), saved])
  }

  async function addNote(form: FormData) {
    const saved = await api<StudentNote>(`${studentPath}/notes`, { method: 'POST', body: { content: String(form.get('content')).trim() } })
    notes.setData((current) => [saved, ...current])
  }

  if (isStaff && !selectedStudentId) return <ChooseStudent eyebrow="ŠKOLSKI USPEH" title="Evidencija ocena" />

  return (
    <div className="grades-layout">
      <section className="panel data-panel">
        <PanelHeading eyebrow="ŠKOLSKI USPEH" title="Evidencija ocena">
          {isStaff && <>
            <StudentSelect />
            {isTeacher
              ? <button className="button button-primary" onClick={() => setEditing(null)} disabled={!selectedStudentId || mySubjects.length === 0}><Plus size={16} /> Unesi ocenu</button>
              : <span className="class-label">Samo pregled</span>}
          </>}
          <span className="average-chip"><Award size={16} /> {seesAll ? 'Prosek' : 'Prosek predmeta'} <strong>{formatAverage(averageOf(grades.data))}</strong></span>
        </PanelHeading>

        <div className="grade-history">
          {shownSubjects.map((subject) => {
            const subjectGrades = grades.data.filter((grade) => grade.subjectId === subject.id)
            const subjectAverage = averageOf(subjectGrades)
            const suggested = subject.usesDescriptiveScale || subjectAverage === null ? null : finalGrade(subjectAverage)
            const current = finalOf(subject.id, term)
            return (
              <section className="subject-history" key={subject.id}>
                <div className="subject-history-heading">
                  <span className="subject-title">
                    <strong>{subject.name}</strong>
                    <span>
                      {subjectGrades.length} {plural(subjectGrades.length, 'ocena', 'ocene', 'ocena')}
                      {subject.usesDescriptiveScale
                        ? subjectGrades[0] && ` · poslednja: ${subjectGrades[0].description}`
                        : ` · prosek ${formatAverage(subjectAverage)}`}
                    </span>
                  </span>
                  <span className="subject-final">
                    {current
                      ? <span className={`final-box is-final ${subject.usesDescriptiveScale ? 'is-descriptive' : `final-${current.value}`}`} title={`Zaključio/la ${current.recordedByName}, ${formatDateTime(current.recordedAt)}`}>
                        <small>Zaključna · {term}. polug.</small>
                        <strong>{finalLabel(current.value, subject.usesDescriptiveScale)}</strong>
                      </span>
                      : <span className="final-box">
                        <small>Nije zaključeno</small>
                        <strong>{suggested ? `predlog ${suggested}` : '—'}</strong>
                      </span>}
                    {canManage(subject.id) && (
                      <button className="button button-primary final-button" onClick={() => setFinalTarget({ subject, suggested, current })}>
                        <Lock size={13} /> {current ? 'Izmeni' : 'Zaključi'}
                      </button>
                    )}
                  </span>
                </div>
                {subjectGrades.map((grade) => (
                  <div className="grade-history-row" key={grade.id}>
                    <span className={`grade-badge grade-${grade.value}`}>{grade.value}</span>
                    <span className="grade-subject"><strong>{grade.gradeType}</strong><small>{grade.description ?? 'Brojčana ocena'}</small></span>
                    <span className="grade-date">{formatDate(grade.gradedOn)}</span>
                    {canManage(grade.subjectId) && (
                      <span className="grade-actions">
                        <button className="icon-button" aria-label={`Izmeni ocenu iz ${subject.name}`} title="Izmeni" onClick={() => setEditing(grade)}><Pencil size={14} /></button>
                        <button className="icon-button" aria-label={`Obriši ocenu iz ${subject.name}`} title="Obriši" onClick={() => removeGrade(grade)}><Trash2 size={14} /></button>
                      </span>
                    )}
                  </div>
                ))}
              </section>
            )
          })}
          {!grades.loading && shownSubjects.length === 0 && <EmptyState>Nema evidentiranih ocena.</EmptyState>}
        </div>

        {seesAll
          ? <div className="final-grade">
            <span><b>Opšti uspeh · {term}. polugodište</b>{recordedThisTerm > 0 ? `Zaključeno ${recordedThisTerm} od ${shownSubjects.length} ${plural(shownSubjects.length, 'predmeta', 'predmeta', 'predmeta')}, ostalo je predlog iz proseka` : 'Predlog na osnovu proseka, još nema zaključnih ocena'}</span>
            <strong>{success ? `${success.label} · ${formatAverage(success.average)}` : 'Evidencija u toku'}</strong>
          </div>
          : <p className="table-note">Prikazane su samo ocene iz predmeta koje predajete ({mySubjects.map((subject) => subject.name).join(', ')}). Sve ocene i opšti uspeh vidi razredni starešina.</p>}
      </section>

      <section className="panel data-panel">
        <PanelHeading eyebrow="EVIDENCIJA ZAKLJUČNIH OCENA" title="Zaključne ocene" />
        <div className="table-scroll">
          <table>
            <thead><tr><th>PREDMET</th><th>I POLUGODIŠTE</th><th>II POLUGODIŠTE</th></tr></thead>
            <tbody>
              {shownSubjects.map((subject) => (
                <tr key={subject.id}>
                  <td><strong>{subject.name}</strong></td>
                  {[1, 2].map((forTerm) => {
                    const final = finalOf(subject.id, forTerm)
                    return (
                      <td key={forTerm}>
                        {final ? <span className="final-cell" title={`Upisao/la ${final.recordedByName}, ${formatDateTime(final.recordedAt)}`}>
                          {subject.usesDescriptiveScale
                            ? <strong className="final-text">{finalLabel(final.value, true)}</strong>
                            : <span className={`grade-badge grade-${final.value}`}>{final.value}</span>}
                          {canManage(subject.id) && <button className="text-link" onClick={() => removeFinal(final)}>Poništi</button>}
                        </span> : '—'}
                      </td>
                    )
                  })}
                </tr>
              ))}
            </tbody>
          </table>
          {!finals.loading && shownSubjects.length === 0 && <EmptyState>Nema predmeta sa ocenama.</EmptyState>}
        </div>
      </section>

      <section className="panel data-panel">
        <PanelHeading eyebrow="ZAPAŽANJA O UČENIKU" title="Beleške nastavnika">
          {isTeacher && <button className="button button-primary" onClick={() => setNoteOpen(true)} disabled={!selectedStudentId}><NotebookPen size={16} /> Nova beleška</button>}
        </PanelHeading>
        <div className="test-list">
          {notes.data.map((note) => (
            <div className="test-row note-row" key={note.id}>
              <div><strong>{note.content}</strong><small>{note.authorName} · {formatDateTime(note.createdAt)}</small></div>
              {isTeacher && note.canDelete && <button className="icon-button" aria-label="Obriši belešku" title="Obriši" onClick={() => removeNote(note)}><Trash2 size={14} /></button>}
            </div>
          ))}
          {!notes.loading && notes.data.length === 0 && <EmptyState>Nema beleški o učeniku.</EmptyState>}
        </div>
      </section>

      {editing !== undefined && selectedStudentId && (
        <GradeModal grade={editing} studentId={selectedStudentId} subjects={mySubjects} onClose={() => setEditing(undefined)} onSaved={savedGrade} />
      )}
      {finalTarget && (
        <Modal eyebrow={`${term}. POLUGODIŠTE · ${selectedStudentName.toUpperCase()}`} title={`Zaključna ocena · ${finalTarget.subject.name}`} submitLabel="Zaključi" onClose={() => setFinalTarget(null)} onSubmit={saveFinal}>
          <label>Ocena
            <select name="value" defaultValue={finalTarget.current?.value ?? finalTarget.suggested ?? 5}>
              {[5, 4, 3, 2, 1].map((value) => (
                <option key={value} value={value}>
                  {finalTarget.subject.usesDescriptiveScale ? `${value} · ${descriptiveScale[value - 1]}` : value}{value === finalTarget.suggested ? ' (predlog iz proseka)' : ''}
                </option>
              ))}
            </select>
          </label>
          <p className="table-note">Nastavnik može zaključiti i drugačiju ocenu od predloga, na osnovu zalaganja i napretka učenika.</p>
        </Modal>
      )}
      {noteOpen && (
        <Modal eyebrow={selectedStudentName.toUpperCase()} title="Nova beleška" submitLabel="Sačuvaj belešku" onClose={() => setNoteOpen(false)} onSubmit={addNote}>
          <label>Zapažanje<textarea name="content" rows={4} minLength={3} maxLength={2000} required placeholder="Npr. Aktivno učestvuje u radu, treba više vežbe u pisanju sastava." /></label>
        </Modal>
      )}
    </div>
  )
}

function GradeModal({ grade, studentId, subjects, onClose, onSaved }: {
  grade: Grade | null
  studentId: number
  subjects: Subject[]
  onClose: () => void
  onSaved: (grade: Grade) => void
}) {
  const [subjectId, setSubjectId] = useState(grade?.subjectId ?? subjects[0]?.id ?? 0)
  const descriptive = subjects.find((subject) => subject.id === subjectId)?.usesDescriptiveScale ?? false

  async function submit(form: FormData) {
    const body = {
      subjectId,
      value: Number(form.get('value')),
      gradeType: String(form.get('gradeType')),
      gradedOn: String(form.get('gradedOn')),
    }
    const saved = grade
      ? await api<Grade>(`/api/grades/${grade.id}`, { method: 'PUT', body })
      : await api<Grade>(`/api/students/${studentId}/grades`, { method: 'POST', body })
    onSaved(saved)
  }

  return (
    <Modal eyebrow="EVIDENCIJA ODELJENJA" title={grade ? 'Izmeni ocenu' : 'Nova ocena'} submitLabel="Sačuvaj" onClose={onClose} onSubmit={submit}>
      <label>Predmet
        <select value={subjectId} onChange={(event) => setSubjectId(Number(event.target.value))} required>
          {subjects.map((subject) => <option key={subject.id} value={subject.id}>{subject.name}</option>)}
        </select>
      </label>
      <div className="form-grid">
        <label>Ocena
          <select name="value" defaultValue={grade?.value ?? 5}>
            {[5, 4, 3, 2, 1].map((value) => <option key={value} value={value}>{value}{descriptive ? ` · ${descriptiveScale[value - 1]}` : ''}</option>)}
          </select>
        </label>
        <label>Vrsta
          <select name="gradeType" defaultValue={grade?.gradeType ?? gradeTypes[0]}>
            {gradeTypes.map((type) => <option key={type}>{type}</option>)}
          </select>
        </label>
      </div>
      <label>Datum<input name="gradedOn" type="date" max={todayIso()} defaultValue={grade?.gradedOn ?? todayIso()} required /></label>
    </Modal>
  )
}
