import { Navigate, Route, Routes } from "react-router-dom";
import { AuthProvider, useAuth } from "./auth/AuthContext";
import Login from "./pages/Login";
import Register from "./pages/Register";
import Dashboard from "./pages/Dashboard";
import Team from "./pages/Team";
import Accept from "./pages/Accept";
import Explore from "./pages/Explore";
import Dashboards from "./pages/Dashboards";
import DashboardView from "./pages/DashboardView";
import Billing from "./pages/Billing";
import type { JSX } from "react";
import "./App.css";

function RequireAuth({ children }: { children: JSX.Element }) {
  const { isAuthenticated } = useAuth();
  return isAuthenticated ? children : <Navigate to="/login" replace />;
}

export default function App() {
  return (
    <AuthProvider>
      <Routes>
        <Route path="/login" element={<Login />} />
        <Route path="/register" element={<Register />} />
        <Route path="/accept" element={<Accept />} />
        <Route
          path="/"
          element={
            <RequireAuth>
              <Dashboard />
            </RequireAuth>
          }
        />
        <Route
          path="/team"
          element={
            <RequireAuth>
              <Team />
            </RequireAuth>
          }
        />
        <Route
          path="/datasets/:id/explore"
          element={
            <RequireAuth>
              <Explore />
            </RequireAuth>
          }
        />
        <Route
          path="/dashboards"
          element={
            <RequireAuth>
              <Dashboards />
            </RequireAuth>
          }
        />
        <Route
          path="/dashboards/:id"
          element={
            <RequireAuth>
              <DashboardView />
            </RequireAuth>
          }
        />
        <Route
          path="/billing"
          element={
            <RequireAuth>
              <Billing />
            </RequireAuth>
          }
        />
      </Routes>
    </AuthProvider>
  );
}
