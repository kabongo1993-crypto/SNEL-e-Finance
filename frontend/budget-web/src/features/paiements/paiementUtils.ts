import { formatMontantUsd } from '../suivi-previsions/suiviUtils';
import { hasPerm } from '../suivi-previsions/suiviUtils';
import { extractApiErrorMessage } from '../../services/apiErrors';

export { formatMontantUsd, hasPerm };

export const STATUTS_DPM = [
  'BROUILLON',
  'EN_VALIDATION_N1',
  'EN_VALIDATION_N2',
  'VALIDEE_ENTITE',
  'SOUMISE',
  'EN_TRAITEMENT_DPM',
  'EN_CONTROLE_BUDGETAIRE',
  'A_CORRIGER',
  'VISEE_BUDGETAIREMENT',
] as const;

export type StatutDpm = (typeof STATUTS_DPM)[number];

export const STATUT_DPM_LABELS: Record<string, string> = {
  BROUILLON: 'Brouillon',
  EN_VALIDATION_N1: 'En validation N1',
  EN_VALIDATION_N2: 'En validation N2',
  VALIDEE_ENTITE: 'Validée entité',
  SOUMISE: 'Soumise',
  EN_TRAITEMENT_DPM: 'En traitement DPM',
  RECEPTIONNEE_BUDGETS: 'En traitement DPM',
  EN_CONTROLE_BUDGETAIRE: 'En contrôle budgétaire',
  A_CORRIGER: 'Retour au demandeur',
  VISEE_BUDGETAIREMENT: 'Visée budgétairement',
};

export type StatutDpmTone = 'default' | 'info' | 'warning' | 'success' | 'error' | 'secondary';

export function normalizeStatutDpm(statut: string | null | undefined): string {
  const s = (statut ?? '').trim().toUpperCase();
  if (s === 'RECEPTIONNEE_BUDGETS') return 'EN_TRAITEMENT_DPM';
  return s;
}

export function labelStatutDpm(statut: string | null | undefined): string {
  const s = normalizeStatutDpm(statut);
  return STATUT_DPM_LABELS[s] ?? (s || '—');
}

export function toneStatutDpm(statut: string | null | undefined): StatutDpmTone {
  switch (normalizeStatutDpm(statut)) {
    case 'BROUILLON':
      return 'default';
    case 'EN_VALIDATION_N1':
    case 'EN_VALIDATION_N2':
      return 'warning';
    case 'VALIDEE_ENTITE':
      return 'success';
    case 'SOUMISE':
    case 'EN_TRAITEMENT_DPM':
    case 'RECEPTIONNEE_BUDGETS':
      return 'info';
    case 'EN_CONTROLE_BUDGETAIRE':
      return 'warning';
    case 'A_CORRIGER':
      return 'error';
    case 'VISEE_BUDGETAIREMENT':
      return 'success';
    default:
      return 'secondary';
  }
}

/** Chip styles cohérents mode clair/sombre (tokens existants). */
export function statutDpmChipSx(statut: string | null | undefined): {
  bgcolor: string;
  color: string;
  borderColor: string;
} {
  switch (normalizeStatutDpm(statut)) {
    case 'BROUILLON':
      return {
        bgcolor: 'var(--ef-surface-secondary)',
        color: 'var(--ef-text-secondary)',
        borderColor: 'var(--ef-border)',
      };
    case 'EN_VALIDATION_N1':
    case 'EN_VALIDATION_N2':
      return {
        bgcolor: 'var(--ef-warning-soft)',
        color: 'var(--ef-warning)',
        borderColor: 'var(--ef-border)',
      };
    case 'VALIDEE_ENTITE':
      return {
        bgcolor: 'var(--ef-success-soft)',
        color: 'var(--ef-success)',
        borderColor: 'var(--ef-border)',
      };
    case 'SOUMISE':
    case 'EN_TRAITEMENT_DPM':
    case 'RECEPTIONNEE_BUDGETS':
      return {
        bgcolor: 'var(--ef-info-soft)',
        color: 'var(--ef-primary)',
        borderColor: 'var(--ef-border)',
      };
    case 'EN_CONTROLE_BUDGETAIRE':
      return {
        bgcolor: 'var(--ef-warning-soft)',
        color: 'var(--ef-warning)',
        borderColor: 'var(--ef-border)',
      };
    case 'A_CORRIGER':
      return {
        bgcolor: 'var(--ef-danger-soft)',
        color: 'var(--ef-danger)',
        borderColor: 'var(--ef-border)',
      };
    case 'VISEE_BUDGETAIREMENT':
      return {
        bgcolor: 'var(--ef-success-soft)',
        color: 'var(--ef-success)',
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

export function estModifiableDemandeur(statut: string | null | undefined): boolean {
  const s = normalizeStatutDpm(statut);
  return s === 'BROUILLON' || s === 'A_CORRIGER';
}

export function canLirePaiements(
  user: { permissions?: string[]; roles?: string[] } | null | undefined,
): boolean {
  return (
    hasPerm(user, 'paiements.lire') ||
    hasPerm(user, 'paiements.ecrire') ||
    hasPerm(user, 'paiements.soumettre') ||
    hasPerm(user, 'paiements.envoyer_validation') ||
    hasPerm(user, 'paiements.valider_n1') ||
    hasPerm(user, 'paiements.valider_n2') ||
    hasPerm(user, 'paiements.declarer_validation_physique') ||
    hasPerm(user, 'paiements.imprimer') ||
    hasPerm(user, 'paiements.charge_dpm') ||
    hasPerm(user, 'paiements.reception_budget') ||
    hasPerm(user, 'paiements.imputer_dc') ||
    hasPerm(user, 'paiements.imputer_ae') ||
    hasPerm(user, 'paiements.imputer_bi') ||
    hasPerm(user, 'paiements.controler_budget') ||
    hasPerm(user, 'paiements.viser_budget')
  );
}

export function canEcrirePaiements(
  user: { permissions?: string[]; roles?: string[] } | null | undefined,
): boolean {
  return hasPerm(user, 'paiements.ecrire');
}

/** Suppression définitive — statut BROUILLON uniquement (permission paiements.ecrire). */
export function canSupprimerBrouillon(
  user: { permissions?: string[]; roles?: string[] } | null | undefined,
  statut: string | null | undefined,
): boolean {
  return canEcrirePaiements(user) && normalizeStatutDpm(statut) === 'BROUILLON';
}

export function canSoumettrePaiements(
  user: { permissions?: string[]; roles?: string[] } | null | undefined,
): boolean {
  return hasPerm(user, 'paiements.soumettre');
}

export function canEnvoyerValidationPaiements(
  user: { permissions?: string[]; roles?: string[] } | null | undefined,
): boolean {
  return hasPerm(user, 'paiements.envoyer_validation');
}

export function canValiderN1Paiements(
  user: { permissions?: string[]; roles?: string[] } | null | undefined,
): boolean {
  return hasPerm(user, 'paiements.valider_n1');
}

export function canValiderN2Paiements(
  user: { permissions?: string[]; roles?: string[] } | null | undefined,
): boolean {
  return hasPerm(user, 'paiements.valider_n2');
}

export function canDeclarerValidationPhysiquePaiements(
  user: { permissions?: string[]; roles?: string[] } | null | undefined,
): boolean {
  return hasPerm(user, 'paiements.declarer_validation_physique');
}

export function canRejeterValidationEntitePaiements(
  user: { permissions?: string[]; roles?: string[] } | null | undefined,
): boolean {
  return hasPerm(user, 'paiements.rejeter_validation_entite');
}

export function canImprimerDemandePaiement(
  user: { permissions?: string[]; roles?: string[] } | null | undefined,
): boolean {
  return hasPerm(user, 'paiements.imprimer');
}

export function canJoindreDocumentSignePaiements(
  user: { permissions?: string[]; roles?: string[] } | null | undefined,
): boolean {
  return hasPerm(user, 'paiements.joindre_document_signe');
}

export function estSoumissibleAuBudget(statut: string | null | undefined): boolean {
  return normalizeStatutDpm(statut) === 'VALIDEE_ENTITE';
}

export function estActeurValidation(
  validation: { idUtilisateurValidateur?: number | null; idUtilisateurDeclarant?: number | null } | undefined,
  idUtilisateurCourant: number | null | undefined,
): boolean {
  if (idUtilisateurCourant == null) return false;
  return (
    validation?.idUtilisateurValidateur === idUtilisateurCourant ||
    validation?.idUtilisateurDeclarant === idUtilisateurCourant
  );
}

export function canAnnulerSoumissionDemande(
  user: { permissions?: string[]; roles?: string[]; idUtilisateur?: number | null } | null | undefined,
  statut: string | null | undefined,
  dateReception?: string | null,
): boolean {
  return (
    canSoumettrePaiements(user) &&
    normalizeStatutDpm(statut) === 'SOUMISE' &&
    !dateReception
  );
}

export function canAnnulerValidationN2Demande(
  user: { permissions?: string[]; roles?: string[]; idUtilisateur?: number | null } | null | undefined,
  statut: string | null | undefined,
  n2: { statut?: string | null; modeValidation?: string | null; idUtilisateurValidateur?: number | null; idUtilisateurDeclarant?: number | null } | undefined,
): boolean {
  if (normalizeStatutDpm(statut) !== 'VALIDEE_ENTITE') return false;
  if ((n2?.statut ?? '').toUpperCase() !== 'VALIDEE') return false;
  if (!estActeurValidation(n2, user?.idUtilisateur)) return false;
  const physique = (n2?.modeValidation ?? '').toUpperCase() === 'PHYSIQUE';
  return physique
    ? canDeclarerValidationPhysiquePaiements(user)
    : canValiderN2Paiements(user);
}

export function canAnnulerValidationN1Demande(
  user: { permissions?: string[]; roles?: string[]; idUtilisateur?: number | null } | null | undefined,
  statut: string | null | undefined,
  n1: { statut?: string | null; modeValidation?: string | null; idUtilisateurValidateur?: number | null; idUtilisateurDeclarant?: number | null } | undefined,
  n2: { statut?: string | null } | undefined,
): boolean {
  const s = normalizeStatutDpm(statut);
  if (s !== 'EN_VALIDATION_N1' && s !== 'EN_VALIDATION_N2') return false;
  if ((n1?.statut ?? '').toUpperCase() !== 'VALIDEE') return false;
  if ((n2?.statut ?? '').toUpperCase() !== 'EN_ATTENTE') return false;
  if (!estActeurValidation(n1, user?.idUtilisateur)) return false;
  const physique = (n1?.modeValidation ?? '').toUpperCase() === 'PHYSIQUE';
  return physique
    ? canDeclarerValidationPhysiquePaiements(user)
    : canValiderN1Paiements(user);
}

export function canChargeDpm(
  user: { permissions?: string[]; roles?: string[] } | null | undefined,
): boolean {
  return (
    hasPerm(user, 'paiements.charge_dpm') ||
    hasPerm(user, 'paiements.reception_budget') ||
    hasPerm(user, 'admin.all')
  );
}

export function canReceptionBudget(
  user: { permissions?: string[]; roles?: string[] } | null | undefined,
): boolean {
  return canChargeDpm(user);
}

export function canImputerDc(
  user: { permissions?: string[]; roles?: string[] } | null | undefined,
): boolean {
  return hasPerm(user, 'paiements.imputer_dc') || hasPerm(user, 'admin.all');
}

export function canImputerAe(
  user: { permissions?: string[]; roles?: string[] } | null | undefined,
): boolean {
  return hasPerm(user, 'paiements.imputer_ae') || hasPerm(user, 'admin.all');
}

export function canImputerBi(
  user: { permissions?: string[]; roles?: string[] } | null | undefined,
): boolean {
  return hasPerm(user, 'paiements.imputer_bi') || hasPerm(user, 'admin.all');
}

export function canControlerBudget(
  user: { permissions?: string[]; roles?: string[] } | null | undefined,
): boolean {
  return hasPerm(user, 'paiements.controler_budget') || hasPerm(user, 'admin.all');
}

export function canViserBudget(
  user: { permissions?: string[]; roles?: string[] } | null | undefined,
): boolean {
  return hasPerm(user, 'paiements.viser_budget') || hasPerm(user, 'admin.all');
}

export function canAccessPaiementsBudget(
  user: { permissions?: string[]; roles?: string[] } | null | undefined,
): boolean {
  return (
    canChargeDpm(user) ||
    canImputerDc(user) ||
    canImputerAe(user) ||
    canImputerBi(user) ||
    canControlerBudget(user) ||
    canViserBudget(user)
  );
}

/** Statuts visibles dans la file Chargé DP — parcours DPM complet. */
export const STATUTS_FILE_CHARGE_DPM = [...STATUTS_DPM] as const;
export const STATUTS_FILE_CHARGE_DP = STATUTS_FILE_CHARGE_DPM;

/** Statuts prioritaires dans la file Chargé DP / Budgets. */
export const STATUTS_FILE_BUDGETS = [
  'SOUMISE',
  'EN_TRAITEMENT_DPM',
  'EN_CONTROLE_BUDGETAIRE',
  'A_CORRIGER',
] as const;

export function formatDateFr(iso: string | null | undefined): string {
  if (!iso) return '—';
  const d = new Date(iso.length <= 10 ? `${iso}T00:00:00` : iso);
  if (Number.isNaN(d.getTime())) return '—';
  return d.toLocaleDateString('fr-FR', { day: '2-digit', month: '2-digit', year: 'numeric' });
}

export function formatDateTimeFr(iso: string | null | undefined): string {
  if (!iso) return '—';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '—';
  return d.toLocaleString('fr-FR', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

export function formatMontantDevise(n: number, devise: string): string {
  const formatted = new Intl.NumberFormat('fr-FR', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(n ?? 0);
  return `${formatted} ${(devise || 'USD').toUpperCase()}`;
}

/** Total imputations vs montant USD demande — tolérance 0,01. */
export function totalImputationsOk(totalImpute: number, montantUsdDemande: number): boolean {
  return Math.abs((totalImpute ?? 0) - (montantUsdDemande ?? 0)) < 0.01;
}

export function calculerMontantUsd(montantBrut: number, taux: number): number {
  if (!taux || taux <= 0) return 0;
  return Math.round((montantBrut / taux) * 100) / 100;
}

export function apiErrorMessage(err: unknown, fallback: string): string {
  return extractApiErrorMessage(err, fallback);
}

export function isApiConflictError(err: unknown): boolean {
  if (typeof err === 'object' && err !== null && 'response' in err) {
    const status = (err as { response?: { status?: number } }).response?.status;
    return status === 409;
  }
  return false;
}

/** Filtre filière Junior — exclut null, vide et codes non conformes. */
export function matchesJuniorCodeTypeBudget(
  codeTypeBudget: string | null | undefined,
  filiere: 'DC' | 'AE' | 'BI',
): boolean {
  const code = (codeTypeBudget ?? '').trim().toUpperCase();
  if (!code) return false;
  return code === filiere;
}

export const MOIS_LABELS: { value: number; label: string }[] = [
  { value: 1, label: 'Janvier' },
  { value: 2, label: 'Février' },
  { value: 3, label: 'Mars' },
  { value: 4, label: 'Avril' },
  { value: 5, label: 'Mai' },
  { value: 6, label: 'Juin' },
  { value: 7, label: 'Juillet' },
  { value: 8, label: 'Août' },
  { value: 9, label: 'Septembre' },
  { value: 10, label: 'Octobre' },
  { value: 11, label: 'Novembre' },
  { value: 12, label: 'Décembre' },
];
