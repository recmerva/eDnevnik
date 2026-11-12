import { createContext, useContext } from 'react'
import type { ClassRoom, Notifications, Page, SchoolInfo, Student, Subject, UserProfile } from './types'

export type AppState = {
  user: UserProfile
  school: SchoolInfo
  isStaff: boolean
  // Samo nastavnik upisuje ocene, izostanke, obaveze i beleške; administrator ima uvid.
  isTeacher: boolean
  classes: ClassRoom[]
  subjects: Subject[]
  selectedClass: ClassRoom | undefined
  selectClass: (classId: number) => void
  students: Student[]
  selectedStudentId: number | null
  selectedStudentName: string
  selectStudent: (studentId: number) => void
  setPage: (page: Page) => void
  showError: (message: string) => void
  notifications: Notifications
  refreshNotifications: () => void
  // Ponovo učitava odeljenja, predmete i podatke o školskoj godini posle izmena u administraciji.
  reloadCatalog: () => void
  setUser: (user: UserProfile) => void
}

export const AppContext = createContext<AppState | null>(null)

export function useApp() {
  const state = useContext(AppContext)
  if (!state) throw new Error('useApp se koristi van AppContext-a.')
  return state
}
