import { useState } from 'react'
import { useApp } from '../AppContext'
import { EmptyState } from '../components/ui'
import { AccountsSection } from './admin/AccountsSection'
import { CatalogSection } from './admin/CatalogSection'
import { ActivitySection } from './admin/ActivitySection'
import { SchoolYearSection } from './admin/SchoolYearSection'
import { StaffSection } from './admin/StaffSection'
import { StudentsSection } from './admin/StudentsSection'

// Redosled prati učestalost posla, a uz svaku sekciju piše jednom rečenicom čemu služi.
const sections = {
  'Nastavnici odeljenja': 'Za svako odeljenje izaberite razrednog starešinu (učitelja) i nastavnika za svaki predmet.',
  'Učenici i kodovi': 'Upis učenika i kodovi koje roditelji dobijaju u školi za registraciju.',
  'Nalozi': 'Nalozi nastavnika, roditelja i administratora: novi nalog, izmena i nova lozinka.',
  'Odeljenja i predmeti': 'Spisak odeljenja i predmeta koje škola ima.',
  'Školska godina': 'Početak i kraj polugodišta.',
  'Aktivnosti': 'Ko je i kada šta menjao na sajtu.',
} as const
type Section = keyof typeof sections

export function AdminPage() {
  const { user } = useApp()
  const [section, setSection] = useState<Section>('Nastavnici odeljenja')

  if (user.role !== 'Administrator') return <section className="panel data-panel"><EmptyState>Ova stranica je dostupna samo administratoru.</EmptyState></section>

  return (
    <>
      <div className="day-tabs admin-tabs" role="tablist" aria-label="Sekcije administracije">
        {(Object.keys(sections) as Section[]).map((item) => (
          <button key={item} role="tab" aria-selected={section === item} className={`day-tab ${section === item ? 'is-active' : ''}`} onClick={() => setSection(item)}>
            <span>{item}</span>
          </button>
        ))}
      </div>
      <p className="section-hint">{sections[section]}</p>
      {section === 'Nalozi' && <AccountsSection />}
      {section === 'Nastavnici odeljenja' && <StaffSection />}
      {section === 'Učenici i kodovi' && <StudentsSection />}
      {section === 'Odeljenja i predmeti' && <CatalogSection />}
      {section === 'Školska godina' && <SchoolYearSection />}
      {section === 'Aktivnosti' && <ActivitySection />}
    </>
  )
}
