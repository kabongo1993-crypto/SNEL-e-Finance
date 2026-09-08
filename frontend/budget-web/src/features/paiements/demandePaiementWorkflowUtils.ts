import { labelStatutDpm, normalizeStatutDpm } from './paiementUtils';

export type WorkflowEtapeLabels = {
  statutLabel: string;
  etapePrecedente: string | null;
  etapeSuivante: string | null;
};

/** Étapes métier affichables sans historique de routage API. */
export function getWorkflowEtapeLabels(statut: string | null | undefined): WorkflowEtapeLabels {
  const s = normalizeStatutDpm(statut);
  const statutLabel = labelStatutDpm(s).toUpperCase();

  switch (s) {
    case 'BROUILLON':
      return { statutLabel, etapePrecedente: null, etapeSuivante: 'Validation entité' };
    case 'EN_VALIDATION_N1':
      return { statutLabel, etapePrecedente: 'Brouillon', etapeSuivante: 'Validation N2' };
    case 'EN_VALIDATION_N2':
      return { statutLabel, etapePrecedente: 'Validation N1', etapeSuivante: 'Validée entité' };
    case 'VALIDEE_ENTITE':
      return { statutLabel, etapePrecedente: 'Validation N2', etapeSuivante: 'Soumission Budget' };
    case 'SOUMISE':
      return { statutLabel, etapePrecedente: 'Validée entité', etapeSuivante: 'Chargé DP' };
    case 'EN_TRAITEMENT_DPM':
      return { statutLabel, etapePrecedente: 'Chargé DP', etapeSuivante: 'Contrôle budgétaire' };
    case 'EN_CONTROLE_BUDGETAIRE':
      return { statutLabel, etapePrecedente: 'Chargé DP', etapeSuivante: 'Visa budgétaire' };
    case 'VISEE_BUDGETAIREMENT':
      return { statutLabel, etapePrecedente: 'Contrôle budgétaire', etapeSuivante: null };
    case 'A_CORRIGER':
      return {
        statutLabel: 'RETOUR AU DEMANDEUR',
        etapePrecedente: 'Étape métier précédente',
        etapeSuivante: 'Correction par le demandeur',
      };
    default:
      return { statutLabel, etapePrecedente: null, etapeSuivante: null };
  }
}
