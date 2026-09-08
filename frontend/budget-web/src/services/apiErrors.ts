import axios from 'axios';

type ErrorBody = Record<string, unknown>;

function pickMessageField(body: ErrorBody): string | null {
  for (const key of ['message', 'Message', 'detail', 'Detail', 'title', 'Title']) {
    const value = body[key];
    if (typeof value === 'string' && value.trim()) {
      return value.trim();
    }
  }
  return null;
}

function parseErrorBody(data: unknown): string | null {
  if (data == null) return null;

  if (typeof data === 'string') {
    const trimmed = data.trim();
    if (!trimmed) return null;
    try {
      const parsed = JSON.parse(trimmed) as unknown;
      if (typeof parsed === 'object' && parsed !== null) {
        return pickMessageField(parsed as ErrorBody);
      }
    } catch {
      return trimmed;
    }
    return null;
  }

  if (typeof data === 'object') {
    return pickMessageField(data as ErrorBody);
  }

  return null;
}

function statusFallback(status: number | undefined, fallback: string): string {
  switch (status) {
    case 413:
      return 'Le fichier dépasse la taille maximale autorisée par le serveur.';
    case 415:
      return 'Le fichier n’a pas pu être transmis (format d’envoi incorrect). Rechargez la page et réessayez.';
    case 401:
      return 'Session expirée ou non autorisée.';
    case 403:
      return 'Permission insuffisante pour cette opération.';
    case 404:
      return 'Ressource introuvable.';
    case 409:
      return 'La demande a été modifiée entre-temps. Actualisez la page avant de poursuivre.';
    default:
      return fallback;
  }
}

/**
 * Extrait le message d'erreur renvoyé par l'API (ApiErrorResponse, ProblemDetails, texte brut).
 * Ne masque pas les messages métier (extension, taille, statut, etc.).
 */
export function extractApiErrorMessage(err: unknown, fallback: string): string {
  if (axios.isAxiosError(err)) {
    const fromBody = parseErrorBody(err.response?.data);
    if (fromBody) return fromBody;
    if (err.response?.status != null) {
      return statusFallback(err.response.status, fallback);
    }
    if (err.code === 'ERR_NETWORK') {
      return 'Impossible de joindre le serveur. Vérifiez votre connexion.';
    }
    if (err.message?.trim()) return err.message.trim();
    return fallback;
  }

  if (typeof err === 'object' && err !== null && 'response' in err) {
    const response = (err as { response?: { status?: number; data?: unknown } }).response;
    const fromBody = parseErrorBody(response?.data);
    if (fromBody) return fromBody;
    if (response?.status != null) {
      return statusFallback(response.status, fallback);
    }
  }

  if (err instanceof Error && err.message.trim()) {
    return err.message.trim();
  }

  return fallback;
}
