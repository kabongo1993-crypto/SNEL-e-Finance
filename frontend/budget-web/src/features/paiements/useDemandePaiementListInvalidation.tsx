import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useRef,
  type ReactNode,
} from 'react';
import {
  notifyDemandePaiementListInvalidation,
  subscribeDemandePaiementListInvalidation,
} from './demandePaiementListInvalidationBus';

export { notifyDemandePaiementListInvalidation } from './demandePaiementListInvalidationBus';

const NotifyContext = createContext<(() => void) | null>(null);

export function DemandePaiementInvalidationProvider({ children }: { children: ReactNode }) {
  const notify = useCallback(() => {
    notifyDemandePaiementListInvalidation();
  }, []);

  return <NotifyContext.Provider value={notify}>{children}</NotifyContext.Provider>;
}

/** Hook pour déclencher l'invalidation après une mutation réussie. */
export function useNotifyDemandePaiementMutated(): () => void {
  const fromContext = useContext(NotifyContext);
  return fromContext ?? notifyDemandePaiementListInvalidation;
}

/**
 * Rafraîchit liste (+ compteurs) lorsqu'une mutation DPM réussit ailleurs dans l'app.
 * N'appelle pas `onInvalidate` au montage — uniquement sur événement.
 */
export function useDemandePaiementListInvalidation(onInvalidate: () => void | Promise<void>): void {
  const onInvalidateRef = useRef(onInvalidate);
  onInvalidateRef.current = onInvalidate;

  useEffect(() => {
    return subscribeDemandePaiementListInvalidation(() => {
      void onInvalidateRef.current();
    });
  }, []);
}
