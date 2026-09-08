/**
 * npx --yes tsx src/features/paiements/documentsEtablisUtils.selftest.ts
 */
import {
  defaultDocumentsEtablisPeriode,
  documentEtabliSelectionToken,
  documentsEtablisRowId,
  documentsEtablisSelectionParam,
  typeDocumentEtabliLabel,
} from './documentsEtablisUtils.ts';

function assert(cond: unknown, msg: string): void {
  if (!cond) throw new Error(msg);
}

assert(typeDocumentEtabliLabel('BILLET_CONVERSION') === 'Billet de conversion', 'libellé billet');
assert(typeDocumentEtabliLabel('PIECE_CAISSE') === 'Pièce de caisse', 'libellé pièce');
assert(typeDocumentEtabliLabel('BON_PROVISOIRE') === 'Bon provisoire', 'libellé bon');
assert(typeDocumentEtabliLabel('MINUTE_CHEQUE') === 'Minute de chèque', 'libellé minute');

const periode = defaultDocumentsEtablisPeriode(new Date(2026, 8, 5));
assert(periode.dateDebut === '2026-09-01', 'début du mois');
assert(periode.dateFin === '2026-09-05', 'aujourd hui');

assert(
  documentsEtablisRowId(431, 'BON_PROVISOIRE', 'BP-1') === '431-BON_PROVISOIRE-BP-1',
  'id ligne unique',
);
assert(
  documentEtabliSelectionToken({
    idDemandePaiement: 431,
    typeDocument: 'BON_PROVISOIRE',
    numeroDocument: 'BP-1',
  }) === '431|BON_PROVISOIRE|BP-1',
  'jeton sélection',
);
assert(
  documentsEtablisSelectionParam([
    { idDemandePaiement: 431, typeDocument: 'BON_PROVISOIRE', numeroDocument: 'BP-1' },
    { idDemandePaiement: 430, typeDocument: 'PIECE_CAISSE', numeroDocument: '' },
  ]) === '431|BON_PROVISOIRE|BP-1,430|PIECE_CAISSE|',
  'paramètre sélection',
);

console.log('documentsEtablisUtils.selftest OK');
