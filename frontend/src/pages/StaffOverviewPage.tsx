import { useEffect, useState } from 'react'
import { AlertTriangle, CalendarClock, ChevronRight, MessageCircle, School, Users } from 'lucide-react'
import { useApp } from '../AppContext'
import { NoticePanel } from '../components/NoticePanel'
import { EmptyState, PanelHeading, Stat } from '../components/ui'
import { classRole } from '../lib/classes'
import { plural } from '../lib/format'
import { currentLesson, dayNames, lessonTime, nextLessonNumber, schoolDay } from '../lib/schedule'
import { useApi } from '../lib/useApi'
import type { Dashboard, DashboardLesson } from '../types'

// Početna strana nastavnika i administratora: šta je sada na času, današnji časovi i izbor odeljenja.
export function StaffOverviewPage() {
  const { user, classes, subjects, notifications, selectClass, setPage } = useApp()
  const isAdmin = user.role === 'Administrator'
  const dashboard = useApi<Dashboard>('/api/dashboard', { lessons: [], attention: [], counts: null })
  const [now, setNow] = useState(() => new Date())

  // Osvežava se svakog minuta, da oznaka "u toku" prati zvono.
  useEffect(() => {
    const timer = window.setInterval(() => setNow(new Date()), 60_000)
    return () => window.clearInterval(timer)
  }, [])

  const today = schoolDay(now)
  const running = today ? currentLesson(now) : null
  const upcomingNumber = today ? nextLessonNumber(now) : null
  // Vikendom i posle nastave prikazuje se sledeći radni dan.
  const todayLessons = dashboard.data.lessons.filter((lesson) => lesson.day === today)
  const schoolOver = today === null || (running === null && todayLessons.every((lesson) => upcomingNumber === null || lesson.lessonNumber < upcomingNumber))
  const shownDay = !schoolOver ? today! : today === null || today === 5 ? 1 : today + 1
  const shownLessons = dashboard.data.lessons.filter((lesson) => lesson.day === shownDay)
  const pendingComplaints = notifications.items.filter((item) => item.kind === 'complaint').length
  const linked = dashboard.data.counts?.linkedStudents ?? 0

  function openClass(classId: number, page: 'Odeljenje' | 'Ocene' = 'Odeljenje') {
    selectClass(classId)
    setPage(page)
  }

  function status(lesson: DashboardLesson) {
    if (schoolOver) return null
    if (lesson.lessonNumber === running) return 'U toku'
    if (lesson.lessonNumber === upcomingNumber) return 'Sledeći'
    return lesson.lessonNumber < (running ?? upcomingNumber ?? 8) ? 'Završen' : null
  }

  const myToday = isAdmin ? [] : todayLessons
  const nextMine = isAdmin ? undefined : shownLessons.find((lesson) => schoolOver || lesson.lessonNumber >= (running ?? upcomingNumber ?? 8))

  return <>
    <section className="stats-grid">
      {isAdmin ? <>
        <Stat icon={School} label="Odeljenja" value={String(classes.length)} detail={`${dashboard.data.counts?.students ?? 0} učenika u školi`} tone="green" />
        <Stat icon={Users} label="Nastavnici" value={String(dashboard.data.counts?.teachers ?? 0)} detail={`${linked} ${plural(linked, 'učenik ima', 'učenika imaju', 'učenika ima')} povezanog roditelja`} tone="blue" />
        <Stat icon={AlertTriangle} label="Za podešavanje" value={String(dashboard.data.attention.length)} detail={dashboard.data.attention.length ? 'Odeljenja bez razrednog ili nastavnika' : 'Sve dodele su popunjene'} tone="coral" />
      </> : <>
        <Stat
          icon={CalendarClock}
          label="Časovi danas"
          value={String(myToday.length)}
          detail={nextMine ? `${schoolOver ? dayNames[shownDay - 1] : 'Sledeći'}: ${nextMine.lessonNumber}. čas · ${nextMine.className} · ${nextMine.subject}` : today ? 'Nemate više časova danas' : 'Danas nema nastave'}
          tone="blue"
        />
        <Stat icon={Users} label="Moja odeljenja" value={String(classes.length)} detail={classes.some((item) => item.isMyHomeroom) ? `Razredni starešina: ${classes.filter((item) => item.isMyHomeroom).map((item) => item.name).join(', ')}` : 'Predmetni nastavnik'} tone="green" />
        <Stat icon={MessageCircle} label="Nepročitane poruke" value={String(notifications.unreadMessages)} detail={pendingComplaints ? `${pendingComplaints} ${plural(pendingComplaints, 'žalba čeka', 'žalbe čekaju', 'žalbi čeka')} odgovor` : 'Od roditelja učenika'} tone="coral" />
      </>}
    </section>

    <div className="overview-lower">
      <section className="panel">
        <PanelHeading
          eyebrow={schoolOver ? `RASPORED · ${dayNames[shownDay - 1].toUpperCase()}` : running ? `SADA JE ${running}. ČAS · ${lessonTime(running)}` : upcomingNumber === 1 ? `NASTAVA POČINJE U ${lessonTime(1).split('–')[0]}` : `ODMOR · SLEDI ${upcomingNumber}. ČAS`}
          title={isAdmin ? (schoolOver ? 'Nastava za sledeći radni dan' : 'Danas u školi') : (schoolOver ? `Vaši časovi · ${dayNames[shownDay - 1].toLowerCase()}` : 'Vaši časovi danas')}
        >
          <button className="text-link" onClick={() => setPage('Raspored')}>Raspored <ChevronRight size={15} /></button>
        </PanelHeading>
        {isAdmin
          ? <SchoolNow lessons={shownLessons} lessonNumber={schoolOver ? 1 : running ?? upcomingNumber ?? 1} onOpen={openClass} />
          : <div className="test-list">
            {shownLessons.map((lesson) => {
              const state = status(lesson)
              return (
                <button className={`test-row lesson-link ${state === 'U toku' ? 'is-now' : ''}`} key={`${lesson.day}-${lesson.lessonNumber}`} onClick={() => openClass(lesson.classId, 'Ocene')}>
                  <span className="test-icon lesson-number">{lesson.lessonNumber}.</span>
                  <div><strong>{lesson.className} · {lesson.subject}</strong><small>{lessonTime(lesson.lessonNumber)}</small></div>
                  {state && <span className={`status-pill ${state === 'Završen' ? 'is-done' : ''}`}>{state}</span>}
                  <ChevronRight size={15} />
                </button>
              )
            })}
            {!dashboard.loading && shownLessons.length === 0 && <EmptyState>Nemate časove {schoolOver ? 'tog dana' : 'danas'}.</EmptyState>}
          </div>}
        {!isAdmin && shownLessons.length > 0 && <p className="table-note">Klik na čas otvara ocene tog odeljenja.</p>}
      </section>
      <NoticePanel />
    </div>

    {isAdmin && dashboard.data.attention.length > 0 && (
      <section className="panel section-block">
        <PanelHeading eyebrow="POTREBNO PODESITI" title="Dodele koje nedostaju">
          <button className="text-link" onClick={() => setPage('Administracija')}>Administracija <ChevronRight size={15} /></button>
        </PanelHeading>
        <div className="test-list">
          {dashboard.data.attention.map((item) => (
            <div className="test-row" key={`${item.kind}-${item.classId}`}>
              <span className="test-icon homework-icon"><AlertTriangle size={17} /></span>
              <div><strong>{item.text}</strong></div>
            </div>
          ))}
        </div>
      </section>
    )}

    <section className="section-block">
      <div className="section-heading"><div><span className="eyebrow">{isAdmin ? 'SVA ODELJENJA' : 'MOJA ODELJENJA'}</span><h2>Izaberite odeljenje</h2></div><span className="muted">{classes.length} {plural(classes.length, 'odeljenje', 'odeljenja', 'odeljenja')}</span></div>
      <div className="quick-grid">
        {classes.map((schoolClass, index) => (
          <button className="quick-card" key={schoolClass.id} onClick={() => openClass(schoolClass.id)}>
            <span className={`quick-icon quick-${index % 4}`}>{schoolClass.name.split('-')[0]}</span>
            <strong>{schoolClass.name}</strong><span>{classRole(schoolClass, subjects, isAdmin)}</span>
            <ChevronRight size={16} className="quick-arrow" />
          </button>
        ))}
        {classes.length === 0 && <EmptyState>Još niste dodeljeni nijednom odeljenju. Obratite se administratoru škole.</EmptyState>}
      </div>
    </section>
  </>
}

// Administrator: šta svako odeljenje ima na trenutnom (ili sledećem) času.
function SchoolNow({ lessons, lessonNumber, onOpen }: { lessons: DashboardLesson[]; lessonNumber: number; onOpen: (classId: number) => void }) {
  const { classes } = useApp()
  return (
    <div className="test-list">
      {classes.map((schoolClass) => {
        const lesson = lessons.find((item) => item.classId === schoolClass.id && item.lessonNumber === lessonNumber)
        return (
          <button className="test-row lesson-link" key={schoolClass.id} onClick={() => onOpen(schoolClass.id)}>
            <span className="test-icon lesson-number">{schoolClass.name.split('-')[0]}</span>
            <div>
              <strong>{schoolClass.name} · {lesson?.subject ?? 'nema nastave'}</strong>
              <small>{lesson ? `${lessonNumber}. čas · ${lesson.teacher ?? 'nastavnik nije dodeljen'}` : `${lessonNumber}. čas`}</small>
            </div>
            <ChevronRight size={15} />
          </button>
        )
      })}
    </div>
  )
}
