import { useState } from 'react'
import { Check, ChevronRight, Copy, KeyRound, RefreshCw } from 'lucide-react'
import { api } from '../api/client'
import { useApp } from '../AppContext'
import { ClassSelect, PanelHeading } from '../components/ui'
import { formatCode } from '../lib/format'
import { useApi } from '../lib/useApi'
import type { AdminStudent, Page } from '../types'
import { useAdminAction } from './admin/useAdminAction'

export function ClassPage() {
  const { user, isStaff, isTeacher, classes, selectedClass, students, selectedStudentId, selectStudent, setPage, showError } = useApp()
  const isAdmin = user.role === 'Administrator'
  const run = useAdminAction()
  // Administrator vidi kod svakog učenika koji roditelju saopštava pri dolasku u školu.
  const codes = useApi<AdminStudent[]>(isAdmin && selectedClass ? `/api/admin/students?classId=${selectedClass.id}` : null, [])
  const [copied, setCopied] = useState<number | null>(null)
  const linkedParents = students.filter((student) => student.parentName).length

  function openStudent(studentId: number, page: Page) {
    selectStudent(studentId)
    setPage(page)
  }

  async function copy(row: AdminStudent) {
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
      `Izdati novi kod za učenika ${row.fullName}? Stari kod prestaje da važi.`)) codes.reload()
  }

  return (
    <section className="panel data-panel">
      <PanelHeading eyebrow="ODELJENJE" title={isAdmin ? 'Učenici i kodovi za roditelje' : 'Učenici i razredni starešina'}>
        {isStaff && classes.length > 1 ? <ClassSelect /> : <span className="class-label">{selectedClass?.name}</span>}
      </PanelHeading>
      <div className="class-summary">
        <span><strong>{students.length}</strong> učenika</span>
        <span><strong>{selectedClass?.gradeLevel ?? ''}</strong> razred</span>
        {isStaff && <span><strong>{linkedParents}</strong> povezanih roditelja</span>}
      </div>
      {isAdmin && (
        <p className="code-hint"><KeyRound size={16} /> Kod učenika se roditelju saopštava lično, pri dolasku u školu. Roditelj ga unosi pri registraciji, a posle povezivanja kod prestaje da važi.</p>
      )}
      <div className="table-scroll">
        <table>
          <thead><tr><th>UČENIK</th>{isAdmin ? <><th>KOD ZA RODITELJA</th><th>RODITELJ</th></> : <th>ODELJENJE</th>}{isStaff && !isAdmin && <th>RODITELJ</th>}{isStaff && <th>AKCIJA</th>}</tr></thead>
          <tbody>
            {students.map((student) => {
              const code = codes.data.find((row) => row.studentId === student.id)
              return (
                <tr className={isStaff && selectedStudentId === student.id ? 'selected-student-row' : ''} key={student.id}>
                  <td><strong>{student.fullName}</strong></td>
                  {isAdmin
                    ? <>
                      <td>
                        {student.parentName
                          ? <span className="status-pill">Iskorišćen</span>
                          : code && <span className="code-cell">
                            <code className="code-chip">{formatCode(code.registrationCode)}</code>
                            <button className="icon-button" onClick={() => copy(code)} title="Kopiraj kod" aria-label={`Kopiraj kod za ${student.fullName}`}>{copied === student.id ? <Check size={14} /> : <Copy size={14} />}</button>
                            <button className="icon-button" onClick={() => regenerate(code)} title="Izdaj novi kod" aria-label={`Novi kod za ${student.fullName}`}><RefreshCw size={14} /></button>
                          </span>}
                      </td>
                      <td>{student.parentName ?? 'Nije povezan'}</td>
                    </>
                    : <td>{student.className}</td>}
                  {isStaff && !isAdmin && <td>{student.parentName ?? 'Nije povezan'}</td>}
                  {isStaff && (
                    <td>
                      <span className="row-actions">
                        <button className="text-link" onClick={() => openStudent(student.id, 'Ocene')}>Ocene <ChevronRight size={14} /></button>
                        <button className="text-link" onClick={() => openStudent(student.id, 'Izostanci i žalbe')}>Izostanci</button>
                        {isTeacher && student.parentName && <button className="text-link" onClick={() => openStudent(student.id, 'Poruke')}>Poruka roditelju</button>}
                      </span>
                    </td>
                  )}
                </tr>
              )
            })}
          </tbody>
        </table>
        {!isStaff && <p className="table-note">Ocenama i dosijeom svakog učenika može da pristupi samo njegov roditelj.</p>}
      </div>
    </section>
  )
}
