import { useCallback, useEffect, useMemo, useRef, useState, type FormEvent } from 'react'
import { ArrowLeft, PenSquare, Search, Send } from 'lucide-react'
import { api, errorMessage } from '../api/client'
import { useApp } from '../AppContext'
import { Modal } from '../components/Modal'
import { EmptyState } from '../components/ui'
import { formatDateTime, initials } from '../lib/format'
import { useApi } from '../lib/useApi'
import type { ChatMessage, Conversation, Student } from '../types'

// Levo su razgovori (roditelj vidi sve nastavnike svog deteta, nastavnik roditelje kojima je pisao),
// desno izabrani razgovor. Na telefonu se prikazuje jedno pa drugo.
export function MessagesPage() {
  const { isStaff, students, selectedStudentId, selectedClass, refreshNotifications } = useApp()
  const conversations = useApi<Conversation[]>('/api/conversations', [], 30_000)
  // Sa stranice odeljenja ("Poruka roditelju") odmah se otvara razgovor sa roditeljem izabranog učenika.
  const startsWithStudent = isStaff && students.some((student) => student.id === selectedStudentId && student.parentName)
  const [activeKey, setActiveKey] = useState<string | null>(() => startsWithStudent ? String(selectedStudentId) : null)
  const [threadOpen, setThreadOpen] = useState(startsWithStudent)
  const [query, setQuery] = useState('')
  const [composing, setComposing] = useState(false)

  // Nastavnik može da otvori razgovor sa roditeljem učenika kome još nije pisao.
  const draft = useMemo<Conversation | null>(() => {
    if (!isStaff || !activeKey || conversations.data.some((item) => item.key === activeKey)) return null
    const student = students.find((item) => String(item.id) === activeKey)
    return student?.parentName ? newConversation(student, selectedClass?.id ?? 0) : null
  }, [isStaff, activeKey, conversations.data, students, selectedClass])

  const list = draft ? [draft, ...conversations.data] : conversations.data
  const active = list.find((item) => item.key === activeKey) ?? (conversations.loading ? undefined : list[0])
  const search = query.trim().toLocaleLowerCase('sr')
  const shown = search
    ? list.filter((item) => `${item.counterpartName} ${item.counterpartRole} ${item.studentName}`.toLocaleLowerCase('sr').includes(search))
    : list
  const children = new Set(conversations.data.map((item) => item.studentId)).size

  function open(conversation: Conversation) {
    setActiveKey(conversation.key)
    setThreadOpen(true)
  }

  const { setData: setConversations } = conversations
  const markRead = useCallback((key: string) => {
    setConversations((current) => current.map((item) => item.key === key ? { ...item, unread: 0 } : item))
    refreshNotifications()
  }, [setConversations, refreshNotifications])

  function sent(conversation: Conversation, message: ChatMessage) {
    const updated = { ...conversation, lastMessage: message.content, lastFromMe: true, lastAt: message.createdAt }
    conversations.setData((current) => [updated, ...current.filter((item) => item.key !== conversation.key)])
    setActiveKey(conversation.key)
  }

  return (
    <section className={`messages-layout ${threadOpen ? 'thread-open' : ''}`}>
      <aside className="panel conversation-panel">
        <div className="conversation-head">
          <div><span className="eyebrow">{isStaff ? 'RODITELJI UČENIKA' : 'NASTAVNICI ODELJENJA'}</span><h2>Razgovori</h2></div>
          {isStaff && <button className="button button-primary" onClick={() => setComposing(true)}><PenSquare size={15} /> Nova</button>}
        </div>
        <label className="conversation-search">
          <Search size={15} />
          <input value={query} onChange={(event) => setQuery(event.target.value)} placeholder={isStaff ? 'Roditelj ili učenik' : 'Nastavnik ili predmet'} aria-label="Pretraga razgovora" />
        </label>
        <div className="conversation-list">
          {shown.map((item) => (
            <button className={`conversation-item ${item.key === active?.key ? 'active' : ''}`} key={item.key} onClick={() => open(item)}>
              <span className="teacher-avatar">{initials(item.counterpartName)}</span>
              <span className="conversation-copy">
                <strong>{item.counterpartName}</strong>
                <small>{item.counterpartRole}{!isStaff && children > 1 ? ` · ${item.studentName}` : ''}</small>
                {item.lastMessage && <em>{item.lastFromMe ? 'Vi: ' : ''}{item.lastMessage}</em>}
              </span>
              <span className="conversation-meta">
                {item.lastAt && <time>{shortTime(item.lastAt)}</time>}
                {item.unread > 0 && <span className="unread-count">{item.unread}</span>}
              </span>
            </button>
          ))}
          {!conversations.loading && shown.length === 0 && (
            <EmptyState>{search ? 'Nema razgovora za tu pretragu.' : isStaff ? 'Još nemate razgovore. Kliknite „Nova” da pišete roditelju.' : 'Odeljenju još nisu dodeljeni nastavnici.'}</EmptyState>
          )}
        </div>
      </aside>

      {active
        ? <Thread key={active.key} conversation={active} onBack={() => setThreadOpen(false)} onRead={markRead} onSent={sent} />
        : <section className="panel messages-panel thread-empty"><EmptyState>{conversations.loading ? 'Učitavanje razgovora...' : 'Izaberite razgovor sa leve strane.'}</EmptyState></section>}

      {composing && (
        <NewMessage
          onClose={() => setComposing(false)}
          onSent={(conversation, message) => { sent(conversation, message); setThreadOpen(true) }}
        />
      )}
    </section>
  )
}

function Thread({ conversation, onBack, onRead, onSent }: {
  conversation: Conversation
  onBack: () => void
  onRead: (key: string) => void
  onSent: (conversation: Conversation, message: ChatMessage) => void
}) {
  const { isStaff, showError } = useApp()
  const partner = isStaff ? '' : `?teacherId=${conversation.teacherId ?? 0}`
  const path = `/api/students/${conversation.studentId}/messages${partner}`
  // Otvoren razgovor se osvežava na 15 sekundi, kao jednostavan chat.
  const messages = useApi<ChatMessage[]>(path, [], 15_000)
  const [sending, setSending] = useState(false)
  const listRef = useRef<HTMLDivElement>(null)
  const hasUnread = messages.data.some((message) => !message.isMine && !message.readAt)

  const { key, studentId } = conversation
  const { setData: setMessages } = messages
  useEffect(() => {
    if (!hasUnread) return
    const now = new Date().toISOString()
    setMessages((current) => current.map((message) => message.isMine || message.readAt ? message : { ...message, readAt: now }))
    api(`/api/students/${studentId}/messages/read${partner}`, { method: 'POST' })
      .then(() => onRead(key))
      .catch(() => undefined)
  }, [hasUnread, key, studentId, partner, onRead, setMessages])

  useEffect(() => {
    const list = listRef.current
    if (list) list.scrollTop = list.scrollHeight
  }, [messages.data.length])

  async function send(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = event.currentTarget
    const content = String(new FormData(form).get('message') ?? '').trim()
    if (!content) return
    setSending(true)
    try {
      const saved = await api<ChatMessage>(`/api/students/${conversation.studentId}/messages`, {
        method: 'POST',
        body: { content, teacherUserId: isStaff ? null : conversation.teacherId },
      })
      messages.setData((current) => [...current, saved])
      onSent(conversation, saved)
      form.reset()
    } catch (error) {
      showError(errorMessage(error, 'Poruka nije poslata.'))
    } finally {
      setSending(false)
    }
  }

  const placeholder = conversation.canWrite
    ? `Poruka za: ${conversation.counterpartName}`
    : isStaff ? 'Učenik nema povezan roditeljski nalog' : 'Nastavnik više ne predaje odeljenju'

  return (
    <section className="panel messages-panel">
      <div className="thread-head">
        <button className="icon-button thread-back" onClick={onBack} aria-label="Nazad na razgovore"><ArrowLeft size={18} /></button>
        <span className="conversation-person">
          <span className="teacher-avatar">{initials(conversation.counterpartName)}</span>
          <span className="conversation-copy"><strong>{conversation.counterpartName}</strong><small>{isStaff ? conversation.counterpartRole : `${conversation.counterpartRole} · ${conversation.studentName}, ${conversation.className}`}</small></span>
        </span>
      </div>
      <div className="message-list" ref={listRef}>
        {messages.data.map((message) => (
          <article className={`message-bubble ${message.isMine ? 'mine' : ''}`} key={message.id}>
            <small>{message.senderName} · {formatDateTime(message.createdAt)}{message.isMine && message.readAt ? ' · pročitano' : ''}</small>
            <p>{message.content}</p>
          </article>
        ))}
        {!messages.loading && messages.data.length === 0 && <EmptyState>Još nema poruka. Napišite prvu poruku ispod.</EmptyState>}
      </div>
      <form className="message-compose" onSubmit={send}>
        <input name="message" maxLength={4000} disabled={!conversation.canWrite} placeholder={placeholder} aria-label="Nova poruka" autoComplete="off" />
        <button className="send-button" aria-label="Pošalji poruku" disabled={!conversation.canWrite || sending}><Send size={17} /></button>
      </form>
    </section>
  )
}

// Nastavnik bira odeljenje i učenika čijem roditelju piše.
function NewMessage({ onClose, onSent }: { onClose: () => void; onSent: (conversation: Conversation, message: ChatMessage) => void }) {
  const { classes, selectedClass } = useApp()
  const [classId, setClassId] = useState(selectedClass?.id ?? classes[0]?.id ?? 0)
  const students = useApi<Student[]>(classId ? `/api/classes/${classId}/students` : null, [])
  const withParent = students.data.filter((student) => student.parentName)

  async function submit(form: FormData) {
    const student = students.data.find((item) => item.id === Number(form.get('studentId')))
    if (!student) throw new Error('Izaberite učenika.')
    const saved = await api<ChatMessage>(`/api/students/${student.id}/messages`, {
      method: 'POST',
      body: { content: String(form.get('content')).trim(), teacherUserId: null },
    })
    onSent(newConversation(student, classId), saved)
  }

  return (
    <Modal eyebrow="PORUKA RODITELJU" title="Nova poruka" submitLabel="Pošalji" submitIcon={<Send size={15} />} onClose={onClose} onSubmit={submit}>
      <label>Odeljenje
        <select value={classId} onChange={(event) => setClassId(Number(event.target.value))}>
          {classes.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
        </select>
      </label>
      <label>Učenik
        <select name="studentId" key={classId} defaultValue="" required>
          <option value="" disabled>{students.loading ? 'Učitavanje...' : 'Izaberite učenika'}</option>
          {students.data.map((student) => (
            <option key={student.id} value={student.id} disabled={!student.parentName}>
              {student.fullName}{student.parentName ? ` — ${student.parentName}` : ' (roditelj nije povezan)'}
            </option>
          ))}
        </select>
      </label>
      {!students.loading && withParent.length === 0 && <p className="table-note">Nijedan učenik ovog odeljenja još nema povezan roditeljski nalog.</p>}
      <label>Poruka<textarea name="content" rows={5} minLength={2} maxLength={4000} required /></label>
    </Modal>
  )
}

function newConversation(student: Student, classId: number): Conversation {
  return {
    key: String(student.id),
    studentId: student.id,
    studentName: student.fullName,
    classId,
    className: student.className,
    teacherId: null,
    counterpartName: student.parentName ?? 'Roditelj nije povezan',
    counterpartRole: `Roditelj · ${student.fullName}, ${student.className}`,
    lastMessage: null,
    lastFromMe: false,
    lastAt: null,
    unread: 0,
    canWrite: student.parentName !== null,
  }
}

// Danas: samo vreme; ranije: datum.
function shortTime(value: string) {
  const date = new Date(value)
  const today = new Date()
  return date.toDateString() === today.toDateString()
    ? date.toLocaleTimeString('sr-Latn-RS', { hour: '2-digit', minute: '2-digit' })
    : date.toLocaleDateString('sr-Latn-RS', { day: 'numeric', month: 'numeric' })
}
