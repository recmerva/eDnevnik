import { useState } from 'react'
import { Search } from 'lucide-react'
import { EmptyState, PanelHeading } from '../../components/ui'
import { formatDateTime } from '../../lib/format'
import { useApi } from '../../lib/useApi'
import type { ActivityLog } from '../../types'

// Pregled sistemskih aktivnosti: ko je, šta i kada uradio.
export function ActivitySection() {
  const [search, setSearch] = useState('')
  const [query, setQuery] = useState('')
  const logs = useApi<ActivityLog[]>(`/api/admin/activity${query ? `?search=${encodeURIComponent(query)}` : ''}`, [], 30_000)

  return (
    <section className="panel data-panel">
      <PanelHeading eyebrow="DNEVNIK SISTEMA" title="Aktivnosti korisnika">
        <form className="search-form" onSubmit={(event) => { event.preventDefault(); setQuery(search.trim()) }}>
          <input className="class-select" type="search" value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Korisnik, radnja ili učenik" aria-label="Pretraga aktivnosti" />
          <button className="icon-button" type="submit" aria-label="Pretraži" title="Pretraži"><Search size={15} /></button>
        </form>
      </PanelHeading>
      <div className="test-list">
        {logs.data.map((log) => (
          <div className="test-row note-row" key={log.id}>
            <div><strong>{log.action} · {log.userName}</strong><small>{log.details}</small></div>
            <span className="test-result">{formatDateTime(log.createdAt)}</span>
          </div>
        ))}
        {!logs.loading && logs.data.length === 0 && <EmptyState>Nema zabeleženih aktivnosti.</EmptyState>}
      </div>
      <p className="table-note">Prikazuje se poslednjih 200 radnji: prijave, ocene, izostanci, obaveze, izmene naloga i rasporeda.</p>
    </section>
  )
}
