/** Aligné sur Storage:PiecesJustificatives (appsettings.json). */
export const PIECE_JUSTIFICATIVE_MAX_BYTES = 10 * 1024 * 1024;

export const PIECE_JUSTIFICATIVE_ALLOWED_EXTENSIONS = [
  '.pdf',
  '.png',
  '.jpg',
  '.jpeg',
  '.tif',
  '.tiff',
  '.doc',
  '.docx',
  '.xls',
  '.xlsx',
] as const;

export const PIECE_JUSTIFICATIVE_ACCEPT = PIECE_JUSTIFICATIVE_ALLOWED_EXTENSIONS.join(',');

function fileExtension(name: string): string {
  const dot = name.lastIndexOf('.');
  if (dot < 0) return '';
  return name.slice(dot).toLowerCase();
}

/**
 * Validation locale avant upload — messages calqués sur PieceJointeUploadValidator (backend).
 * Retourne le message d'erreur ou null si le fichier est acceptable.
 */
export function validateDemandePaiementPieceFile(file: File): string | null {
  if (!file.name.trim()) {
    return 'Le nom du fichier est obligatoire.';
  }

  const ext = fileExtension(file.name);
  if (!ext) {
    return 'Le fichier doit avoir une extension.';
  }

  const allowed = new Set(
    PIECE_JUSTIFICATIVE_ALLOWED_EXTENSIONS.map((e) => e.toLowerCase()),
  );
  if (!allowed.has(ext)) {
    return `Extension non autorisée (${ext}). Autorisées : ${[...allowed].sort().join(', ')}.`;
  }

  if (file.size <= 0) {
    return 'Le fichier est vide.';
  }

  if (file.size > PIECE_JUSTIFICATIVE_MAX_BYTES) {
    const maxMo = PIECE_JUSTIFICATIVE_MAX_BYTES / (1024 * 1024);
    return `Le fichier dépasse la taille maximale autorisée (${maxMo.toFixed(0)} Mo).`;
  }

  return null;
}
