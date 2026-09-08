export const INSTRUMENT_PAIEMENT_TYPES = [
  'PIECE_CAISSE',
  'BON_PROVISOIRE',
  'MINUTE_CHEQUE',
] as const;

export type InstrumentPaiementType = (typeof INSTRUMENT_PAIEMENT_TYPES)[number];

export const INSTRUMENT_PAIEMENT_LABELS: Record<InstrumentPaiementType, string> = {
  PIECE_CAISSE: 'Pièce de caisse',
  BON_PROVISOIRE: 'Bon provisoire',
  MINUTE_CHEQUE: 'Minute de chèque / O.P.',
};

export function emptyParametreForm(typeInstrument: InstrumentPaiementType) {
  return {
    typeInstrument,
    sr: '',
    comptabiliteGenerale: '',
    cp: '',
    cpa: '',
    compteGeneral: '',
    compteParticulier: '',
    cpCa: '',
    ls: '',
    suiviExtraComptable: '',
    montantSuiviExtraComptable: '',
    numeroAppariement: '',
    recuInstitutionnel: '',
    actif: true,
  };
}

export type ParametreInstrumentFormState = ReturnType<typeof emptyParametreForm>;

export function parametreDtoToForm(dto: {
  typeInstrument: InstrumentPaiementType;
  sr: string | null;
  comptabiliteGenerale: string | null;
  cp: string | null;
  cpa: string | null;
  compteGeneral: string | null;
  compteParticulier: string | null;
  cpCa: string | null;
  ls: string | null;
  suiviExtraComptable: string | null;
  montantSuiviExtraComptable: number | null;
  numeroAppariement: string | null;
  recuInstitutionnel: string | null;
  actif: boolean;
}): ParametreInstrumentFormState {
  return {
    typeInstrument: dto.typeInstrument,
    sr: dto.sr ?? '',
    comptabiliteGenerale: dto.comptabiliteGenerale ?? '',
    cp: dto.cp ?? '',
    cpa: dto.cpa ?? '',
    compteGeneral: dto.compteGeneral ?? '',
    compteParticulier: dto.compteParticulier ?? '',
    cpCa: dto.cpCa ?? '',
    ls: dto.ls ?? '',
    suiviExtraComptable: dto.suiviExtraComptable ?? '',
    montantSuiviExtraComptable:
      dto.montantSuiviExtraComptable != null ? String(dto.montantSuiviExtraComptable) : '',
    numeroAppariement: dto.numeroAppariement ?? '',
    recuInstitutionnel: dto.recuInstitutionnel ?? '',
    actif: dto.actif,
  };
}

export function formToUpsertPayload(form: ParametreInstrumentFormState) {
  const montant =
    form.montantSuiviExtraComptable.trim() === ''
      ? null
      : Number.parseFloat(form.montantSuiviExtraComptable.replace(',', '.'));

  return {
    sr: form.sr || null,
    comptabiliteGenerale: form.comptabiliteGenerale || null,
    cp: form.cp || null,
    cpa: form.cpa || null,
    compteGeneral: form.compteGeneral || null,
    compteParticulier: form.compteParticulier || null,
    cpCa: form.cpCa || null,
    ls: form.ls || null,
    suiviExtraComptable: form.suiviExtraComptable || null,
    montantSuiviExtraComptable: Number.isFinite(montant) ? montant : null,
    numeroAppariement: form.numeroAppariement || null,
    recuInstitutionnel: form.recuInstitutionnel || null,
    actif: form.actif,
  };
}
