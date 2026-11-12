import { useApp } from '../AppContext'
import { classRole } from '../lib/classes'

// Nastavnik i administrator biraju odeljenje u kome rade.
export function ClassPicker({ onDone }: { onDone: () => void }) {
  const { user, classes, subjects, selectedClass, selectClass } = useApp()
  const isAdmin = user.role === 'Administrator'

  return (
    <>
      <span className="eyebrow">{isAdmin ? 'ODELJENJA ŠKOLE' : 'MOJA ODELJENJA'}</span>
      {classes.map((schoolClass) => (
        <button
          key={schoolClass.id}
          type="button"
          className={`popover-item ${schoolClass.id === selectedClass?.id ? 'is-current' : ''}`}
          onClick={() => { selectClass(schoolClass.id); onDone() }}
        >
          <strong>{schoolClass.name}</strong>
          <small>{classRole(schoolClass, subjects, isAdmin)}</small>
        </button>
      ))}
      {classes.length === 0 && <p className="popover-empty">Još niste dodeljeni nijednom odeljenju. Obratite se administratoru.</p>}
    </>
  )
}
