import { Route, Routes } from 'react-router'
import { HomePage } from './pages/HomePage.tsx'
import { ForecastPage } from './pages/ForecastPage.tsx'
import { SystemInfoPage } from './pages/SystemInfoPage.tsx'
import { ManageTripsPage } from './pages/ManageTripsPage.tsx'
import { NotFoundPage } from './pages/NotFoundPage.tsx'
import { AppShell, PageTitle } from './shell/AppShell.tsx'

export default function App() {
  return (
    <Routes>
      <Route element={<AppShell />}>
        <Route
          index
          element={
            <>
              <PageTitle title="Residency Roll - Travel Tracker" />
              <HomePage />
            </>
          }
        />
        <Route
          path="manage-trips"
          element={
            <>
              <PageTitle title="Manage Trips" />
              <ManageTripsPage />
            </>
          }
        />
        <Route
          path="forecast"
          element={
            <>
              <PageTitle title="Forecast - Residency Roll" />
              <ForecastPage />
            </>
          }
        />
        <Route
          path="system-info"
          element={
            <>
              <PageTitle title="System Info - Residency Roll" />
              <SystemInfoPage />
            </>
          }
        />
        <Route
          path="*"
          element={
            <>
              <PageTitle title="Not Found - Residency Roll" />
              <NotFoundPage />
            </>
          }
        />
      </Route>
    </Routes>
  )
}
