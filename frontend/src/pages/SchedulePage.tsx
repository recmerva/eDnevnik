import { useState } from 'react'
import { api, errorMessage } from '../api/client'
import { useApp } from '../AppContext'
import { ClassSelect, EmptyState, PanelHeading } from '../components/ui'
import { initials, plural } from '../lib/format'
import { useApi } from '../lib/useApi'
import type { Schedule, StaffOverview } from '../types'
import { ClassStaff } from './admin/ClassStaff'

const days = ['Ponedeljak', 'Utorak', 'Sreda', 'Četvrtak', 'Petak']
const lessonsPerDay = 7
// Vikendom se prikazuje ponedeljak, a radnim danom tekući dan.
const todayIndex = () => { const day = new Date().getDay(); return day >= 1 && day <= 5 ? day : 1 }

export function SchedulePage() {
  const { user, isStaff, classes, subjects, selectedClass, showError, refreshNotifications } = useApp()
  const isAdmin = user.role === 'Administrator'
  const schedule = useApi<Schedule>(selectedClass ? `/api/classes/${selectedClass.id}/schedule` : null, { entries: [], subjects: [] })
  // Administrator na istom mestu bira i nastavnika za svaki predmet odeljenja.
  const staff = useApi<StaffOverview>(isAdmin ? '/api/admin/staff' : null, { teachers: [], classes: [] })
  const classStaff = staff.data.classes.find((item) => item.classId === selectedClass?.id)
  const [day, setDay] = useState(todayIndex)
  const [saving, setSaving] = useState<number | null>(null)

  const lessonCount = (weekday: number) => schedule.data.entries.filter((entry) => entry.day === weekday).length
  const dayEntries = schedule.data.entries.filter((entry) => entry.day === day)
  const lastLesson = Math.max(0, ...dayEntries.map((entry) => entry.lessonNumber))
  const slots = Array.from({ length: isAdmin ? lessonsPerDay : lastLesson }, (_, index) => index + 1)

  async function setLesson(lessonNumber: number, value: string) {
    setSaving(lessonNumber)
    try {
      await api(`/api/classes/${selectedClass!.id}/schedule`, {
        method: 'PUT',
        body: { day, lessonNumber, subjectId: value ? Number(value) : null },
      })
      schedule.reload()
      if (isAdmin) staff.reload()
      refreshNotifications()
    } catch (error) {
      showError(errorMessage(error, 'Raspored nije izmenjen.'))
    } finally {
      setSaving(null)
    }
  }

  return (
    <div className="schedule-layout">
      <section className="panel data-panel">
        <PanelHeading eyebrow="NEDELJNI RASPORED" title={`Raspored časova · ${selectedClass?.name ?? ''}`}>
          {isStaff && classes.length > 1 && <ClassSelect />}
        </PanelHeading>
        <div className="day-tabs schedule-tabs" role="tablist" aria-label="Dan u nedelji">
          {days.map((name, index) => (
            <button key={name} role="tab" aria-selected={day === index + 1} className={`day-tab ${day === index + 1 ? 'is-active' : ''}`} onClick={() => setDay(index + 1)}>
              <span className="day-full">{name}</span><span className="day-short">{name.slice(0, 3)}</span><small>{lessonCount(index + 1)} {plural(lessonCount(index + 1), 'čas', 'časa', 'časova')}</small>
            </button>
          ))}
        </div>
        {isAdmin && <p className="table-note">Izmena časa automatski šalje obaveštenje roditeljima i nastavnicima odeljenja.</p>}
        <div className="test-list">
          {slots.map((lessonNumber) => {
            const entry = dayEntries.find((item) => item.lessonNumber === lessonNumber)
            return (
              <div className={`test-row ${isAdmin ? 'lesson-row' : ''}`} key={lessonNumber}>
                <span className="test-icon lesson-number">{lessonNumber}.</span>
                <div>
                  <strong>{entry?.subject ?? 'Slobodan čas'}</strong>
                  <small>{entry ? entry.teacher ?? 'Nastavnik nije dodeljen' : 'Nema nastave'}</small>
                </div>
                {isAdmin && (
                  <select
                    className="class-select"
                    value={entry?.subjectId ?? ''}
                    disabled={saving !== null}
                    onChange={(event) => setLesson(lessonNumber, event.target.value)}
                    aria-label={`Predmet za ${lessonNumber}. čas`}
                  >
                    <option value="">Slobodan čas</option>
                    {subjects.map((subject) => <option key={subject.id} value={subject.id}>{subject.name}</option>)}
                  </select>
                )}
              </div>
            )
          })}
          {!schedule.loading && slots.length === 0 && <EmptyState>Za ovaj dan nema unetih časova.</EmptyState>}
        </div>
      </section>

      {isAdmin && classStaff ? (
        <section className="panel data-panel">
          <PanelHeading eyebrow="NASTAVNI KADAR" title="Ko predaje koji predmet" />
          <ClassStaff key={classStaff.classId} schoolClass={classStaff} teachers={staff.data.teachers} onChanged={() => { staff.reload(); schedule.reload() }} />
        </section>
      ) : (
        <section className="panel data-panel">
          <PanelHeading eyebrow="NASTAVNI KADAR" title="Predmeti i nastavnici" />
          <div className="staff-subjects">
            {schedule.data.subjects.map((item) => (
              <div className="staff-subject" key={item.subjectId}>
                <span className="teacher-avatar">{item.teacher ? initials(item.teacher) : '?'}</span>
                <span className="staff-subject-copy">
                  <strong>{item.subject}</strong>
                  <small className={item.teacher ? undefined : 'is-missing'}>{item.teacher ?? 'Nastavnik nije dodeljen'}</small>
                </span>
                <span className="lesson-count" title={`${item.weeklyLessons} ${plural(item.weeklyLessons, 'čas', 'časa', 'časova')} nedeljno`}>
                  <b>{item.weeklyLessons}</b>{plural(item.weeklyLessons, 'čas', 'časa', 'časova')}
                </span>
              </div>
            ))}
            {!schedule.loading && schedule.data.subjects.length === 0 && <EmptyState>Raspored još nije unet.</EmptyState>}
          </div>
          {schedule.data.subjects.length > 0 && (
            <p className="table-note">Ukupno {schedule.data.subjects.reduce((sum, item) => sum + item.weeklyLessons, 0)} časova nedeljno · broj pored predmeta je nedeljni fond.</p>
          )}
        </section>
      )}
    </div>
  )
}
