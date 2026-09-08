/**
 * Validation uploads pièces DPM.
 * npx --yes tsx src/features/paiements/pieceUploadUtils.selftest.ts
 */
import {
  PIECE_JUSTIFICATIVE_MAX_BYTES,
  validateDemandePaiementPieceFile,
} from './pieceUploadUtils.ts';

function assert(cond: unknown, msg: string): void {
  if (!cond) throw new Error(msg);
}

assert(validateDemandePaiementPieceFile(new File(['x'], 'doc.pdf')) === null, 'pdf ok');
assert(
  validateDemandePaiementPieceFile(new File(['x'], 'virus.exe'))?.includes('Extension non autorisée'),
  'extension refusée',
);
assert(
  validateDemandePaiementPieceFile(new File([], 'vide.pdf')) === 'Le fichier est vide.',
  'fichier vide',
);
assert(
  validateDemandePaiementPieceFile(
    new File([new Uint8Array(PIECE_JUSTIFICATIVE_MAX_BYTES + 1)], 'gros.pdf'),
  )?.includes('dépasse la taille maximale'),
  'taille max',
);
assert(
  validateDemandePaiementPieceFile(new File(['x'], 'sans-ext')) === 'Le fichier doit avoir une extension.',
  'sans extension',
);

console.log('pieceUploadUtils.selftest OK');
