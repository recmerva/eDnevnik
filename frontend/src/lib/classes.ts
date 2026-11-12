import type { ClassRoom, Subject } from '../types'
import { plural } from './format'

// Kratak opis odeljenja iz ugla korisnika: administratoru broj učenika i razredni, nastavniku njegova uloga i predmeti.
export function classRole(schoolClass: ClassRoom, subjects: Subject[], isAdmin: boolean) {
  if (isAdmin) return `${schoolClass.studentCount} ${plural(schoolClass.studentCount, 'učenik', 'učenika', 'učenika')} · razredni: ${schoolClass.homeroomTeacher ?? 'nije određen'}`
  const taught = subjects.filter((subject) => schoolClass.teachableSubjectIds?.includes(subject.id)).map((subject) => subject.name)
  // Učitelj predaje gotovo sve predmete, pa se umesto spiska piše "razredna nastava".
  const subjectsText = taught.length > 3 ? 'razredna nastava' : taught.join(', ')
  return [schoolClass.isMyHomeroom ? 'Razredni starešina' : null, subjectsText || null].filter(Boolean).join(' · ')
}
