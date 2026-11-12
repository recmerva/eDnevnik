const locale = 'sr-Latn-RS'

// Datumi bez vremena ("2026-11-11") čitaju se kao lokalni dan, da se ne bi pomerili zbog vremenske zone.
export function parseDate(value: string) {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value)
  return match ? new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3])) : new Date(value)
}

export const formatDate = (value: string) => parseDate(value).toLocaleDateString(locale, { day: 'numeric', month: 'short' })
export const formatLongDate = (value: string) => parseDate(value).toLocaleDateString(locale, { day: 'numeric', month: 'long', year: 'numeric' })
export const formatDateTime = (value: string) =>
  new Date(value).toLocaleString(locale, { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' })

export function todayIso() {
  const now = new Date()
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`
}

export function daysFromToday(value: string) {
  const today = parseDate(todayIso())
  return Math.round((parseDate(value).getTime() - today.getTime()) / 86_400_000)
}

export const initials = (name: string) =>
  name.split(' ').filter(Boolean).map((part) => part[0]).slice(0, 2).join('').toUpperCase()

// Srpska množina: 1 čas, 2 časa, 5 časova.
export function plural(count: number, one: string, few: string, many: string) {
  const lastTwo = count % 100
  const last = count % 10
  if (last === 1 && lastTwo !== 11) return one
  if (last >= 2 && last <= 4 && (lastTwo < 12 || lastTwo > 14)) return few
  return many
}

export const lessonHours = (count: number) => `${count} ${plural(count, 'čas', 'časa', 'časova')}`

// Kod učenika za registraciju roditelja, u obliku lakom za diktiranje: "K7MR-Q4TB".
export const formatCode = (code: string | null) => code ? code.match(/.{1,4}/g)!.join('-') : ''
