import { createContext, useContext, useState, type ReactNode } from "react";
import {
  auth,
  orgs,
  tokens,
  decodeToken,
  type AuthResponse,
} from "../api/client";

interface AuthState {
  userId: string;
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
  switchOrg: (orgId: string) => Promise<void>;
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

/** Derive the working state from whatever access token is currently stored. */
function stateFromToken(): AuthState | null {
  const claims = decodeToken(tokens.access);
  if (!claims) return null;
  return {
    userId: claims.sub,
    organizationId: claims.org_id ?? null,
    role: claims.org_role ?? null,
  };
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<AuthState | null>(stateFromToken);

  const apply = (r: AuthResponse) => {
    tokens.set(r);
    setState(stateFromToken());
  };

  const login = async (email: string, password: string) =>
    apply(await auth.login({ email, password }));

  const register = async (
    email: string,
    password: string,
    fullName: string,
    organizationName: string,
  ) => apply(await auth.register({ email, password, fullName, organizationName }));

  const switchOrg = async (orgId: string) => apply(await orgs.switch(orgId));

  const logout = () => {
    tokens.clear();
    setState(null);
  };

  return (
    <AuthContext.Provider
      value={{
        isAuthenticated: state !== null,
        state,
        login,
        register,
        switchOrg,
        logout,
      }}
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
