import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import { DashboardShell } from '@/layouts/DashboardShell'
import { Overview } from '@/app/routes/Overview'
import { Placeholder } from '@/app/routes/Placeholder'
import { NAV_ITEMS } from '@/app/nav'

const BUILT_PATHS = new Set(['/overview'])

export function AppRoutes() {
  return (
    <Routes>
      <Route element={<DashboardShell />}>
        <Route path="/" element={<Navigate to="/overview" replace />} />
        <Route path="/overview" element={<Overview />} />
        {NAV_ITEMS.filter((item) => !BUILT_PATHS.has(item.to)).map((item) => (
          <Route
            key={item.key}
            path={item.to}
            element={<Placeholder labelKey={item.labelKey} />}
          />
        ))}
      </Route>
    </Routes>
  )
}

export default function App() {
  return (
    <BrowserRouter>
      <AppRoutes />
    </BrowserRouter>
  )
}
