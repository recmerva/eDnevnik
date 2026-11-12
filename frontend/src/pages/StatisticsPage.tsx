import { ArrowDownRight, ArrowUpRight, FileText } from 'lucide-react'
import { useApp } from '../AppContext'
import { ChooseStudent, ClassSelect, EmptyState, PanelHeading, StudentSelect } from '../components/ui'
import { lessonHours } from '../lib/format'
import { averageOf, formatAverage, overallSuccess, recentTrend, subjectAverages } from '../lib/grades'
import { useApi } from '../lib/useApi'
import type { AbsenceRecord, ClassReport, FinalGrade, Grade, Statistics } from '../types'

const shortName = (subject: string) => subject.length > 10 ? `${subject.slice(0, 9)}.` : subject

export function StatisticsPage() {
  const { isStaff, school, selectedStudentId, selectedStudentName, selectedClass } = useApp()
  const studentPath = selectedStudentId ? `/api/students/${selectedStudentId}` : null
  const grades = useApi<Grade[]>(studentPath && `${studentPath}/grades`, [])
  const finals = useApi<FinalGrade[]>(studentPath && `${studentPath}/final-grades`, [])
  const absences = useApi<AbsenceRecord[]>(studentPath && `${studentPath}/absences`, [])
  const stats = useApi<Statistics | null>(studentPath && `${studentPath}/statistics`, null)

  const studentAverage = averageOf(grades.data)
  const trend = recentTrend(grades.data)
  const success = overallSuccess(grades.data, finals.data, school.currentTerm)
  const classAverage = stats.data?.classAverage ?? null
  // Predmetni nastavnik dobija podatke samo iz svojih predmeta.
  const limited = stats.data?.limited ?? false
  const bars = subjectAverages(grades.data).sort((first, second) => first.subject.localeCompare(second.subject, 'sr'))
  const totalHours = absences.data.reduce((sum, item) => sum + item.lessonHours, 0)
  const unexcusedHours = absences.data.filter((item) => !item.isExcused).reduce((sum, item) => sum + item.lessonHours, 0)
  const comparison = studentAverage === null || classAverage === null
    ? 'Nema dovoljno podataka za poređenje'
    : studentAverage >= classAverage
      ? `${formatAverage(studentAverage - classAverage)} iznad proseka odeljenja`
      : `${formatAverage(classAverage - studentAverage)} ispod proseka odeljenja`

  if (isStaff && !selectedStudentId) {
    return <>
      <ChooseStudent eyebrow="IZVEŠTAJ UČENIKA" title="Uspeh učenika" />
      {selectedClass && <ClassReportPanel classId={selectedClass.id} className={selectedClass.name} />}
    </>
  }

  return (
    <>
      <section className="panel data-panel">
        <PanelHeading eyebrow={`IZVEŠTAJ · ${selectedStudentName.toUpperCase()}`} title="Uspeh učenika">
          {isStaff && <StudentSelect />}
          <button className="button button-secondary" onClick={() => window.print()}><FileText size={15} /> Izvezi PDF</button>
        </PanelHeading>
        <div className="report-summary">
          <div>
            <span>Prosek učenika</span>
            <strong>{formatAverage(studentAverage)}</strong>
            {trend === null
              ? <small>Na osnovu evidentiranih ocena</small>
              : trend >= 0
                ? <small className="trend"><ArrowUpRight size={14} /> +{formatAverage(trend)} u poslednjih 30 dana</small>
                : <small><ArrowDownRight size={14} /> −{formatAverage(-trend)} u poslednjih 30 dana</small>}
          </div>
          <div>
            <span>Prosek odeljenja</span>
            <strong>{formatAverage(classAverage)}</strong>
            <small>{comparison}</small>
          </div>
          <div>
            <span>Rang u odeljenju</span>
            <strong>{stats.data?.rank ? `${stats.data.rank}.` : '—'}</strong>
            <small>{stats.data?.rank ? `od ${stats.data.rankedStudents} učenika po proseku` : 'Još nema ocena za rangiranje'}</small>
          </div>
          {limited
            ? <div>
              <span>Opšti uspeh · {school.currentTerm}. polugodište</span>
              <strong>—</strong>
              <small>Vidi ga razredni starešina; ovde su samo vaši predmeti</small>
            </div>
            : <div>
              <span>Opšti uspeh · {school.currentTerm}. polugodište</span>
              <strong>{success ? success.label : '—'}</strong>
              <small>Zaključne ocene, a gde ih nema predlog iz proseka</small>
            </div>}
          <div>
            <span>Izostanci ukupno</span>
            <strong>{totalHours}</strong>
            <small>{lessonHours(totalHours)} u školskoj {school.schoolYear}</small>
          </div>
          <div>
            <span>Neopravdani izostanci</span>
            <strong>{unexcusedHours}</strong>
            <small>{totalHours - unexcusedHours} opravdano</small>
          </div>
        </div>
        <div className="chart-title"><strong>Prosek po predmetima</strong><span>Skala 1–5 · bez opisnih ocena</span></div>
        {bars.length > 0 ? (
          <div className="bar-chart">
            {bars.map((item) => {
              const classValue = stats.data?.subjects.find((subject) => subject.subject === item.subject)?.classAverage
              return (
                <div className="bar-column" key={item.subjectId} title={`${item.subject}: ${formatAverage(item.average)}${classValue ? ` (odeljenje ${formatAverage(classValue)})` : ''}`}>
                  <span>{item.average.toFixed(1).replace('.', ',')}</span>
                  <i style={{ height: `${item.average / 5 * 100}%` }} />
                  <small>{shortName(item.subject)}</small>
                </div>
              )
            })}
          </div>
        ) : <EmptyState>Nema ocena za prikaz grafikona.</EmptyState>}
      </section>

      {isStaff && selectedClass && <ClassReportPanel classId={selectedClass.id} className={selectedClass.name} />}
    </>
  )
}

// Uspeh celog odeljenja: raspodela uspeha, proseci predmeta i tabela svih učenika.
function ClassReportPanel({ classId, className }: { classId: number; className: string }) {
  const { classes, selectStudent } = useApp()
  const report = useApi<ClassReport | null>(`/api/classes/${classId}/report`, null)
  const data = report.data
  const ranked = data ? [...data.students].sort((first, second) => (second.average ?? 0) - (first.average ?? 0)) : []
  const largest = Math.max(1, ...(data?.distribution.map((item) => item.count) ?? []))

  return (
    <section className="panel data-panel class-report">
      <PanelHeading eyebrow={`STATISTIKA ODELJENJA · ${data?.term ?? ''}. POLUGODIŠTE`} title={`Uspeh odeljenja ${className}`}>
        {classes.length > 1 && <ClassSelect />}
      </PanelHeading>
      {data && <>
        <div className="report-summary">
          <div><span>Prosek odeljenja</span><strong>{formatAverage(data.classAverage)}</strong><small>{data.students.length} učenika</small></div>
          {!data.limited && <div><span>Odličnih i vrlo dobrih</span><strong>{data.distribution.filter((item) => item.success === 'Odličan' || item.success === 'Vrlo dobar').reduce((sum, item) => sum + item.count, 0)}</strong><small>{data.distribution.find((item) => item.success === 'Nedovoljan')?.count ?? 0} sa nedovoljnim uspehom</small></div>}
          <div><span>Izostanci odeljenja</span><strong>{data.absenceHours}</strong><small>{data.unexcusedHours} neopravdano</small></div>
        </div>
        {data.limited
          ? <>
            <div className="chart-title"><strong>Prosek odeljenja po vašim predmetima</strong><span>Skala 1–5</span></div>
            <div className="bar-chart">
              {data.subjects.map((item) => (
                <div className="bar-column" key={item.subject} title={`${item.subject}: ${formatAverage(item.average)}`}>
                  <span>{item.average.toFixed(1).replace('.', ',')}</span>
                  <i style={{ height: `${item.average / 5 * 100}%` }} />
                  <small>{shortName(item.subject)}</small>
                </div>
              ))}
            </div>
          </>
          : <>
            <div className="chart-title"><strong>Raspodela uspeha</strong><span>Broj učenika</span></div>
            <div className="bar-chart">
              {data.distribution.map((item) => (
                <div className="bar-column" key={item.success} title={`${item.success}: ${item.count}`}>
                  <span>{item.count}</span>
                  <i style={{ height: `${item.count / largest * 100}%`, minHeight: item.count ? undefined : 0 }} />
                  <small>{item.success}</small>
                </div>
              ))}
            </div>
          </>}
        <div className="chart-title"><strong>Učenici po proseku</strong><span>Klik na ime otvara izveštaj učenika</span></div>
        <div className="table-scroll">
          <table>
            <thead><tr><th>#</th><th>UČENIK</th><th>{data.limited ? 'PROSEK (VAŠI PREDMETI)' : 'PROSEK'}</th>{!data.limited && <th>USPEH</th>}<th>IZOSTANCI</th><th>NEOPRAVDANI</th></tr></thead>
            <tbody>
              {ranked.map((row, index) => (
                <tr key={row.studentId}>
                  <td>{index + 1}.</td>
                  <td><button className="text-link" onClick={() => { selectStudent(row.studentId); window.scrollTo({ top: 0, behavior: 'smooth' }) }}>{row.fullName}</button></td>
                  <td className="average-number">{formatAverage(row.average)}</td>
                  {!data.limited && <td>{row.success ?? '—'}</td>}
                  <td>{row.absenceHours}</td>
                  <td>{row.unexcusedHours}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <p className="table-note">{data.limited
          ? 'Prikazani su samo predmeti koje predajete u odeljenju; opšti uspeh vidi razredni starešina.'
          : 'Uspeh se računa iz zaključnih ocena tekućeg polugodišta; gde zaključna nije upisana, koristi se predlog iz proseka.'}</p>
      </>}
      {!report.loading && !data && <EmptyState>Izveštaj nije dostupan.</EmptyState>}
    </section>
  )
}
