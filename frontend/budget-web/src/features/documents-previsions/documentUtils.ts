import type { DocumentPrevision } from '../../services/apiClient';

export function formatDocumentShortRef(reference: string): string {
  const parts = reference.split('/').filter(Boolean);
  if (parts.length < 2) return reference;
  const seq = parts[parts.length - 1];
  const type = parts[parts.length - 2];
  return `${type}-${seq}`;
}

export function documentActionTitle(type: string): string {
  switch ((type || '').toUpperCase()) {
    case 'SUB':
      return 'Prévision soumise';
    case 'REJ':
      return 'Prévision rejetée';
    case 'CTL':
      return 'Prévision contrôlée';
    case 'VAL':
      return 'Prévision validée';
    default:
      return 'Document généré';
  }
}

export function documentViewLabel(type: string): string {
  switch ((type || '').toUpperCase()) {
    case 'SUB':
      return 'Voir le document';
    case 'REJ':
      return "Voir l'avis de rejet";
    case 'CTL':
      return 'Voir la fiche de contrôle';
    case 'VAL':
      return "Voir l'acte de validation";
    default:
      return 'Voir le document';
  }
}

/** Actions historique susceptibles d’avoir un document lié. */
export function historiqueActionHasDocument(action: string): boolean {
  const a = (action || '').toUpperCase();
  return (
    a.includes('SOUMET') ||
    a.includes('REJET') ||
    a.includes('CONTROL') ||
    a.includes('VALID')
  );
}

export function openDocumentPdfBlob(blob: Blob): void {
  const url = URL.createObjectURL(blob);
  window.open(url, '_blank', 'noopener,noreferrer');
  // Révocation différée pour laisser le navigateur charger l’onglet.
  window.setTimeout(() => URL.revokeObjectURL(url), 60_000);
}

export function printDocumentPdfBlob(blob: Blob): void {
  const url = URL.createObjectURL(blob);
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
      window.setTimeout(() => URL.revokeObjectURL(url), 60_000);
    }
  };
  // Certains navigateurs déclenchent onload sur about:blank puis PDF.
  w.addEventListener('load', () => window.setTimeout(trigger, 400));
  window.setTimeout(trigger, 1200);
}

export function isDocumentPrevision(value: unknown): value is DocumentPrevision {
  return (
    !!value &&
    typeof value === 'object' &&
    'idDocument' in value &&
    'reference' in value &&
    typeof (value as DocumentPrevision).idDocument === 'number'
  );
}
