import { ChartLine, House, Info, List, Moon, Sun, Users } from 'lucide-react'
import { Component, useEffect, useState, type ReactNode } from 'react'
import { NavLink, Outlet } from 'react-router'
import { useQuery } from '@tanstack/react-query'
import { getCurrentUser } from '../api/client.ts'
import { Button } from '@/components/ui/button'
import { TravellerProvider, useTravellers } from './travellers.tsx'

const themeKey = 'residencyroll_theme'

const links = [
  { to: '/', label: 'Home', icon: House },
  { to: '/manage-trips', label: 'Manage Trips', icon: List },
  { to: '/forecast', label: 'Forecast', icon: ChartLine },
  { to: '/travellers', label: 'Travellers', icon: Users },
  { to: '/system-info', label: 'System Info', icon: Info },
]

export function PageTitle({ title }: { title: string }) {
  useEffect(() => {
    document.title = title
  }, [title])
  return null
}

export function AppShell() {
  return (
    <TravellerProvider>
      <ShellFrame />
    </TravellerProvider>
  )
}

function ShellFrame() {
  const user = useQuery({ queryKey: ['bff', 'user'], queryFn: getCurrentUser })
  const { travellers, selected, select } = useTravellers()
  const [dark, setDark] = useState(() => window.localStorage.getItem(themeKey) === 'dark')

  useEffect(() => {
    document.documentElement.classList.toggle('dark', dark)
    window.localStorage.setItem(themeKey, dark ? 'dark' : 'light')
  }, [dark])

  return (
    <div className="flex min-h-screen bg-muted/40 text-foreground">
      <aside className="flex w-60 shrink-0 flex-col border-r bg-sidebar text-sidebar-foreground">
        <div className="flex h-14 items-center gap-3 border-b px-4">
          <span className="grid size-8 shrink-0 place-items-center rounded-md bg-primary text-sm font-semibold text-primary-foreground">RR</span>
          <p className="text-sm font-semibold leading-tight">Residency Roll</p>
          <span className="sr-only">Residency Roll - Residency Status Dashboard</span>
        </div>
        <nav className="flex flex-1 flex-col gap-1 p-3">
          {links.map((link) => (
            <NavLink
              key={link.to}
              to={link.to}
              end={link.to === '/'}
              className={({ isActive }) =>
                `flex items-center gap-2 rounded-lg px-3 py-2 text-sm font-medium ${
                  isActive ? 'bg-primary text-primary-foreground' : 'text-sidebar-foreground/80 hover:bg-sidebar-accent hover:text-sidebar-accent-foreground'
                }`
              }
            >
              <link.icon className="size-4" />
              {link.label}
            </NavLink>
          ))}
        </nav>
      </aside>
      <div className="flex min-w-0 flex-1 flex-col">
        <header className="flex h-14 items-center justify-end gap-3 border-b bg-sidebar px-5">
          {travellers.length > 0 ? (
            <label className="flex items-center gap-2 text-sm">
              <span className="text-muted-foreground">Traveller</span>
              <select
                aria-label="Traveller"
                className="h-8 rounded-lg border border-input bg-background px-2 text-sm"
                value={selected?.id ?? ''}
                onChange={(event) => select(Number(event.target.value))}
              >
                {travellers.map((traveller) => (
                  <option key={traveller.id} value={traveller.id}>
                    {traveller.name}
                  </option>
                ))}
              </select>
            </label>
          ) : null}
          <span className="text-sm text-muted-foreground">{user.data?.name}</span>
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
        <main className="min-w-0 flex-1 overflow-auto">
          <div className="w-full max-w-6xl px-5 py-6">
            <ErrorBoundary>
              <Outlet />
            </ErrorBoundary>
          </div>
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
        <section className="rounded-lg border bg-card p-6">
          <h1 className="text-xl font-semibold">Something went wrong</h1>
          <p className="mt-2 text-sm text-muted-foreground">Reload the page and try again.</p>
        </section>
      )
    }

    return this.props.children
  }
}
