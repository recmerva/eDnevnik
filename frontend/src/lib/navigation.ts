import {
  Award, CalendarClock, ChartNoAxesCombined, ClipboardCheck, ClipboardList, Home, MessageCircle, ShieldCheck, Users, type LucideIcon,
} from 'lucide-react'
import type { Page } from '../types'

export const navItems: { label: Page; icon: LucideIcon; description: string }[] = [
  { label: 'Pregled', icon: Home, description: 'Sažetak školskog dana' },
  { label: 'Odeljenje', icon: Users, description: 'Odeljenje i razredni starešina' },
  { label: 'Ocene', icon: Award, description: 'Ocene, zaključne i beleške' },
  { label: 'Raspored', icon: CalendarClock, description: 'Časovi po danima' },
  { label: 'Obaveze', icon: ClipboardList, description: 'Domaći zadaci i testovi' },
  { label: 'Poruke', icon: MessageCircle, description: 'Razgovori roditelja i nastavnika' },
  { label: 'Izostanci i žalbe', icon: ClipboardCheck, description: 'Prisustvo i opravdanja' },
  { label: 'Statistika', icon: ChartNoAxesCombined, description: 'Uspeh i izveštaji' },
  { label: 'Administracija', icon: ShieldCheck, description: 'Nalozi, odeljenja i aktivnosti' },
]

// Administracija se prikazuje samo administratoru, a poruke vode roditelji i nastavnici.
export const visibleNavItems = (role: string) => navItems.filter(({ label }) =>
  label === 'Administracija' ? role === 'Administrator' : label !== 'Poruke' || role !== 'Administrator')

// Adresa strane u pregledaču: "Izostanci i žalbe" → #izostanci-i-zalbe.
export const pageSlug = (page: Page) => page.toLowerCase().replace(/ž/g, 'z').replace(/\s+/g, '-')

export function pageFromHash(role: string): Page | null {
  const slug = decodeURIComponent(window.location.hash.slice(1))
  return visibleNavItems(role).find(({ label }) => pageSlug(label) === slug)?.label ?? null
}
