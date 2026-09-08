/**
 * Détection MIME / aperçu document.
 * npx --yes tsx src/components/documentViewer/documentMime.selftest.ts
 */
import {
  mimeTypeFromFileName,
  resolvePreviewKind,
} from './documentMime.ts';

function assert(cond: unknown, msg: string): void {
  if (!cond) throw new Error(msg);
}

assert(resolvePreviewKind('facture.pdf') === 'pdf', 'pdf preview');
assert(mimeTypeFromFileName('facture.pdf') === 'application/pdf', 'pdf mime');
assert(resolvePreviewKind('scan.png') === 'image', 'png preview');
assert(resolvePreviewKind('photo.jpg') === 'image', 'jpg preview');
assert(resolvePreviewKind('doc.docx') === 'unsupported', 'docx unsupported');
assert(resolvePreviewKind('sheet.xlsx') === 'unsupported', 'xlsx unsupported');

console.log('documentMime.selftest.ts OK');
