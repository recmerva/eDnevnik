import { useEffect, useRef, useState, type CSSProperties } from 'react'
import { createPortal } from 'react-dom'
import { ChevronDown, GraduationCap, LogOut, ShieldCheck } from 'lucide-react'
import { useApp } from '../AppContext'
import { initials } from '../lib/format'
import { visibleNavItems } from '../lib/navigation'
import type { Page } from '../types'
import { ChildPicker } from './ChildPicker'
import { ClassPicker } from './ClassPicker'
import { useSwitchCard } from './useSwitchCard'

// Meni za telefon: hamburger u gornjoj traci otvara meni preko celog ekrana.
// Pozadina se širi u krug iz ugla dugmeta, a stavke ulaze jedna za drugom.
// Prikazuje se direktno u body, da ga ne bi ograničila gornja traka (sticky + blur).
export function MobileMenu({ page, unreadMessages, onLogout }: { page: Page; unreadMessages: number; onLogout: () => void }) {
  const { user, school, isStaff, setPage } = useApp()
  const card = useSwitchCard()
  const [open, setOpen] = useState(false)
  const [pickerOpen, setPickerOpen] = useState(false)
  const closeRef = useRef<HTMLButtonElement>(null)
  const items = visibleNavItems(user.role)

  useEffect(() => {
    if (!open) return
    closeRef.current?.focus()
    const closeOnEscape = (event: KeyboardEvent) => { if (event.key === 'Escape') setOpen(false) }
    document.addEventListener('keydown', closeOnEscape)
    document.body.classList.add('menu-locked')
    return () => {
      document.removeEventListener('keydown', closeOnEscape)
      document.body.classList.remove('menu-locked')
    }
  }, [open])

  function close() {
    setOpen(false)
    setPickerOpen(false)
  }

  function go(next: Page) {
    setPage(next)
    close()
  }

  return <>
    <button className="hamburger" onClick={() => setOpen(true)} aria-label="Otvori meni" aria-expanded={open} aria-controls="mobile-menu">
      <span /><span /><span />
    </button>

    {createPortal(<div id="mobile-menu" className={`mobile-menu ${open ? 'is-open' : ''}`} role="dialog" aria-modal="true" aria-label="Glavni meni" aria-hidden={!open} inert={!open}>
      <div className="mobile-menu-head">
        <span className="mobile-menu-brand"><span className="brand-icon"><GraduationCap size={19} /></span><span>moj<span>dnevnik</span></span></span>
        <button ref={closeRef} className="hamburger" onClick={close} aria-label="Zatvori meni"><span /><span /><span /></button>
      </div>

      <div className="mobile-menu-body">
        <div className="menu-profile menu-reveal" style={{ '--i': 0 } as CSSProperties}>
          <span className="menu-avatar">{initials(user.fullName)}</span>
          <span><strong>{user.fullName}</strong><small>{user.role} · {school.name}</small></span>
        </div>

        <div className="menu-reveal" style={{ '--i': 1 } as CSSProperties}>
          <button className={`menu-switch ${pickerOpen ? 'is-open' : ''}`} onClick={() => setPickerOpen((value) => !value)} aria-expanded={pickerOpen}>
            <span className="menu-switch-label">{isStaff ? 'Odeljenje' : 'Dete'}</span>
            <span><strong>{card.title}</strong><small>{card.detail}</small></span>
            <ChevronDown size={18} />
          </button>
          {pickerOpen && (
            <div className="menu-picker">
              {isStaff ? <ClassPicker onDone={() => setPickerOpen(false)} /> : <ChildPicker onDone={() => setPickerOpen(false)} />}
            </div>
          )}
        </div>

        <nav className="menu-links" aria-label="Stranice">
          {items.map(({ label, icon: Icon, description }, index) => (
            <button
              key={label}
              className={`menu-link menu-reveal ${page === label ? 'is-current' : ''}`}
              style={{ '--i': index + 2 } as CSSProperties}
              onClick={() => go(label)}
              aria-current={page === label ? 'page' : undefined}
            >
              <span className="menu-link-icon"><Icon size={19} strokeWidth={1.8} /></span>
              <span><strong>{label}</strong><small>{description}</small></span>
              {label === 'Poruke' && unreadMessages > 0 && <span className="menu-unread">{unreadMessages}</span>}
            </button>
          ))}
        </nav>

        <div className="menu-footer menu-reveal" style={{ '--i': items.length + 2 } as CSSProperties}>
          <span><ShieldCheck size={16} /> {school.city ? `${school.city} · ` : ''}{school.schoolYear}</span>
          <button onClick={() => { close(); onLogout() }}><LogOut size={16} /> Odjavi se</button>
        </div>
      </div>
    </div>, document.body)}
  </>
}
