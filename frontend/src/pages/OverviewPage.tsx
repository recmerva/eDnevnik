import { Award, CalendarDays, ChevronRight, ClipboardCheck } from 'lucide-react'
import { useApp } from '../AppContext'
import { NoticePanel } from '../components/NoticePanel'
import { EmptyState, PanelHeading, Stat } from '../components/ui'
import { daysFromToday, formatDate, lessonHours } from '../lib/format'
import { averageOf, formatAverage } from '../lib/grades'
import { navItems } from '../lib/navigation'
import { useApi } from '../lib/useApi'
import type { AbsenceRecord, Grade, Homework, SchoolTest } from '../types'

// Početna strana roditelja: sažetak za izabrano dete.
export function OverviewPage() {
  const { selectedClass, selectedStudentId, setPage } = useApp()
  const studentPath = selectedStudentId ? `/api/students/${selectedStudentId}` : null
  const classPath = selectedClass ? `/api/classes/${selectedClass.id}` : null
  const grades = useApi<Grade[]>(studentPath && `${studentPath}/grades`, [])
  const absences = useApi<AbsenceRecord[]>(studentPath && `${studentPath}/absences`, [])
  const tests = useApi<SchoolTest[]>(classPath && `${classPath}/tests`, [])
  const homework = useApi<Homework[]>(classPath && `${classPath}/homework`, [])

  const totalHours = absences.data.reduce((sum, item) => sum + item.lessonHours, 0)
  const excusedHours = absences.data.filter((item) => item.isExcused).reduce((sum, item) => sum + item.lessonHours, 0)
  // Predstojeće obaveze: testovi i domaći zadaci sa rokom od danas nadalje, najbliži prvi.
  const upcoming = [
    ...tests.data.map((test) => ({ title: test.subject, kind: 'test', date: test.heldOn })),
    ...homework.data.map((item) => ({ title: item.subject, kind: 'domaći', date: item.dueDate })),
  ].filter((item) => daysFromToday(item.date) >= 0).sort((first, second) => first.date.localeCompare(second.date))
  const countedGrades = grades.data.filter((grade) => !grade.usesDescriptiveScale).length
  const quickLinks = navItems.filter((item) => ['Ocene', 'Raspored', 'Obaveze', 'Poruke'].includes(item.label))

  return <>
    <section className="stats-grid">
      <Stat icon={Award} label="Prosek ocena" value={formatAverage(averageOf(grades.data))} detail={countedGrades ? `Na osnovu ${countedGrades} ocena (bez opisnih)` : 'Još nema ocena'} tone="green" />
      <Stat icon={ClipboardCheck} label="Izostanci" value={lessonHours(totalHours)} detail={`${excusedHours} opravdano · ${totalHours - excusedHours} neopravdano`} tone="coral" />
      <Stat
        icon={CalendarDays}
        label="Predstojeće obaveze"
        value={String(upcoming.length)}
        detail={upcoming[0] ? `Sledeće: ${upcoming[0].title} (${upcoming[0].kind}) · ${formatDate(upcoming[0].date)}` : 'Nema zakazanih testova ni domaćih'}
        tone="blue"
      />
    </section>

    <section className="section-block">
      <div className="section-heading"><div><span className="eyebrow">BRZI PRISTUP</span><h2>Školski pregled</h2></div><span className="muted">{selectedClass?.name}</span></div>
      <div className="quick-grid">
        {quickLinks.map(({ label, icon: Icon, description }, index) => (
          <button className="quick-card" key={label} onClick={() => setPage(label)}>
            <span className={`quick-icon quick-${index}`}><Icon size={20} /></span>
            <strong>{label}</strong><span>{description}</span>
            <ChevronRight size={16} className="quick-arrow" />
          </button>
        ))}
      </div>
    </section>

    <div className="overview-lower">
      <section className="panel recent-panel">
        <PanelHeading eyebrow="NAJNOVIJE" title="Poslednje ocene">
          <button className="text-link" onClick={() => setPage('Ocene')}>Sve ocene <ChevronRight size={15} /></button>
        </PanelHeading>
        <div className="grade-list">
          {grades.data.slice(0, 3).map((grade) => (
            <div className="grade-row" key={grade.id}>
              <span className={`grade-badge grade-${grade.value}`}>{grade.value}</span>
              <span className="grade-subject"><strong>{grade.subject}</strong><small>{grade.description ?? grade.gradeType}</small></span>
              <span className="grade-date">{formatDate(grade.gradedOn)}</span>
            </div>
          ))}
          {grades.data.length === 0 && <EmptyState>Nema evidentiranih ocena.</EmptyState>}
        </div>
      </section>
      <NoticePanel />
    </div>
  </>
}
