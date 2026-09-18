import { useState } from 'react'
import './App.css'

// Matches the http launch profile in FindThatBook.Api/Properties/launchSettings.json.
const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5216'

export default function App() {
  const [query, setQuery] = useState('')
  const [status, setStatus] = useState('idle')
  const [data, setData] = useState(null)
  const [error, setError] = useState('')

  const isLoading = status === 'loading'

  async function handleSubmit(event) {
    event.preventDefault()

    const trimmed = query.trim()
    if (trimmed === '' || isLoading) {
      return
    }

    setStatus('loading')
    setError('')

    try {
      const response = await fetch(`${API_BASE_URL}/api/search`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ query: trimmed }),
      })

      if (!response.ok) {
        throw new Error(`The search failed (HTTP ${response.status}).`)
      }

      setData(await response.json())
      setStatus('ready')
    } catch (caught) {
      // A network-level failure gives an opaque "Failed to fetch".
      setError(
        caught instanceof TypeError
          ? 'Could not reach the search API. Is it running?'
          : caught.message,
      )
      setStatus('error')
    }
  }

  return (
    <main className="page">
      <h1>Find That Book</h1>
      <p className="tagline">Describe the book however you remember it.</p>

      <form className="search" onSubmit={handleSubmit}>
        <input
          type="text"
          value={query}
          onChange={(event) => setQuery(event.target.value)}
          placeholder="tolkien hobbit illustrated deluxe 1937"
          aria-label="Book search"
        />
        <button type="submit" disabled={isLoading || query.trim() === ''}>
          {isLoading ? 'Searching…' : 'Search'}
        </button>
      </form>

      {status === 'idle' && (
        <p className="notice">
          Try a title, an author, a half-remembered phrase, or all three at once.
        </p>
      )}

      {isLoading && <p className="notice">Searching…</p>}

      {status === 'error' && <p className="notice error">{error}</p>}

      {status === 'ready' && data && <Results data={data} />}
    </main>
  )
}

function Results({ data }) {
  const { interpretation, results } = data

  return (
    <section>
      <Interpretation interpretation={interpretation} />

      {results.length === 0 ? (
        <p className="notice">No matches found. Try describing the book differently.</p>
      ) : (
        <ul className="cards">
          {results.map((book) => (
            <BookCard key={book.openLibraryUrl} book={book} />
          ))}
        </ul>
      )}
    </section>
  )
}

function Interpretation({ interpretation }) {
  if (interpretation.isFallback) {
    return (
      <p className="interpretation fallback">
        {"Couldn't parse the query, so this is a general text search."}
      </p>
    )
  }

  const parts = []

  if (interpretation.title) {
    parts.push(`title: ${interpretation.title}`)
  }
  if (interpretation.author) {
    parts.push(`author: ${interpretation.author}`)
  }
  if (interpretation.keywords.length > 0) {
    parts.push(`keywords: ${interpretation.keywords.join(', ')}`)
  }

  if (parts.length === 0) {
    return null
  }

  return <p className="interpretation">Interpreted as — {parts.join(' · ')}</p>
}

function BookCard({ book }) {
  // Covers 404 often enough that a missing image needs handling too.
  const [coverFailed, setCoverFailed] = useState(false)
  const showCover = book.coverUrl && !coverFailed

  return (
    <li className="card">
      {showCover ? (
        <img
          className="cover"
          src={book.coverUrl}
          alt={`Cover of ${book.title}`}
          loading="lazy"
          onError={() => setCoverFailed(true)}
        />
      ) : (
        <div className="cover cover-missing">No cover</div>
      )}

      <div className="details">
        <h2>{book.title}</h2>

        {book.primaryAuthor && <p className="author">{book.primaryAuthor}</p>}

        {book.contributors.length > 0 && (
          <p className="contributors">with {book.contributors.join(', ')}</p>
        )}

        <p className="meta">
          {book.firstPublishYear ?? 'Year unknown'}
          {' · '}
          {book.editionCount} {book.editionCount === 1 ? 'edition' : 'editions'}
        </p>

        {book.explanation && <p className="explanation">{book.explanation}</p>}

        <a href={book.openLibraryUrl} target="_blank" rel="noreferrer">
          View on Open Library
        </a>
      </div>
    </li>
  )
}
