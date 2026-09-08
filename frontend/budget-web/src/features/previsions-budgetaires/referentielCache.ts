/** Cache session des référentiels stables pour /budget/previsions (évite rechargements SQL inutiles). */

type CacheEntry<T> = { at: number; data: T };

const TTL_MS = 5 * 60 * 1000;
const store = new Map<string, CacheEntry<unknown>>();

export async function getCachedReferentiel<T>(
  key: string,
  loader: () => Promise<T>,
  ttlMs = TTL_MS,
): Promise<T> {
  const hit = store.get(key) as CacheEntry<T> | undefined;
  if (hit && Date.now() - hit.at < ttlMs) {
    return hit.data;
  }
  const data = await loader();
  store.set(key, { at: Date.now(), data });
  return data;
}

export function invalidatePrevisionsReferentielCache(key?: string) {
  if (key) store.delete(key);
  else store.clear();
}
