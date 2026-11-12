import { ChevronRight, GraduationCap, LogOut, ShieldCheck } from 'lucide-react'
import { useApp } from '../AppContext'
import { initials } from '../lib/format'
import { visibleNavItems } from '../lib/navigation'
import type { Page } from '../types'
import { ChildPicker } from './ChildPicker'
import { ClassPicker } from './ClassPicker'
import { usePopover } from './usePopover'
import { useSwitchCard } from './useSwitchCard'

// Bočni meni za računar i tablet; na telefonu ga zamenjuje MobileMenu.
export function Sidebar({ page, unreadMessages, onLogout }: { page: Page; unreadMessages: number; onLogout: () => void }) {
  const { user, school, isStaff, selectedClass, selectedStudentName, setPage } = useApp()
  const { open, setOpen, toggle, anchorRef } = usePopover()
  const card = useSwitchCard()

  return (
    <aside className="sidebar">
      <a className="brand" href="#pregled" onClick={(event) => { event.preventDefault(); setPage('Pregled') }}><span className="brand-icon"><GraduationCap size={21} /></span><span>moj<span className="brand-light">dnevnik</span></span></a>
      <div className="popover-anchor" ref={anchorRef}>
        <button className="child-switch" onClick={toggle} aria-expanded={open} title={isStaff ? 'Izaberi odeljenje' : 'Izaberi dete'}>
          <span className="child-avatar">{isStaff ? selectedClass?.name.split('-')[0] ?? '–' : initials(selectedStudentName)}</span>
          <span><strong>{card.title}</strong><small>{card.detail}</small></span>
          <ChevronRight size={16} />
        </button>
        {open && <div className="popover child-popover">{isStaff ? <ClassPicker onDone={() => setOpen(false)} /> : <ChildPicker onDone={() => setOpen(false)} />}</div>}
      </div>
      <nav className="side-nav" aria-label="Glavni meni">
        <span className="nav-caption">MENI</span>
        {visibleNavItems(user.role).map(({ label, icon: Icon }) => (
          <button key={label} className={`nav-link ${page === label ? 'nav-active' : ''}`} onClick={() => setPage(label)} aria-current={page === label ? 'page' : undefined}>
            <span className="nav-icon"><Icon size={17} strokeWidth={1.8} /></span><span>{label}</span>
            {label === 'Poruke' && unreadMessages > 0 && <i className="nav-unread" aria-label={`${unreadMessages} nepročitanih poruka`} />}
          </button>
        ))}
      </nav>
      <div className="sidebar-bottom">
        <div className="school-note"><span className="school-note-icon"><ShieldCheck size={17} /></span><span><strong>{school.name}</strong><small>{school.city} · {school.schoolYear}</small></span></div>
        <button className="logout-button" onClick={onLogout}><LogOut size={17} /> Odjavi se</button>
      </div>
    </aside>
  )
}
