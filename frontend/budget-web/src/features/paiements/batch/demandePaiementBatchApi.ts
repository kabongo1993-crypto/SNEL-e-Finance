import { apiClient } from '../../../services/apiClient';
import type { DemandePaiementBatchOperationRoute } from './demandePaiementBatchConfig';

export interface DeclarationValidationPhysiquePayload {
  nomSignataire: string;
  fonctionSignataire?: string | null;
  dateSignature: string;
  commentaire?: string | null;
}

/** Aligné sur TraitementChargeDpmRequest — taux toujours null côté client. */
export interface TraitementChargeDpmPayload {
  typeInstrument: string;
  devisePaiement?: string | null;
  tauxPaiement?: number | null;
  idTauxChangePaiement?: number | null;
  modePaiementSollicite?: string | null;
  typeBudgetSollicite?: string | null;
  itemSollicite?: string | null;
}

/** Aligné sur DemandePaiementBatchDocumentsRequest — DTO unitaires réutilisés. */
export interface DemandePaiementBatchDocumentsPayload {
  billet?: Record<string, unknown> | null;
  pieceCaisse?: { datePiece?: string | null } | null;
  bonProvisoire?: { dateBon?: string | null } | null;
  minuteCheque?: { dateDocument?: string | null } | null;
  typeInstrumentForce?: string | null;
}

/** Aligné sur OrienterDemandePaiementRequest — null cible = pool. */
export interface OrienterDemandePaiementPayload {
  idUtilisateurCible?: number | null;
}

export interface DemandePaiementBatchItemRequest {
  idDemandePaiement: number;
  validation?: { commentaire?: string | null } | null;
  declaration?: DeclarationValidationPhysiquePayload | null;
  traitement?: TraitementChargeDpmPayload | null;
  documents?: DemandePaiementBatchDocumentsPayload | null;
  /** Absent ou null = orientation pool (même sémantique backend). */
  orientation?: OrienterDemandePaiementPayload | null;
  /** Rejet validation entité — aligné sur RetourDemandePaiementRequest. */
  retour?: {
    motifRetour: string;
    commentaireRetour?: string | null;
    etapeConcernee?: string | null;
  } | null;
}

export interface DemandePaiementBatchRequest {
  statutFiltre: string;
  items: DemandePaiementBatchItemRequest[];
}

export interface DemandePaiementBatchItemResult {
  idDemandePaiement: number;
  reference: string | null;
  outcome: 'SUCCESS' | 'IGNORED' | 'ERROR' | string;
  codeErreur: string | null;
  message: string | null;
  statutAvant: string;
  statutApres: string | null;
}

export interface DemandePaiementBatchResult {
  operation: string;
  statutFiltre: string;
  correlationId: string;
  totalSelectionne: number;
  traitees: number;
  reussies: number;
  ignorees: number;
  erreurs: number;
  details: DemandePaiementBatchItemResult[];
}

export async function executerDemandePaiementBatch(
  operation: DemandePaiementBatchOperationRoute,
  body: DemandePaiementBatchRequest,
): Promise<DemandePaiementBatchResult> {
  if (!body.statutFiltre?.trim()) {
    throw new Error('Le filtre statut est obligatoire pour un traitement par lot.');
  }
  const response = await apiClient.post<DemandePaiementBatchResult>(
    `/api/v1/demandes-paiement/batch/${operation}`,
    body,
  );
  return response.data;
}
