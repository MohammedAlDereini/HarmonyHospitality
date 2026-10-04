import { Navigate, Route, Routes } from 'react-router-dom'
import { hasSession } from './session'
import LoginPage from './pages/LoginPage'
import ForgotPasswordPage from './pages/ForgotPasswordPage'
import SetPasswordPage from './pages/SetPasswordPage'
import ChangePasswordPage from './pages/ChangePasswordPage'
import HomePage from './pages/HomePage'

/** A page that needs a signed-in person: without a session, go to the login page. */
function Private({ children }: { children: React.ReactNode }) {
  return hasSession() ? <>{children}</> : <Navigate to="/login" replace />
}

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/forgot-password" element={<ForgotPasswordPage />} />
      {/* The two links Identity mails out: same page, different wording. */}
      <Route path="/account/reset-password" element={<SetPasswordPage mode="reset" />} />
      <Route path="/account/set-password" element={<SetPasswordPage mode="invite" />} />
      <Route path="/account/change-password" element={<Private><ChangePasswordPage /></Private>} />
      <Route path="/" element={<Private><HomePage /></Private>} />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
