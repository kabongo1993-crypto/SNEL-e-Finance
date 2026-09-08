/**
 * npx --yes tsx src/features/auth/authSession.selftest.ts
 */
import {
  AUTH_TOKEN_KEY,
  AUTH_USER_KEY,
  LAST_USER_ID_KEY,
  POST_LOGOUT_FLAG_KEY,
  clearStoredAuthKeys,
} from '../../services/authStorage';
import {
  DEFAULT_POST_LOGIN_PATH,
  LOGOUT_QUERY_PARAM,
  LOGIN_PATH,
  clearPostLogoutFlag,
  completePostLoginSession,
  getLastLoggedInUserId,
  isPostLogoutLogin,
  performCompleteLogout,
  readPostLogoutFlag,
  redirectToLoginAfterLogout,
  resolvePostLoginRedirect,
} from './authSession';

function assert(cond: unknown, msg: string): void {
  if (!cond) throw new Error(msg);
}

const store = new Map<string, string>();
(globalThis as { sessionStorage?: Storage }).sessionStorage = {
  get length() {
    return store.size;
  },
  clear() {
    store.clear();
  },
  getItem(key: string) {
    return store.get(key) ?? null;
  },
  key(index: number) {
    return [...store.keys()][index] ?? null;
  },
  removeItem(key: string) {
    store.delete(key);
  },
  setItem(key: string, value: string) {
    store.set(key, value);
  },
};

const localStore = new Map<string, string>();
(globalThis as { localStorage?: Storage }).localStorage = {
  get length() {
    return localStore.size;
  },
  clear() {
    localStore.clear();
  },
  getItem(key: string) {
    return localStore.get(key) ?? null;
  },
  key(index: number) {
    return [...localStore.keys()][index] ?? null;
  },
  removeItem(key: string) {
    localStore.delete(key);
  },
  setItem(key: string, value: string) {
    localStore.set(key, value);
  },
};

// --- logout depuis /paiements/nouveau : marque post-logout + mémorise l'utilisateur ---
store.clear();
localStore.clear();
store.set(AUTH_TOKEN_KEY, 'test-token');
store.set(AUTH_USER_KEY, JSON.stringify({ idUtilisateur: 42, nomUtilisateur: 'LEKA' }));
localStore.set('efinance-theme-mode', 'dark');
localStore.set('budgetweb.ui-lang', 'fr');

assert(store.has(AUTH_TOKEN_KEY), 'token présent avant logout');

performCompleteLogout();

assert(!store.has(AUTH_TOKEN_KEY), 'token supprimé après logout');
assert(!store.has(AUTH_USER_KEY), 'user supprimé après logout');
assert(readPostLogoutFlag(), 'flag post-logout posé');
assert(getLastLoggedInUserId() === 42, 'dernier utilisateur mémorisé (LEKA)');

// Préférences UI non sensibles conservées
assert(localStore.get('efinance-theme-mode') === 'dark', 'thème conservé après logout');
assert(localStore.get('budgetweb.ui-lang') === 'fr', 'langue conservée après logout');

// --- reconnexion adminFull : pas de restauration route Nathan ---
const adminFullId = 1;
const fromNathan = '/paiements/nouveau';

assert(
  resolvePostLoginRedirect({
    fromPath: fromNathan,
    newUserId: adminFullId,
    isPostLogout: true,
    previousUserId: 42,
  }) === DEFAULT_POST_LOGIN_PATH,
  'post-logout : ignore from=/paiements/nouveau',
);

assert(
  resolvePostLoginRedirect({
    fromPath: fromNathan,
    newUserId: adminFullId,
    isPostLogout: false,
    previousUserId: 42,
  }) === DEFAULT_POST_LOGIN_PATH,
  'changement utilisateur : ignore from même sans flag explicite',
);

// Même utilisateur, accès direct non authentifié : deep-link autorisé
assert(
  resolvePostLoginRedirect({
    fromPath: '/paiements/demandes',
    newUserId: 42,
    isPostLogout: false,
    previousUserId: 42,
  }) === '/paiements/demandes',
  'même utilisateur sans post-logout : restaure from légitime',
);

// from invalide ou login → dashboard
assert(
  resolvePostLoginRedirect({
    fromPath: '/login',
    newUserId: 42,
    isPostLogout: false,
    previousUserId: 42,
  }) === DEFAULT_POST_LOGIN_PATH,
  'from=/login ignoré',
);

assert(
  resolvePostLoginRedirect({
    fromPath: undefined,
    newUserId: 42,
    isPostLogout: false,
    previousUserId: null,
  }) === DEFAULT_POST_LOGIN_PATH,
  'sans from → dashboard',
);

// --- isPostLogoutLogin : query + sessionStorage ---
store.set(POST_LOGOUT_FLAG_KEY, '1');
assert(
  isPostLogoutLogin({ get: () => null }),
  'flag sessionStorage détecté',
);
clearPostLogoutFlag();
assert(
  isPostLogoutLogin({ get: (k) => (k === LOGOUT_QUERY_PARAM ? '1' : null) }),
  'query logout=1 détectée',
);

// --- completePostLoginSession ---
store.set(POST_LOGOUT_FLAG_KEY, '1');
completePostLoginSession(adminFullId);
assert(!readPostLogoutFlag(), 'flag post-logout effacé après login');
assert(getLastLoggedInUserId() === adminFullId, 'nouvel utilisateur enregistré');

// --- clearStoredAuthKeys ne touche pas aux signaux de session ---
store.set(POST_LOGOUT_FLAG_KEY, '1');
store.set(LAST_USER_ID_KEY, '99');
clearStoredAuthKeys();
assert(store.get(POST_LOGOUT_FLAG_KEY) === '1', 'post-logout survit à clearStoredAuthKeys');
assert(store.get(LAST_USER_ID_KEY) === '99', 'lastUserId survit à clearStoredAuthKeys');

// --- redirectToLoginAfterLogout ---
let redirected = '';
(globalThis as { window?: { location: { replace: (url: string) => void } } }).window = {
  location: {
    replace(url: string) {
      redirected = url;
    },
  },
};
redirectToLoginAfterLogout();
assert(redirected === `${LOGIN_PATH}?${LOGOUT_QUERY_PARAM}=1`, 'redirect login?logout=1');

assert(LOGIN_PATH === '/login', 'login path');

console.log('authSession.selftest OK');
