import type { ConversionResult, TauxChangeApplicable } from '../../services/apiClient';
import { todayLocalIsoDate } from './paiementDateUtils';

export type ChargeDpmTauxState =
  | { status: 'idle' }
  | { status: 'loading' }
  | { status: 'identity'; taux: 1 }
  | { status: 'found'; applicable: TauxChangeApplicable; conversion: ConversionResult }
  | { status: 'not_found'; deviseSource: string; deviseCible: string; dateReference: string }
  | { status: 'error'; message: string };

export function normaliserDeviseCode(code: string): string {
  return code.trim().toUpperCase();
}

/** Date métier DPM : jour du traitement (aujourd'hui, fuseau local). */
export function dateTraitementDpmReference(): string {
  return todayLocalIsoDate();
}

/** @deprecated Utiliser dateTraitementDpmReference pour le Charge DPM. */
export function extractDateReference(dateEmission: string | null | undefined): string | null {
  if (!dateEmission?.trim()) return null;
  return dateEmission.trim().slice(0, 10);
}

export function needsTauxConversion(deviseSource: string, deviseCible: string): boolean {
  return normaliserDeviseCode(deviseSource) !== normaliserDeviseCode(deviseCible);
}

/** Billet requis : mode CAISSE et devise de la demande ≠ CDF. */
export function needsBilletConversion(
  modePaiement: string,
  deviseDemande: string,
): boolean {
  if (normaliserDeviseCode(modePaiement) !== 'CAISSE') return false;
  return normaliserDeviseCode(deviseDemande) !== 'CDF';
}

export function canSubmitChargeTraitement(tauxState: ChargeDpmTauxState, conversionNeeded: boolean): boolean {
  if (!conversionNeeded) return true;
  if (tauxState.status === 'identity' || tauxState.status === 'found') return true;
  return false;
}

export function isTauxStateStale(
  state: ChargeDpmTauxState,
  deviseSource: string,
  deviseCible: string,
  dateReference: string,
): boolean {
  if (state.status === 'not_found') {
    return (
      state.deviseSource !== normaliserDeviseCode(deviseSource)
      || state.deviseCible !== normaliserDeviseCode(deviseCible)
      || state.dateReference !== dateReference
    );
  }
  if (state.status === 'found') {
    const a = state.applicable;
    return (
      normaliserDeviseCode(a.deviseSource) !== normaliserDeviseCode(deviseSource)
      || normaliserDeviseCode(a.deviseCible) !== normaliserDeviseCode(deviseCible)
      || a.dateEffet.slice(0, 10) > dateReference
    );
  }
  return false;
}
