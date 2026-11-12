import { useCallback, useEffect, useMemo, useState } from 'react'
import { ChevronRight, GraduationCap } from 'lucide-react'
import { api, onUnauthorized, resetCsrfToken } from './api/client'
import { AppContext, type AppState } from './AppContext'
import { AccountMenu } from './components/AccountMenu'
import { AuthScreen } from './components/AuthScreen'
import { NotificationsMenu } from './components/NotificationsMenu'
import { MobileMenu } from './components/MobileMenu'
import { Sidebar } from './components/Sidebar'
import { pageFromHash, pageSlug } from './lib/navigation'
import { AbsencesPage } from './pages/AbsencesPage'
import { AdminPage } from './pages/AdminPage'
import { ClassPage } from './pages/ClassPage'
import { GradesPage } from './pages/GradesPage'
import { MessagesPage } from './pages/MessagesPage'
import { OverviewPage } from './pages/OverviewPage'
import { StaffOverviewPage } from './pages/StaffOverviewPage'
import { StatisticsPage } from './pages/StatisticsPage'
import { ObligationsPage } from './pages/ObligationsPage'
import { SchedulePage } from './pages/SchedulePage'
import type { ClassRoom, Notifications, Page, SchoolInfo, Student, Subject, UserProfile } from './types'
import './styles/App.css'

const fallbackSchool: SchoolInfo = {
  name: 'Školski dnevnik', city: '', schoolYear: '', currentTerm: 1, firstTermStart: '', firstTermEnd: '', secondTermStart: '', secondTermEnd: '',
}
const todayLabel = () => new Date().toLocaleDateString('sr-Latn-RS', { weekday: 'long', day: 'numeric', month: 'long' }).replace(/^./, (letter) => letter.toUpperCase())
const portalNames = { Administrator: 'ADMINISTRATORSKI PORTAL', Nastavnik: 'NASTAVNIČKI PORTAL', Roditelj: 'RODITELJSKI PORTAL' }

export default function App() {
  const [school, setSchool] = useState<SchoolInfo>(fallbackSchool)
  const [user, setUser] = useState<UserProfile | null>(null)
  const [checking, setChecking] = useState(true)
  const reloadSchool = useCallback(() => { api<SchoolInfo>('/api/school').then(setSchool).catch(() => undefined) }, [])

  useEffect(() => {
    reloadSchool()
    api<UserProfile>('/api/auth/me').then(setUser).catch(() => undefined).finally(() => setChecking(false))
    onUnauthorized(() => { resetCsrfToken(); setUser(null) })
    return () => onUnauthorized(null)
  }, [reloadSchool])

  if (checking) return <main className="auth-loading"><span className="brand-icon"><GraduationCap size={21} /></span><p>Provera prijave...</p></main>
  if (!user) return <AuthScreen school={school} onSignedIn={setUser} />
  // Ključ po korisniku: posle odjave i nove prijave sve se učitava ispočetka.
  return <Portal key={user.id} user={user} setUser={setUser} school={school} reloadSchool={reloadSchool} onLoggedOut={() => setUser(null)} />
}

function Portal({ user, setUser, school, reloadSchool, onLoggedOut }: {
  user: UserProfile
  setUser: (user: UserProfile) => void
  school: SchoolInfo
  reloadSchool: () => void
  onLoggedOut: () => void
}) {
  const isStaff = user.role !== 'Roditelj'
  const isTeacher = user.role === 'Nastavnik'
  const [page, setPageState] = useState<Page>(() => pageFromHash(user.role) ?? 'Pregled')
  const [error, setError] = useState('')
  const [classes, setClasses] = useState<ClassRoom[]>([])
  const [subjects, setSubjects] = useState<Subject[]>([])
  const [selectedClassId, setSelectedClassId] = useState<number | null>(isStaff ? null : user.children[0]?.classId ?? null)
  const [students, setStudents] = useState<Student[]>([])
  const [selectedStudentId, setSelectedStudentId] = useState<number | null>(isStaff ? null : user.children[0]?.id ?? null)
  const [notifications, setNotifications] = useState<Notifications>({ unreadMessages: 0, items: [] })
  const [notificationsVersion, setNotificationsVersion] = useState(0)
  const [catalogVersion, setCatalogVersion] = useState(0)

  const showError = useCallback((message: string) => setError(message), [])
  const refreshNotifications = useCallback(() => setNotificationsVersion((value) => value + 1), [])
  const reloadCatalog = useCallback(() => { setCatalogVersion((value) => value + 1); reloadSchool() }, [reloadSchool])
  const childCount = user.children.length

  useEffect(() => {
    let active = true
    Promise.all([api<ClassRoom[]>('/api/classes'), api<Subject[]>('/api/subjects')])
      .then(([availableClasses, availableSubjects]) => {
        if (!active) return
        setClasses(availableClasses)
        setSubjects(availableSubjects)
        setSelectedClassId((current) => availableClasses.some((item) => item.id === current) ? current : availableClasses[0]?.id ?? null)
      })
      .catch((loadError: Error) => { if (active) setError(loadError.message) })
    return () => { active = false }
  }, [childCount, catalogVersion])

  useEffect(() => {
    if (selectedClassId === null) return
    let active = true
    api<Student[]>(`/api/classes/${selectedClassId}/students`)
      .then((classStudents) => {
        if (!active) return
        setStudents(classStudents)
        if (isStaff) {
          // Nastavnik sam bira učenika; pri promeni odeljenja izbor se poništava.
          setSelectedStudentId((current) => classStudents.some((student) => student.id === current) ? current : null)
        }
      })
      .catch((loadError: Error) => { if (active) setError(loadError.message) })
    return () => { active = false }
  }, [selectedClassId, isStaff, catalogVersion])

  useEffect(() => {
    let active = true
    const load = () => api<Notifications>('/api/notifications')
      .then((result) => { if (active) setNotifications(result) })
      .catch(() => undefined)
    load()
    const timer = window.setInterval(load, 60_000)
    return () => { active = false; window.clearInterval(timer) }
  }, [notificationsVersion])

  const selectClass = useCallback((classId: number) => {
    setSelectedClassId(classId)
    const child = user.children.find((item) => item.classId === classId)
    if (child) setSelectedStudentId(child.id)
  }, [user.children])

  const selectStudent = useCallback((studentId: number) => {
    const child = user.children.find((item) => item.id === studentId)
    if (child) setSelectedClassId(child.classId)
    setSelectedStudentId(studentId)
  }, [user.children])

  // Svaka strana ima svoju adresu (#ocene, #raspored...), pa dugme "nazad" u pregledaču vraća na prethodnu stranu dnevnika.
  const setPage = useCallback((next: Page) => {
    setError('')
    setPageState(next)
    if (window.location.hash !== `#${pageSlug(next)}`) window.history.pushState(null, '', `#${pageSlug(next)}`)
    window.scrollTo({ top: 0 })
  }, [])

  useEffect(() => {
    if (window.location.hash !== `#${pageSlug(page)}`) window.history.replaceState(null, '', `#${pageSlug(page)}`)
    const onBack = () => {
      setError('')
      setPageState(pageFromHash(user.role) ?? 'Pregled')
    }
    window.addEventListener('popstate', onBack)
    return () => window.removeEventListener('popstate', onBack)
  }, [page, user.role])

  async function logout() {
    await api('/api/auth/logout', { method: 'POST' }).catch(() => undefined)
    resetCsrfToken()
    window.history.replaceState(null, '', window.location.pathname)
    onLoggedOut()
  }

  const selectedClass = classes.find((item) => item.id === selectedClassId)
  const selectedStudentName = isStaff
    ? students.find((student) => student.id === selectedStudentId)?.fullName ?? 'Izaberite učenika'
    : user.children.find((child) => child.id === selectedStudentId)?.fullName ?? 'Dete nije povezano'

  const state = useMemo<AppState>(() => ({
    user, school, isStaff, isTeacher, classes, subjects, selectedClass, selectClass, students, selectedStudentId,
    selectedStudentName, selectStudent, setPage, showError, notifications, refreshNotifications, reloadCatalog, setUser,
  }), [user, school, isStaff, isTeacher, classes, subjects, selectedClass, selectClass, students, selectedStudentId,
    selectedStudentName, selectStudent, setPage, showError, notifications, refreshNotifications, reloadCatalog, setUser])

  const className = selectedClass?.name ?? 'Odeljenje'
  const subtitles: Partial<Record<Page, string>> = {
    Pregled: isStaff ? `${todayLabel()} · ${user.role === 'Administrator' ? 'pregled cele škole' : 'vaši časovi i odeljenja'}` : `Informacije za učenika ${selectedStudentName}.`,
    Odeljenje: `${className} · razredni starešina ${selectedClass?.homeroomTeacher ?? 'nije određen'}`,
    Raspored: `${className} · ${school.currentTerm}. polugodište školske ${school.schoolYear}`,
    Obaveze: `${className} · domaći zadaci, testovi i rokovi`,
    Poruke: isStaff ? 'Razgovori sa roditeljima učenika iz vaših odeljenja' : 'Razgovori sa razrednim starešinom i nastavnicima odeljenja',
    Administracija: `${school.name} · ${classes.length} odeljenja`,
  }
  const subtitle = subtitles[page] ?? `${selectedStudentName} · ${selectedClass?.name ?? ''}`

  return (
    <AppContext.Provider value={state}>
      <div className="app-shell">
        <Sidebar page={page} unreadMessages={notifications.unreadMessages} onLogout={logout} />

        <main className="main-area">
          <header className="topbar">
            <div className="mobile-brand"><span className="brand-icon"><GraduationCap size={19} /></span>mojdnevnik</div>
            <div className="breadcrumb">{portalNames[user.role]} <ChevronRight size={13} /> <strong>{page.toUpperCase()}</strong></div>
            <div className="top-actions">
              <span className="role-label">{user.role}</span>
              <NotificationsMenu notifications={notifications} />
              <AccountMenu onLogout={logout} />
              <MobileMenu page={page} unreadMessages={notifications.unreadMessages} onLogout={logout} />
            </div>
          </header>
          <div className="page-content">
            <div className="page-heading">
              <div>
                <span className="eyebrow">ŠKOLSKA {school.schoolYear}</span>
                <h1>{page === 'Pregled' ? `Dobro došli, ${user.fullName.split(' ')[0]}` : page}</h1>
                <p>{subtitle}</p>
              </div>
              <span className="student-tag"><span className="green-dot" /> {selectedClass?.name ?? 'Školski dnevnik'}</span>
            </div>
            {error && <p className="data-error" role="alert">{error}</p>}
            <PageContent page={page} isStaff={isStaff} />
            <footer className="demo-footnote">Prikaz podataka prema dodeljenoj ulozi i odeljenju.</footer>
          </div>
        </main>
      </div>
    </AppContext.Provider>
  )
}

function PageContent({ page, isStaff }: { page: Page; isStaff: boolean }) {
  switch (page) {
    case 'Pregled': return isStaff ? <StaffOverviewPage /> : <OverviewPage />
    case 'Odeljenje': return <ClassPage />
    case 'Ocene': return <GradesPage />
    case 'Raspored': return <SchedulePage />
    case 'Obaveze': return <ObligationsPage />
    case 'Poruke': return <MessagesPage />
    case 'Izostanci i žalbe': return <AbsencesPage />
    case 'Statistika': return <StatisticsPage />
    case 'Administracija': return <AdminPage />
  }
}
