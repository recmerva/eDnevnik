import { useApp } from '../../AppContext'
import { EmptyState, PanelHeading } from '../../components/ui'
import { useApi } from '../../lib/useApi'
import type { StaffClass, StaffOverview } from '../../types'
import { ClassStaff } from './ClassStaff'

const missingIn = (item: StaffClass) => item.subjects.filter((subject) => subject.teacherId === null).length + (item.homeroomId ? 0 : 1)

// Administrator bira odeljenje i za svaki predmet bira nastavnika.
export function StaffSection() {
  const { selectedClass, selectClass, reloadCatalog } = useApp()
  const staff = useApi<StaffOverview>('/api/admin/staff', { teachers: [], classes: [] })
  const current = staff.data.classes.find((item) => item.classId === selectedClass?.id) ?? staff.data.classes[0]

  function changed() {
    staff.reload()
    reloadCatalog()
  }

  return (
    <section className="panel data-panel">
      <PanelHeading eyebrow="NASTAVNICI ODELJENJA" title={current ? `Ko predaje u odeljenju ${current.name}` : 'Nastavnici odeljenja'} />
      <div className="class-chips" role="tablist" aria-label="Odeljenje">
        {staff.data.classes.map((item) => {
          const missing = missingIn(item)
          return (
            <button key={item.classId} type="button" role="tab" aria-selected={item.classId === current?.classId}
              className={`class-chip ${item.classId === current?.classId ? 'is-active' : ''}`} onClick={() => selectClass(item.classId)}
              title={missing ? `Nedostaje ${missing}` : 'Sve popunjeno'}>
              {item.name}{missing > 0 && <i className="chip-missing">{missing}</i>}
            </button>
          )
        })}
      </div>
      {current
        ? <ClassStaff key={current.classId} schoolClass={current} teachers={staff.data.teachers} onChanged={changed} />
        : !staff.loading && <EmptyState>Prvo dodajte odeljenje u sekciji „Odeljenja i predmeti”.</EmptyState>}
      {staff.data.teachers.length === 0 && !staff.loading && <p className="table-note">Nema naloga nastavnika. Napravite ih u sekciji „Nalozi”.</p>}
    </section>
  )
}
