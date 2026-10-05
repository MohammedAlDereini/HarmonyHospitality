import { Navigate, Outlet, Route, Routes } from 'react-router-dom'
import { hasSession } from './session'
import Shell from './components/Shell'
import LoginPage from './pages/LoginPage'
import ForgotPasswordPage from './pages/ForgotPasswordPage'
import SetPasswordPage from './pages/SetPasswordPage'
import ChangePasswordPage from './pages/ChangePasswordPage'
import HomePage from './pages/HomePage'
import SecurityPage from './pages/SecurityPage'
import ReferenceTablePage from './pages/reference/ReferenceTablePage'

/** A page that needs a signed-in person: without a session, go to the login page. */
function Private({ children }: { children: React.ReactNode }) {
  return hasSession() ? <>{children}</> : <Navigate to="/login" replace />
}

/** The sign-in family renders one card in the middle of the screen, outside the shell. */
function Centered() {
  return (
    <div className="centered">
      <Outlet />
    </div>
  )
}

export default function App() {
  return (
    <Routes>
      <Route element={<Centered />}>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/forgot-password" element={<ForgotPasswordPage />} />
        {/* The two links Identity mails out: same page, different wording. */}
        <Route path="/account/reset-password" element={<SetPasswordPage mode="reset" />} />
        <Route path="/account/set-password" element={<SetPasswordPage mode="invite" />} />
        {/* A forced change (first sign-in) must not see the rest of the app, so this stays outside the shell. */}
        <Route path="/account/change-password" element={<Private><ChangePasswordPage /></Private>} />
      </Route>
      <Route element={<Private><Shell /></Private>}>
        <Route path="/" element={<HomePage />} />
        <Route path="/reference/:slug" element={<ReferenceTablePage />} />
        <Route path="/account/security" element={<SecurityPage />} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
