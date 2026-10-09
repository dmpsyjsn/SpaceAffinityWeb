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

  return (
    <main>
      <h1>Space Affinity</h1>
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
