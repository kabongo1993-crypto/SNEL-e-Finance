import DeleteOutlinedIcon from '@mui/icons-material/DeleteOutlined';
import SendOutlinedIcon from '@mui/icons-material/SendOutlined';
import type { NavigateFunction } from 'react-router-dom';
import type { DataTableAction } from '../../components';
import type { DemandeEnrichie } from './paiementBudgetUtils';
import { buildAssignationView } from './demandePaiementAssignationFromApi';
import { peutAfficherActionsMetierAssignation } from './demandePaiementAssignationUtils';
import {
  canEcrirePaiements,
  canSoumettrePaiements,
  canSupprimerBrouillon,
  canAnnulerSoumissionDemande,
  estSoumissibleAuBudget,
  normalizeStatutDpm,
} from './paiementUtils';

type DemandeRow = Pick<
  DemandeEnrichie,
  | 'idDemandePaiement'
  | 'statut'
  | 'idUtilisateurAssigne'
  | 'nomUtilisateurAssigne'
  | 'prenomUtilisateurAssigne'
>;

function peutAgirSurDemande(
  d: DemandeRow,
  idUtilisateurCourant: number | null | undefined,
): boolean {
  const view = buildAssignationView(d, idUtilisateurCourant);
  return peutAfficherActionsMetierAssignation(view);
}

const MODIFIABLE_STATUTS = ['BROUILLON', 'A_CORRIGER', 'VALIDEE_ENTITE'] as const;

function estModifiableListe(statut: string | null | undefined): boolean {
  return MODIFIABLE_STATUTS.includes(
    normalizeStatutDpm(statut) as (typeof MODIFIABLE_STATUTS)[number],
  );
}

/** Actions ⋮ — liste « Mes demandes » (Service demandeur / initiateur). */
export function buildMesDemandesRowActions(deps: {
  user: { permissions?: string[]; roles?: string[]; idUtilisateur?: number | null } | null | undefined;
  navigate: NavigateFunction;
  onSoumettre: (idDemande: number) => void;
  onAnnulerSoumission?: (idDemande: number) => void;
  onSupprimer?: (idDemande: number) => void;
  isChargeDpm: boolean;
  isRowBusy?: (idDemande: number) => boolean;
}): DataTableAction<DemandeRow>[] {
  const canWrite = canEcrirePaiements(deps.user);
  const canSoumettre = canSoumettrePaiements(deps.user);

  return [
    {
      id: 'voir',
      label: 'Voir',
      onClick: (d) => deps.navigate(`/paiements/${d.idDemandePaiement}`),
    },
    {
      id: 'modifier',
      label: 'Modifier',
      hidden: (d) => !canWrite || !estModifiableListe(d.statut),
      onClick: (d) => deps.navigate(`/paiements/${d.idDemandePaiement}/modifier`),
    },
    {
      id: 'soumettre',
      label: 'Soumettre au Budget',
      icon: <SendOutlinedIcon fontSize="small" />,
      hidden: (d) => !canSoumettre || !estSoumissibleAuBudget(d.statut),
      disabled: (d) => deps.isRowBusy?.(d.idDemandePaiement) ?? false,
      onClick: (d) => deps.onSoumettre(d.idDemandePaiement),
    },
    {
      id: 'annuler-soumission',
      label: 'Annuler la soumission',
      hidden: (d) =>
        !deps.onAnnulerSoumission ||
        !canAnnulerSoumissionDemande(deps.user, d.statut),
      disabled: (d) => deps.isRowBusy?.(d.idDemandePaiement) ?? false,
      onClick: (d) => deps.onAnnulerSoumission?.(d.idDemandePaiement),
    },
    {
      id: 'supprimer',
      label: 'Supprimer définitivement',
      icon: <DeleteOutlinedIcon fontSize="small" />,
      hidden: (d) => !canSupprimerBrouillon(deps.user, d.statut) || !deps.onSupprimer,
      disabled: (d) => deps.isRowBusy?.(d.idDemandePaiement) ?? false,
      onClick: (d) => deps.onSupprimer?.(d.idDemandePaiement),
    },
    {
      id: 'traiter',
      label: 'Traiter',
      hidden: (d) => {
        if (!deps.isChargeDpm) return true;
        const s = normalizeStatutDpm(d.statut);
        return !['SOUMISE', 'EN_TRAITEMENT_DPM', 'BROUILLON'].includes(s);
      },
      onClick: (d) => deps.navigate(`/paiements/charge-dpm/${d.idDemandePaiement}`),
    },
  ];
}

/** Actions ⋮ — file Chargé DPM. */
export function buildChargeDpmFileRowActions(deps: {
  navigate: NavigateFunction;
  onReceptionner: (idDemande: number) => void;
  idUtilisateurCourant?: number | null;
  isRowBusy?: (idDemande: number) => boolean;
}): DataTableAction<DemandeRow>[] {
  return [
    {
      id: 'voir',
      label: 'Voir',
      onClick: (d) => deps.navigate(`/paiements/${d.idDemandePaiement}`),
    },
    {
      id: 'traiter',
      label: 'Traiter',
      hidden: (d) => !peutAgirSurDemande(d, deps.idUtilisateurCourant),
      onClick: (d) => deps.navigate(`/paiements/charge-dpm/${d.idDemandePaiement}`),
    },
    {
      id: 'soumettre',
      label: 'Réceptionner',
      icon: <SendOutlinedIcon fontSize="small" />,
      hidden: (d) =>
        !peutAgirSurDemande(d, deps.idUtilisateurCourant) ||
        normalizeStatutDpm(d.statut) !== 'SOUMISE',
      disabled: (d) => deps.isRowBusy?.(d.idDemandePaiement) ?? false,
      onClick: (d) => deps.onReceptionner(d.idDemandePaiement),
    },
  ];
}

/** Actions ⋮ — gestionnaire junior (imputation). */
export function buildJuniorRowActions(deps: {
  navigate: NavigateFunction;
  idUtilisateurCourant?: number | null;
}): DataTableAction<DemandeRow>[] {
  return [
    {
      id: 'voir',
      label: 'Voir',
      onClick: (d) => deps.navigate(`/paiements/${d.idDemandePaiement}`),
    },
    {
      id: 'valider',
      label: 'Imputer / contrôler',
      hidden: (d) => !peutAgirSurDemande(d, deps.idUtilisateurCourant),
      onClick: (d) => deps.navigate(`/paiements/budget/${d.idDemandePaiement}/controle`),
    },
  ];
}

/** Actions ⋮ — file Budgets / Chargé DPM. */
export function buildBudgetDemandesRowActions(deps: {
  navigate: NavigateFunction;
  canReception: boolean;
  canControler: boolean;
  idUtilisateurCourant?: number | null;
  isRowBusy?: (idDemande: number) => boolean;
  onReceptionner: (idDemande: number) => void;
}): DataTableAction<DemandeRow>[] {
  return [
    {
      id: 'voir',
      label: 'Voir',
      onClick: (d) => deps.navigate(`/paiements/${d.idDemandePaiement}`),
    },
    {
      id: 'soumettre',
      label: 'Réceptionner',
      icon: <SendOutlinedIcon fontSize="small" />,
      hidden: (d) =>
        !deps.canReception ||
        !peutAgirSurDemande(d, deps.idUtilisateurCourant) ||
        normalizeStatutDpm(d.statut) !== 'SOUMISE',
      disabled: (d) => deps.isRowBusy?.(d.idDemandePaiement) ?? false,
      onClick: (d) => deps.onReceptionner(d.idDemandePaiement),
    },
    {
      id: 'traiter',
      label: 'Traitement DPM',
      hidden: (d) =>
        !deps.canControler ||
        !peutAgirSurDemande(d, deps.idUtilisateurCourant) ||
        normalizeStatutDpm(d.statut) !== 'EN_TRAITEMENT_DPM',
      onClick: (d) => deps.navigate(`/paiements/charge-dpm/${d.idDemandePaiement}`),
    },
    {
      id: 'valider',
      label: 'Contrôler',
      hidden: (d) =>
        !peutAgirSurDemande(d, deps.idUtilisateurCourant) ||
        normalizeStatutDpm(d.statut) !== 'EN_CONTROLE_BUDGETAIRE',
      onClick: (d) => deps.navigate(`/paiements/budget/${d.idDemandePaiement}/controle`),
    },
    {
      id: 'historique',
      label: 'Voir visa',
      hidden: (d) => normalizeStatutDpm(d.statut) !== 'VISEE_BUDGETAIREMENT',
      onClick: (d) => deps.navigate(`/paiements/budget/${d.idDemandePaiement}/controle`),
    },
  ];
}
