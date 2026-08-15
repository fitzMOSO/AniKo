import { Outlet } from 'react-router-dom'
import { Sidebar } from './Sidebar'
import { Header } from './Header'

export function DashboardShell() {
  return (
    <div className="flex min-h-screen">
      <div className="hidden lg:block">
        <Sidebar />
      </div>
      <div className="flex min-w-0 flex-1 flex-col">
        <Header />
        <main className="grid grid-cols-1 gap-6 px-6 pb-10 md:grid-cols-6 lg:grid-cols-12">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
