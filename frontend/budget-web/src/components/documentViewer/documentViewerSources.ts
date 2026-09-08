import {
  downloadDemandePaiementDocumentPdf,
  downloadDemandePaiementPiece,
  downloadDocumentPrevisionPdf,
  type DemandePaiementPiece,
  type DocumentPrevision,
} from '../../services/apiClient';
import type { DocumentViewerOpenOptions } from './DocumentViewerContext';
import { fetchBlobWithPerf } from './documentViewerPerf';
import { mimeTypeFromFileName } from './documentMime';

function perfLoadContent(
  _perfLabel: string,
  url: string,
  params?: Record<string, unknown>,
): () => Promise<Blob> {
  return async () => {
    const { blob } = await fetchBlobWithPerf(url, params);
    return blob;
  };
}

export function buildDocumentPrevisionPdfViewerOptions(
  document: DocumentPrevision,
): DocumentViewerOpenOptions {
  const fileName = `${document.reference.replace(/\//g, '_')}.pdf`;
  return {
    title: document.titre || document.reference,
    fileName,
    mimeType: 'application/pdf',
    perfLabel: 'DOCUMENT-PREVISION-PDF',
    loadContent: perfLoadContent(
      'DOCUMENT-PREVISION-PDF',
      `/api/v1/documents-previsions/${document.idDocument}/pdf`,
    ),
    loadDownload: () => downloadDocumentPrevisionPdf(document.idDocument),
  };
}

export function buildDemandePaiementPieceViewerOptions(
  idDemande: number,
  piece: Pick<DemandePaiementPiece, 'idPieceJointe' | 'libelle' | 'nomFichierOriginal' | 'tailleOctets'>,
): DocumentViewerOpenOptions {
  const fileName = piece.nomFichierOriginal || 'piece';
  return {
    title: piece.libelle || fileName,
    fileName,
    mimeType: mimeTypeFromFileName(fileName),
    fileSizeBytes: piece.tailleOctets,
    perfLabel: 'PIECE-PREVIEW',
    loadContent: perfLoadContent(
      'PIECE-PREVIEW',
      `/api/v1/demandes-paiement/${idDemande}/pieces/${piece.idPieceJointe}/apercu`,
    ),
    loadDownload: () => downloadDemandePaiementPiece(idDemande, piece.idPieceJointe),
  };
}

export function buildDemandePaiementDocumentPdfViewerOptions(
  idDemande: number,
  reference?: string | null,
): DocumentViewerOpenOptions {
  const fileName = `demande-paiement-${reference?.replace(/[^\w-]+/g, '_') || idDemande}.pdf`;
  return {
    title: reference ? `Demande de paiement ${reference}` : 'Demande de paiement',
    fileName,
    mimeType: 'application/pdf',
    perfLabel: 'DPM-PDF',
    loadContent: perfLoadContent('DPM-PDF', `/api/v1/demandes-paiement/${idDemande}/document-pdf`, {
      inline: true,
    }),
    loadDownload: () => downloadDemandePaiementDocumentPdf(idDemande),
  };
}

export function buildBilletConversionPdfViewerOptions(idDemande: number, reference?: string | null): DocumentViewerOpenOptions {
  const fileName = `billet-conversion-${idDemande}.pdf`;
  return {
    title: reference ? `Billet de conversion — ${reference}` : 'Billet de conversion',
    fileName,
    mimeType: 'application/pdf',
    perfLabel: 'BILLET-CONVERSION-PDF',
    loadContent: perfLoadContent(
      'BILLET-CONVERSION-PDF',
      `/api/v1/demandes-paiement/${idDemande}/billet-conversion/pdf`,
      { inline: true },
    ),
  };
}

export function buildPieceCaissePdfViewerOptions(idDemande: number): DocumentViewerOpenOptions {
  const fileName = `piece-caisse-${idDemande}.pdf`;
  return {
    title: 'Pièce de caisse',
    fileName,
    mimeType: 'application/pdf',
    perfLabel: 'PIECE-CAISSE-PDF',
    loadContent: perfLoadContent(
      'PIECE-CAISSE-PDF',
      `/api/v1/demandes-paiement/${idDemande}/piece-caisse/pdf`,
      { inline: true },
    ),
  };
}

export function buildBonProvisoirePdfViewerOptions(idDemande: number): DocumentViewerOpenOptions {
  const fileName = `bon-provisoire-${idDemande}.pdf`;
  return {
    title: 'Bon provisoire',
    fileName,
    mimeType: 'application/pdf',
    perfLabel: 'BON-PROVISOIRE-PDF',
    loadContent: perfLoadContent(
      'BON-PROVISOIRE-PDF',
      `/api/v1/demandes-paiement/${idDemande}/bon-provisoire/pdf`,
      { inline: true },
    ),
  };
}

export function buildDocumentsEtablisListePdfViewerOptions(params: {
  dateDebut: string;
  dateFin: string;
  typeDocument?: string;
  selection?: string;
}): DocumentViewerOpenOptions {
  const fileName = `documents-etablis-${params.dateDebut}-${params.dateFin}.pdf`;
  return {
    title: 'Liste des documents établis',
    fileName,
    mimeType: 'application/pdf',
    perfLabel: 'DOCUMENTS-ETABLIS-LISTE-PDF',
    loadContent: perfLoadContent(
      'DOCUMENTS-ETABLIS-LISTE-PDF',
      '/api/v1/demandes-paiement/documents-etablis/liste-pdf',
      { ...params, inline: true },
    ),
  };
}

export function buildDocumentsEtablisDocumentsPdfViewerOptions(params: {
  dateDebut: string;
  dateFin: string;
  typeDocument?: string;
  selection?: string;
  count: number;
}): DocumentViewerOpenOptions {
  const fileName = `documents-etablis-${params.dateDebut}-${params.dateFin}.pdf`;
  return {
    title:
      params.count <= 1
        ? 'Document établi'
        : `${params.count} documents établis`,
    fileName,
    mimeType: 'application/pdf',
    perfLabel: 'DOCUMENTS-ETABLIS-DOCUMENTS-PDF',
    loadContent: perfLoadContent(
      'DOCUMENTS-ETABLIS-DOCUMENTS-PDF',
      '/api/v1/demandes-paiement/documents-etablis/documents-pdf',
      { ...params, inline: true },
    ),
  };
}

export function buildMinuteChequePdfViewerOptions(idDemande: number): DocumentViewerOpenOptions {
  const fileName = `minute-cheque-${idDemande}.pdf`;
  return {
    title: 'Minute chèque',
    fileName,
    mimeType: 'application/pdf',
    perfLabel: 'MINUTE-CHEQUE-PDF',
    loadContent: perfLoadContent(
      'MINUTE-CHEQUE-PDF',
      `/api/v1/demandes-paiement/${idDemande}/minute-cheque/pdf`,
      { inline: true },
    ),
  };
}
