import type { ProfilCatalogueItem } from '../../services/apiClient';

export type RoleGroupId = 'entite' | 'direction' | 'historique' | 'admin';

export interface RoleGroupDef {
  id: RoleGroupId;
  title: string;
  hint?: string;
  codes: string[];
}

/** Libellés métier prioritaires (sinon libellé catalogue / code). */
export const ROLE_LIBELLES: Record<string, string> = {
  SERVICE_DEMANDEUR: 'Service demandeur',
  RESPONSABLE_SERVICE_DEMANDEUR: 'Responsable du service demandeur',
  RESPONSABLE_ENTITE_INITIATRICE: "Responsable de l'entité initiatrice",
  CHARGE_DP: 'Chargé des demandes de paiement',
  GESTIONNAIRE_JUNIOR: 'Gestionnaire Junior',
  GESTIONNAIRE_SENIOR: 'Gestionnaire Senior',
  CHEF_DIVISION: 'Chef de Division',
  DIRECTEUR_BUDGETS: 'Directeur des Budgets',
  DEMANDEUR: 'Demandeur (historique)',
  CHARGE_DPM: 'Chargé DPM (historique)',
  GESTIONNAIRE_JUNIOR_DC: 'Gestionnaire junior DC (historique)',
  GESTIONNAIRE_JUNIOR_AE: 'Gestionnaire junior AE (historique)',
  GESTIONNAIRE_JUNIOR_BI: 'Gestionnaire junior BI (historique)',
  CONTROLE_BUDGET: 'Contrôle budget (historique)',
  ADMIN: 'Administrateur (historique)',
  ADMINISTRATEUR_SYSTEME: 'Administrateur système',
};

export const ROLE_GROUPS: RoleGroupDef[] = [
  {
    id: 'entite',
    title: 'Entité initiatrice',
    hint: 'Saisie et soumission des demandes de paiement.',
    codes: [
      'SERVICE_DEMANDEUR',
      'RESPONSABLE_SERVICE_DEMANDEUR',
      'RESPONSABLE_ENTITE_INITIATRICE',
    ],
  },
  {
    id: 'direction',
    title: 'Direction des Budgets',
    hint: 'Circuit DPM / contrôle / visa / validation. Périmètre recommandé : Tous départements + Toutes UB.',
    codes: [
      'CHARGE_DP',
      'GESTIONNAIRE_JUNIOR',
      'GESTIONNAIRE_SENIOR',
      'CHEF_DIVISION',
      'DIRECTEUR_BUDGETS',
    ],
  },
  {
    id: 'historique',
    title: 'Profils historiques',
    hint: 'Conservés pour compatibilité — préférer les rôles cibles ci-dessus.',
    codes: [
      'DEMANDEUR',
      'CHARGE_DPM',
      'GESTIONNAIRE_JUNIOR_DC',
      'GESTIONNAIRE_JUNIOR_AE',
      'GESTIONNAIRE_JUNIOR_BI',
      'CONTROLE_BUDGET',
      'ADMIN',
    ],
  },
  {
    id: 'admin',
    title: 'Administration technique',
    hint: 'Accès technique global — distinct du Directeur des Budgets.',
    codes: ['ADMINISTRATEUR_SYSTEME'],
  },
];

export const DIRECTION_BUDGETS_CODES = new Set(
  ROLE_GROUPS.find((g) => g.id === 'direction')!.codes,
);

export const JUNIOR_CODES = new Set([
  'GESTIONNAIRE_JUNIOR',
  'GESTIONNAIRE_JUNIOR_DC',
  'GESTIONNAIRE_JUNIOR_AE',
  'GESTIONNAIRE_JUNIOR_BI',
]);

export const IMPUTER_PERMISSIONS = [
  'paiements.imputer_dc',
  'paiements.imputer_ae',
  'paiements.imputer_bi',
] as const;

export function libelleRole(code: string, catalogue?: ProfilCatalogueItem[]): string {
  const key = code.trim().toUpperCase();
  if (ROLE_LIBELLES[key]) return ROLE_LIBELLES[key];
  const fromCat = catalogue?.find((p) => p.code.toUpperCase() === key);
  return fromCat?.libelle ?? code;
}

export function isPerimetreConfigure(p: {
  tousDepartements: boolean;
  toutesUnitesBudgetaires: boolean;
  idDepartements: number[];
  idUnitesBudgetaires: number[];
}): boolean {
  return (
    p.tousDepartements ||
    p.toutesUnitesBudgetaires ||
    p.idDepartements.length > 0 ||
    p.idUnitesBudgetaires.length > 0
  );
}

export type EtatPerimetreListe = 'tous' | 'restreint' | 'vide';

export function etatPerimetreListe(p: {
  tousDepartements: boolean;
  toutesUnitesBudgetaires: boolean;
  idDepartements: number[];
  idUnitesBudgetaires: number[];
} | null | undefined): EtatPerimetreListe {
  if (!p) return 'vide';
  if (p.tousDepartements && p.toutesUnitesBudgetaires) return 'tous';
  if (isPerimetreConfigure(p)) return 'restreint';
  return 'vide';
}

export function permissionsHeriteesDesProfils(
  profilsSelectionnes: string[],
  catalogue: ProfilCatalogueItem[],
): Set<string> {
  const set = new Set<string>();
  for (const code of profilsSelectionnes) {
    const item = catalogue.find((p) => p.code.toUpperCase() === code.toUpperCase());
    if (!item) continue;
    for (const perm of item.permissions) set.add(perm);
  }
  return set;
}

export function groupProfilsForForm(
  catalogue: ProfilCatalogueItem[],
): { group: RoleGroupDef; items: ProfilCatalogueItem[] }[] {
  const byCode = new Map(catalogue.map((p) => [p.code.toUpperCase(), p]));
  const used = new Set<string>();
  const result: { group: RoleGroupDef; items: ProfilCatalogueItem[] }[] = [];

  for (const group of ROLE_GROUPS) {
    const items: ProfilCatalogueItem[] = [];
    for (const code of group.codes) {
      const item = byCode.get(code);
      if (item) {
        items.push(item);
        used.add(code);
      }
    }
    if (items.length > 0) result.push({ group, items });
  }

  const orphans = catalogue.filter((p) => !used.has(p.code.toUpperCase()));
  if (orphans.length > 0) {
    result.push({
      group: {
        id: 'historique',
        title: 'Autres profils',
        hint: 'Profils présents au catalogue mais hors groupes standards.',
        codes: orphans.map((p) => p.code),
      },
      items: orphans,
    });
  }

  return result;
}
