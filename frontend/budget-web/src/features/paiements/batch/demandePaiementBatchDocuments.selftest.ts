/**
 * Dialogue batch « Établir les documents » — mode/instrument depuis la DPM.
 * npx --yes tsx src/features/paiements/batch/demandePaiementBatchDocuments.selftest.ts
 */
import { needsBilletConversion } from '../chargeDpmTauxUtils.ts';
import {
  DOCUMENTS_DIALOG_MODE_EDITABLE,
  buildDocumentsDraftFromDpm,
  instrumentLabel,
  instrumentsForMode,
  payloadHasCaisseInstrument,
  resolveInstrumentForMode,
  resolveModePaiementSollicite,
  toDocumentsPayload,
  type DocumentsRowSource,
} from './demandePaiementBatchDocuments.ts';

function assert(cond: unknown, msg: string): void {
  if (!cond) throw new Error(msg);
}

assert(!DOCUMENTS_DIALOG_MODE_EDITABLE, 'mode non éditable dans le dialogue documents');

assert(resolveModePaiementSollicite('BANQUE') === 'BANQUE', 'BANQUE reconnu');
assert(resolveModePaiementSollicite('banque') === 'BANQUE', 'banque normalisé');
assert(resolveModePaiementSollicite('CAISSE') === 'CAISSE', 'CAISSE reconnu');
assert(resolveModePaiementSollicite(null) === null, 'mode absent : pas de défaut CAISSE');
assert(resolveModePaiementSollicite('') === null, 'mode vide : pas de défaut CAISSE');
assert(resolveModePaiementSollicite('CHEQUE') === null, 'mode inconnu refusé');

assert(resolveInstrumentForMode('BANQUE', 'PIECE_CAISSE') === 'MINUTE_CHEQUE', 'BANQUE ignore PIECE_CAISSE');
assert(resolveInstrumentForMode('BANQUE', 'BON_PROVISOIRE') === 'MINUTE_CHEQUE', 'BANQUE ignore BON_PROVISOIRE');
assert(resolveInstrumentForMode('BANQUE', null) === 'MINUTE_CHEQUE', 'BANQUE → minute');
assert(resolveInstrumentForMode('CAISSE', 'BON_PROVISOIRE') === 'BON_PROVISOIRE', 'CAISSE conserve bon');
assert(resolveInstrumentForMode('CAISSE', 'MINUTE_CHEQUE') === 'PIECE_CAISSE', 'CAISSE refuse minute');
assert(resolveInstrumentForMode('CAISSE', null) === 'PIECE_CAISSE', 'CAISSE défaut pièce');

assert(
  instrumentsForMode('BANQUE').every((o) => o.value === 'MINUTE_CHEQUE'),
  'BANQUE : seul MINUTE_CHEQUE',
);
assert(
  instrumentsForMode('CAISSE').every((o) => o.value === 'PIECE_CAISSE' || o.value === 'BON_PROVISOIRE'),
  'CAISSE : pièce ou bon',
);

const caisseCdf: DocumentsRowSource = {
  idDemandePaiement: 431,
  reference: 'DP-2026-00431-CDF',
  deviseSollicitee: 'CDF',
  modePaiementSollicite: 'CAISSE',
  typeInstrumentPaiement: 'PIECE_CAISSE',
};
const draftCaisseCdf = buildDocumentsDraftFromDpm(caisseCdf)!;
assert(draftCaisseCdf.modePaiementSollicite === 'CAISSE', 'CAISSE+CDF : mode');
assert(draftCaisseCdf.typeInstrument === 'PIECE_CAISSE', 'CAISSE+CDF : pièce');
assert(!draftCaisseCdf.etablirBillet, 'CAISSE+CDF : billet non requis');
assert(instrumentLabel(draftCaisseCdf.typeInstrument) === 'Pièce de caisse', 'libellé pièce');
assert(!needsBilletConversion('CAISSE', 'CDF'), 'règle billet CDF');

const caisseUsd: DocumentsRowSource = {
  idDemandePaiement: 432,
  reference: 'DP-USD',
  deviseSollicitee: 'USD',
  modePaiementSollicite: 'CAISSE',
  typeInstrumentPaiement: 'PIECE_CAISSE',
};
const draftCaisseUsd = buildDocumentsDraftFromDpm(caisseUsd)!;
assert(draftCaisseUsd.modePaiementSollicite === 'CAISSE', 'CAISSE+USD : mode');
assert(draftCaisseUsd.typeInstrument === 'PIECE_CAISSE', 'CAISSE+USD : pièce');
assert(draftCaisseUsd.etablirBillet, 'CAISSE+USD : billet requis');

const caisseEur: DocumentsRowSource = {
  idDemandePaiement: 433,
  reference: 'DP-EUR',
  deviseSollicitee: 'EUR',
  modePaiementSollicite: 'CAISSE',
  typeInstrumentPaiement: null,
};
const draftCaisseEur = buildDocumentsDraftFromDpm(caisseEur)!;
assert(draftCaisseEur.modePaiementSollicite === 'CAISSE', 'CAISSE+EUR : mode');
assert(
  draftCaisseEur.typeInstrument === 'PIECE_CAISSE' || draftCaisseEur.typeInstrument === 'BON_PROVISOIRE',
  'CAISSE+EUR : instrument caisse autorisé',
);
assert(draftCaisseEur.etablirBillet === needsBilletConversion('CAISSE', 'EUR'), 'CAISSE+EUR : billet selon règle');

const banqueUsd: DocumentsRowSource = {
  idDemandePaiement: 434,
  reference: 'DP-2026-00431',
  deviseSollicitee: 'USD',
  modePaiementSollicite: 'BANQUE',
  typeInstrumentPaiement: 'MINUTE_CHEQUE',
};
const draftBanqueUsd = buildDocumentsDraftFromDpm(banqueUsd)!;
assert(draftBanqueUsd.modePaiementSollicite === 'BANQUE', 'BANQUE+USD : mode');
assert(draftBanqueUsd.typeInstrument === 'MINUTE_CHEQUE', 'BANQUE+USD : minute');
assert(!draftBanqueUsd.etablirBillet, 'BANQUE+USD : pas de billet');
assert(instrumentLabel(draftBanqueUsd.typeInstrument) === 'Minute de chèque', 'libellé minute');

const banqueCdf: DocumentsRowSource = {
  idDemandePaiement: 435,
  reference: 'DP-BANQUE-CDF',
  deviseSollicitee: 'CDF',
  modePaiementSollicite: 'BANQUE',
  typeInstrumentPaiement: null,
};
const draftBanqueCdf = buildDocumentsDraftFromDpm(banqueCdf)!;
assert(draftBanqueCdf.modePaiementSollicite === 'BANQUE', 'BANQUE+CDF : mode');
assert(draftBanqueCdf.typeInstrument === 'MINUTE_CHEQUE', 'BANQUE+CDF : minute même sans instrument stocké');
assert(!draftBanqueCdf.etablirBillet, 'BANQUE+CDF : pas de billet');

const payloadBanqueForce = toDocumentsPayload({
  modePaiementSollicite: 'BANQUE',
  typeInstrument: 'PIECE_CAISSE',
  etablirBillet: false,
});
assert(payloadBanqueForce.minuteCheque != null, 'BANQUE : payload minute');
assert(!payloadHasCaisseInstrument(payloadBanqueForce), 'BANQUE : jamais PIECE_CAISSE dans le payload');
assert(payloadBanqueForce.bonProvisoire == null, 'BANQUE : jamais BON_PROVISOIRE');

const payloadBanqueBon = toDocumentsPayload({
  modePaiementSollicite: 'BANQUE',
  typeInstrument: 'BON_PROVISOIRE',
  etablirBillet: true,
});
assert(payloadBanqueBon.minuteCheque != null, 'BANQUE+bon forcé : minute quand même');
assert(payloadBanqueBon.billet == null || payloadBanqueBon.minuteCheque != null, 'BANQUE ignore instrument caisse');
assert(!payloadHasCaisseInstrument(payloadBanqueBon), 'BANQUE+bon : pas d instrument caisse');

const lot: DocumentsRowSource[] = [
  caisseCdf,
  caisseUsd,
  banqueUsd,
  banqueCdf,
];
const lotPayloads = lot.map((row) => {
  const draft = buildDocumentsDraftFromDpm(row)!;
  return { row, draft, payload: toDocumentsPayload(draft) };
});
assert(lotPayloads.length === 4, 'lot hétérogène 4 DPM');
assert(lotPayloads[0]!.draft.modePaiementSollicite === 'CAISSE', 'lot DPM1 CAISSE');
assert(lotPayloads[1]!.draft.modePaiementSollicite === 'CAISSE', 'lot DPM2 CAISSE');
assert(lotPayloads[2]!.draft.modePaiementSollicite === 'BANQUE', 'lot DPM3 BANQUE');
assert(lotPayloads[3]!.draft.modePaiementSollicite === 'BANQUE', 'lot DPM4 BANQUE');
assert(lotPayloads[0]!.payload.pieceCaisse != null && lotPayloads[0]!.payload.billet == null, 'lot CDF pièce sans billet');
assert(lotPayloads[1]!.payload.pieceCaisse != null && lotPayloads[1]!.payload.billet != null, 'lot USD pièce + billet');
assert(lotPayloads[2]!.payload.minuteCheque != null && !payloadHasCaisseInstrument(lotPayloads[2]!.payload), 'lot BANQUE USD');
assert(lotPayloads[3]!.payload.minuteCheque != null && !payloadHasCaisseInstrument(lotPayloads[3]!.payload), 'lot BANQUE CDF');
assert(
  lotPayloads.every((item) => item.draft.modePaiementSollicite === resolveModePaiementSollicite(item.row.modePaiementSollicite)),
  'payload aligné sur le mode enregistré de chaque DPM',
);

const sansDefaut = buildDocumentsDraftFromDpm({
  idDemandePaiement: 1,
  reference: 'X',
  deviseSollicitee: 'USD',
  modePaiementSollicite: null,
});
assert(sansDefaut === null, 'pas d initialisation globale CAISSE');

console.log('demandePaiementBatchDocuments.selftest OK');
