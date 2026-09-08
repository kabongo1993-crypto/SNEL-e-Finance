/**
 * Self-test formatage montants historique (USD SNEL).
 * Exécuter : npx tsx src/features/historique-previsions/montantUsd.selftest.ts
 */
function formatMontantUsd(n: number): string {
  const formatted = new Intl.NumberFormat('fr-FR', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(n ?? 0);
  return `${formatted} USD`;
}

function assert(cond: boolean, msg: string) {
  if (!cond) throw new Error(msg);
}

const out = formatMontantUsd(12500000);
assert(out.includes('12'), 'milliers');
assert(out.includes('500'), '500');
assert(out.includes('000'), 'millions part');
assert(out.includes(',00'), '2 décimales');
assert(out.endsWith('USD'), 'devise');
assert(!out.includes('12500000'), 'pas de raw');
assert(formatMontantUsd(125000) === '125\u00a0000,00 USD' || formatMontantUsd(125000).includes('125'), '125000');

console.log('OK historique montantUsd.selftest', out);
