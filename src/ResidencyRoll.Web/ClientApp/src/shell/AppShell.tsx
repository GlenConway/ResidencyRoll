import { ChartLine, House, Info, List, Moon, Sun } from 'lucide-react'
import { Component, useEffect, useState, type ReactNode } from 'react'

export function PageTitle({ title }: { title: string }) {
  useEffect(() => {
    document.title = title
  }, [title])
  return null
}
import { NavLink, Outlet } from 'react-router'
import { useQuery } from '@tanstack/react-query'
import { getCurrentUser } from '../api/client.ts'
import { Button } from '@/components/ui/button'

const themeKey = 'residencyroll_theme'

const links = [
  { to: '/', label: 'Home', icon: House },
  { to: '/manage-trips', label: 'Manage Trips', icon: List },
  { to: '/forecast', label: 'Forecast', icon: ChartLine },
  { to: '/system-info', label: 'System Info', icon: Info },
]

export function AppShell() {
  const user = useQuery({ queryKey: ['bff', 'user'], queryFn: getCurrentUser })
  const [dark, setDark] = useState(() => window.localStorage.getItem(themeKey) === 'dark')

  useEffect(() => {
    document.documentElement.classList.toggle('dark', dark)
    window.localStorage.setItem(themeKey, dark ? 'dark' : 'light')
  }, [dark])

  return (
    <div className="flex min-h-screen bg-background text-foreground">
      <aside className="flex w-64 shrink-0 flex-col border-r bg-sidebar text-sidebar-foreground">
        <div className="px-5 py-6">
          <p className="text-sm font-semibold tracking-tight">Residency Roll</p>
          <p className="text-xs text-muted-foreground">Residency Status Dashboard</p>
          <span className="sr-only">Residency Roll - Residency Status Dashboard</span>
        </div>
        <nav className="flex flex-1 flex-col gap-1 px-3">
          {links.map((link) => (
            <NavLink
              key={link.to}
              to={link.to}
              end={link.to === '/'}
              className={({ isActive }) =>
                `flex items-center gap-2 rounded-lg px-3 py-2 text-sm ${isActive ? 'bg-sidebar-accent text-sidebar-accent-foreground' : 'hover:bg-sidebar-accent/60'}`
              }
            >
              <link.icon className="size-4" />
              {link.label}
            </NavLink>
          ))}
        </nav>
      </aside>
      <div className="flex min-w-0 flex-1 flex-col">
        <header className="flex h-14 items-center justify-end gap-3 border-b px-4">
          <span className="text-sm">{user.data?.name}</span>
          <Button variant="ghost" size="icon" aria-label={dark ? 'Switch to light theme' : 'Switch to dark theme'} onClick={() => setDark((value) => !value)}>
            {dark ? <Sun /> : <Moon />}
          </Button>
          <form method="post" action="/auth/logout">
            <input type="hidden" name="X-CSRF" value="1" />
            <Button type="submit" variant="outline" size="sm">
              Logout
            </Button>
          </form>
        </header>
        <main className="flex-1 p-6">
          <ErrorBoundary>
            <Outlet />
          </ErrorBoundary>
        </main>
      </div>
    </div>
  )
}

class ErrorBoundary extends Component<{ children: ReactNode }, { failed: boolean }> {
  state = { failed: false }

  static getDerivedStateFromError(): { failed: boolean } {
    return { failed: true }
  }

  render() {
    if (this.state.failed) {
      return (
        <section>
          <h1 className="text-xl font-semibold">Something went wrong</h1>
          <p className="mt-2 text-sm text-muted-foreground">Reload the page and try again.</p>
        </section>
      )
    }

    return this.props.children
  }
}
