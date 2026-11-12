import { useState } from 'react'
import { Check, UserRound } from 'lucide-react'
import { api } from '../../api/client'
import { initials, plural } from '../../lib/format'
import type { StaffClass, StaffTeacher } from '../../types'
import { useAdminAction } from './useAdminAction'

// Ko predaje u jednom odeljenju: razredni starešina (učitelj u I–IV) i nastavnik za svaki predmet.
// Izbor se čuva odmah i ostaje dok ga administrator ne promeni ili ne ukloni.
export function ClassStaff({ schoolClass, teachers, onChanged }: { schoolClass: StaffClass; teachers: StaffTeacher[]; onChanged: () => void }) {
  const run = useAdminAction()
  const [saved, setSaved] = useState<string | null>(null)
  const [busy, setBusy] = useState<string | null>(null)
  const classTeacher = schoolClass.gradeLevel <= 4
  const roleName = classTeacher ? 'Učitelj' : 'Razredni starešina'

  async function save(key: string, path: string, teacherId: number | null, question?: string) {
    setBusy(key)
    const ok = await run(() => api(path, { method: 'PUT', body: { teacherUserId: teacherId } }), 'Izmena nije sačuvana.', question)
    setBusy(null)
    if (!ok) return
    setSaved(key)
    window.setTimeout(() => setSaved((current) => (current === key ? null : current)), 2200)
    onChanged()
  }

  function changeHomeroom(value: string) {
    const teacherId = value ? Number(value) : null
    const question = teacherId === null
      ? classTeacher
        ? `Ukloniti učitelja ${schoolClass.homeroomName} iz odeljenja ${schoolClass.name}? Predmeti koje predaje u tom odeljenju ostaju bez nastavnika.`
        : `Ukloniti razrednog starešinu ${schoolClass.homeroomName} u odeljenju ${schoolClass.name}?`
      : undefined
    save('homeroom', `/api/admin/staff/${schoolClass.classId}/homeroom`, teacherId, question)
  }

  function changeSubject(subjectId: number, subjectName: string, current: string | null, value: string) {
    const teacherId = value ? Number(value) : null
    const question = teacherId === null ? `Ukloniti nastavnika ${current} sa predmeta ${subjectName} u odeljenju ${schoolClass.name}?` : undefined
    save(`s${subjectId}`, `/api/admin/staff/${schoolClass.classId}/subjects/${subjectId}`, teacherId, question)
  }

  const missing = schoolClass.subjects.filter((item) => item.teacherId === null).length

  return (
    <div className="class-staff">
      <div className={`homeroom-card ${schoolClass.homeroomId ? '' : 'is-missing'}`}>
        <span className="homeroom-icon"><UserRound size={18} /></span>
        <div className="homeroom-copy">
          <strong>{roleName}</strong>
          <small>{classTeacher ? 'Predaje sve predmete osim engleskog jezika i veronauke i zaključuje ocene.' : 'Opravdava izostanke, odgovara na žalbe i vidi sve ocene odeljenja.'}</small>
        </div>
        <TeacherSelect
          value={schoolClass.homeroomId}
          teachers={teachers}
          disabled={busy !== null}
          emptyLabel={`Izaberite ${classTeacher ? 'učitelja' : 'razrednog'}`}
          label={`${roleName} odeljenja ${schoolClass.name}`}
          onChange={changeHomeroom}
        />
        <SavedMark show={saved === 'homeroom'} />
      </div>

      <div className="subject-teacher-head">
        <span>Predmet</span>
        <span>{missing === 0 ? 'Svi predmeti imaju nastavnika' : `${missing} ${plural(missing, 'predmet nema', 'predmeta nemaju', 'predmeta nema')} nastavnika`}</span>
      </div>
      <div className="subject-teacher-rows">
        {schoolClass.subjects.map((item) => (
          <div className={`subject-teacher-row ${item.teacherId ? '' : 'is-missing'}`} key={item.subjectId}>
            <span className="teacher-avatar">{item.teacherName ? initials(item.teacherName) : '?'}</span>
            <span className="subject-teacher-copy">
              <strong>{item.subject}</strong>
              <small>{item.weeklyLessons > 0 ? `${item.weeklyLessons} ${plural(item.weeklyLessons, 'čas', 'časa', 'časova')} nedeljno` : 'Nije u rasporedu'}</small>
            </span>
            <TeacherSelect
              value={item.teacherId}
              teachers={teachers}
              subjectId={item.subjectId}
              subjectName={item.subject}
              disabled={busy !== null}
              emptyLabel="Nastavnik nije dodeljen"
              label={`Nastavnik za ${item.subject}, ${schoolClass.name}`}
              onChange={(value) => changeSubject(item.subjectId, item.subject, item.teacherName, value)}
            />
            <SavedMark show={saved === `s${item.subjectId}`} />
          </div>
        ))}
        {schoolClass.subjects.length === 0 && <p className="table-note">Odeljenje još nema predmete u rasporedu. Dodajte časove na stranici Raspored.</p>}
      </div>
    </div>
  )
}

// Nastavnici koji već predaju taj predmet su na vrhu liste.
function TeacherSelect({ value, teachers, subjectId, subjectName, disabled, emptyLabel, label, onChange }: {
  value: number | null
  teachers: StaffTeacher[]
  subjectId?: number
  subjectName?: string
  disabled: boolean
  emptyLabel: string
  label: string
  onChange: (value: string) => void
}) {
  const experts = subjectId === undefined ? [] : teachers.filter((teacher) => teacher.subjectIds.includes(subjectId))
  const others = teachers.filter((teacher) => !experts.includes(teacher))
  return (
    <select className="staff-select" value={value ?? ''} disabled={disabled} aria-label={label} onChange={(event) => onChange(event.target.value)}>
      <option value="">{value ? '— Ukloni nastavnika —' : emptyLabel}</option>
      {experts.length > 0
        ? <>
          <optgroup label={`Predaju ${subjectName?.toLocaleLowerCase('sr')}`}>{experts.map((teacher) => <option key={teacher.id} value={teacher.id}>{teacher.fullName}</option>)}</optgroup>
          <optgroup label="Ostali nastavnici">{others.map((teacher) => <option key={teacher.id} value={teacher.id}>{teacher.fullName}</option>)}</optgroup>
        </>
        : others.map((teacher) => <option key={teacher.id} value={teacher.id}>{teacher.fullName}</option>)}
    </select>
  )
}

const SavedMark = ({ show }: { show: boolean }) => <span className={`saved-mark ${show ? 'is-visible' : ''}`} aria-live="polite">{show && <><Check size={13} /> Sačuvano</>}</span>
