/** Types MIME prévisualisables nativement dans le navigateur (e-Finance). */
export type DocumentPreviewKind = 'pdf' | 'image' | 'unsupported';

const IMAGE_EXTENSIONS = new Set(['.jpg', '.jpeg', '.png', '.gif', '.webp']);

const OFFICE_EXTENSIONS = new Set(['.doc', '.docx', '.xls', '.xlsx']);

export function extensionFromFileName(fileName: string): string {
  const idx = fileName.lastIndexOf('.');
  if (idx < 0) return '';
  return fileName.slice(idx).toLowerCase();
}

export function mimeTypeFromFileName(fileName: string): string {
  switch (extensionFromFileName(fileName)) {
    case '.pdf':
      return 'application/pdf';
    case '.png':
      return 'image/png';
    case '.jpg':
    case '.jpeg':
      return 'image/jpeg';
    case '.gif':
      return 'image/gif';
    case '.webp':
      return 'image/webp';
    case '.tif':
    case '.tiff':
      return 'image/tiff';
    case '.doc':
      return 'application/msword';
    case '.docx':
      return 'application/vnd.openxmlformats-officedocument.officedocument.wordprocessingml.document';
    case '.xls':
      return 'application/vnd.ms-excel';
    case '.xlsx':
      return 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet';
    default:
      return 'application/octet-stream';
  }
}

export function resolvePreviewKind(fileName: string, mimeType?: string): DocumentPreviewKind {
  const mime = (mimeType || mimeTypeFromFileName(fileName)).toLowerCase();
  if (mime === 'application/pdf' || extensionFromFileName(fileName) === '.pdf') return 'pdf';
  if (mime.startsWith('image/') && mime !== 'image/tiff') {
    if (IMAGE_EXTENSIONS.has(extensionFromFileName(fileName)) || mime.startsWith('image/')) {
      return 'image';
    }
  }
  if (IMAGE_EXTENSIONS.has(extensionFromFileName(fileName))) return 'image';
  return 'unsupported';
}

export function isOfficeDocument(fileName: string): boolean {
  return OFFICE_EXTENSIONS.has(extensionFromFileName(fileName));
}

export function formatFileSize(bytes?: number): string | null {
  if (bytes == null || bytes <= 0) return null;
  if (bytes < 1024) return `${bytes} o`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} Ko`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} Mo`;
}

export function mimeTypeLabel(mimeType: string): string {
  switch (mimeType.toLowerCase()) {
    case 'application/pdf':
      return 'PDF';
    case 'image/png':
      return 'PNG';
    case 'image/jpeg':
      return 'JPEG';
    case 'image/gif':
      return 'GIF';
    case 'image/webp':
      return 'WEBP';
    case 'image/tiff':
      return 'TIFF';
    case 'application/msword':
      return 'Word';
    case 'application/vnd.openxmlformats-officedocument.wordprocessingml.document':
      return 'Word';
    case 'application/vnd.ms-excel':
      return 'Excel';
    case 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet':
      return 'Excel';
    default:
      return mimeType.split('/').pop()?.toUpperCase() ?? 'Fichier';
  }
}
