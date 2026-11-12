import type { ReactNode } from 'react'
import type { LucideIcon } from 'lucide-react'
import { useApp } from '../AppContext'

export function PanelHeading({ eyebrow, title, children }: { eyebrow: string; title: string; children?: ReactNode }) {
  return (
    <div className="panel-heading">
      <div><span className="eyebrow">{eyebrow}</span><h2>{title}</h2></div>
      <div className="panel-heading-actions">{children}</div>
    </div>
  )
}

export function Stat({ icon: Icon, label, value, detail, tone }: { icon: LucideIcon; label: string; value: string; detail: string; tone: string }) {
  return (
    <article className={`stat-card stat-${tone}`}>
      <span className={`stat-icon ${tone}`}><Icon size={19} /></span>
      <span className="stat-label">{label}</span>
      <strong>{value}</strong>
      <small>{detail}</small>
    </article>
  )
}

export function StudentSelect() {
  const { students, selectedStudentId, selectStudent } = useApp()
  return (
    <select className="class-select" value={selectedStudentId ?? ''} onChange={(event) => selectStudent(Number(event.target.value))} aria-label="Izaberi učenika">
      <option value="" disabled>{students.length === 0 ? 'Nema učenika' : 'Izaberite učenika'}</option>
      {students.map((student) => <option key={student.id} value={student.id}>{student.fullName}</option>)}
    </select>
  )
}

// Nastavnik prvo bira učenika; dok ga ne izabere, stranica prikazuje samo izbor.
export function ChooseStudent({ eyebrow, title, children }: { eyebrow: string; title: string; children?: ReactNode }) {
  const { classes, selectedClass } = useApp()
  return (
    <section className="panel data-panel">
      <PanelHeading eyebrow={eyebrow} title={title}>
        {classes.length > 1 && <ClassSelect />}
        <StudentSelect />
      </PanelHeading>
      <EmptyState>Izaberite učenika iz odeljenja {selectedClass?.name ?? ''} da biste videli i uneli podatke.</EmptyState>
      {children}
    </section>
  )
}

export function ClassSelect() {
  const { classes, selectedClass, selectClass } = useApp()
  return (
    <select className="class-select" value={selectedClass?.id ?? ''} onChange={(event) => selectClass(Number(event.target.value))} aria-label="Izaberi odeljenje">
      {classes.length === 0 && <option value="">Nema odeljenja</option>}
      {classes.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
    </select>
  )
}

export const EmptyState = ({ children }: { children: ReactNode }) => <p className="empty-state">{children}</p>
