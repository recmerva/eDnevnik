import { useState, type FormEvent } from 'react'
import { ArrowUpRight, Award, BookOpen, Check, GraduationCap, ShieldCheck } from 'lucide-react'
import { api, errorMessage, resetCsrfToken } from '../api/client'
import type { SchoolInfo, UserProfile } from '../types'

export function AuthScreen({ school, onSignedIn }: { school: SchoolInfo; onSignedIn: (user: UserProfile) => void }) {
  const [register, setRegister] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState('')

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = new FormData(event.currentTarget)
    const body = register
      ? { fullName: form.get('fullName'), studentCode: form.get('studentCode'), email: form.get('email'), password: form.get('password') }
      : { email: form.get('email'), password: form.get('password') }
    setSubmitting(true)
    setError('')
    try {
      const user = await api<UserProfile>(`/api/auth/${register ? 'register' : 'login'}`, { method: 'POST', body })
      resetCsrfToken()
      onSignedIn(user)
    } catch (submitError) {
      setError(errorMessage(submitError, 'Prijava nije uspela.'))
    } finally {
      setSubmitting(false)
    }
  }

  function toggleMode() {
    setError('')
    setRegister(!register)
  }

  return (
    <main className="auth-layout">
      <section className="auth-aside">
        <span className="brand brand-inverse"><span className="brand-icon"><GraduationCap size={21} /></span><span>moj<span className="brand-light">dnevnik</span></span></span>
        <div className="auth-art">
          <span className="art-label">ŠKOLA · PORODICA · ZAJEDNO</span>
          <h1>Svaki školski dan,<br /><em>jasniji.</em></h1>
          <p>Ocene, obaveze i poruke nastavnika — pregledno i uvek dostupno.</p>
          <div className="art-orbit orbit-one" /><div className="art-orbit orbit-two" />
          <div className="art-book"><BookOpen size={54} strokeWidth={1.2} /></div>
          <div className="art-check"><Check size={18} /></div>
          <div className="art-star"><Award size={22} /></div>
        </div>
        <div className="auth-footer">{school.name} <span>·</span> školska {school.schoolYear}</div>
      </section>
      <section className="auth-main">
        <div className="auth-form-wrap">
          <span className="eyebrow">{register ? 'REGISTRACIJA RODITELJA' : 'PRIJAVA NA DNEVNIK'}</span>
          <h2>{register ? 'Kreirajte nalog roditelja' : 'Dobro došli nazad'}</h2>
          <p className="auth-subtitle">{register ? 'Za registraciju je potreban kod učenika koji izdaje škola.' : 'Prijava za roditelje, nastavnike i administraciju škole.'}</p>
          <form className="auth-form" onSubmit={submit} key={register ? 'register' : 'login'}>
            {register && <>
              <label>Ime i prezime<input name="fullName" placeholder="Amina Hadžić" minLength={2} maxLength={120} autoComplete="name" required /></label>
              <label>Kod učenika<input name="studentCode" placeholder="Kod iz škole, npr. K7MR-Q4TB" maxLength={32} autoComplete="off" required />
                <small className="field-hint">Imate nalog za drugo dete? Unesite isti email i lozinku — dete će biti dodato na postojeći nalog.</small></label>
            </>}
            <label>Email adresa<input name="email" type="email" placeholder="ime@primer.rs" autoComplete="email" required /></label>
            <label>Lozinka<input name="password" type="password" placeholder={register ? 'Najmanje 10 karaktera' : 'Unesite lozinku'} autoComplete={register ? 'new-password' : 'current-password'} minLength={register ? 10 : undefined} required /></label>
            {error && <p className="auth-error" role="alert">{error}</p>}
            <button className="button button-primary auth-submit" type="submit" disabled={submitting}>
              {submitting ? 'Molimo sačekajte...' : register ? 'Napravi nalog' : 'Prijavi se'} <ArrowUpRight size={17} />
            </button>
          </form>
          <div className="auth-switch">
            {register ? 'Već imate nalog?' : 'Nemate nalog?'} <button type="button" onClick={toggleMode}>{register ? 'Prijavite se' : 'Registrujte se'}</button>
          </div>
          <p className="auth-privacy"><ShieldCheck size={14} /> Pristup je ograničen na naloge koje je škola povezala sa učenikom.</p>
        </div>
      </section>
    </main>
  )
}
