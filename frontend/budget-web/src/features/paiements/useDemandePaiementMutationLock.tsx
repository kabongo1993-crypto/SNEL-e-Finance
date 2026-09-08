import axios from 'axios';

import {

  createContext,

  useCallback,

  useContext,

  useMemo,

  useRef,

  useState,

  type ReactNode,

} from 'react';

import { GlobalOperationOverlay, DEFAULT_OPERATION_OVERLAY_MESSAGE } from '../../components/GlobalOperationOverlay';

import { apiErrorMessage, isApiConflictError } from './paiementUtils';



export interface RunDemandePaiementMutationOptions {

  onConflict?: () => void | Promise<void>;

  /** Message affiché dans l'overlay global pendant l'opération. */

  message?: string;

}



export interface DemandePaiementMutationLock {

  isMutating: boolean;

  operationMessage: string | null;

  runMutation: <T>(

    action: () => Promise<T>,

    options?: RunDemandePaiementMutationOptions,

  ) => Promise<T | undefined>;

}



const DemandePaiementMutationLockContext = createContext<DemandePaiementMutationLock | null>(

  null,

);



function MutationLockOverlay({

  isMutating,

  operationMessage,

}: Pick<DemandePaiementMutationLock, 'isMutating' | 'operationMessage'>) {

  return (

    <GlobalOperationOverlay

      open={isMutating}

      message={operationMessage ?? DEFAULT_OPERATION_OVERLAY_MESSAGE}

    />

  );

}



export function DemandePaiementMutationLockProvider({ children }: { children: ReactNode }) {

  const lock = useDemandePaiementMutationLock();

  return (

    <DemandePaiementMutationLockContext.Provider value={lock}>

      {children}

      <MutationLockOverlay isMutating={lock.isMutating} operationMessage={lock.operationMessage} />

    </DemandePaiementMutationLockContext.Provider>

  );

}



export function useDemandePaiementMutationLockContext(): DemandePaiementMutationLock {

  const ctx = useContext(DemandePaiementMutationLockContext);

  if (!ctx) {

    throw new Error(

      'useDemandePaiementMutationLockContext doit être utilisé dans DemandePaiementMutationLockProvider.',

    );

  }

  return ctx;

}



/** Contexte partagé si présent, sinon verrou local (ex. section validation seule). */

export function useDemandePaiementMutationLockShared(): DemandePaiementMutationLock {

  const ctx = useContext(DemandePaiementMutationLockContext);

  const local = useDemandePaiementMutationLock();

  return ctx ?? local;

}



/** Overlay à placer sur les pages utilisant le hook sans provider. */

export function DemandePaiementMutationLockOverlay({

  lock,

}: {

  lock: Pick<DemandePaiementMutationLock, 'isMutating' | 'operationMessage'>;

}) {

  return (

    <MutationLockOverlay isMutating={lock.isMutating} operationMessage={lock.operationMessage} />

  );

}



/** Hook autonome (pages sans provider) ou implémentation du provider. */

export function useDemandePaiementMutationLock(): DemandePaiementMutationLock {

  const [isMutating, setIsMutating] = useState(false);

  const [operationMessage, setOperationMessage] = useState<string | null>(null);

  const mutationRef = useRef(false);



  const runMutation = useCallback(

    async <T,>(

      action: () => Promise<T>,

      options?: RunDemandePaiementMutationOptions,

    ): Promise<T | undefined> => {

      if (mutationRef.current) return undefined;



      mutationRef.current = true;

      setOperationMessage(options?.message ?? DEFAULT_OPERATION_OVERLAY_MESSAGE);

      setIsMutating(true);

      try {

        return await action();

      } catch (err) {

        if (isApiConflictError(err)) {

          await options?.onConflict?.();

        }

        throw err;

      } finally {

        mutationRef.current = false;

        setIsMutating(false);

        setOperationMessage(null);

      }

    },

    [],

  );



  return useMemo(

    () => ({ isMutating, operationMessage, runMutation }),

    [isMutating, operationMessage, runMutation],

  );

}



export interface DemandePaiementRowMutationLock {

  isRowBusy: (idDemande: number) => boolean;

  isMutating: boolean;

  operationMessage: string | null;

  runRowMutation: <T>(

    idDemande: number,

    action: () => Promise<T>,

    options?: RunDemandePaiementMutationOptions,

  ) => Promise<T | undefined>;

}



/** Verrou par identifiant de demande (listes — une mutation à la fois par ligne). */

export function useDemandePaiementRowMutationLock(): DemandePaiementRowMutationLock {

  const [busyIds, setBusyIds] = useState<ReadonlySet<number>>(() => new Set());

  const [operationMessage, setOperationMessage] = useState<string | null>(null);

  const busyRef = useRef<Set<number>>(new Set());



  const syncBusyIds = useCallback(() => {

    setBusyIds(new Set(busyRef.current));

  }, []);



  const isMutating = busyIds.size > 0;



  const runRowMutation = useCallback(

    async <T,>(

      idDemande: number,

      action: () => Promise<T>,

      options?: RunDemandePaiementMutationOptions,

    ): Promise<T | undefined> => {

      if (busyRef.current.has(idDemande)) return undefined;



      busyRef.current.add(idDemande);

      setOperationMessage(options?.message ?? DEFAULT_OPERATION_OVERLAY_MESSAGE);

      syncBusyIds();

      try {

        return await action();

      } catch (err) {

        if (isApiConflictError(err)) {

          await options?.onConflict?.();

        }

        throw err;

      } finally {

        busyRef.current.delete(idDemande);

        syncBusyIds();

        if (busyRef.current.size === 0) {

          setOperationMessage(null);

        }

      }

    },

    [syncBusyIds],

  );



  const isRowBusy = useCallback((idDemande: number) => busyIds.has(idDemande), [busyIds]);



  return { isRowBusy, isMutating, operationMessage, runRowMutation };

}



/** Overlay pour pages utilisant le verrou par ligne. */

export function DemandePaiementRowMutationLockOverlay({

  lock,

}: {

  lock: Pick<DemandePaiementRowMutationLock, 'isMutating' | 'operationMessage'>;

}) {

  return (

    <MutationLockOverlay isMutating={lock.isMutating} operationMessage={lock.operationMessage} />

  );

}



export function isMutationLockActive(lock: DemandePaiementMutationLock): boolean {

  return lock.isMutating;

}



export function getConflictUserMessage(err: unknown, fallback: string): string {

  if (isApiConflictError(err)) {

    return apiErrorMessage(err, 'La demande a été modifiée entre-temps. Actualisez la demande avant de poursuivre.');

  }

  return apiErrorMessage(err, fallback);

}



export function isRequestAbortedError(err: unknown): boolean {

  return (

    axios.isCancel(err) ||

    (axios.isAxiosError(err) &&

      (err.code === 'ERR_CANCELED' || err.message === 'canceled'))

  );

}


