/**
 * Bus d'invalidation listes/compteurs DPM.
 * npx --yes tsx src/features/paiements/demandePaiementListInvalidationBus.selftest.ts
 */
import {
  notifyDemandePaiementListInvalidation,
  resetDemandePaiementListInvalidationListeners,
  subscribeDemandePaiementListInvalidation,
} from './demandePaiementListInvalidationBus.ts';

function assert(cond: unknown, msg: string): void {
  if (!cond) throw new Error(msg);
}

resetDemandePaiementListInvalidationListeners();

let calls = 0;
subscribeDemandePaiementListInvalidation(() => {
  calls += 1;
});

assert(calls === 0, 'pas de refresh au subscribe');

notifyDemandePaiementListInvalidation();
assert(calls === 1, 'mutation réussie → 1 invalidation');

notifyDemandePaiementListInvalidation();
assert(calls === 2, 'deuxième mutation → 2 invalidations');

let failedCalls = 0;
subscribeDemandePaiementListInvalidation(() => {
  failedCalls += 1;
});

// Simule échec mutation : pas de notify
assert(failedCalls === 0, 'mutation échouée → pas de notify');

resetDemandePaiementListInvalidationListeners();
assert(true, 'reset OK');

console.log('demandePaiementListInvalidationBus.selftest OK');
