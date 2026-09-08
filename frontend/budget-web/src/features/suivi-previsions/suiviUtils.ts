/** Helpers statut / montants — modules Suivi & Soumissions. */

export function normalizeStatut(statut: string): string {
  return (statut ?? '').trim().toUpperCase();
}

export function formatMontantSuivi(n: number): string {
  return formatMontantUsd(n);
}

/** Affichage budgétaire SNEL : espaces milliers, 2 décimales, devise USD. */
export function formatMontantUsd(n: number): string {
  const formatted = new Intl.NumberFormat('fr-FR', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(n ?? 0);
  return `${formatted} USD`;
}

export function formatDateSuivi(iso: string | null | undefined): string {
  if (!iso) return '—';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '—';
  return d.toLocaleDateString('fr-FR', { day: '2-digit', month: '2-digit', year: 'numeric' });
}

export function versionLabel(numero: number, libelle?: string): string {
  const v = `V${numero}`;
  return libelle ? `${v} — ${libelle}` : v;
}

export type StatutTone = 'default' | 'info' | 'warning' | 'success' | 'error';

export function statutTone(statut: string): StatutTone {
  switch (normalizeStatut(statut)) {
    case 'SOUMISE':
      return 'info';
    case 'CONTROLEE':
      return 'warning';
    case 'VALIDEE':
      return 'success';
    case 'REJETEE':
      return 'error';
    default:
      return 'default';
  }
}

export function statutChipSx(statut: string): { bgcolor: string; color: string; borderColor: string } {
  switch (normalizeStatut(statut)) {
    case 'BROUILLON':
      return {
        bgcolor: 'var(--ef-surface-secondary)',
        color: 'var(--ef-text-secondary)',
        borderColor: 'var(--ef-border)',
      };
    case 'SOUMISE':
      return {
        bgcolor: 'var(--ef-info-soft)',
        color: 'var(--ef-primary)',
        borderColor: 'var(--ef-border)',
      };
    case 'CONTROLEE':
      return {
        bgcolor: 'var(--ef-warning-soft)',
        color: 'var(--ef-warning)',
        borderColor: 'var(--ef-border)',
      };
    case 'VALIDEE':
      return {
        bgcolor: 'var(--ef-success-soft)',
        color: 'var(--ef-success)',
        borderColor: 'var(--ef-border)',
      };
    case 'REJETEE':
      return {
        bgcolor: 'var(--ef-danger-soft)',
        color: 'var(--ef-danger)',
        borderColor: 'var(--ef-border)',
      };
    default:
      return {
        bgcolor: 'var(--ef-surface-secondary)',
        color: 'var(--ef-text-secondary)',
        borderColor: 'var(--ef-border)',
      };
  }
}

export function buildPrevisionsContextUrl(opts: {
  idExercice: number;
  idVersion: number;
  idUB: number;
}): string {
  const q = new URLSearchParams({
    exercice: String(opts.idExercice),
    version: String(opts.idVersion),
    ub: String(opts.idUB),
  });
  return `/budget/previsions?${q.toString()}`;
}

import { hasPerm, hasAnyPerm, PERMS_PREVISIONS_ACCESS, PERMS_VERSIONS_WORKFLOW, type AuthzUser } from '../auth/permissions';

export { hasPerm, hasAnyPerm } from '../auth/permissions';

/** Service émetteur (préparateur) : accès prévisions sans workflow Budget. */
export function ouvrirViaGrillePrevisions(user: AuthzUser): boolean {
  return hasAnyPerm(user, PERMS_PREVISIONS_ACCESS) && !hasAnyPerm(user, PERMS_VERSIONS_WORKFLOW);
}

/** Actions workflow sur une UB — basées sur le statut WORKFLOW_PREVISION_UB, pas VERSION. */
export function peutControlerUb(
  statut: string,
  user: { permissions?: string[]; roles?: string[] } | null | undefined,
): boolean {
  return normalizeStatut(statut) === 'SOUMISE' && hasPerm(user, 'versions.controler');
}

export function peutValiderUb(
  statut: string,
  user: { permissions?: string[]; roles?: string[] } | null | undefined,
): boolean {
  return normalizeStatut(statut) === 'CONTROLEE' && hasPerm(user, 'versions.valider');
}

export function peutRejeterUb(
  statut: string,
  user: { permissions?: string[]; roles?: string[] } | null | undefined,
): boolean {
  const s = normalizeStatut(statut);
  return (s === 'SOUMISE' || s === 'CONTROLEE') && hasPerm(user, 'versions.rejeter');
}

export function messageResultatBulkDepartement(
  actionLabel: string,
  traitees: number,
  ignorees: number,
  details: { outcome: string; statutAvant: string }[],
): string {
  const analysees = traitees + ignorees;
  const parts: string[] = [`${actionLabel}`];
  parts.push(`${analysees} UB analysée${analysees > 1 ? 's' : ''}.`);
  parts.push(`${traitees} UB traitée${traitees > 1 ? 's' : ''}.`);
  if (ignorees > 0) {
    const byStatut = new Map<string, number>();
    for (const d of details) {
      if (d.outcome === 'IGNORE') {
        byStatut.set(d.statutAvant, (byStatut.get(d.statutAvant) ?? 0) + 1);
      }
    }
    const raisons = [...byStatut.entries()]
      .map(([st, n]) => `${n} ${st}`)
      .join(', ');
    parts.push(`${ignorees} UB ignorée${ignorees > 1 ? 's' : ''} (${raisons}).`);
  }
  return parts.join(' ');
}

/** @deprecated Use messageResultatBulkDepartement */
export function messageResultatRejetDepartement(rejetees: number, ignorees: number): string {
  return messageResultatBulkDepartement('Rejet terminé.', rejetees, ignorees, []);
}

export const MOIS_COURTS = ['Jan', 'Fév', 'Mar', 'Avr', 'Mai', 'Juin', 'Juil', 'Aoû', 'Sep', 'Oct', 'Nov', 'Déc'];
