import { createContext, useContext, useState, type ReactNode } from "react";
import { auth, tokens, type AuthResponse } from "../api/client";

interface AuthState {
  organizationId: string | null;
  role: string | null;
}

interface AuthContextValue {
  isAuthenticated: boolean;
  state: AuthState | null;
  login: (email: string, password: string) => Promise<void>;
  register: (
    email: string,
    password: string,
    fullName: string,
    organizationName: string,
  ) => Promise<void>;
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

function toState(r: AuthResponse): AuthState {
  return { organizationId: r.organizationId, role: r.role };
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<AuthState | null>(
    tokens.access ? { organizationId: null, role: null } : null,
  );

  const login = async (email: string, password: string) => {
    const r = await auth.login({ email, password });
    tokens.set(r);
    setState(toState(r));
  };

  const register = async (
    email: string,
    password: string,
    fullName: string,
    organizationName: string,
  ) => {
    const r = await auth.register({ email, password, fullName, organizationName });
    tokens.set(r);
    setState(toState(r));
  };

  const logout = () => {
    tokens.clear();
    setState(null);
  };

  return (
    <AuthContext.Provider
      value={{ isAuthenticated: state !== null, state, login, register, logout }}
    >
      {children}
    </AuthContext.Provider>
  );
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within AuthProvider");
  return ctx;
}
