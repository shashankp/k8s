import './App.css'
import { Routes, Route } from 'react-router-dom'
import { useState } from 'react'

function BrokenComponent() {
  throw new Error('Rendering error test');
  return <div>This won't render</div>;
}

function Home() {
  const [count, setCount] = useState(0)
  const [showBroken, setShowBroken] = useState(false);

  const testError = () => {
    throw new Error("testError");
  }

  const testFetch = () => {
    fetch('https://jsonplaceholder.typicode.com/posts/1')
      .then(res => console.log('Response:', res.status))
  }

  return (
    <>
      <h1>Home</h1>
      <button onClick={() => setCount(count + 1)}>count is {count}</button>
      <button onClick={testFetch}>Test Fetch</button>
      <button onClick={testError}>Test Error</button>
      
      <button onClick={() => setShowBroken(true)}>Trigger Render Error</button>
      {showBroken && <BrokenComponent />}
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