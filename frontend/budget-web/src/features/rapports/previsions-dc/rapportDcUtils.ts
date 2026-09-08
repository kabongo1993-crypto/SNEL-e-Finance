export function formatUsdFr(n: number): string {
  return (
    new Intl.NumberFormat('fr-FR', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(n) +
    ' USD'
  );
}

export function formatUsdCell(n: number | null | undefined, mensuel: boolean): string {
  if (!mensuel) return '—';
  if (n == null || n === 0) return '—';
  return new Intl.NumberFormat('fr-FR', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(n);
}

/** Affichage colonne MODE des rapports : ANNUEL→A, MENSUEL→M (codes métier inchangés). */
export function formatModeRapport(codeMode: string | null | undefined): string {
  const c = (codeMode ?? '').trim().toUpperCase();
  if (c === 'ANNUEL' || c === 'ANN' || c === 'A') return 'A';
  if (c === 'MENSUEL' || c === 'MENS' || c === 'M') return 'M';
  return c;
}

export const STATUTS_CONSULTATION = [
  'VALIDEE',
  'CONTROLEE',
  'SOUMISE',
  'REJETEE',
  'BROUILLON',
] as const;

export const MOIS_LABELS = [
  'JAN',
  'FÉV',
  'MAR',
  'AVR',
  'MAI',
  'JUN',
  'JUL',
  'AOÛ',
  'SEP',
  'OCT',
  'NOV',
  'DÉC',
] as const;
