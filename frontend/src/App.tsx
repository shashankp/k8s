import './App.css'
import { Routes, Route } from 'react-router-dom'
import { useState } from 'react'

type WeatherForecast = {
  date: string
  temperatureC: number
  temperatureF: number
  summary: string | null
}

function BrokenComponent() {
  throw new Error('Rendering error test');
  return <div>This won't render</div>;
}

function Home() {
  const [count, setCount] = useState(0)
  const [showBroken, setShowBroken] = useState(false)
  const [data, setData] = useState<WeatherForecast[] | null>(null)
  const [loading, setLoading] = useState<boolean>(false)
  const [error, setError] = useState<string | null>(null)

  const onClickHandler = async (): Promise<void> => {
    setCount((c) => c + 1)
    setLoading(true)
    setError(null)

    try {
      const baseUrl = import.meta.env.VITE_API_BASE_URL
      if (!baseUrl) {
        throw new Error('VITE_API_BASE_URL is not defined')
      }

      const res = await fetch(`${baseUrl}/weatherforecast`)

      if (!res.ok) {
        throw new Error(`HTTP ${res.status}`)
      }

      const json: WeatherForecast[] = await res.json()
      setData(json)
    } catch (err) {
      console.error(err)
      setError(err instanceof Error ? err.message : 'Unknown error')
    } finally {
      setLoading(false)
    }
  }

  const testError = () => {
    throw new Error("testError");
  }

  const testFetch = () => {
    fetch('https://jsonplaceholder.typicode.com/posts/1')
      .then(res => console.log('Response:', res.status))
  }

  return (
    <>
      <h1>Weather</h1>
      
      <div className="card">
        <button onClick={() => setCount(count + 1)}>count is {count}</button>
        <button onClick={testFetch}>Test Fetch</button>
        <button onClick={testError}>Test Error</button>
        <button onClick={() => setShowBroken(true)}>Trigger Render Error</button>
        {showBroken && <BrokenComponent />}
        <button onClick={onClickHandler} disabled={loading}>
          {loading ? 'Loading…' : `Fetch weather`}
        </button>
      </div>

      {error && <p style={{ color: 'red' }}>Error: {error}</p>}

      {data && (
        <table>
          <thead>
            <tr>
              <th>Date</th>
              <th>°C</th>
              <th>°F</th>
              <th>Summary</th>
            </tr>
          </thead>
          <tbody>
            {data.map((w) => (
              <tr key={w.date}>
                <td>{w.date}</td>
                <td>{w.temperatureC}</td>
                <td>{w.temperatureF}</td>
                <td>{w.summary}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  )
}

function NotFound() {
  return <h1>404 - Page Not Found</h1>
}

function App() {
  return (
    <Routes>
      <Route path="/" element={<Home />} />
      <Route path="*" element={<NotFound />} />
    </Routes>
  )
}

export default App