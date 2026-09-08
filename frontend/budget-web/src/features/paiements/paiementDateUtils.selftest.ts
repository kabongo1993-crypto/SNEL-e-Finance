/**
 * Date locale DPM (filtres liste).
 * npx --yes tsx src/features/paiements/paiementDateUtils.selftest.ts
 */
import { todayLocalIsoDate } from './paiementDateUtils.ts';

function assert(cond: unknown, msg: string): void {
  if (!cond) throw new Error(msg);
}

const today = todayLocalIsoDate();
assert(/^\d{4}-\d{2}-\d{2}$/.test(today), 'format YYYY-MM-DD');

const now = new Date();
const expected = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`;
assert(today === expected, 'date locale du navigateur');

console.log('paiementDateUtils.selftest OK');
