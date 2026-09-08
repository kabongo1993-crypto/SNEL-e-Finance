/** Clés sessionStorage auth — partagées apiClient / authSession (sans dépendance Vite). */
export const AUTH_TOKEN_KEY = 'budgetweb.auth.token';
export const AUTH_USER_KEY = 'budgetweb.auth.user';

/** Signal post-déconnexion volontaire — conservé après clearStoredAuthKeys. */
export const POST_LOGOUT_FLAG_KEY = 'budgetweb.auth.postLogout';

/** Dernier utilisateur authentifié (détection changement de compte). */
export const LAST_USER_ID_KEY = 'budgetweb.auth.lastUserId';

export function clearStoredAuthKeys(): void {
  if (typeof sessionStorage === 'undefined') return;
  sessionStorage.removeItem(AUTH_TOKEN_KEY);
  sessionStorage.removeItem(AUTH_USER_KEY);
}

export function readStoredUserId(): number | null {
  if (typeof sessionStorage === 'undefined') return null;
  const raw = sessionStorage.getItem(AUTH_USER_KEY);
  if (!raw) return null;
  try {
    const parsed = JSON.parse(raw) as { idUtilisateur?: number };
    return typeof parsed.idUtilisateur === 'number' ? parsed.idUtilisateur : null;
  } catch {
    return null;
  }
}
