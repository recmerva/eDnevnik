import { useState } from 'react'
import { Bell } from 'lucide-react'
import { useApp } from '../AppContext'
import { formatDate, formatDateTime } from '../lib/format'
import type { NotificationItem, Notifications } from '../types'
import { usePopover } from './usePopover'

const storageKey = (userId: number) => `mojdnevnik.seen-notifications.${userId}`

function readSeen(userId: number) {
  try { return new Set<string>(JSON.parse(localStorage.getItem(storageKey(userId)) ?? '[]')) } catch { return new Set<string>() }
}

export function NotificationsMenu({ notifications }: { notifications: Notifications }) {
  const { user, setPage, selectClass, selectStudent } = useApp()
  const { open, setOpen, anchorRef } = usePopover()
  const [seen, setSeen] = useState(() => readSeen(user.id))
  const [fresh, setFresh] = useState<Set<string>>(new Set())
  const hasNew = notifications.items.some((item) => !seen.has(item.key))

  function toggle() {
    if (!open) {
      setFresh(new Set(notifications.items.filter((item) => !seen.has(item.key)).map((item) => item.key)))
      // Pamtimo samo ključeve koji se trenutno prikazuju, da lista ne raste beskonačno.
      const next = new Set(notifications.items.map((item) => item.key))
      setSeen(next)
      try { localStorage.setItem(storageKey(user.id), JSON.stringify([...next])) } catch { /* privatni režim pregledača */ }
    }
    setOpen(!open)
  }

  function openItem(item: NotificationItem) {
    if (item.classId) selectClass(item.classId)
    if (item.studentId) selectStudent(item.studentId)
    setPage(item.page)
    setOpen(false)
  }

  return (
    <div className="popover-anchor" ref={anchorRef}>
      <button className="notification-button" aria-label="Obaveštenja" aria-expanded={open} onClick={toggle}>
        <Bell size={18} />{hasNew && <i />}
      </button>
      {open && (
        <div className="popover" role="menu">
          <span className="eyebrow">OBAVEŠTENJA</span>
          {notifications.items.map((item) => (
            <button key={item.key} type="button" className={`popover-item ${fresh.has(item.key) ? 'is-current' : ''}`} onClick={() => openItem(item)}>
              <strong>{item.title}</strong>
              <small>{item.detail}</small>
              <small>{['test', 'homework', 'grade', 'absence'].includes(item.kind) ? formatDate(item.date.slice(0, 10)) : formatDateTime(item.date)}</small>
            </button>
          ))}
          {notifications.items.length === 0 && <p className="popover-empty">Nema novih obaveštenja.</p>}
        </div>
      )}
    </div>
  )
}
