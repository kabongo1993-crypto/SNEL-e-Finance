/**
 * Self-test — documents workflow (références / libellés).
 * Run: npx tsx src/features/documents-previsions/documentUtils.selftest.ts
 */
import {
  documentActionTitle,
  documentViewLabel,
  formatDocumentShortRef,
  historiqueActionHasDocument,
} from './documentUtils';

function assert(cond: boolean, msg: string) {
  if (!cond) throw new Error(msg);
}

assert(
  formatDocumentShortRef('SNEL/DFI/BUD/2026/V04/SUB/00015') === 'SUB-00015',
  'short ref SUB',
);
assert(
  formatDocumentShortRef('SNEL/DFI/BUD/2026/V04/VAL/00015') === 'VAL-00015',
  'short ref VAL',
);
assert(documentActionTitle('SUB') === 'Prévision soumise', 'title SUB');
assert(documentActionTitle('REJ') === 'Prévision rejetée', 'title REJ');
assert(documentViewLabel('CTL') === 'Voir la fiche de contrôle', 'view CTL');
assert(historiqueActionHasDocument('SOUMETTRE_UB'), 'has doc soumettre');
assert(historiqueActionHasDocument('VALIDATION_DEPARTEMENT'), 'has doc validation dept');
assert(!historiqueActionHasDocument('REOUVRIR_UB'), 'no doc reouvrir');

console.log('documentUtils.selftest: OK');
