/**
 * Comportement selection (sans React) — miroir de useDemandePaiementBatchSelection.
 * npx --yes tsx src/features/paiements/batch/demandePaiementBatchSelection.selftest.ts
 */
import { isBatchSelectableStatut, type DemandePaiementBatchScope } from './demandePaiementBatchConfig.ts';

function assert(cond: unknown, msg: string): void {
  if (!cond) throw new Error(msg);
}

type Statut = '' | 'BROUILLON' | 'EN_VALIDATION_N1' | 'SOUMISE' | 'EN_TRAITEMENT_DPM';

function simulate(statut: Statut, scope: DemandePaiementBatchScope = 'mes-demandes') {
  let selected = new Set<number>();
  let current = statut;
  const api = {
    get enabled() {
      return isBatchSelectableStatut(current, scope);
    },
    get selectedSize() {
      return selected.size;
    },
    add(id: number) {
      if (!api.enabled) return;
      selected.add(id);
    },
    clear() {
      selected = new Set();
    },
    changeStatut(next: Statut) {
      current = next;
      selected = new Set();
    },
    showActionBar() {
      return api.enabled && selected.size > 0;
    },
  };
  return api;
}

const toutes = simulate('');
assert(!toutes.enabled, 'Toutes -> checkbox off');

const page = simulate('BROUILLON');
assert(page.enabled, 'BROUILLON -> checkbox on');
page.add(1);
assert(page.showActionBar(), 'selection -> barre visible');
page.changeStatut('EN_VALIDATION_N1');
assert(page.selectedSize === 0, 'changement filtre -> selection videe');

const charge = simulate('SOUMISE', 'charge-dpm');
charge.add(101);
charge.changeStatut('EN_TRAITEMENT_DPM');
assert(charge.enabled, 'charge EN_TRAITEMENT -> selectable');
assert(charge.selectedSize === 0, 'SOUMISE -> EN_TRAITEMENT vide selection');
charge.add(201);
charge.changeStatut('SOUMISE');
assert(charge.enabled, 'retour SOUMISE selectable');
assert(charge.selectedSize === 0, 'EN_TRAITEMENT -> SOUMISE vide selection');
charge.add(301);
charge.changeStatut('');
assert(!charge.enabled, 'Toutes -> off');
assert(charge.selectedSize === 0, 'filtre Toutes vide selection');

console.log('demandePaiementBatchSelection.selftest OK');
