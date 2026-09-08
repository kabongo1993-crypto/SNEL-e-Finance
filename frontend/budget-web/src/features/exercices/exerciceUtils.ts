export const STATUTS_EXERCICE = ['OUVERT', 'CLOTURE'] as const;
export type StatutExercice = (typeof STATUTS_EXERCICE)[number];

export function extraireDateIso(value: string | null | undefined): string | null {
  if (!value) return null;
  const iso = value.slice(0, 10);
  return /^\d{4}-\d{2}-\d{2}$/.test(iso) ? iso : null;
}

export function formatDateExercice(value: string | null | undefined): string {
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
