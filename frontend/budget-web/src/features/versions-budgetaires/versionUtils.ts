export const STATUTS_VERSION = ['BROUILLON', 'SOUMISE', 'CONTROLEE', 'VALIDEE', 'REJETEE'] as const;
export type StatutVersion = (typeof STATUTS_VERSION)[number];

export function extraireDateIso(value: string | null | undefined): string {
  if (!value) return '';
  const iso = value.slice(0, 10);
  return /^\d{4}-\d{2}-\d{2}$/.test(iso) ? iso : '';
}

export function formatDateFr(value: string | null | undefined): string {
  const iso = extraireDateIso(value);
  if (!iso) return '—';
  const [year, month, day] = iso.split('-');
  return `${day}/${month}/${year}`;
}

export function apiErrorMessage(err: unknown, fallback: string): string {
  if (typeof err === 'object' && err !== null && 'response' in err) {
    const data = (err as { response?: { data?: { message?: string; Message?: string } } }).response?.data;
    return data?.message ?? data?.Message ?? fallback;
  }
  return fallback;
}

export function libelleUtilisateur(nomUtilisateur: string, nom: string, prenom: string | null): string {
  const identite = [nom, prenom].filter(Boolean).join(' ').trim();
  return identite ? `${nomUtilisateur} — ${identite}` : nomUtilisateur;
}
