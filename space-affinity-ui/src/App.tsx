import { useState, useEffect } from 'react'
import './App.css'

type SpaceNote = { id: number; description: string; pictureUrl: string }

function App() {
  const [spaceNotes, setSpaceNotes] = useState<SpaceNote[]>([]);
  const [editingId, setEditingId] = useState<number | null>(null);

  useEffect(() => {
    fetch('/api/spacenotes')
      .then((res) => res.json())
      .then(setSpaceNotes)
  }, []);

  // Same POST endpoint as adding; sending the id makes it an edit
  async function editSpaceNote(id: number, formData: FormData) {
    const res = await fetch('/api/spacenotes', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ id, description: formData.get('description'), pictureUrl: formData.get('pictureurl') }),
    })
    if (!res.ok) return
    const saved: SpaceNote = await res.json()
    setSpaceNotes((prev) => prev.map((note) => (note.id === saved.id ? saved : note)))
    setEditingId(null)
  }

  return (
    <main>
      <h1>Space Notes</h1>
      {spaceNotes.map((spaceNote) => (
        <section key={spaceNote.id}>
          {editingId === spaceNote.id ? (
            <form className="note-form" action={(formData) => editSpaceNote(spaceNote.id, formData)}>
              <input name="description" defaultValue={spaceNote.description} placeholder="Description" required />
              <input name="pictureurl" type="url" defaultValue={spaceNote.pictureUrl} placeholder="Picture URL (http...)" required />
              <button type="submit">Save</button>
              <button type="button" onClick={() => setEditingId(null)}>Cancel</button>
            </form>
          ) : (
            <>
              <p>{spaceNote.description}</p>
              <button type="button" onClick={() => setEditingId(spaceNote.id)}>Edit</button>
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
