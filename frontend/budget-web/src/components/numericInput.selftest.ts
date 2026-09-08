import {
  parseMontantInput,
  parseNumericInput,
  sanitizeNumericInput,
} from './numericInput';

function assert(cond: boolean, msg: string) {
  if (!cond) throw new Error(`numericInput.selftest: ${msg}`);
}

assert(sanitizeNumericInput('ABC') === '', 'refuse lettres seules');
assert(sanitizeNumericInput('12A5') === '125', 'retire lettres au milieu');
assert(sanitizeNumericInput('15ABC') === '15', 'retire suffixe lettres');
assert(sanitizeNumericInput('TEST') === '', 'refuse TEST');
assert(sanitizeNumericInput('1A000') === '1000', '1A000 → 1000');
assert(sanitizeNumericInput('15000') === '15000', 'entiers OK');
assert(sanitizeNumericInput('15000,50') === '15000,50', 'virgule FR OK');
assert(sanitizeNumericInput('15000.50') === '15000.50', 'point OK');
assert(sanitizeNumericInput('12,3,4') === '12,34', 'un seul séparateur');
assert(sanitizeNumericInput('-10') === '10', 'négatif refusé par défaut');
assert(sanitizeNumericInput('-10', { allowNegative: true }) === '-10', 'négatif autorisé');
assert(sanitizeNumericInput('1,23456789', { maxDecimals: 2 }) === '1,23', 'max décimales');
assert(sanitizeNumericInput('1 200,5') === '1 200,5', 'espaces milliers OK');

assert(parseNumericInput('ABC') === null, 'parse ABC null');
assert(parseNumericInput('15000,50') === 15000.5, 'parse FR');
assert(parseNumericInput('1 200.5') === 1200.5, 'parse espaces + point');
assert(parseNumericInput('') === null, 'vide → null');
assert(parseMontantInput('') === 0, 'montant vide → 0');
assert(parseMontantInput('-10') === null, 'montant négatif null');
assert(parseMontantInput('1 200,5') === 1200.5, 'montant FR');

console.log('numericInput.selftest: OK');
