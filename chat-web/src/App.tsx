import { Navigate, Route, Routes } from 'react-router-dom'
import { RotaProtegida } from './auth/RotaProtegida'
import { Layout } from './components/Layout'
import { Cadastro } from './pages/Cadastro'
import { Home } from './pages/Home'
import { Login } from './pages/Login'

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<Login />} />
      <Route path="/cadastro" element={<Cadastro />} />
      <Route
        path="/"
        element={
          <RotaProtegida>
            <Layout>
              <Home />
            </Layout>
          </RotaProtegida>
        }
      />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
