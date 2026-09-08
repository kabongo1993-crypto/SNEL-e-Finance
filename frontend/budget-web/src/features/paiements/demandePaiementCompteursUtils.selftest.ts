/**
 * Mapping DTO compteurs → chips navigation.
 * npx --yes tsx src/features/paiements/demandePaiementCompteursUtils.selftest.ts
 */
import {
  applyBudgetFileCompteurs,
  countForStatutNavValue,
  formatStatutNavChipLabel,
} from './demandePaiementCompteursUtils.ts';
import type { DemandePaiementCompteursDto } from '../../services/apiClient.ts';

function assert(cond: unknown, msg: string): void {
  if (!cond) throw new Error(msg);
}

const sample: DemandePaiementCompteursDto = {
  total: 10,
  brouillon: 2,
  enValidationN1: 1,
  enValidationN2: 0,
  valideeEntite: 0,
  soumise: 3,
  enTraitementDpm: 2,
  enControleBudgetaire: 1,
  aCorriger: 1,
  viseeBudgetairement: 0,
};

assert(countForStatutNavValue(sample, '') === 10, 'total Toutes');
assert(countForStatutNavValue(sample, 'BROUILLON') === 2, 'brouillon');
assert(countForStatutNavValue(sample, 'SOUMISE') === 3, 'soumise');
assert(countForStatutNavValue(sample, 'EN_TRAITEMENT_DPM') === 2, 'traitement DPM');
assert(countForStatutNavValue(null, 'BROUILLON') === undefined, 'null → undefined');

const fileApplied = applyBudgetFileCompteurs(sample, 'FILE');
assert(fileApplied.total === 7, 'FILE total = somme statuts file (3+2+1+1)');
assert(fileApplied.viseeBudgetairement === 0, 'FILE masque visées');
assert(applyBudgetFileCompteurs(sample, 'TOUTES').total === 10, 'TOUTES conserve total API');

assert(formatStatutNavChipLabel('Brouillons', 4, false) === 'Brouillons (4)', 'label avec count');
assert(formatStatutNavChipLabel('Brouillons', undefined, true) === 'Brouillons (—)', 'loading placeholder');
assert(formatStatutNavChipLabel('Brouillons', undefined, false) === 'Brouillons', 'sans count');

console.log('demandePaiementCompteursUtils.selftest.ts OK');
