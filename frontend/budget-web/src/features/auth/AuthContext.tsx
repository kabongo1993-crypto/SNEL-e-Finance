import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import {
  bootstrapAdminRequest,
  fetchAuthMe,
  getStoredAccessToken,
  getStoredAuthUser,
  loginRequest,
  setStoredAuth,
  type AuthUser,
  type LoginResponse,
} from '../../services/apiClient';
import { clearSessionAuth, performCompleteLogout } from './authSession';

interface BootstrapPayload {
  nomUtilisateur: string;
  motDePasse: string;
  nom: string;
  prenom?: string;
  email?: string;
}

interface AuthContextValue {
  user: AuthUser | null;
  token: string | null;
  isAuthenticated: boolean;
  isBootstrapping: boolean;
  login: (nomUtilisateur: string, motDePasse: string) => Promise<LoginResponse>;
  bootstrapAdmin: (payload: BootstrapPayload) => Promise<LoginResponse>;
  logout: () => void;
  displayName: string;
  primaryRole: string;
}

const AuthContext = createContext<AuthContextValue | null>(null);

function applySession(response: LoginResponse, setToken: (t: string) => void, setUser: (u: AuthUser) => void) {
  setStoredAuth(response.accessToken, response.utilisateur);
  setToken(response.accessToken);
  setUser(response.utilisateur);
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(() => getStoredAuthUser());
  const [token, setToken] = useState<string | null>(() => getStoredAccessToken());
  const [isBootstrapping, setIsBootstrapping] = useState(true);

  useEffect(() => {
    let cancelled = false;
    async function restore() {
      const existing = getStoredAccessToken();
      if (!existing) {
        if (!cancelled) setIsBootstrapping(false);
        return;
      }
      try {
        const me = await fetchAuthMe();
        if (!cancelled) {
          setUser(me.utilisateur);
          setToken(existing);
          setStoredAuth(existing, me.utilisateur);
        }
      } catch {
        clearSessionAuth();
        if (!cancelled) {
          setUser(null);
          setToken(null);
        }
      } finally {
        if (!cancelled) setIsBootstrapping(false);
      }
    }
    void restore();
    return () => {
      cancelled = true;
    };
  }, []);

  const login = useCallback(async (nomUtilisateur: string, motDePasse: string) => {
    const response = await loginRequest(nomUtilisateur, motDePasse);
    applySession(response, setToken, setUser);
    return response;
  }, []);

  const bootstrapAdmin = useCallback(async (payload: BootstrapPayload) => {
    const response = await bootstrapAdminRequest(payload);
    applySession(response, setToken, setUser);
    return response;
  }, []);

  const logout = useCallback(() => {
    performCompleteLogout();
    setToken(null);
    setUser(null);
  }, []);

  const displayName = useMemo(() => {
    if (!user) return '';
    const parts = [user.prenom, user.nom].filter(Boolean);
    return parts.length > 0 ? parts.join(' ') : user.nomUtilisateur;
  }, [user]);

  const primaryRole = useMemo(() => user?.roles?.[0] ?? '', [user]);

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      token,
      isAuthenticated: !!token && !!user,
      isBootstrapping,
      login,
      bootstrapAdmin,
      logout,
      displayName,
      primaryRole,
    }),
    [user, token, isBootstrapping, login, bootstrapAdmin, logout, displayName, primaryRole],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) {
    throw new Error('useAuth doit être utilisé dans AuthProvider.');
  }
  return ctx;
}
