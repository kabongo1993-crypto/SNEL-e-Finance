export const TYPE_DOCUMENT_ETABLI_OPTIONS = [
  { value: '', label: 'Tous les types' },
  { value: 'BILLET_CONVERSION', label: 'Billet de conversion' },
  { value: 'PIECE_CAISSE', label: 'Pièce de caisse' },
  { value: 'BON_PROVISOIRE', label: 'Bon provisoire' },
  { value: 'MINUTE_CHEQUE', label: 'Minute de chèque' },
] as const;

export function typeDocumentEtabliLabel(code: string): string {
  return TYPE_DOCUMENT_ETABLI_OPTIONS.find((o) => o.value === code)?.label ?? 'Document';
}

export function defaultDocumentsEtablisPeriode(today = new Date()): { dateDebut: string; dateFin: string } {
  const y = today.getFullYear();
  const m = today.getMonth();
  const pad = (n: number) => String(n).padStart(2, '0');
  return {
    dateDebut: `${y}-${pad(m + 1)}-01`,
    dateFin: `${y}-${pad(m + 1)}-${pad(today.getDate())}`,
  };
}

export function documentsEtablisRowId(
  idDemandePaiement: number,
  typeDocument: string,
  numeroDocument: string,
): string {
  return `${idDemandePaiement}-${typeDocument}-${numeroDocument || 'x'}`;
}

export function documentEtabliSelectionToken(row: {
  idDemandePaiement: number;
  typeDocument: string;
  numeroDocument: string;
}): string {
  return `${row.idDemandePaiement}|${row.typeDocument}|${row.numeroDocument ?? ''}`;
}

export function documentsEtablisSelectionParam(
  rows: Array<{ idDemandePaiement: number; typeDocument: string; numeroDocument: string }>,
): string {
  return rows.map(documentEtabliSelectionToken).join(',');
}
