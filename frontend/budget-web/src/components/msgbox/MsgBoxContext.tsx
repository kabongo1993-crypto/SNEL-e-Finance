import {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react';
import { MsgBoxHost } from './MsgBoxHost';

export type MsgBoxSeverity = 'success' | 'error' | 'warning' | 'info';

export type MsgBoxAlertOptions = {
  /** Titre (sinon titre par défaut selon la sévérité). */
  title?: string;
  /** Libellé du bouton OK. */
  okLabel?: string;
};

export type MsgBoxConfirmOptions = {
  title?: string;
  message: string;
  danger?: boolean;
  confirmLabel?: string;
  cancelLabel?: string;
};

export type MsgBoxAlertItem = {
  kind: 'alert';
  id: number;
  severity: MsgBoxSeverity;
  message: string;
  title?: string;
  okLabel?: string;
  resolve: () => void;
};

export type MsgBoxConfirmItem = {
  kind: 'confirm';
  id: number;
  title: string;
  message: string;
  danger?: boolean;
  confirmLabel?: string;
  cancelLabel?: string;
  resolve: (value: boolean) => void;
};

export type MsgBoxQueueItem = MsgBoxAlertItem | MsgBoxConfirmItem;

export type MsgBoxApi = {
  success: (message: string, options?: MsgBoxAlertOptions) => Promise<void>;
  error: (message: string, options?: MsgBoxAlertOptions) => Promise<void>;
  warning: (message: string, options?: MsgBoxAlertOptions) => Promise<void>;
  info: (message: string, options?: MsgBoxAlertOptions) => Promise<void>;
  confirm: (options: MsgBoxConfirmOptions) => Promise<boolean>;
};

const MsgBoxContext = createContext<MsgBoxApi | null>(null);

const DEFAULT_TITLES: Record<MsgBoxSeverity, string> = {
  success: 'Succès',
  error: 'Erreur',
  warning: 'Attention',
  info: 'Information',
};

/**
 * Provider global — une seule instance pour toute l’application.
 * File d’attente : un dialog à la fois, pas de superpositions.
 */
export function MsgBoxProvider({ children }: { children: ReactNode }) {
  const seq = useRef(0);
  const [queue, setQueue] = useState<MsgBoxQueueItem[]>([]);
  const current = queue[0] ?? null;

  const enqueue = useCallback(
    (item: Omit<MsgBoxAlertItem, 'id'> | Omit<MsgBoxConfirmItem, 'id'>) => {
      const id = ++seq.current;
      setQueue((prev) => [...prev, { ...item, id } as MsgBoxQueueItem]);
      return id;
    },
    [],
  );

  const dequeue = useCallback(() => {
    setQueue((prev) => prev.slice(1));
  }, []);

  const showAlert = useCallback(
    (severity: MsgBoxSeverity, message: string, options?: MsgBoxAlertOptions) =>
      new Promise<void>((resolve) => {
        let settled = false;
        enqueue({
          kind: 'alert',
          severity,
          message,
          title: options?.title ?? DEFAULT_TITLES[severity],
          okLabel: options?.okLabel ?? 'OK',
          resolve: () => {
            if (settled) return;
            settled = true;
            resolve();
            dequeue();
          },
        });
      }),
    [enqueue, dequeue],
  );

  const api = useMemo<MsgBoxApi>(
    () => ({
      success: (message, options) => showAlert('success', message, options),
      error: (message, options) => showAlert('error', message, options),
      warning: (message, options) => showAlert('warning', message, options),
      info: (message, options) => showAlert('info', message, options),
      confirm: (options) =>
        new Promise<boolean>((resolve) => {
          let settled = false;
          enqueue({
            kind: 'confirm',
            title: options.title ?? 'Confirmation',
            message: options.message,
            danger: options.danger,
            confirmLabel: options.confirmLabel,
            cancelLabel: options.cancelLabel,
            resolve: (value) => {
              if (settled) return;
              settled = true;
              resolve(value);
              dequeue();
            },
          });
        }),
    }),
    [showAlert, enqueue, dequeue],
  );

  return (
    <MsgBoxContext.Provider value={api}>
      {children}
      <MsgBoxHost current={current} />
    </MsgBoxContext.Provider>
  );
}

/** Hook d’accès au MsgBox global. */
export function useMsgBox(): MsgBoxApi {
  const ctx = useContext(MsgBoxContext);
  if (!ctx) {
    throw new Error('useMsgBox() doit être utilisé dans un MsgBoxProvider.');
  }
  return ctx;
}
