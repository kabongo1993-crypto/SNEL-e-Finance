import {

  clearStoredAuthKeys,

  LAST_USER_ID_KEY,

  POST_LOGOUT_FLAG_KEY,

  readStoredUserId,

} from '../../services/authStorage';



/** Chemin public de connexion (hors routes protégées). */

export const LOGIN_PATH = '/login';



/** Destination par défaut après connexion (contexte applicatif propre). */

export const DEFAULT_POST_LOGIN_PATH = '/dashboard';



/** Paramètre URL posé par redirectToLoginAfterLogout. */

export const LOGOUT_QUERY_PARAM = 'logout';



export interface PostLoginRedirectInput {

  fromPath?: string | null;

  newUserId: number;

  isPostLogout: boolean;

  previousUserId: number | null;

}



/**

 * Détermine la route post-login en empêchant la reprise d'une navigation ou d'un

 * contexte laissé par un autre utilisateur (ou après déconnexion volontaire).

 */

export function resolvePostLoginRedirect(input: PostLoginRedirectInput): string {

  const { fromPath, newUserId, isPostLogout, previousUserId } = input;



  if (isPostLogout) {

    return DEFAULT_POST_LOGIN_PATH;

  }



  if (previousUserId !== null && previousUserId !== newUserId) {

    return DEFAULT_POST_LOGIN_PATH;

  }



  const from = fromPath?.trim();

  if (from && from !== LOGIN_PATH && from.startsWith('/')) {

    return from;

  }



  return DEFAULT_POST_LOGIN_PATH;

}



export function readPostLogoutFlag(): boolean {

  if (typeof sessionStorage === 'undefined') return false;

  return sessionStorage.getItem(POST_LOGOUT_FLAG_KEY) === '1';

}



export function isPostLogoutLogin(

  searchParams: { get: (key: string) => string | null },

): boolean {

  return searchParams.get(LOGOUT_QUERY_PARAM) === '1' || readPostLogoutFlag();

}



export function clearPostLogoutFlag(): void {

  if (typeof sessionStorage === 'undefined') return;

  sessionStorage.removeItem(POST_LOGOUT_FLAG_KEY);

}



export function getLastLoggedInUserId(): number | null {

  if (typeof sessionStorage === 'undefined') return null;

  const raw = sessionStorage.getItem(LAST_USER_ID_KEY);

  if (!raw) return null;

  const parsed = Number(raw);

  return Number.isFinite(parsed) ? parsed : null;

}



export function setLastLoggedInUserId(idUtilisateur: number): void {

  if (typeof sessionStorage === 'undefined') return;

  sessionStorage.setItem(LAST_USER_ID_KEY, String(idUtilisateur));

}



/**

 * Nettoie la session d'authentification côté client (JWT stateless — pas d'endpoint serveur).

 * Ne supprime pas les préférences UI non sensibles (thème, sidebar, langue).

 */

export function clearSessionAuth(): void {

  clearStoredAuthKeys();

}



/** Marque la fin de session et mémorise l'utilisateur pour détecter un changement de compte. */

export function markPostLogoutSession(): void {

  const previousUserId = readStoredUserId();

  if (previousUserId !== null) {

    setLastLoggedInUserId(previousUserId);

  }

  if (typeof sessionStorage !== 'undefined') {

    sessionStorage.setItem(POST_LOGOUT_FLAG_KEY, '1');

  }

}



/** Finalise une connexion réussie (efface le signal post-logout, enregistre le nouvel utilisateur). */

export function completePostLoginSession(newUserId: number): void {

  clearPostLogoutFlag();

  setLastLoggedInUserId(newUserId);

}



/** Redirection post-déconnexion : remplace l'historique pour empêcher Back vers une page protégée. */

export function redirectToLoginAfterLogout(): void {

  if (typeof window !== 'undefined') {

    window.location.replace(`${LOGIN_PATH}?${LOGOUT_QUERY_PARAM}=1`);

  }

}



/** Déconnexion complète côté client avant redirection. */

export function performCompleteLogout(): void {

  markPostLogoutSession();

  clearSessionAuth();

}


