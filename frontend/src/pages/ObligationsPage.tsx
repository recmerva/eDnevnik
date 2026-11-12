import { useState } from 'react'
import { CalendarDays, Check, FileText, NotebookPen, Plus, Trash2 } from 'lucide-react'
import { api, errorMessage } from '../api/client'
import { useApp } from '../AppContext'
import { Modal } from '../components/Modal'
import { ClassSelect, EmptyState, PanelHeading } from '../components/ui'
import { daysFromToday, formatDate, todayIso } from '../lib/format'
import { teachableSubjects } from '../lib/grades'
import { useApi } from '../lib/useApi'
import type { Homework, SchoolTest } from '../types'

function dueStatus(date: string, pastLabel: string) {
  const days = daysFromToday(date)
  if (days > 1) return `Za ${days} dana`
  if (days === 1) return 'Sutra'
  if (days === 0) return 'Danas'
  return pastLabel
}

// Predstojeće stavke prve (najbliži rok na vrhu), zatim prošle od najnovije.
function byDeadline<T>(items: T[], dateOf: (item: T) => string) {
  const upcoming = items.filter((item) => daysFromToday(dateOf(item)) >= 0).sort((first, second) => dateOf(first).localeCompare(dateOf(second)))
  const past = items.filter((item) => daysFromToday(dateOf(item)) < 0).sort((first, second) => dateOf(second).localeCompare(dateOf(first)))
  return { upcoming, past }
}

export function ObligationsPage() {
  const { isStaff, isTeacher, classes, selectedClass, subjects, showError, refreshNotifications } = useApp()
  const classPath = selectedClass ? `/api/classes/${selectedClass.id}` : null
  const homework = useApi<Homework[]>(classPath && `${classPath}/homework`, [])
  const tests = useApi<SchoolTest[]>(classPath && `${classPath}/tests`, [])
  const [form, setForm] = useState<'homework' | 'test' | null>(null)
  const mySubjects = teachableSubjects(subjects, selectedClass)
  const canManage = (subjectId: number) => isTeacher && mySubjects.some((subject) => subject.id === subjectId)

  const homeworkList = byDeadline(homework.data, (item) => item.dueDate)
  const testList = byDeadline(tests.data, (item) => item.heldOn)

  async function addHomework(data: FormData) {
    const saved = await api<Homework>(`${classPath}/homework`, {
      method: 'POST',
      body: {
        subjectId: Number(data.get('subjectId')),
        title: String(data.get('title')).trim(),
        description: String(data.get('description')).trim() || null,
        dueDate: String(data.get('dueDate')),
      },
    })
    homework.setData((current) => [saved, ...current])
    refreshNotifications()
  }

  async function addTest(data: FormData) {
    const saved = await api<SchoolTest>(`${classPath}/tests`, {
      method: 'POST',
      body: { title: String(data.get('title')).trim(), subjectId: Number(data.get('subjectId')), heldOn: String(data.get('heldOn')) },
    })
    tests.setData((current) => [saved, ...current])
    refreshNotifications()
  }

  async function remove(path: string, question: string, onDone: () => void) {
    if (!window.confirm(question)) return
    try {
      await api(path, { method: 'DELETE' })
      onDone()
      refreshNotifications()
    } catch (error) {
      showError(errorMessage(error, 'Brisanje nije uspelo.'))
    }
  }

  const testSubjects = mySubjects.filter((subject) => !subject.usesDescriptiveScale)

  return (
    <div className="obligations-layout">
      <section className="panel data-panel">
        <PanelHeading eyebrow="DOMAĆI ZADACI" title={`Obaveze učenika · ${selectedClass?.name ?? ''}`}>
          {isStaff && classes.length > 1 && <ClassSelect />}
          {isTeacher && <button className="button button-primary" onClick={() => setForm('homework')} disabled={!selectedClass || mySubjects.length === 0}><Plus size={16} /> Novi domaći</button>}
        </PanelHeading>
        <div className="test-list">
          {[...homeworkList.upcoming, ...homeworkList.past].map((item) => {
            const status = dueStatus(item.dueDate, 'Rok istekao')
            return (
              <div className="test-row" key={item.id}>
                <span className="test-icon homework-icon"><NotebookPen size={18} /></span>
                <div>
                  <strong>{item.title}</strong>
                  <small>{item.subject} · rok {formatDate(item.dueDate)} · {item.authorName}</small>
                  {item.description && <small className="obligation-description">{item.description}</small>}
                </div>
                <span className="test-result">{status} {status === 'Rok istekao' ? <Check size={13} /> : <CalendarDays size={13} />}</span>
                {canManage(item.subjectId) && (
                  <button className="icon-button" aria-label={`Obriši domaći ${item.title}`} title="Obriši"
                    onClick={() => remove(`/api/homework/${item.id}`, `Obrisati domaći "${item.title}"?`, () => homework.setData((current) => current.filter((other) => other.id !== item.id)))}>
                    <Trash2 size={14} />
                  </button>
                )}
              </div>
            )
          })}
          {!homework.loading && homework.data.length === 0 && <EmptyState>Nema zadatih domaćih zadataka.</EmptyState>}
        </div>
      </section>

      <section className="panel data-panel">
        <PanelHeading eyebrow="PLAN PROVERA ZNANJA" title="Testovi">
          {isTeacher && <button className="button button-primary" onClick={() => setForm('test')} disabled={!selectedClass || testSubjects.length === 0}><Plus size={16} /> Novi test</button>}
        </PanelHeading>
        <div className="test-list">
          {[...testList.upcoming, ...testList.past].map((test) => {
            const status = dueStatus(test.heldOn, 'Održan')
            return (
              <div className="test-row" key={test.id}>
                <span className="test-icon"><FileText size={18} /></span>
                <div><strong>{test.title}</strong><small>{test.subject} · {formatDate(test.heldOn)}</small></div>
                <span className="test-result">{status} {status === 'Održan' ? <Check size={13} /> : <CalendarDays size={13} />}</span>
                {canManage(test.subjectId) && (
                  <button className="icon-button" aria-label={`Obriši test ${test.title}`} title="Obriši"
                    onClick={() => remove(`/api/tests/${test.id}`, `Obrisati test "${test.title}"?`, () => tests.setData((current) => current.filter((other) => other.id !== test.id)))}>
                    <Trash2 size={14} />
                  </button>
                )}
              </div>
            )
          })}
          {!tests.loading && tests.data.length === 0 && <EmptyState>Nema evidentiranih testova.</EmptyState>}
        </div>
      </section>

      {form === 'homework' && (
        <Modal eyebrow="DOMAĆI ZADATAK" title={`Novi domaći · ${selectedClass?.name ?? ''}`} submitLabel="Zadaj domaći" onClose={() => setForm(null)} onSubmit={addHomework}>
          <label>Predmet
            <select name="subjectId" defaultValue={mySubjects[0]?.id}>
              {mySubjects.map((subject) => <option key={subject.id} value={subject.id}>{subject.name}</option>)}
            </select>
          </label>
          <label>Naslov<input name="title" placeholder="Npr. Zadaci 1–10, strana 45" required minLength={3} maxLength={160} /></label>
          <label>Uputstvo i komentar<textarea name="description" rows={3} maxLength={1000} placeholder="Šta učenici treba da urade i na šta da obrate pažnju" /></label>
          <label>Rok izrade<input name="dueDate" type="date" min={todayIso()} defaultValue={todayIso()} required /></label>
        </Modal>
      )}
      {form === 'test' && (
        <Modal eyebrow="PLAN PROVERA ZNANJA" title={`Novi test · ${selectedClass?.name ?? ''}`} submitLabel="Sačuvaj test" onClose={() => setForm(null)} onSubmit={addTest}>
          <label>Naziv testa<input name="title" placeholder="Npr. Kontrolni zadatak: razlomci" required minLength={3} maxLength={160} /></label>
          <label>Predmet
            <select name="subjectId" defaultValue={testSubjects[0]?.id}>
              {testSubjects.map((subject) => <option key={subject.id} value={subject.id}>{subject.name}</option>)}
            </select>
          </label>
          <label>Datum održavanja<input name="heldOn" type="date" defaultValue={todayIso()} required /></label>
        </Modal>
      )}
    </div>
  )
}
