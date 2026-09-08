import type { DemandePaiementDetail, DemandePaiementListItem } from '../../services/apiClient';
import type { DemandeAssignationView } from './demandePaiementAssignationUtils';

export type AssignationApiFields = Pick<
  DemandePaiementListItem,
  'idUtilisateurAssigne' | 'nomUtilisateurAssigne' | 'prenomUtilisateurAssigne'
>;

export function buildAssignationView(
  fields: AssignationApiFields,
  idUtilisateurCourant: number | null | undefined,
): DemandeAssignationView | null {
  if (idUtilisateurCourant == null || Number.isNaN(idUtilisateurCourant)) return null;

  return {
    idUtilisateurAssigne: fields.idUtilisateurAssigne ?? null,
    nomUtilisateurAssigne: fields.nomUtilisateurAssigne ?? null,
    prenomUtilisateurAssigne: fields.prenomUtilisateurAssigne ?? null,
    idUtilisateurCourant,
  };
}

export function buildAssignationViewFromDetail(
  demande: Pick<
    DemandePaiementDetail,
    'idUtilisateurAssigne' | 'nomUtilisateurAssigne' | 'prenomUtilisateurAssigne'
  >,
  idUtilisateurCourant: number | null | undefined,
): DemandeAssignationView | null {
  return buildAssignationView(demande, idUtilisateurCourant);
}
