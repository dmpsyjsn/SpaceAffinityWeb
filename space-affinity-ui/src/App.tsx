import { useState, useEffect } from 'react'
import './App.css'

type SpaceNote = { id: number; description: string; pictureUrl: string }
type Session = { token: string; expiresAt: string }

// Kept in sessionStorage so a refresh doesn't log you out; ignored once the token has expired
function loadSession(): Session | null {
  try {
    const session: Session | null = JSON.parse(sessionStorage.getItem('session') ?? 'null')
    return session && new Date(session.expiresAt) > new Date() ? session : null
  } catch {
    return null
  }
}

function App() {
  const [spaceNotes, setSpaceNotes] = useState<SpaceNote[]>([]);
  const [session, setSession] = useState<Session | null>(loadSession);
  const [showLogin, setShowLogin] = useState(false);
  const [loginError, setLoginError] = useState('');
  const [editingId, setEditingId] = useState<number | null>(null);

  useEffect(() => {
    fetch('/api/spacenotes')
      .then((res) => res.json())
      .then(setSpaceNotes)
  }, []);

  async function login(formData: FormData) {
    const res = await fetch('/api/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ username: formData.get('username'), password: formData.get('password') }),
    })
    if (!res.ok) {
      setLoginError(res.status === 401 ? 'Wrong username or password.' : 'Login failed, please try again.')
      return
    }
    const newSession: Session = await res.json()
    sessionStorage.setItem('session', JSON.stringify(newSession))
    setSession(newSession)
    setShowLogin(false)
    setLoginError('')
  }

  function logout() {
    sessionStorage.removeItem('session')
    setSession(null)
    setEditingId(null)
  }

  // Same POST endpoint for both; sending the id makes it an edit
  async function saveSpaceNote(formData: FormData, id?: number) {
    const res = await fetch('/api/spacenotes', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${session?.token}` },
      body: JSON.stringify({ id, description: formData.get('description'), pictureUrl: formData.get('pictureurl') }),
    })
    if (res.status === 401) {
      // The token expired or was rejected, so ask for a fresh login
      logout()
      setShowLogin(true)
      return
    }
    if (!res.ok) return
    const saved: SpaceNote = await res.json()
    setSpaceNotes((prev) => (id ? prev.map((note) => (note.id === saved.id ? saved : note)) : [...prev, saved]))
    setEditingId(null)
  }

  return (
    <main>
      <h1>Space Affinity</h1>
      {session ? (
        <>
          <button type="button" onClick={logout}>Log out</button>
          <form className="note-form" action={(formData) => saveSpaceNote(formData)}>
            <input name="description" placeholder="New note" required />
            <input name="pictureurl" type="url" placeholder="Picture URL (http...)" required />
            <button type="submit">Add</button>
          </form>
        </>
      ) : showLogin ? (
        <form className="note-form" action={login}>
          <input name="username" placeholder="Username" autoComplete="username" required autoFocus />
          <input name="password" type="password" placeholder="Password" autoComplete="current-password" required />
          <button type="submit">Log in</button>
          <button type="button" onClick={() => { setShowLogin(false); setLoginError('') }}>Cancel</button>
          {loginError && <p className="form-error" role="alert">{loginError}</p>}
        </form>
      ) : (
        <button type="button" onClick={() => setShowLogin(true)}>Log in</button>
      )}
      {spaceNotes.map((spaceNote) => (
        <section key={spaceNote.id}>
          {session && editingId === spaceNote.id ? (
            <form className="note-form" action={(formData) => saveSpaceNote(formData, spaceNote.id)}>
              <input name="description" defaultValue={spaceNote.description} placeholder="Description" required />
              <input name="pictureurl" type="url" defaultValue={spaceNote.pictureUrl} placeholder="Picture URL (http...)" required />
              <button type="submit">Save</button>
              <button type="button" onClick={() => setEditingId(null)}>Cancel</button>
            </form>
          ) : (
            <>
              <p>{spaceNote.description}</p>
              {session && <button type="button" onClick={() => setEditingId(spaceNote.id)}>Edit</button>}
            </>
          )}
          {spaceNote.pictureUrl && (
            <>
              <img src={spaceNote.pictureUrl} referrerPolicy="no-referrer" alt={spaceNote.description} style={{ maxWidth: '100%' }} />
              <p>
                <a href={spaceNote.pictureUrl} target="_blank" rel="noreferrer">
                  {spaceNote.pictureUrl}
                </a>
              </p>
            </>
          )}
        </section>
      ))}
    </main>
  )
}

export default App
