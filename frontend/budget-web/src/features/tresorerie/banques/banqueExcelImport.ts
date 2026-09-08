import type { XlsxSheetData } from './xlsxSheetReader';

export type BanqueImportStatut = 'a_importer' | 'doublon_fichier' | 'deja_existante' | 'erreur';

export interface BanqueImportLigne {
  ligneExcel: number;
  idBanque: string;
  libelleBanque: string;
  pays: string | null;
  statut: BanqueImportStatut;
  resultat: string;
}

export interface BanqueImportResume {
  analysees: number;
  aImporter: number;
  doublons: number;
  dejaExistantes: number;
  erreurs: number;
}

export interface BanqueImportPreview {
  nomFichier: string;
  lignesDetectees: number;
  lignes: BanqueImportLigne[];
  resume: BanqueImportResume;
}

const MAX_ID = 50;
const MAX_LIBELLE = 200;
const MAX_PAYS = 100;

function foldHeader(value: string): string {
  return value
    .normalize('NFD')
    .replace(/\p{M}/gu, '')
    .replace(/[°º]/g, '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '');
}

function isIgnoredHeader(folded: string): boolean {
  return (
    folded === 'nenr' ||
    folded === 'noenr' ||
    folded === 'numeroenr' ||
    folded === 'nenregistrement' ||
    folded === 'n' ||
    folded === 'no'
  );
}

function mapHeader(folded: string): 'idBanque' | 'libelleBanque' | 'pays' | 'ignore' | null {
  if (!folded) return null;
  if (isIgnoredHeader(folded)) return 'ignore';
  if (folded === 'idbanque' || folded === 'id') return 'idBanque';
  if (folded === 'libellebanque' || folded === 'libelle') return 'libelleBanque';
  if (folded === 'pays') return 'pays';
  return null;
}

export function resolveBanqueExcelColumns(headers: string[]): {
  idBanque: number;
  libelleBanque: number;
  pays: number | null;
} {
  let idBanque = -1;
  let libelleBanque = -1;
  let pays: number | null = null;

  headers.forEach((header, index) => {
    const role = mapHeader(foldHeader(header));
    if (role === 'idBanque' && idBanque < 0) idBanque = index;
    if (role === 'libelleBanque' && libelleBanque < 0) libelleBanque = index;
    if (role === 'pays' && pays == null) pays = index;
  });

  if (idBanque < 0 || libelleBanque < 0) {
    throw new Error(
      'Colonnes Excel attendues introuvables : ID_Banque et Libelle_Banque. La colonne « N° Enr. » est ignorée.',
    );
  }

  return { idBanque, libelleBanque, pays };
}

function emptyToNull(value: string): string | null {
  const trimmed = value.trim();
  return trimmed.length ? trimmed : null;
}

export function analyserImportBanques(
  nomFichier: string,
  sheet: XlsxSheetData,
  existantes: ReadonlyArray<{ idBanque: string }>,
): BanqueImportPreview {
  const cols = resolveBanqueExcelColumns(sheet.headers);
  const existing = new Set(existantes.map((b) => b.idBanque.trim().toLowerCase()).filter(Boolean));
  const firstLineById = new Map<string, number>();

  const lignes: BanqueImportLigne[] = sheet.rows.map((row) => {
    const idBanque = (row.values[cols.idBanque] ?? '').trim();
    const libelleBanque = (row.values[cols.libelleBanque] ?? '').trim();
    const pays = cols.pays == null ? null : emptyToNull(row.values[cols.pays] ?? '');
    const key = idBanque.toLowerCase();

    let statut: BanqueImportStatut = 'a_importer';
    let resultat = 'À importer';

    if (!idBanque) {
      statut = 'erreur';
      resultat = 'ID_Banque obligatoire';
    } else if (firstLineById.has(key)) {
      statut = 'doublon_fichier';
      resultat = `Doublon dans le fichier (ligne ${firstLineById.get(key)})`;
    } else if (!libelleBanque) {
      statut = 'erreur';
      resultat = 'Libelle_Banque obligatoire';
      firstLineById.set(key, row.rowNumber);
    } else if (idBanque.length > MAX_ID) {
      statut = 'erreur';
      resultat = `ID_Banque trop long (max. ${MAX_ID})`;
      firstLineById.set(key, row.rowNumber);
    } else if (libelleBanque.length > MAX_LIBELLE) {
      statut = 'erreur';
      resultat = `Libelle_Banque trop long (max. ${MAX_LIBELLE})`;
      firstLineById.set(key, row.rowNumber);
    } else if (pays && pays.length > MAX_PAYS) {
      statut = 'erreur';
      resultat = `Pays trop long (max. ${MAX_PAYS})`;
      firstLineById.set(key, row.rowNumber);
    } else if (existing.has(key)) {
      statut = 'deja_existante';
      resultat = 'Déjà existante';
      firstLineById.set(key, row.rowNumber);
    } else {
      firstLineById.set(key, row.rowNumber);
    }

    return {
      ligneExcel: row.rowNumber,
      idBanque,
      libelleBanque,
      pays,
      statut,
      resultat,
    };
  });

  const resume: BanqueImportResume = {
    analysees: lignes.length,
    aImporter: lignes.filter((l) => l.statut === 'a_importer').length,
    doublons: lignes.filter((l) => l.statut === 'doublon_fichier').length,
    dejaExistantes: lignes.filter((l) => l.statut === 'deja_existante').length,
    erreurs: lignes.filter((l) => l.statut === 'erreur').length,
  };

  return {
    nomFichier,
    lignesDetectees: lignes.length,
    lignes,
    resume,
  };
}

export function lignesAImporter(preview: BanqueImportPreview): Array<{
  idBanque: string;
  libelleBanque: string;
  pays: string | null;
}> {
  return preview.lignes
    .filter((l) => l.statut === 'a_importer')
    .map((l) => ({
      idBanque: l.idBanque,
      libelleBanque: l.libelleBanque,
      pays: l.pays,
    }));
}
