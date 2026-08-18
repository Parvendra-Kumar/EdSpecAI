import { useEffect, useState } from 'react'
import './dashboard.css'
import './dashboard-enhancements.css'
import './layout-fixes.css'
import './ui-change-overrides.css'
import './generator.css'
import './assessment-library.css'
import './specification-library.css'
import './specification-delete.css'
import './toast.css'

type User = { email: string; password: string; name: string; role: string }
type View = 'overview' | 'create' | 'detail' | 'all-specifications' | 'generate' | 'assessment'

const users: User[] = [
  { email: 'teacher@edspec.demo', password: 'Teacher@123', name: 'Demo Teacher', role: 'Teacher' },
  { email: 'reviewer@edspec.demo', password: 'Reviewer@123', name: 'Demo Reviewer', role: 'Reviewer' },
  { email: 'student@edspec.demo', password: 'Student@123', name: 'Demo Student', role: 'Student' },
  { email: 'admin@edspec.demo', password: 'Admin@123', name: 'Demo Admin', role: 'Admin' },
]

// Demo-only POC credentials. There is no real authentication in this prototype.
type Spec = {
  id: string
  version: string
  status: string
  title: string
  subject: string
  learningObjective: string
  questionRules: { totalQuestions: number; questionType: string; optionsPerQuestion: number }
  difficultyDistribution: { easy: number; medium: number; hard: number }
  scoringRules: { pointsPerQuestion: number; totalPoints: number }
  approval: { required: boolean; approvedBy: string | null; approvedAt: string | null }
  createdAt: string
  updatedAt: string
}

type Question = {
  id: string
  learningObjective: string
  difficulty: string
  questionType: string
  prompt: string
  options: { id: string; text: string }[]
  correctOptionId: string
  points: number
}

type Assessment = {
  id: string
  specificationId: string
  specificationVersion: string
  status: string
  questions: Question[]
  createdBy: string
  createdAt: string
}

type AssessmentSummary = {
  id: string
  specificationId: string
  specificationVersion: string
  status: string
  questionCount: number
  totalPoints: number
  createdBy: string
  createdAt: string
}

type Form = {
  id: string
  version: string
  title: string
  subject: string
  learningObjective: string
  totalQuestions: number
  questionType: string
  optionsPerQuestion: number
  easy: number
  medium: number
  hard: number
  pointsPerQuestion: number
  totalPoints: number
}

const blank: Form = {
  id: '', version: '', title: '', subject: '', learningObjective: '',
  totalQuestions: 0, questionType: '', optionsPerQuestion: 0,
  easy: 0, medium: 0, hard: 0, pointsPerQuestion: 0, totalPoints: 0,
}

async function api<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(path, {
    headers: { 'Content-Type': 'application/json' },
    ...init,
  })
  const body = await response.json().catch(() => ({}))

  if (!response.ok) {
    const errors = body.errors ? Object.values(body.errors).flat().join(' ') : body.message
    throw new Error(errors || `${response.status} request failed`)
  }

  return body as T
}

function specificationKey(specification: Spec) {
  return `${specification.id}::${specification.version}`
}

function App() {
  const [user, setUser] = useState<User | null>(() => JSON.parse(sessionStorage.getItem('edspec-user') || 'null'))
  const [spec, setSpec] = useState<Spec | null>(null)
  const [assessment, setAssessment] = useState<Assessment | null>(null)
  const [assessments, setAssessments] = useState<AssessmentSummary[]>([])
  const [assessmentsLoading, setAssessmentsLoading] = useState(false)
  const [assessmentDetailLoading, setAssessmentDetailLoading] = useState(false)
  const [selectedAssessmentId, setSelectedAssessmentId] = useState('')
  const [specifications, setSpecifications] = useState<Spec[]>([])
  const [selectedSpecificationKey, setSelectedSpecificationKey] = useState('')
  const [requestedBy, setRequestedBy] = useState('')
  const [specificationsLoading, setSpecificationsLoading] = useState(false)
  const [view, setView] = useState<View>('overview')
  const [form, setForm] = useState<Form>(blank)
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')
  const [toast, setToast] = useState('')
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    if (!user) return
    void api<Spec[]>('/api/specifications').then(setSpecifications).catch(() => undefined)
    void api<AssessmentSummary[]>('/api/assessments').then(setAssessments).catch(() => undefined)
  }, [user])

  useEffect(() => {
    if (user && !requestedBy) setRequestedBy(user.name)
  }, [user, requestedBy])

  useEffect(() => {
    if (!toast) return
    const timer = window.setTimeout(() => setToast(''), 3500)
    return () => window.clearTimeout(timer)
  }, [toast])

  if (!user) {
    return <Login onLogin={u => { sessionStorage.setItem('edspec-user', JSON.stringify(u)); setUser(u) }} />
  }

  const run = async (action: () => Promise<void>, success: string) => {
    setBusy(true)
    setError('')
    setMessage('')
    setToast('')
    try {
      await action()
      setToast(success)
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setBusy(false)
    }
  }

  const loadSpecifications = async () => {
    setSpecificationsLoading(true)
    setError('')
    try {
      const list = await api<Spec[]>('/api/specifications')
      setSpecifications(list)
      setSelectedSpecificationKey(current => {
        if (list.some(item => specificationKey(item) === current)) return current
        const firstAvailable = list.find(item => item.status === 'approved') || list[0]
        return firstAvailable ? specificationKey(firstAvailable) : ''
      })
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setSpecificationsLoading(false)
    }
  }

  const openGenerator = () => {
    setView('generate')
    setMessage('')
    void loadSpecifications()
  }

  const openCreate = () => {
    setError('')
    setMessage('')
    setSpec(null)
    setForm(blank)
    setView('create')
  }

  const openAllSpecifications = () => {
    setView('all-specifications')
    setMessage('')
    setSpec(null)
    void loadSpecifications()
  }

  const openSpecification = (id: string, version: string) => run(async () => {
    const loaded = await api<Spec>(`/api/specifications/${encodeURIComponent(id)}/versions/${encodeURIComponent(version)}`)
    setSpec(loaded)
    fromSpec(loaded, setForm)
  }, 'Specification loaded')

  const deleteSpecification = (id: string, version: string) => {
    const target = specifications.find(item => item.id === id && item.version === version)
    if (!target) {
      setError('Specification was not found in the current list.')
      return
    }

    if (!window.confirm(`Delete "${target.title}" version ${target.version}? This action cannot be undone.`)) {
      return
    }

    void run(async () => {
      await api<void>(`/api/specifications/${encodeURIComponent(id)}/versions/${encodeURIComponent(version)}`, {
        method: 'DELETE',
        body: JSON.stringify({ deletedBy: user.name }),
      })
      setSpecifications(current => current.filter(item => specificationKey(item) !== specificationKey(target)))
      if (spec && specificationKey(spec) === specificationKey(target)) {
        setSpec(null)
      }
    }, 'Specification deleted successfully')
  }

  const loadAssessments = async () => {
    setAssessmentsLoading(true)
    setError('')
    try {
      const list = await api<AssessmentSummary[]>('/api/assessments')
      setAssessments(list)
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setAssessmentsLoading(false)
    }
  }

  const openAssessments = () => {
    setView('assessment')
    setMessage('')
    setAssessment(null)
    setSelectedAssessmentId('')
    void loadAssessments()
  }

  const openAssessment = async (assessmentId: string) => {
    setAssessmentDetailLoading(true)
    setError('')
    try {
      const detail = await api<Assessment>(`/api/assessments/${encodeURIComponent(assessmentId)}`)
      setAssessment(detail)
      setSelectedAssessmentId(detail.id)
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setAssessmentDetailLoading(false)
    }
  }

  const create = () => run(async () => {
    const created = await api<Spec>('/api/specifications/drafts', {
      method: 'POST',
      body: JSON.stringify(toCreate(form)),
    })
    setSpec(created)
    fromSpec(created, setForm)
    setView('detail')
  }, 'Specification created successfully')

  const load = () => run(async () => {
    const loaded = await api<Spec>(`/api/specifications/${encodeURIComponent(form.id)}/versions/${encodeURIComponent(form.version)}`)
    setSpec(loaded)
    fromSpec(loaded, setForm)
    setView('detail')
  }, 'Specification loaded')

  const save = () => spec && run(async () => {
    const saved = await api<Spec>(`/api/specifications/${spec.id}/versions/${spec.version}`, {
      method: 'PUT',
      body: JSON.stringify(toUpdate(form)),
    })
    setSpec(saved)
    setSpecifications(current => current.map(item => specificationKey(item) === specificationKey(saved) ? saved : item))
  }, 'Specification saved successfully')

  const approve = () => spec && run(async () => {
    const approved = await api<Spec>(`/api/specifications/${spec.id}/versions/${spec.version}/approve`, {
      method: 'POST',
      body: JSON.stringify({ approvedBy: user.name }),
    })
    setSpec(approved)
    setSpecifications(current => current.map(item => specificationKey(item) === specificationKey(approved) ? approved : item))
  }, 'Specification approved')

  const generate = (selected: Spec = spec as Spec, requester = user.name) => selected && run(async () => {
    const result = await api<{ assessment: Assessment }>(
      `/api/specifications/${encodeURIComponent(selected.id)}/versions/${encodeURIComponent(selected.version)}/assessments/generate`,
      { method: 'POST', body: JSON.stringify({ requestedBy: requester }) },
    )
    setSpec(selected)
    setAssessment(result.assessment)
    setSelectedAssessmentId(result.assessment.id)
    setAssessments(current => [toAssessmentSummary(result.assessment), ...current.filter(item => item.id !== result.assessment.id)])
    setView('assessment')
  }, 'Assessment generated')

  const generateSelected = () => {
    const selected = specifications.find(item => specificationKey(item) === selectedSpecificationKey)
    const requester = requestedBy.trim()

    if (!selected) {
      setError('Select a specification before generating an assessment.')
      return
    }
    if (selected.status !== 'approved') {
      setError('Only approved specifications can be used to generate an assessment.')
      return
    }
    if (requester.length < 2) {
      setError('Requested by must contain at least 2 characters.')
      return
    }

    void generate(selected, requester)
  }

  const logout = () => {
    sessionStorage.removeItem('edspec-user')
    setUser(null)
  }

  return (
    <div className="app">
      {toast && <div className="toast toast-success" role="status" aria-live="polite">✓ {toast}</div>}
      <aside>
        <div className="brand"><b>EdSpec</b><small>AI · POC WORKSPACE</small></div>
        <div className="workspace">WORKSPACE</div>
        <nav>
          <button className={view === 'overview' ? 'active' : ''} onClick={() => setView('overview')}>⌂　Overview</button>
          <button className={view === 'create' || view === 'detail' ? 'active' : ''} onClick={openCreate}>◇　Create Specification</button>
          <button className={view === 'all-specifications' ? 'active' : ''} onClick={openAllSpecifications}>☆　View all Specifications</button>
          <button className={view === 'assessment' ? 'active' : ''} onClick={openAssessments}>▤　Assessments</button>
          <button className={view === 'generate' ? 'active' : ''} onClick={openGenerator}>✦　Generate assessment</button>
        </nav>
        <div className="profile">
          <b>{user.name}</b>
          <small>{user.role} · Demo identity</small>
          <button className="link" onClick={logout}>Sign out</button>
        </div>
      </aside>
      <main>
        <header><span>REVIEWER WORKSPACE <i>/ {view}</i></span><strong>POC workspace</strong></header>
        <section className="wrap">
          {error && <div className="error"><b>Request failed</b><div>{error}</div></div>}
          {view === 'overview' && <Dashboard specifications={specifications} assessments={assessments} onCreate={openCreate} onOpenSpecifications={openAllSpecifications} onOpenAssessments={openAssessments} />}
          {view === 'create' && <Create form={form} setForm={setForm} onCreate={create} onLoad={load} busy={busy} />}
          {view === 'detail' && spec && <Detail spec={spec} form={form} setForm={setForm} onSave={save} onApprove={approve} onGenerate={() => generate(spec)} busy={busy} user={user} />}
          {view === 'all-specifications' && <SpecificationsLibraryPage
            specifications={specifications}
            selectedSpecification={spec}
            form={form}
            setForm={setForm}
            loading={specificationsLoading}
            busy={busy}
            onRefresh={() => void loadSpecifications()}
            onOpen={openSpecification}
            onDelete={deleteSpecification}
            onBack={() => setSpec(null)}
            onSave={save}
            onApprove={approve}
            user={user}
          />}
          {view === 'generate' && <GenerateAssessmentPage
            specifications={specifications}
            selectedSpecificationKey={selectedSpecificationKey}
            onSelect={setSelectedSpecificationKey}
            requestedBy={requestedBy}
            onRequestedByChange={setRequestedBy}
            onGenerate={generateSelected}
            onRefresh={() => void loadSpecifications()}
            loading={specificationsLoading}
            busy={busy}
          />}
          {view === 'assessment' && <AssessmentsPage
            assessments={assessments}
            selectedAssessment={assessment}
            selectedAssessmentId={selectedAssessmentId}
            loading={assessmentsLoading}
            detailLoading={assessmentDetailLoading}
            onRefresh={() => void loadAssessments()}
            onOpen={openAssessment}
            onGenerate={openGenerator}
            onBack={() => { setAssessment(null); setSelectedAssessmentId('') }}
          />}
        </section>
      </main>
    </div>
  )
}

function StatCards({ approved, assessments, drafts, onOpenSpecifications, onOpenAssessments }: { approved: number; assessments: number; drafts: number; onOpenSpecifications: () => void; onOpenAssessments: () => void }) {
  return <div className="stats live-stats">
    <button className="stat-card" onClick={onOpenSpecifications}><span>◇</span><label>Approved specifications<b>{approved}</b><small>Open specification library</small></label></button>
    <button className="stat-card" onClick={onOpenAssessments}><span>▤</span><label>Assessments generated<b>{assessments}</b><small>View generated assessments</small></label></button>
    <button className="stat-card" onClick={onOpenSpecifications}><span>◉</span><label>Awaiting review<b>{drafts}</b><small>{drafts ? 'Drafts need attention' : 'Nothing waiting for review'}</small></label></button>
  </div>
}

function Dashboard({ specifications, assessments, onCreate, onOpenSpecifications, onOpenAssessments }: { specifications: Spec[]; assessments: AssessmentSummary[]; onCreate: () => void; onOpenSpecifications: () => void; onOpenAssessments: () => void }) {
  const approved = specifications.filter(item => item.status === 'approved').length
  const drafts = specifications.filter(item => item.status === 'draft').length
  return <>
    <div className="dashboard-title">
      <div><small>REVIEWER WORKSPACE</small><h1>Workspace overview</h1><p>Here’s what needs your attention today.</p></div>
      <button className="primary" onClick={onCreate}>＋ New specification</button>
    </div>
    <StatCards approved={approved} assessments={assessments.length} drafts={drafts} onOpenSpecifications={onOpenSpecifications} onOpenAssessments={onOpenAssessments} />
    <div className="stats legacy-stats" aria-hidden="true">
      <div><span>◇</span><label>Approved specifications<b>8</b><small>↑ 1 this month</small></label></div>
      <div><span>▤</span><label>Assessments generated<b>12</b><small>↑ 18% this month</small></label></div>
      <div><span>◉</span><label>Awaiting review<b>2</b><small>Needs your attention</small></label></div>
    </div>
    <div className="dashboard-grid">
      <section className="card"><h2>Recent activity</h2><p className="muted">Your latest specification and assessment work.</p>{[
        ['●', 'Specification approved', 'Basic Algebra · v5.0.1', 'Approved'],
        ['●', 'Assessment generated', 'Basic Algebra · 5 questions', 'Generated'],
        ['●', 'Draft specification', 'Basic Science · v4.0.0', 'Draft'],
      ].map(x => <div className="activity" key={x[1]}><i>{x[0]}</i><div><b>{x[1]}</b><small>{x[2]}</small></div><em>{x[3]}</em></div>)}</section>
      <section className="card"><h2>Needs your attention</h2><p className="muted">Items waiting for a decision.</p><div className="queue"><b>!</b><div><strong>Basic Algebra assessment</strong><small>3 findings · 1 high severity</small></div><button className="secondary" onClick={onOpenAssessments}>Open</button></div><div className="queue"><b>◇</b><div><strong>Basic Science</strong><small>Draft specification</small></div><button className="secondary" onClick={onOpenSpecifications}>Open</button></div></section>
    </div>
  </>
}

function Login({ onLogin }: { onLogin: (u: User) => void }) {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')

  return <div className="login"><section className="login-card"><div className="logo">E</div><small>EDSPEC AI</small><h1>Welcome back</h1><p>Sign in with a demo identity to enter the POC workspace.</p><label>Email<input type="email" value={email} onChange={e => setEmail(e.target.value)} /></label><label>Password<input type="password" value={password} onChange={e => setPassword(e.target.value)} /></label>{error && <div className="error">{error}</div>}<button className="primary" onClick={() => { const found = users.find(x => x.email === email && x.password === password); found ? onLogin(found) : setError('Invalid demo email or password') }}>Sign in</button><div className="hint">Demo: teacher@edspec.demo / Teacher@123<br />Reviewer: reviewer@edspec.demo / Reviewer@123</div></section></div>
}

function SpecificationsLibraryPage({ specifications, selectedSpecification, form, setForm, loading, busy, onRefresh, onOpen, onDelete, onBack, onSave, onApprove, user }: { specifications: Spec[]; selectedSpecification: Spec | null; form: Form; setForm: (form: Form) => void; loading: boolean; busy: boolean; onRefresh: () => void; onOpen: (id: string, version: string) => Promise<void>; onDelete: (id: string, version: string) => void; onBack: () => void; onSave: () => void; onApprove: () => void; user: User }) {
  if (selectedSpecification) {
    return <SpecificationLibraryDetail spec={selectedSpecification} form={form} setForm={setForm} busy={busy} onBack={onBack} onSave={onSave} onApprove={onApprove} user={user} />
  }

  return <section className="specifications-library-page">
    <div className="page-heading"><div><small>SPECIFICATION LIBRARY</small><h1>View all specifications</h1><p>Review every specification stored by EdSpec, including its approval status and assessment rules.</p></div><button className="secondary" onClick={onRefresh} disabled={loading}>Refresh specifications</button></div>
    {loading && <section className="card loading-state">Loading specifications...</section>}
    {!loading && specifications.length === 0 && <section className="card empty-state"><h2>No specifications found</h2><p className="muted">Create a specification draft to add it to the library.</p></section>}
    {!loading && specifications.length > 0 && <section className="specification-list" aria-label="All specifications">{specifications.map(item => <article className="card specification-list-item" key={specificationKey(item)}><div className="specification-list-main"><a className="specification-link" href={`#${encodeURIComponent(specificationKey(item))}`} onClick={event => { event.preventDefault(); void onOpen(item.id, item.version) }}>{item.title} <span>v{item.version}</span></a><small>{item.id} · {item.subject}</small><p>{item.learningObjective}</p><time dateTime={item.updatedAt}>Updated {new Date(item.updatedAt).toLocaleString()} · {item.questionRules.totalQuestions} questions · {item.scoringRules.totalPoints} points</time></div><div className="specification-list-actions"><span className={`badge ${item.status}`}>{item.status}</span><button className="icon-button danger" type="button" title={`Delete ${item.title} version ${item.version}`} aria-label={`Delete ${item.title} version ${item.version}`} onClick={() => onDelete(item.id, item.version)}>🗑</button></div></article>)}</section>}
  </section>
}

function SpecificationLibraryDetail({ spec, form, setForm, busy, onBack, onSave, onApprove, user }: { spec: Spec; form: Form; setForm: (form: Form) => void; busy: boolean; onBack: () => void; onSave: () => void; onApprove: () => void; user: User }) {
  return <section className="specification-detail-page">
    <div className="page-heading"><div><small>SPECIFICATION DETAIL</small><h1>Edit specification</h1><p>{spec.id} · v{spec.version}</p></div><button className="secondary" onClick={onBack}>Back to specifications</button></div>
    <section className="card"><div className="detail-head"><div><small>{spec.subject}</small><h2>{spec.title}</h2></div><span className={`badge ${spec.status}`}>{spec.status}</span></div>{fields(form, setForm)}<dl><div><dt>Approved by</dt><dd>{spec.approval.approvedBy || 'Pending approval'}</dd></div><div><dt>Created</dt><dd>{new Date(spec.createdAt).toLocaleString()}</dd></div><div><dt>Updated</dt><dd>{new Date(spec.updatedAt).toLocaleString()}</dd></div></dl><div className="actions"><button className="secondary" onClick={onBack}>Cancel</button><button className="primary" onClick={onSave} disabled={busy}>{busy ? 'Saving...' : 'Save specification'}</button>{spec.status === 'draft' && <button className="secondary" onClick={onApprove} disabled={busy}>Approve as {user.name}</button>}</div></section>
  </section>
}

function GenerateAssessmentPage({ specifications, selectedSpecificationKey, onSelect, requestedBy, onRequestedByChange, onGenerate, onRefresh, loading, busy }: { specifications: Spec[]; selectedSpecificationKey: string; onSelect: (key: string) => void; requestedBy: string; onRequestedByChange: (value: string) => void; onGenerate: () => void; onRefresh: () => void; loading: boolean; busy: boolean }) {
  const selected = specifications.find(item => specificationKey(item) === selectedSpecificationKey)
  const approvedCount = specifications.filter(item => item.status === 'approved').length

  return <section className="generator-page">
    <div className="page-heading"><div><small>ASSESSMENT WORKFLOW</small><h1>Generate assessment</h1><p>Choose an approved specification and generate an assessment from its learning objectives and question rules.</p></div><button className="secondary" onClick={onRefresh} disabled={loading}>↻ Refresh specifications</button></div>
    <section className="card generator-card">
      <div className="generator-step"><span>1</span><div><h2>Choose a specification</h2><p className="muted">The list is loaded from the specifications already stored by EdSpec.</p></div></div>
      <label className="select-label">Specification<select value={selectedSpecificationKey} onChange={e => onSelect(e.target.value)} disabled={loading || specifications.length === 0}>
        {loading && <option value="">Loading specifications…</option>}
        {!loading && specifications.length === 0 && <option value="">No specifications found</option>}
        {specifications.map(item => <option key={specificationKey(item)} value={specificationKey(item)} disabled={item.status !== 'approved'}>{item.title} · {item.id} · v{item.version} · {item.subject} ({item.status})</option>)}
      </select></label>
      {specifications.length > 0 && approvedCount === 0 && <div className="inline-warning">There are no approved specifications available. Approve a specification before generating an assessment.</div>}
      {selected && <div className="selected-spec"><div><small>{selected.id} · v{selected.version}</small><h3>{selected.title}</h3><p>{selected.learningObjective}</p></div><span className={`badge ${selected.status}`}>{selected.status}</span></div>}
    </section>
    <section className="card generator-card">
      <div className="generator-step"><span>2</span><div><h2>Generation details</h2><p className="muted">These details are sent with the existing assessment-generation request.</p></div></div>
      <div className="generator-details">
        <label>Requested by<input value={requestedBy} onChange={e => onRequestedByChange(e.target.value)} placeholder="Reviewer name" /></label>
        {selected && <div className="rules-preview"><small>Specification rules</small><strong>{selected.questionRules.totalQuestions} questions · {selected.questionRules.questionType}</strong><span>{selected.questionRules.optionsPerQuestion} options per question</span><span>{selected.difficultyDistribution.easy} easy · {selected.difficultyDistribution.medium} medium · {selected.difficultyDistribution.hard} hard</span><span>{selected.scoringRules.totalPoints} total points</span></div>}
      </div>
      <div className="generator-footer"><p className="muted">The generated questions will be reviewed by the existing backend workflow and shown in Assessments.</p><button className="primary" onClick={onGenerate} disabled={busy || loading || !selected || selected.status !== 'approved'}>{busy ? 'Generating…' : 'Generate assessment'}</button></div>
    </section>
  </section>
}

function EmptyAssessment({ onGenerate }: { onGenerate: () => void }) {
  return <section className="card empty-state"><h2>No assessment in this session</h2><p className="muted">Generate an assessment from an approved specification to view the generated questions here.</p><button className="primary" onClick={onGenerate}>Generate assessment</button></section>
}

function fields(f: Form, set: (f: Form) => void, disabled = false) {
  const update = (key: keyof Form, value: string) => set({ ...f, [key]: ['totalQuestions', 'optionsPerQuestion', 'easy', 'medium', 'hard', 'pointsPerQuestion', 'totalPoints'].includes(key) ? Number(value) : value })
  return <div className="grid">{(['title', 'subject', 'learningObjective', 'questionType'] as const).map(key => <label key={key}>{key === 'learningObjective' ? 'Learning objective' : key[0].toUpperCase() + key.slice(1)}<input disabled={disabled} value={f[key]} onChange={e => update(key, e.target.value)} /></label>)}{(['totalQuestions', 'optionsPerQuestion', 'easy', 'medium', 'hard', 'pointsPerQuestion', 'totalPoints'] as const).map(key => <label key={key}>{key.replace(/[A-Z]/g, m => ` ${m}`).replace(/^./, m => m.toUpperCase())}<input disabled={disabled} type="number" min={key === 'optionsPerQuestion' ? 2 : 0} value={f[key] === 0 ? '' : f[key]} onChange={e => update(key, e.target.value)} /></label>)}</div>
}

function Create({ form, setForm, onCreate, onLoad, busy }: { form: Form; setForm: (f: Form) => void; onCreate: () => void; onLoad: () => void; busy: boolean }) {
  const update = (key: keyof Form, value: string) => setForm({ ...form, [key]: key === 'id' || key === 'version' ? value : form[key] })
  return <section className="card"><h2>Create a specification draft</h2><div className="grid"><label>ID (optional)<input value={form.id} onChange={e => update('id', e.target.value)} /></label><label>Version<input value={form.version} onChange={e => update('version', e.target.value)} /></label></div>{fields(form, setForm)}<div className="checks"><span className={form.easy + form.medium + form.hard === form.totalQuestions ? 'ok' : 'bad'}>Difficulty: {form.easy + form.medium + form.hard} / {form.totalQuestions}</span><span className={form.totalPoints === form.totalQuestions * form.pointsPerQuestion ? 'ok' : 'bad'}>Points: {form.totalPoints} / {form.totalQuestions * form.pointsPerQuestion}</span></div><div className="actions"><button className="secondary" onClick={onLoad} disabled={busy || !form.id}>Load ID/version</button><button className="primary" onClick={onCreate} disabled={busy}>{busy ? 'Working…' : 'Create specification'}</button></div></section>
}

function Detail({ spec, form, setForm, onSave, onApprove, onGenerate, busy, user }: { spec: Spec; form: Form; setForm: (f: Form) => void; onSave: () => void; onApprove: () => void; onGenerate: () => void; busy: boolean; user: User }) {
  const edit = spec.status === 'draft'
  return <section className="card"><div className="detail-head"><div><small>{spec.id} · v{spec.version}</small><h2>{spec.title}</h2></div><span className={`badge ${spec.status}`}>{spec.status}</span></div>{fields(form, setForm, !edit)}<dl><div><dt>Approved by</dt><dd>{spec.approval.approvedBy || 'Pending'}</dd></div><div><dt>Created</dt><dd>{new Date(spec.createdAt).toLocaleString()}</dd></div><div><dt>Updated</dt><dd>{new Date(spec.updatedAt).toLocaleString()}</dd></div></dl><div className="actions">{edit && <><button className="secondary" onClick={onSave} disabled={busy}>Save</button><button className="primary" onClick={onApprove} disabled={busy}>Approve as {user.name}</button></>}{!edit && <button className="primary" onClick={onGenerate} disabled={busy}>{busy ? 'Generating…' : 'Generate assessment'}</button>}</div></section>
}

function AssessmentView({ assessment }: { assessment: Assessment }) {
  return <section><div className="card"><div className="detail-head"><div><small>{assessment.specificationId} · v{assessment.specificationVersion}</small><h2>Assessment output</h2></div><span className="badge generated">{assessment.status}</span></div><p>Created by {assessment.createdBy}. This result is held in React state for this session because no assessment GET endpoint exists.</p></div>{assessment.questions.map((q, i) => <article className="card question" key={q.id}><div className="question-title"><b>Question {i + 1}</b><span>{q.difficulty} · {q.points} points</span></div><h3>{q.prompt}</h3><p className="muted">{q.learningObjective} · {q.questionType}</p>{q.options.map(o => <div className={o.id === q.correctOptionId ? 'option correct' : 'option'} key={o.id}>{o.id}. {o.text}{o.id === q.correctOptionId && <b> ✓ Correct</b>}</div>)}</article>)}</section>
}

function AssessmentsPage({ assessments, selectedAssessment, selectedAssessmentId, loading, detailLoading, onRefresh, onOpen, onGenerate, onBack }: { assessments: AssessmentSummary[]; selectedAssessment: Assessment | null; selectedAssessmentId: string; loading: boolean; detailLoading: boolean; onRefresh: () => void; onOpen: (id: string) => Promise<void>; onGenerate: () => void; onBack: () => void }) {
  if (selectedAssessment && selectedAssessment.id === selectedAssessmentId) {
    return <AssessmentDetail assessment={selectedAssessment} onBack={onBack} />
  }

  return <section className="assessments-page">
    <div className="page-heading">
      <div><small>ASSESSMENT LIBRARY</small><h1>Assessments</h1><p>Open an assessment to review its questions or download a teacher copy.</p></div>
      <div className="assessment-heading-actions"><button className="secondary" onClick={onRefresh} disabled={loading || detailLoading}>Refresh</button><button className="primary" onClick={onGenerate}>Generate assessment</button></div>
    </div>
    {loading && <section className="card loading-state">Loading assessments...</section>}
    {!loading && assessments.length === 0 && <section className="card empty-state"><h2>No assessments found</h2><p className="muted">Generate an assessment from an approved specification to add it to this library.</p><button className="primary" onClick={onGenerate}>Generate assessment</button></section>}
    {!loading && assessments.length > 0 && <section className="assessment-list" aria-label="Generated assessments">{assessments.map(item => <article className="card assessment-list-item" key={item.id}><div className="assessment-list-main"><a className="assessment-link" href={`#${encodeURIComponent(item.id)}`} onClick={event => { event.preventDefault(); void onOpen(item.id) }}>{item.specificationId} <span>v{item.specificationVersion}</span></a><small>{item.id}</small><p>{item.questionCount} questions · {item.totalPoints} points · Created by {item.createdBy}</p><time dateTime={item.createdAt}>{new Date(item.createdAt).toLocaleString()}</time></div><span className={`badge ${item.status}`}>{item.status}</span></article>)}</section>}
  </section>
}

function AssessmentDetail({ assessment, onBack }: { assessment: Assessment; onBack: () => void }) {
  return <section className="assessment-detail">
    <div className="page-heading"><div><small>ASSESSMENT DETAIL</small><h1>Assessment</h1><p>Review the generated questions and teacher answer key.</p></div><div className="assessment-heading-actions"><button className="secondary" onClick={onBack}>Back to assessments</button><a className="primary download-button" href={`/api/assessments/${encodeURIComponent(assessment.id)}/download`} download>Download assessment</a></div></div>
    <div className="card assessment-summary"><div className="detail-head"><div><small>{assessment.specificationId} · v{assessment.specificationVersion}</small><h2>Assessment output</h2></div><span className={`badge ${assessment.status}`}>{assessment.status}</span></div><dl><div><dt>Created by</dt><dd>{assessment.createdBy}</dd></div><div><dt>Questions</dt><dd>{assessment.questions.length}</dd></div><div><dt>Total points</dt><dd>{assessment.questions.reduce((total, question) => total + question.points, 0)}</dd></div><div><dt>Created</dt><dd>{new Date(assessment.createdAt).toLocaleString()}</dd></div></dl></div>
    {assessment.questions.map((q, i) => <article className="card question" key={q.id}><div className="question-title"><b>Question {i + 1}</b><span>{q.difficulty} · {q.points} points</span></div><h3>{q.prompt}</h3><p className="muted">{q.learningObjective} · {q.questionType}</p>{q.options.map(o => <div className={o.id === q.correctOptionId ? 'option correct' : 'option'} key={o.id}>{o.id}. {o.text}{o.id === q.correctOptionId && <b> ✓ Correct</b>}</div>)}</article>)}
  </section>
}

function toAssessmentSummary(assessment: Assessment): AssessmentSummary {
  return {
    id: assessment.id,
    specificationId: assessment.specificationId,
    specificationVersion: assessment.specificationVersion,
    status: assessment.status,
    questionCount: assessment.questions.length,
    totalPoints: assessment.questions.reduce((total, question) => total + question.points, 0),
    createdBy: assessment.createdBy,
    createdAt: assessment.createdAt,
  }
}

function toCreate(f: Form) {
  return { id: f.id || undefined, version: f.version, title: f.title, subject: f.subject, learningObjective: f.learningObjective, questionRules: { totalQuestions: f.totalQuestions, questionType: f.questionType, optionsPerQuestion: f.optionsPerQuestion }, difficultyDistribution: { easy: f.easy, medium: f.medium, hard: f.hard }, scoringRules: { pointsPerQuestion: f.pointsPerQuestion, totalPoints: f.totalPoints } }
}

function toUpdate(f: Form) {
  const { id: _id, version: _version, ...body } = toCreate(f)
  return body
}

function fromSpec(s: Spec, set: (f: Form) => void) {
  set({ id: s.id, version: s.version, title: s.title, subject: s.subject, learningObjective: s.learningObjective, totalQuestions: s.questionRules.totalQuestions, questionType: s.questionRules.questionType, optionsPerQuestion: s.questionRules.optionsPerQuestion, easy: s.difficultyDistribution.easy, medium: s.difficultyDistribution.medium, hard: s.difficultyDistribution.hard, pointsPerQuestion: s.scoringRules.pointsPerQuestion, totalPoints: s.scoringRules.totalPoints })
}

export default App
