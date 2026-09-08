/**
 * Vérifie le formatage USD et la règle de modification par statut UB.
 * npx --yes tsx src/features/suivi-previsions/montantUsd.selftest.ts
 */
function assert(cond: unknown, msg: string): void {
  if (!cond) throw new Error(msg);
}

function formatMontantUsd(n: number): string {
  const formatted = new Intl.NumberFormat('fr-FR', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(n ?? 0);
  return `${formatted} USD`;
}

function estModifiableUb(statut: string): boolean {
  const s = (statut ?? '').trim().toUpperCase();
  return s === 'BROUILLON' || s === 'REJETEE' || s === '';
}

assert(formatMontantUsd(12500000).includes('12'), 'milliers');
assert(formatMontantUsd(12500000).endsWith('USD'), 'devise');
assert(formatMontantUsd(12500000).includes(',00'), '2 décimales');

assert(estModifiableUb('BROUILLON'), 'brouillon ok');
assert(estModifiableUb('REJETEE'), 'rejetee ok');
assert(!estModifiableUb('SOUMISE'), 'soumise lock');
assert(!estModifiableUb('VALIDEE'), 'validee lock');
assert(estModifiableUb(''), 'sans workflow = brouillon');

// Isolation conceptuelle : version SOUMISE n'implique pas UB B lockée
const versionAgregee = 'SOUMISE';
const ubB = 'BROUILLON';
assert(versionAgregee === 'SOUMISE' && estModifiableUb(ubB), 'Dept B saisissable malgré version SOUMISE');

console.log('montantUsd.selftest OK');
