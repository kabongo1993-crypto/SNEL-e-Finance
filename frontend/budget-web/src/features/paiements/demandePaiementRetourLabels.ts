import { normalizeStatutDpm } from './paiementUtils';

/** Contexte UI d'une action de retour métier (libellés uniquement — le backend tranche). */
export type RetourActionContext =
  | 'validation_n1'
  | 'validation_n2'
  | 'charge_dpm'
  | 'controle_junior'
  | 'controle_viseur';

export function labelRetourAction(context: RetourActionContext): string {
  switch (context) {
    case 'validation_n1':
      return 'Retour au demandeur';
    case 'validation_n2':
      return 'Retour au validateur N1';
    case 'charge_dpm':
      return 'Retour au demandeur';
    case 'controle_junior':
      return 'Retour au Chargé DP';
    case 'controle_viseur':
      return 'Retour au contrôle budgétaire';
    default:
      return 'Retour';
  }
}

export function labelRetourDialogTitle(context: RetourActionContext): string {
  switch (context) {
    case 'validation_n1':
      return 'Retour au demandeur';
    case 'validation_n2':
      return 'Retour au validateur N1';
    case 'charge_dpm':
      return 'Retour au demandeur';
    case 'controle_junior':
      return 'Retour au Chargé DP';
    case 'controle_viseur':
      return 'Retour au contrôle budgétaire';
    default:
      return 'Retour de la demande';
  }
}

export function labelRetourSuccess(context: RetourActionContext): string {
  switch (context) {
    case 'validation_n1':
      return 'Demande retournée au demandeur pour correction.';
    case 'validation_n2':
      return 'Demande retournée au validateur N1.';
    case 'charge_dpm':
      return 'Demande retournée au demandeur pour correction.';
    case 'controle_junior':
      return 'Demande retournée au Chargé DP.';
    case 'controle_viseur':
      return 'Demande retournée au contrôle budgétaire.';
    default:
      return 'Retour enregistré.';
  }
}

export function labelRetourDestinataireHint(
  context: RetourActionContext,
  nomDestinataire?: string | null,
): string | null {
  if (!nomDestinataire?.trim()) return null;
  const base = labelRetourAction(context);
  return `${base}\n→ ${nomDestinataire.trim()}`;
}

export function resolveRetourContextValidation(
  statut: string | null | undefined,
): RetourActionContext | null {
  const s = normalizeStatutDpm(statut);
  if (s === 'EN_VALIDATION_N1') return 'validation_n1';
  if (s === 'EN_VALIDATION_N2') return 'validation_n2';
  return null;
}

/** Heuristique UI contrôle budgétaire (sans assignation API). */
export function resolveRetourContextControle(input: {
  canControler: boolean;
  canViser: boolean;
  mode: 'junior' | 'viseur';
}): RetourActionContext | null {
  if (input.mode === 'viseur' && input.canViser) return 'controle_viseur';
  if (input.mode === 'junior' && input.canControler) return 'controle_junior';
  return null;
}

export function messageAlerteACorriger(): string {
  return 'Cette demande a été retournée au demandeur pour correction.';
}

export function labelTimelineRetourDemandeur(): string {
  return 'Retour au demandeur';
}
