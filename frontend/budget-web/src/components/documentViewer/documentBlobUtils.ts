import { isAxiosError } from 'axios';

export function downloadBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = fileName || 'document';
  a.rel = 'noopener';
  a.click();
  window.setTimeout(() => URL.revokeObjectURL(url), 500);
}

export function printBlob(blob: Blob, kind: 'pdf' | 'image'): void {
  const url = URL.createObjectURL(blob);
  if (kind === 'image') {
    const w = window.open('', '_blank', 'noopener,noreferrer');
    if (!w) {
      URL.revokeObjectURL(url);
      throw new Error('Fenêtre bloquée — autorisez les pop-ups pour imprimer.');
    }
    w.document.write(
      `<html><head><title>Impression</title></head><body style="margin:0;display:flex;justify-content:center;align-items:center;min-height:100vh;"><img src="${url}" style="max-width:100%;height:auto;" onload="window.print()" /></body></html>`,
    );
    w.document.close();
    window.setTimeout(() => URL.revokeObjectURL(url), 120_000);
    return;
  }

  const w = window.open(url, '_blank', 'noopener,noreferrer');
  if (!w) {
    URL.revokeObjectURL(url);
    throw new Error('Fenêtre bloquée — autorisez les pop-ups pour imprimer.');
  }
  const trigger = () => {
    try {
      w.focus();
      w.print();
    } finally {
      window.setTimeout(() => URL.revokeObjectURL(url), 120_000);
    }
  };
  w.addEventListener('load', () => window.setTimeout(trigger, 400));
  window.setTimeout(trigger, 1200);
}

export function printFromIframe(iframe: HTMLIFrameElement | null): void {
  if (!iframe?.contentWindow) {
    throw new Error('Aperçu non prêt pour l’impression.');
  }
  iframe.contentWindow.focus();
  iframe.contentWindow.print();
}

export function documentLoadErrorMessage(err: unknown): string {
  if (isAxiosError(err)) {
    const status = err.response?.status;
    if (status === 401 || status === 403) return 'Accès refusé à ce document.';
    if (status === 404) return 'Document introuvable ou supprimé.';
    if (status === 409) return 'Document indisponible dans l’état actuel de la demande.';
    if (status && status >= 500) return 'Erreur serveur lors du chargement du document.';
    if (!err.response) return 'Impossible de charger le document. Vérifiez votre connexion.';
  }
  if (err instanceof Error && err.message && !err.message.includes('Network Error')) {
    return err.message;
  }
  return 'Impossible de charger le document.';
}

export function ensureBlobMime(blob: Blob, _fileName: string, mimeType?: string): Blob {
  const type = mimeType || blob.type || 'application/octet-stream';
  if (blob.type === type) return blob;
  return new Blob([blob], { type });
}
