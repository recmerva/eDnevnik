import { useApp } from '../AppContext'

// Kartica za izbor u meniju: roditelju izbor deteta, a nastavniku i administratoru izbor odeljenja.
export function useSwitchCard() {
  const { user, isStaff, selectedClass, selectedStudentName } = useApp()
  return {
    title: isStaff ? selectedClass?.name ?? 'Izaberite odeljenje' : selectedStudentName,
    detail: isStaff
      ? user.role === 'Administrator' ? `Razredni: ${selectedClass?.homeroomTeacher ?? 'nije određen'}` : selectedClass?.isMyHomeroom ? 'Razredni starešina' : 'Predmetni nastavnik'
      : selectedClass?.name ?? 'Nema povezanog odeljenja',
  }
}
