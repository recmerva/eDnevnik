export type Role = 'Roditelj' | 'Nastavnik' | 'Administrator'
export type Page = 'Pregled' | 'Odeljenje' | 'Ocene' | 'Raspored' | 'Obaveze' | 'Poruke' | 'Izostanci i žalbe' | 'Statistika' | 'Administracija'

export type SchoolInfo = {
  name: string
  city: string
  schoolYear: string
  currentTerm: number
  firstTermStart: string
  firstTermEnd: string
  secondTermStart: string
  secondTermEnd: string
}
export type Child = { id: number; fullName: string; classId: number; className: string }
export type UserProfile = { id: number; fullName: string; email: string; role: Role; children: Child[] }

// teachableSubjectIds: predmeti koje korisnik predaje u odeljenju; null = svi (administrator).
export type ClassRoom = {
  id: number
  name: string
  gradeLevel: number
  studentCount: number
  homeroomTeacher: string | null
  teachableSubjectIds: number[] | null
  // Razredni starešina (ili administrator): opravdava izostanke i odgovara na žalbe.
  isMyHomeroom: boolean
}
export type Student = { id: number; fullName: string; className: string; parentName: string | null }
export type Subject = { id: number; name: string; usesDescriptiveScale: boolean }

export type Grade = {
  id: number
  subjectId: number
  subject: string
  usesDescriptiveScale: boolean
  value: number
  description: string | null
  gradeType: string
  gradedOn: string
}
export type FinalGrade = {
  id: number
  subjectId: number
  subject: string
  usesDescriptiveScale: boolean
  term: number
  value: number
  recordedByName: string
  recordedAt: string
}
export type StudentNote = { id: number; authorName: string; content: string; createdAt: string; canDelete: boolean }
export type SchoolTest = { id: number; title: string; subjectId: number; subject: string; heldOn: string }
export type Homework = {
  id: number
  subjectId: number
  subject: string
  title: string
  description: string | null
  dueDate: string
  authorName: string
  createdAt: string
}
export type ScheduleEntry = { id: number; day: number; lessonNumber: number; subjectId: number; subject: string; teacher: string | null }
export type Schedule = {
  entries: ScheduleEntry[]
  subjects: { subjectId: number; subject: string; teacher: string | null; weeklyLessons: number }[]
}
export type AbsenceRecord = { id: number; date: string; lessonHours: number; isExcused: boolean; note: string | null }
export type ChatMessage = { id: number; senderName: string; sentByParent: boolean; content: string; createdAt: string; readAt: string | null; isMine: boolean }
export type ComplaintRecord = {
  id: number
  parentName: string
  content: string
  submittedAt: string
  status: string
  response: string | null
  respondedByName: string | null
  respondedAt: string | null
}
export type Announcement = { id: number; classId: number | null; className: string | null; authorName: string; content: string; createdAt: string; canDelete: boolean }
export type Statistics = { limited: boolean; classAverage: number | null; rank: number | null; rankedStudents: number; subjects: { subject: string; classAverage: number }[] }
export type ClassReport = {
  limited: boolean
  term: number
  classAverage: number | null
  absenceHours: number
  unexcusedHours: number
  distribution: { success: string; count: number }[]
  subjects: { subject: string; average: number }[]
  students: {
    studentId: number
    fullName: string
    average: number | null
    success: string | null
    recordedFinals: number
    absenceHours: number
    unexcusedHours: number
  }[]
}

export type NotificationItem = { key: string; kind: string; title: string; detail: string; date: string; page: Page; studentId: number | null; classId: number | null }
export type Notifications = { unreadMessages: number; items: NotificationItem[] }

export type AdminUser = { id: number; fullName: string; email: string; role: Role; classCount: number; childCount: number }
export type AdminStudent = {
  studentId: number
  fullName: string
  classId: number
  className: string
  registrationCode: string | null
  parentName: string | null
  parentEmail: string | null
}
export type ActivityLog = { id: number; userName: string; action: string; details: string; createdAt: string }

// Razgovor roditelja i jednog nastavnika o jednom učeniku.
export type Conversation = {
  key: string
  studentId: number
  studentName: string
  classId: number
  className: string
  teacherId: number | null
  counterpartName: string
  counterpartRole: string
  lastMessage: string | null
  lastFromMe: boolean
  lastAt: string | null
  unread: number
  canWrite: boolean
}

export type DashboardLesson = {
  day: number
  lessonNumber: number
  classId: number
  className: string
  gradeLevel: number
  subject: string
  teacher: string | null
  mine: boolean
}
export type Dashboard = {
  lessons: DashboardLesson[]
  attention: { kind: string; classId: number; text: string }[]
  counts: { teachers: number; students: number; linkedStudents: number } | null
}

// Nastavnici po odeljenjima (administracija): za svaki predmet jedan nastavnik, plus razredni/učitelj.
export type StaffTeacher = { id: number; fullName: string; subjectIds: number[] }
export type StaffSubject = { subjectId: number; subject: string; weeklyLessons: number; teacherId: number | null; teacherName: string | null }
export type StaffClass = { classId: number; name: string; gradeLevel: number; homeroomId: number | null; homeroomName: string | null; subjects: StaffSubject[] }
export type StaffOverview = { teachers: StaffTeacher[]; classes: StaffClass[] }
