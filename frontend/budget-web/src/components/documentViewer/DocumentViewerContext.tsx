import {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useState,
  type ReactNode,
} from 'react';
import { DocumentViewerDialog } from './DocumentViewerDialog';

export interface DocumentViewerOpenOptions {
  /** Titre affiché dans la modal (souvent le libellé métier). */
  title: string;
  fileName: string;
  mimeType?: string;
  fileSizeBytes?: number;
  /** Étiquette pour logs [PERF][FE-…] (instrumentation temporaire). */
  perfLabel?: string;
  /** Charge le contenu binaire (preview et téléchargement utilisent la même source). */
  loadContent: () => Promise<Blob>;
  /** Charge pour téléchargement explicite (endpoint attachment). Si absent, réutilise loadContent. */
  loadDownload?: () => Promise<Blob>;
}

interface DocumentViewerContextValue {
  openDocument: (options: DocumentViewerOpenOptions) => void;
  closeDocument: () => void;
}

const DocumentViewerContext = createContext<DocumentViewerContextValue | null>(null);

export function DocumentViewerProvider({ children }: { children: ReactNode }) {
  const [current, setCurrent] = useState<DocumentViewerOpenOptions | null>(null);

  const closeDocument = useCallback(() => setCurrent(null), []);

  const openDocument = useCallback((options: DocumentViewerOpenOptions) => {
    setCurrent(options);
  }, []);

  const value = useMemo(
    () => ({ openDocument, closeDocument }),
    [openDocument, closeDocument],
  );

  return (
    <DocumentViewerContext.Provider value={value}>
      {children}
      <DocumentViewerDialog open={!!current} options={current} onClose={closeDocument} />
    </DocumentViewerContext.Provider>
  );
}

export function useDocumentViewer(): DocumentViewerContextValue {
  const ctx = useContext(DocumentViewerContext);
  if (!ctx) {
    throw new Error('useDocumentViewer doit être utilisé dans DocumentViewerProvider.');
  }
  return ctx;
}
