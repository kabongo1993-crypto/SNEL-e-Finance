import type { DemandePaiementCompteursDto } from '../../services/apiClient';
import { STATUTS_FILE_BUDGETS } from './paiementUtils';
import type { StatutNavValue } from './paiementStatutNavConfig';

/** Compte les demandes par statut canonique pour un chip de navigation. */
export function countForStatutNavValue(
  compteurs: DemandePaiementCompteursDto | null | undefined,
  value: StatutNavValue,
): number | undefined {
  if (!compteurs) return undefined;
  if (value === '') return compteurs.total;
  switch (value) {
    case 'BROUILLON':
      return compteurs.brouillon;
    case 'EN_VALIDATION_N1':
      return compteurs.enValidationN1;
    case 'EN_VALIDATION_N2':
      return compteurs.enValidationN2;
    case 'VALIDEE_ENTITE':
      return compteurs.valideeEntite;
    case 'SOUMISE':
      return compteurs.soumise;
    case 'EN_TRAITEMENT_DPM':
      return compteurs.enTraitementDpm;
    case 'EN_CONTROLE_BUDGETAIRE':
      return compteurs.enControleBudgetaire;
    case 'A_CORRIGER':
      return compteurs.aCorriger;
    case 'VISEE_BUDGETAIREMENT':
      return compteurs.viseeBudgetairement;
    default:
      return undefined;
  }
}

/** Applique le filtre FILE côté affichage (cohérent avec filtreFileBudgets). */
export function applyBudgetFileCompteurs(
  compteurs: DemandePaiementCompteursDto,
  mode: 'FILE' | 'TOUTES',
): DemandePaiementCompteursDto {
  if (mode === 'TOUTES') return compteurs;

  const fileTotal = STATUTS_FILE_BUDGETS.reduce(
    (sum, statut) => sum + (countForStatutNavValue(compteurs, statut) ?? 0),
    0,
  );

  return {
    ...compteurs,
    total: fileTotal,
    viseeBudgetairement: 0,
  };
}

export function formatStatutNavChipLabel(
  label: string,
  count: number | undefined,
  loading: boolean,
): string {
  if (loading) return `${label} (—)`;
  if (count === undefined) return label;
  return `${label} (${count})`;
}

export function formatStatutDpmChipLabel(
  statutLabel: string,
  compteurs: DemandePaiementCompteursDto | null | undefined,
  statut: StatutNavValue,
  loading: boolean,
): string {
  const count = countForStatutNavValue(compteurs, statut);
  if (loading) return `${statutLabel} (—)`;
  if (count === undefined) return statutLabel;
  return `${statutLabel} (${count})`;
}
