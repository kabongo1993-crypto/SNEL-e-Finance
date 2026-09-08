export { formatUsdFr, formatModeRapport, MOIS_LABELS, STATUTS_CONSULTATION } from '../previsions-dc/rapportDcUtils';

/** Affiche — si null (ANNUEL), sinon montant formaté (y compris 0,00). */
export function formatUsdAeCell(n: number | null | undefined): string {
  if (n == null) return '—';
  return new Intl.NumberFormat('fr-FR', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(n);
}

export const MOIS_FILTRE_OPTIONS = [
  { value: '1', label: 'Janvier' },
  { value: '2', label: 'Février' },
  { value: '3', label: 'Mars' },
  { value: '4', label: 'Avril' },
  { value: '5', label: 'Mai' },
  { value: '6', label: 'Juin' },
  { value: '7', label: 'Juillet' },
  { value: '8', label: 'Août' },
  { value: '9', label: 'Septembre' },
  { value: '10', label: 'Octobre' },
  { value: '11', label: 'Novembre' },
  { value: '12', label: 'Décembre' },
  { value: 'tous', label: 'Tous les mois' },
] as const;
