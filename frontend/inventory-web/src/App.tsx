import { NavLink, Route, Routes } from 'react-router-dom'
import Alerts from './pages/Alerts'
import Batches from './pages/Batches'
import Movements from './pages/Movements'
import Products from './pages/Products'
import Receive from './pages/Receive'
import StockLevels from './pages/StockLevels'
import Warehouses from './pages/Warehouses'

const links = [
  ['/', 'Alerts'], ['/products', 'Products'], ['/warehouses', 'Warehouses'], ['/receive', 'Receive'],
  ['/batches', 'Batches'], ['/stock-levels', 'Stock levels'], ['/movements', 'Movements'],
] as const

export default function App() {
  return (
    <>
      <header className="top">
        <strong>Inventory</strong>
        <nav aria-label="Main">
          {links.map(([to, label]) => <NavLink key={to} to={to} end={to === '/'}>{label}</NavLink>)}
        </nav>
      </header>
      <main>
        <Routes>
          <Route path="/" element={<Alerts />} />
          <Route path="/products" element={<Products />} />
          <Route path="/warehouses" element={<Warehouses />} />
          <Route path="/receive" element={<Receive />} />
          <Route path="/batches" element={<Batches />} />
          <Route path="/stock-levels" element={<StockLevels />} />
          <Route path="/movements" element={<Movements />} />
          <Route path="*" element={<p>Page not found.</p>} />
        </Routes>
      </main>
    </>
  )
}
