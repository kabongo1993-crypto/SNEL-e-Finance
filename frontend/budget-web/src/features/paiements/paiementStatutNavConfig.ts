import { hasPerm } from '../suivi-previsions/suiviUtils';
import type { StatutDpm } from './paiementUtils';
import {
  STATUTS_DPM,
  STATUTS_FILE_CHARGE_DPM,
  canAccessPaiementsBudget,
  canDeclarerValidationPhysiquePaiements,
  canEnvoyerValidationPaiements,
  canEcrirePaiements,
  canRejeterValidationEntitePaiements,
  canSoumettrePaiements,
  canValiderN1Paiements,
  canValiderN2Paiements,
} from './paiementUtils';

/** Valeur vide = « Toutes » (aucun filtre statut API). */
export type StatutNavValue = StatutDpm | '';

export interface StatutNavItem {
  value: StatutNavValue;
  label: string;
  /** Compteur API (optionnel — affiché par DemandePaiementStatutNav). */
  count?: number;
}

/** Page cible de la navigation par statut. */
export type StatutNavScope = 'mes-demandes' | 'charge-dpm';

/** Libellés navigation (segments) — pluriels où pertinent, distincts des badges ligne. */
export const STATUT_NAV_LABELS: Record<StatutNavValue, string> = {
  '': 'Toutes',
  BROUILLON: 'Brouillons',
  EN_VALIDATION_N1: 'En validation N1',
  EN_VALIDATION_N2: 'En validation N2',
  VALIDEE_ENTITE: 'Validées entité',
  SOUMISE: 'Soumises',
  EN_TRAITEMENT_DPM: 'En traitement DPM',
  EN_CONTROLE_BUDGETAIRE: 'En contrôle budgétaire',
  A_CORRIGER: 'Retour au demandeur',
  VISEE_BUDGETAIREMENT: 'Visées',
};

export type StatutNavContext = 'admin' | 'demandeur' | 'valideur' | 'budget' | 'minimal';

/** Cycle DPM visible des intervenants (N1/N2/budget/chargé) — hors brouillon (saisisseur uniquement). */
const CIRCUIT_STATUTS: StatutNavValue[] = [
  'EN_VALIDATION_N1',
  'EN_VALIDATION_N2',
  'VALIDEE_ENTITE',
  'SOUMISE',
  'EN_TRAITEMENT_DPM',
  'EN_CONTROLE_BUDGETAIRE',
  'A_CORRIGER',
  'VISEE_BUDGETAIREMENT',
];

const DEMANDEUR_STATUTS: StatutNavValue[] = [
  'BROUILLON',
  ...CIRCUIT_STATUTS,
];

/** File Chargé DP — parcours DPM complet (tous statuts). */
const CHARGE_DPM_STATUTS: StatutNavValue[] = [...STATUTS_FILE_CHARGE_DPM];

function navItem(value: StatutNavValue): StatutNavItem {
  return { value, label: STATUT_NAV_LABELS[value] };
}

function itemsFromStatuts(statuts: StatutNavValue[]): StatutNavItem[] {
  return [navItem(''), ...statuts.map(navItem)];
}

export function isValidStatutNavValue(value: string | null | undefined): value is StatutDpm {
  if (!value) return false;
  const normalized = value.trim().toUpperCase();
  return (STATUTS_DPM as readonly string[]).includes(normalized);
}

export function normalizeStatutNavValue(
  value: string | null | undefined,
  options?: { allowed?: readonly StatutDpm[] },
): StatutNavValue {
  if (!value || value.trim().toUpperCase() === 'TOUS') return '';
  const normalized = value.trim().toUpperCase();
  if (!isValidStatutNavValue(normalized)) return '';
  if (options?.allowed && !options.allowed.includes(normalized)) return '';
  return normalized;
}

export function resolveStatutNavContext(
  user: { permissions?: string[]; roles?: string[] } | null | undefined,
): StatutNavContext {
  if (!user) return 'minimal';
  if (hasPerm(user, 'admin.all')) return 'admin';

  const isDemandeur =
    canEcrirePaiements(user) ||
    canEnvoyerValidationPaiements(user) ||
    canSoumettrePaiements(user);
  const isValideur =
    canValiderN1Paiements(user) ||
    canValiderN2Paiements(user) ||
    canRejeterValidationEntitePaiements(user) ||
    canDeclarerValidationPhysiquePaiements(user);
  const isBudget = canAccessPaiementsBudget(user);

  if (isBudget && !isDemandeur && !isValideur) return 'budget';
  if (isValideur && !isDemandeur) return 'valideur';
  if (isDemandeur) return 'demandeur';
  if (isBudget) return 'budget';
  if (isValideur) return 'valideur';

  return 'minimal';
}

/** Statuts autorisés (hors « Toutes ») dérivés des segments navigation. */
export function getAllowedStatutsFromNavItems(items: readonly StatutNavItem[]): StatutDpm[] {
  return items
    .map((item) => item.value)
    .filter((value): value is StatutDpm => value !== '');
}

/** Options dropdown alignées sur les chips role-aware. */
export function getStatutDropdownOptions(
  items: readonly StatutNavItem[],
  emptyLabel = 'Tous',
): { value: StatutNavValue; label: string }[] {
  return items.map((item) => ({
    value: item.value,
    label: item.value === '' ? emptyLabel : item.label,
  }));
}

export function getStatutNavItems(
  user: { permissions?: string[]; roles?: string[] } | null | undefined,
  scope: StatutNavScope = 'mes-demandes',
): StatutNavItem[] {
  if (scope === 'charge-dpm') {
    return itemsFromStatuts(CHARGE_DPM_STATUTS);
  }

  const context = resolveStatutNavContext(user);

  switch (context) {
    case 'admin':
      return itemsFromStatuts([...STATUTS_DPM]);
    case 'demandeur':
      return itemsFromStatuts(DEMANDEUR_STATUTS);
    case 'valideur':
    case 'budget':
      return itemsFromStatuts(CIRCUIT_STATUTS);
    default:
      return [navItem('')];
  }
}