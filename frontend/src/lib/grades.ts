import type { ClassRoom, FinalGrade, Grade, Subject } from '../types'
import { parseDate } from './format'

export const gradeTypes = ['Usmeno odgovaranje', 'Aktivnost na času', 'Kontrolni zadatak', 'Pismeni zadatak']
export const descriptiveScale = ['Nezadovoljavajući', 'Zadovoljavajući', 'Dobar', 'Vrlo dobar', 'Odličan']

// Opisne ocene (veronauka) ne ulaze u prosek.
export const countedGrades = (grades: Grade[]) => grades.filter((grade) => !grade.usesDescriptiveScale)

export function average(values: number[]) {
  return values.length ? values.reduce((sum, value) => sum + value, 0) / values.length : null
}

export const averageOf = (grades: Grade[]) => average(countedGrades(grades).map((grade) => grade.value))

export const formatAverage = (value: number | null | undefined) =>
  value === null || value === undefined ? '—' : value.toFixed(2).replace('.', ',')

// Predlog zaključne ocene: prosek se zaokružuje, a x,50 ide naviše.
export const finalGrade = (value: number) => Math.floor(value + 0.5)

export function subjectAverages(grades: Grade[]) {
  const bySubject = new Map<number, { subject: string; values: number[] }>()
  for (const grade of countedGrades(grades)) {
    const entry = bySubject.get(grade.subjectId) ?? { subject: grade.subject, values: [] }
    entry.values.push(grade.value)
    bySubject.set(grade.subjectId, entry)
  }
  return [...bySubject].map(([subjectId, entry]) => ({ subjectId, subject: entry.subject, average: average(entry.values)! }))
}

// Opšti uspeh: zaključna ocena tekućeg polugodišta gde je upisana, inače predlog iz proseka.
// Jedinica iz bilo kog predmeta znači nedovoljan uspeh, kao u školskom pravilniku.
export function overallSuccess(grades: Grade[], finals: FinalGrade[] = [], term = 0) {
  const values = subjectAverages(grades).map((item) =>
    finals.find((final) => final.subjectId === item.subjectId && final.term === term)?.value ?? finalGrade(item.average))
  const mean = average(values)
  if (mean === null) return null
  if (values.includes(1)) return { label: 'Nedovoljan (1)', average: mean }
  const label = mean >= 4.5 ? 'Odličan (5)' : mean >= 3.5 ? 'Vrlo dobar (4)' : mean >= 2.5 ? 'Dobar (3)' : 'Dovoljan (2)'
  return { label, average: mean }
}

// Koliko se prosek promenio ocenama iz poslednjih 30 dana.
export function recentTrend(grades: Grade[]) {
  const counted = countedGrades(grades)
  const limit = new Date()
  limit.setDate(limit.getDate() - 30)
  const older = counted.filter((grade) => parseDate(grade.gradedOn) < limit)
  if (older.length === 0 || older.length === counted.length) return null
  return averageOf(counted)! - averageOf(older)!
}

// Predmeti u kojima korisnik sme da ocenjuje i zadaje obaveze u odeljenju (null sa servera = svi predmeti).
export const teachableSubjects = (subjects: Subject[], classRoom: ClassRoom | undefined) =>
  !classRoom || classRoom.teachableSubjectIds === null ? subjects : subjects.filter((subject) => classRoom.teachableSubjectIds!.includes(subject.id))
