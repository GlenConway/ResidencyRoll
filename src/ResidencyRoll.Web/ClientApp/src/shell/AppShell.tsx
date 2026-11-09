import { Asleep, Awake, ChartLine, Home, Information, List } from '@carbon/icons-react'
import { Content, Header, HeaderGlobalAction, HeaderGlobalBar, HeaderName, SideNav, SideNavItems, SideNavLink, Theme } from '@carbon/react'
import { useQuery } from '@tanstack/react-query'
import { Component, useEffect, useState, type ReactNode } from 'react'
import { NavLink, Outlet, useLocation } from 'react-router'
import { getCurrentUser } from '../api/client.ts'

const themeKey = 'residencyroll_theme'

export function AppShell() {
  const location = useLocation()
  const [dark, setDark] = useState(() => window.localStorage.getItem(themeKey) === 'dark')
  const userQuery = useQuery({ queryKey: ['bff', 'user'], queryFn: getCurrentUser, retry: false })

  useEffect(() => {
    window.localStorage.setItem(themeKey, dark ? 'dark' : 'light')
  }, [dark])

  return (
    <Theme theme={dark ? 'g100' : 'g10'} className="rr-theme">
      <Header aria-label="Residency Roll">
        <HeaderName href="/" prefix="">
          Residency Roll - Residency Status Dashboard
        </HeaderName>
        <HeaderGlobalBar>
          <span className="rr-user-name">{userQuery.data?.name}</span>
          <HeaderGlobalAction
            aria-label={dark ? 'Switch to light mode' : 'Switch to dark mode'}
            tooltipAlignment="end"
            onClick={() => setDark((value) => !value)}
          >
            {dark ? <Awake /> : <Asleep />}
          </HeaderGlobalAction>
          <form method="post" action="/auth/logout">
            <input type="hidden" name="X-CSRF" value="1" />
            <button type="submit" className="rr-logout">
              Logout
            </button>
          </form>
        </HeaderGlobalBar>
      </Header>
      <SideNav expanded isFixedNav isChildOfHeader={false} aria-label="Side navigation">
        <SideNavItems>
          <SideNavLink as={NavLink} to="/" isActive={location.pathname === '/'} renderIcon={Home}>
            Home
          </SideNavLink>
          <SideNavLink as={NavLink} to="/manage-trips" isActive={location.pathname === '/manage-trips'} renderIcon={List}>
            Manage Trips
          </SideNavLink>
          <SideNavLink as={NavLink} to="/forecast" isActive={location.pathname === '/forecast'} renderIcon={ChartLine}>
            Forecast
          </SideNavLink>
          <SideNavLink as={NavLink} to="/system-info" isActive={location.pathname === '/system-info'} renderIcon={Information}>
            System Info
          </SideNavLink>
        </SideNavItems>
      </SideNav>
      <Content className="rr-content">
        <ErrorBoundary>
          <Outlet />
        </ErrorBoundary>
      </Content>
    </Theme>
  )
}

export function PageTitle({ title }: { title: string }) {
  useEffect(() => {
    document.title = title
  }, [title])
  return null
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
          <h1>Something went wrong</h1>
          <p>Reload the page and try again.</p>
        </section>
      )
    }

    return this.props.children
  }
}
