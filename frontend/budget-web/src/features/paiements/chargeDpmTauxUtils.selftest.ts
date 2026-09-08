/**
 * Self-test utilitaires taux Charge DP.
 * Exécuter : npx --yes tsx src/features/paiements/chargeDpmTauxUtils.selftest.ts
 */
import {
  canSubmitChargeTraitement,
  dateTraitementDpmReference,
  extractDateReference,
  needsBilletConversion,
  needsTauxConversion,
  type ChargeDpmTauxState,
} from './chargeDpmTauxUtils';

function assert(cond: boolean, msg: string) {
  if (!cond) throw new Error(msg);
}

const today = dateTraitementDpmReference();
assert(/^\d{4}-\d{2}-\d{2}$/.test(today), 'date traitement iso');

assert(extractDateReference('2026-08-27T00:00:00') === '2026-08-27', 'date ref emission legacy');
assert(extractDateReference('2026-09-05') === '2026-09-05', 'date ref iso');
assert(extractDateReference('') === null, 'date ref empty');

assert(needsTauxConversion('USD', 'CDF'), 'USD CDF needs conversion');
assert(!needsTauxConversion('USD', 'USD'), 'USD USD identity');
assert(!needsTauxConversion('usd', 'USD'), 'case insensitive');

assert(needsBilletConversion('CAISSE', 'USD'), 'USD CAISSE needs billet');
assert(needsBilletConversion('CAISSE', 'EUR'), 'EUR CAISSE needs billet');
assert(!needsBilletConversion('CAISSE', 'CDF'), 'CDF CAISSE no billet');
assert(!needsBilletConversion('BANQUE', 'USD'), 'USD BANQUE no billet');
assert(!needsBilletConversion('BANQUE', 'EUR'), 'EUR BANQUE no billet');
assert(!needsBilletConversion('BANQUE', 'CDF'), 'CDF BANQUE no billet');

const identity: ChargeDpmTauxState = { status: 'identity', taux: 1 };
const notFound: ChargeDpmTauxState = {
  status: 'not_found',
  deviseSource: 'USD',
  deviseCible: 'CDF',
  dateReference: '2026-08-27',
};
const loading: ChargeDpmTauxState = { status: 'loading' };

assert(canSubmitChargeTraitement(identity, false), 'same devise submit');
assert(canSubmitChargeTraitement(identity, true), 'identity submit with conversion flag');
assert(!canSubmitChargeTraitement(notFound, true), 'not found blocks');
assert(!canSubmitChargeTraitement(loading, true), 'loading blocks');

console.log('chargeDpmTauxUtils.selftest OK');
