/**
 * Hook compteurs : deps sans statut, refresh indépendant.
 * npx --yes tsx src/features/paiements/useDemandePaiementCompteurs.selftest.ts
 */
import type { DemandePaiementCompteursParams } from './useDemandePaiementCompteurs.ts';
import { parseMesDemandesUrlFilters, toCompteursApiParams } from './demandePaiementUrlFilters.ts';

function assert(cond: unknown, msg: string): void {
  if (!cond) throw new Error(msg);
}

function buildParams(input: {
  dateDebut?: string;
  statut?: string;
}): DemandePaiementCompteursParams {
  return {
    scope: 'mes-demandes',
    dateDebut: input.dateDebut,
  };
}

const a = buildParams({ dateDebut: '2026-03-01', statut: 'BROUILLON' });
const b = buildParams({ dateDebut: '2026-03-01', statut: 'SOUMISE' });
assert(JSON.stringify(a) === JSON.stringify(b), 'statut seul ne change pas les params compteurs');

const c = buildParams({ dateDebut: '2026-03-01' });
const d = buildParams({ dateDebut: '2026-03-02' });
assert(JSON.stringify(c) !== JSON.stringify(d), 'changement date change les params');

const urlFilters = parseMesDemandesUrlFilters(
  new URLSearchParams('statut=SOUMISE&idExercice=1'),
  { today: '2026-08-31', allowedStatuts: ['SOUMISE', 'BROUILLON'] },
);
const fromUrl = toCompteursApiParams(urlFilters);
assert(fromUrl.idExercice === 1, 'compteurs URL idExercice');
assert(!('statut' in fromUrl), 'compteurs URL sans statut');

console.log('useDemandePaiementCompteurs.selftest.ts OK');
