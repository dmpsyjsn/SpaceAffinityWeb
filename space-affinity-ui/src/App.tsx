import { useState, useEffect } from 'react'
import './App.css'

type SpaceNote = { id: number; description: string; pictureUrl: string }

function App() {
  const [spaceNotes, setSpaceNotes] = useState<SpaceNote[]>([]);

  useEffect(() => {
    fetch('/api/spacenotes')
      .then((res) => res.json())
      .then(setSpaceNotes)
  }, []);

  // React 19 form action: receives the form data and resets the form afterwards
  async function addSpaceNote(formData: FormData) {
    const res = await fetch('/api/spacenotes', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ description: formData.get('description'), pictureUrl: formData.get('pictureurl')  }),
    })
    const created: SpaceNote = await res.json()
    setSpaceNotes((prev) => [...prev, created])
  }

  return (
    <main>
      <h1>Space Affinity</h1>
      <form className="note-form" action={addSpaceNote}>
        <input name="description" placeholder="New note" required />
        <input name="pictureurl" type="url" placeholder="Picture URL (http...)" required />
        <button type="submit">Add</button>
      </form>
      {spaceNotes.map((spaceNote) => (
        <section key={spaceNote.id}>
          <p>{spaceNote.description}</p>
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
