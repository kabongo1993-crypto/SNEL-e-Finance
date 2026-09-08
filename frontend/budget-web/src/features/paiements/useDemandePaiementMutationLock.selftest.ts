/**
 * Verrou mutations DPM + détection HTTP 409.
 * npx --yes tsx src/features/paiements/useDemandePaiementMutationLock.selftest.ts
 */
import { isApiConflictError } from './paiementUtils.ts';

function assert(cond: unknown, msg: string): void {
  if (!cond) throw new Error(msg);
}

assert(isApiConflictError({ response: { status: 409 } }), '409 axios shape');
assert(!isApiConflictError({ response: { status: 400 } }), 'not 409');
assert(!isApiConflictError(new Error('fail')), 'not axios');

let lock = false;
let calls = 0;

async function runMutation(action: () => Promise<number>): Promise<number | undefined> {
  if (lock) return undefined;
  lock = true;
  try {
    return await action();
  } finally {
    lock = false;
  }
}

void (async () => {
  const p1 = runMutation(async () => {
    calls += 1;
    await new Promise((r) => setTimeout(r, 20));
    return 1;
  });
  const p2 = runMutation(async () => {
    calls += 1;
    return 2;
  });
  const [r1, r2] = await Promise.all([p1, p2]);
  assert(r1 === 1, 'first mutation succeeds');
  assert(r2 === undefined, 'second mutation blocked');
  assert(calls === 1, 'single HTTP-equivalent call');
})();

console.log('useDemandePaiementMutationLock.selftest OK');
