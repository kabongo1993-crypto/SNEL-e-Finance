/** Utilisateur côté autorisation (JWT / AuthContext). */
export type AuthzUser = { permissions?: string[]; roles?: string[] } | null | undefined;

/**
 * Vérifie une permission applicative.
 * `admin.all` et un rôle contenant « admin » (ex. ADMINISTRATEUR_SYSTEME) passent tout.
 */
export function hasPerm(user: AuthzUser, permission: string): boolean {
  if (!user) return false;
  if (user.roles?.some((r) => r.toLowerCase().includes('admin'))) return true;
  return (user.permissions ?? []).some((p) => p === permission || p === 'admin.all');
}

/** True si au moins une des permissions est détenue (liste vide = toujours autorisé). */
export function hasAnyPerm(user: AuthzUser, permissions: readonly string[]): boolean {
  if (!permissions.length) return true;
  return permissions.some((p) => hasPerm(user, p));
}

/** Écriture référentiels (CRUD structures, UB, exercices, types, rubriques…). */
export function canWriteReferentiels(user: AuthzUser): boolean {
  return hasPerm(user, 'referentiels.ecrire');
}

/**
 * Création / modification des demandeurs DPM.
 * Distinct de referentiels.ecrire (structures, UB, RB…).
 * referentiels.ecrire reste accepté pour compatibilité admin.
 */
export function canWriteDemandeurs(user: AuthzUser): boolean {
  return hasPerm(user, 'demandeurs.ecrire') || hasPerm(user, 'referentiels.ecrire');
}

/** Lecture / accès menu paiements (toute capacité DPM). */
export const PERMS_PAIEMENTS_ACCESS = [
  'paiements.lire',
  'paiements.ecrire',
  'paiements.soumettre',
  'paiements.envoyer_validation',
  'paiements.valider_n1',
  'paiements.valider_n2',
  'paiements.declarer_validation_physique',
  'paiements.rejeter_validation_entite',
  'paiements.imprimer',
  'paiements.joindre_document_signe',
  'paiements.charge_dpm',
  'paiements.reception_budget',
  'paiements.imputer_dc',
  'paiements.imputer_ae',
  'paiements.imputer_bi',
  'paiements.controler_budget',
  'paiements.viser_budget',
] as const;

export const PERMS_CHARGE_DP = ['paiements.charge_dpm', 'paiements.reception_budget'] as const;

export const PERMS_PAIEMENTS_BUDGET = [
  'paiements.charge_dpm',
  'paiements.reception_budget',
  'paiements.imputer_dc',
  'paiements.imputer_ae',
  'paiements.imputer_bi',
  'paiements.controler_budget',
  'paiements.viser_budget',
] as const;

export const PERMS_VERSIONS_WORKFLOW = [
  'versions.controler',
  'versions.valider',
  'versions.rejeter',
] as const;

export const PERMS_VERSIONS_ACCESS = [
  'versions.ecrire',
  'versions.controler',
  'versions.valider',
  'versions.rejeter',
] as const;

export const PERMS_PREVISIONS_ACCESS = ['previsions.ecrire', 'previsions.soumettre'] as const;

export const PERMS_AJUSTEMENTS_ACCESS = [
  'ajustements.lire',
  'ajustements.ecrire',
  'ajustements.valider',
  'versions.valider',
] as const;

export const PERMS_RAPPORTS_PREVISIONS = [
  'versions.controler',
  'versions.valider',
  'versions.rejeter',
  'previsions.ecrire',
  'previsions.soumettre',
] as const;

export const PERMS_ADMIN_UTILISATEURS = ['admin.utilisateurs'] as const;
export const PERMS_ADMIN_PROFILS = ['admin.profils', 'admin.utilisateurs'] as const;
export const PERMS_ADMIN_TECH = ['admin.all'] as const;
export const PERMS_REFERENTIELS = ['referentiels.ecrire'] as const;
export const PERMS_DEMANDEURS_ECRIRE = ['demandeurs.ecrire', 'referentiels.ecrire'] as const;
