import type { StatutNavValue } from '../paiementStatutNavConfig';
import {
  canChargeDpm,
  canDeclarerValidationPhysiquePaiements,
  canEnvoyerValidationPaiements,
  canEcrirePaiements,
  canRejeterValidationEntitePaiements,
  canSoumettrePaiements,
  canValiderN1Paiements,
  canValiderN2Paiements,
} from '../paiementUtils';

export type DemandePaiementBatchScope = 'mes-demandes' | 'charge-dpm';

export type DemandePaiementBatchOperationRoute =
  | 'envoyer-validation'
  | 'valider-n1'
  | 'valider-n2'
  | 'soumettre-budget'
  | 'declarer-validation-physique-n1'
  | 'declarer-validation-physique-n2'
  | 'receptionner'
  | 'traiter-charge'
  | 'etablir-documents'
  | 'orienter'
  | 'supprimer'
  | 'rejeter-validation-n1'
  | 'rejeter-validation-n2';

export interface DemandePaiementBatchOpDef {
  operation: DemandePaiementBatchOperationRoute;
  label: string;
  permission: string;
  /** Opérations physiques : payload Declaration obligatoire par DPM. */
  requiresDeclaration?: boolean;
  /** Phase 3B : payload TraitementCharge obligatoire par DPM. */
  requiresTraitement?: boolean;
  /** Phase 3C : payload Documents obligatoire par DPM. */
  requiresDocuments?: boolean;
  /** Phase 3D : dialogue orientation (payload optionnel = pool). */
  requiresOrientation?: boolean;
  /** Rejet validation entité : motif de retour obligatoire. */
  requiresRetour?: boolean;
  canExecute: (user: { permissions?: string[]; roles?: string[] } | null | undefined) => boolean;
}

/** Aligné sur DemandePaiementBatchOperationRules.MaxItems. */
export const DEMANDE_PAIEMENT_BATCH_MAX_ITEMS = 100;

const MES_DEMANDES_BATCH_OPS: Partial<
  Record<Exclude<StatutNavValue, ''>, DemandePaiementBatchOpDef[]>
> = {
  BROUILLON: [
    {
      operation: 'envoyer-validation',
      label: 'Envoyer en validation',
      permission: 'paiements.envoyer_validation',
      canExecute: canEnvoyerValidationPaiements,
    },
    {
      operation: 'supprimer',
      label: 'Supprimer définitivement',
      permission: 'paiements.ecrire',
      canExecute: canEcrirePaiements,
    },
  ],
  EN_VALIDATION_N1: [
    {
      operation: 'valider-n1',
      label: 'Valider N1',
      permission: 'paiements.valider_n1',
      canExecute: canValiderN1Paiements,
    },
    {
      operation: 'declarer-validation-physique-n1',
      label: 'Déclarer validation physique N1',
      permission: 'paiements.declarer_validation_physique',
      requiresDeclaration: true,
      canExecute: canDeclarerValidationPhysiquePaiements,
    },
    {
      operation: 'rejeter-validation-n1',
      label: 'Rejeter (retour au demandeur)',
      permission: 'paiements.rejeter_validation_entite',
      requiresRetour: true,
      canExecute: (u) =>
        canRejeterValidationEntitePaiements(u) && canValiderN1Paiements(u),
    },
  ],
  EN_VALIDATION_N2: [
    {
      operation: 'valider-n2',
      label: 'Valider N2',
      permission: 'paiements.valider_n2',
      canExecute: canValiderN2Paiements,
    },
    {
      operation: 'declarer-validation-physique-n2',
      label: 'Déclarer validation physique N2',
      permission: 'paiements.declarer_validation_physique',
      requiresDeclaration: true,
      canExecute: canDeclarerValidationPhysiquePaiements,
    },
    {
      operation: 'rejeter-validation-n2',
      label: 'Rejeter (retour N1)',
      permission: 'paiements.rejeter_validation_entite',
      requiresRetour: true,
      canExecute: (u) =>
        canRejeterValidationEntitePaiements(u) && canValiderN2Paiements(u),
    },
  ],
  VALIDEE_ENTITE: [
    {
      operation: 'soumettre-budget',
      label: 'Soumettre au Budget',
      permission: 'paiements.soumettre',
      canExecute: canSoumettrePaiements,
    },
  ],
};

/** File Charge DPM — Phase 3A–3D. */
const CHARGE_DPM_BATCH_OPS: Partial<
  Record<Exclude<StatutNavValue, ''>, DemandePaiementBatchOpDef[]>
> = {
  SOUMISE: [
    {
      operation: 'receptionner',
      label: 'Réceptionner les DPM sélectionnées',
      permission: 'paiements.charge_dpm',
      canExecute: canChargeDpm,
    },
  ],
  EN_TRAITEMENT_DPM: [
    {
      operation: 'etablir-documents',
      label: 'Établir les documents',
      permission: 'paiements.charge_dpm',
      requiresDocuments: true,
      canExecute: canChargeDpm,
    },
    {
      operation: 'traiter-charge',
      label: 'Traiter la charge des DPM sélectionnées',
      permission: 'paiements.charge_dpm',
      requiresTraitement: true,
      canExecute: canChargeDpm,
    },
    {
      operation: 'orienter',
      label: 'Orienter les DPM sélectionnées',
      permission: 'paiements.charge_dpm',
      requiresOrientation: true,
      canExecute: canChargeDpm,
    },
  ],
};

function opsMapForScope(scope: DemandePaiementBatchScope) {
  return scope === 'charge-dpm' ? CHARGE_DPM_BATCH_OPS : MES_DEMANDES_BATCH_OPS;
}

export function isBatchSelectableStatut(
  statutFiltre: StatutNavValue,
  scope: DemandePaiementBatchScope = 'mes-demandes',
): boolean {
  return statutFiltre !== '' && (opsMapForScope(scope)[statutFiltre]?.length ?? 0) > 0;
}

export function getBatchOpsForStatut(
  statutFiltre: StatutNavValue,
  user: { permissions?: string[]; roles?: string[] } | null | undefined,
  scope: DemandePaiementBatchScope = 'mes-demandes',
): DemandePaiementBatchOpDef[] {
  if (statutFiltre === '') return [];
  const ops = opsMapForScope(scope)[statutFiltre] ?? [];
  return ops.filter((op) => op.canExecute(user));
}

export function getBatchOpLabel(operation: DemandePaiementBatchOperationRoute): string {
  for (const map of [MES_DEMANDES_BATCH_OPS, CHARGE_DPM_BATCH_OPS]) {
    for (const ops of Object.values(map)) {
      const found = ops?.find((o) => o.operation === operation);
      if (found) return found.label;
    }
  }
  return operation;
}

export function operationRequiresDeclaration(
  operation: DemandePaiementBatchOperationRoute | null | undefined,
): boolean {
  if (!operation) return false;
  for (const map of [MES_DEMANDES_BATCH_OPS, CHARGE_DPM_BATCH_OPS]) {
    for (const ops of Object.values(map)) {
      const found = ops?.find((o) => o.operation === operation);
      if (found) return Boolean(found.requiresDeclaration);
    }
  }
  return false;
}

export function operationRequiresTraitement(
  operation: DemandePaiementBatchOperationRoute | null | undefined,
): boolean {
  if (!operation) return false;
  for (const map of [MES_DEMANDES_BATCH_OPS, CHARGE_DPM_BATCH_OPS]) {
    for (const ops of Object.values(map)) {
      const found = ops?.find((o) => o.operation === operation);
      if (found) return Boolean(found.requiresTraitement);
    }
  }
  return false;
}

export function operationRequiresDocuments(
  operation: DemandePaiementBatchOperationRoute | null | undefined,
): boolean {
  if (!operation) return false;
  for (const map of [MES_DEMANDES_BATCH_OPS, CHARGE_DPM_BATCH_OPS]) {
    for (const ops of Object.values(map)) {
      const found = ops?.find((o) => o.operation === operation);
      if (found) return Boolean(found.requiresDocuments);
    }
  }
  return false;
}

export function operationRequiresOrientation(
  operation: DemandePaiementBatchOperationRoute | null | undefined,
): boolean {
  if (!operation) return false;
  for (const map of [MES_DEMANDES_BATCH_OPS, CHARGE_DPM_BATCH_OPS]) {
    for (const ops of Object.values(map)) {
      const found = ops?.find((o) => o.operation === operation);
      if (found) return Boolean(found.requiresOrientation);
    }
  }
  return false;
}

export function operationRequiresRetour(
  operation: DemandePaiementBatchOperationRoute | null | undefined,
): boolean {
  if (!operation) return false;
  for (const map of [MES_DEMANDES_BATCH_OPS, CHARGE_DPM_BATCH_OPS]) {
    for (const ops of Object.values(map)) {
      const found = ops?.find((o) => o.operation === operation);
      if (found) return Boolean(found.requiresRetour);
    }
  }
  return false;
}
