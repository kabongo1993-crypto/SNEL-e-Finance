type InvalidationListener = () => void;

const listeners = new Set<InvalidationListener>();

/** Notifie les listes/compteurs DPM qu'une mutation de statut a réussi. */
export function notifyDemandePaiementListInvalidation(): void {
  listeners.forEach((listener) => listener());
}

export function subscribeDemandePaiementListInvalidation(
  listener: InvalidationListener,
): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

/** Réinitialise les abonnés — usage tests uniquement. */
export function resetDemandePaiementListInvalidationListeners(): void {
  listeners.clear();
}
