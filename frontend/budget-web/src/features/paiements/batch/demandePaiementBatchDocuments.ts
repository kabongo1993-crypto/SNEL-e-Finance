import type { DemandePaiementBatchDocumentsPayload } from './demandePaiementBatchApi';
import { needsBilletConversion } from '../chargeDpmTauxUtils';

export type ModePaiementDocuments = 'CAISSE' | 'BANQUE';
export type InstrumentDocuments = 'PIECE_CAISSE' | 'BON_PROVISOIRE' | 'MINUTE_CHEQUE';

export interface DocumentsRowSource {
  idDemandePaiement: number;
  reference: string;
  deviseSollicitee: string;
  modePaiementSollicite?: string | null;
  typeInstrumentPaiement?: string | null;
}

export interface DocumentsDraft {
  modePaiementSollicite: ModePaiementDocuments;
  typeInstrument: InstrumentDocuments;
  etablirBillet: boolean;
}

/** Le mode n'est pas éditable dans « Établir les documents » — traité ailleurs. */
export const DOCUMENTS_DIALOG_MODE_EDITABLE = false;

export function resolveModePaiementSollicite(
  raw: string | null | undefined,
): ModePaiementDocuments | null {
  const mode = (raw ?? '').trim().toUpperCase();
  if (mode === 'BANQUE') return 'BANQUE';
  if (mode === 'CAISSE') return 'CAISSE';
  return null;
}

export function instrumentsForMode(
  mode: ModePaiementDocuments,
): { value: InstrumentDocuments; label: string }[] {
  if (mode === 'BANQUE') {
    return [{ value: 'MINUTE_CHEQUE', label: 'Minute de chèque' }];
  }
  return [
    { value: 'PIECE_CAISSE', label: 'Pièce de caisse' },
    { value: 'BON_PROVISOIRE', label: 'Bon provisoire' },
  ];
}

export function resolveInstrumentForMode(
  mode: ModePaiementDocuments,
  typeInstrumentPaiement?: string | null,
): InstrumentDocuments {
  if (mode === 'BANQUE') return 'MINUTE_CHEQUE';
  const inst = (typeInstrumentPaiement ?? '').trim().toUpperCase();
  if (inst === 'BON_PROVISOIRE') return 'BON_PROVISOIRE';
  return 'PIECE_CAISSE';
}

export function instrumentLabel(code: string): string {
  switch (code) {
    case 'BON_PROVISOIRE':
      return 'Bon provisoire';
    case 'MINUTE_CHEQUE':
      return 'Minute de chèque';
    default:
      return 'Pièce de caisse';
  }
}

export function buildDocumentsDraftFromDpm(row: DocumentsRowSource): DocumentsDraft | null {
  const mode = resolveModePaiementSollicite(row.modePaiementSollicite);
  if (!mode) return null;
  const typeInstrument = resolveInstrumentForMode(mode, row.typeInstrumentPaiement);
  return {
    modePaiementSollicite: mode,
    typeInstrument,
    etablirBillet: needsBilletConversion(mode, row.deviseSollicitee),
  };
}

export function toDocumentsPayload(draft: DocumentsDraft): DemandePaiementBatchDocumentsPayload {
  const payload: DemandePaiementBatchDocumentsPayload = {
    billet: draft.etablirBillet ? {} : null,
    pieceCaisse: null,
    bonProvisoire: null,
    minuteCheque: null,
    typeInstrumentForce: null,
  };

  if (draft.modePaiementSollicite === 'BANQUE') {
    payload.minuteCheque = {};
    return payload;
  }

  if (draft.typeInstrument === 'BON_PROVISOIRE') {
    payload.bonProvisoire = {};
    return payload;
  }

  payload.pieceCaisse = {};
  return payload;
}

export function payloadHasCaisseInstrument(payload: DemandePaiementBatchDocumentsPayload): boolean {
  return payload.pieceCaisse != null || payload.bonProvisoire != null;
}
