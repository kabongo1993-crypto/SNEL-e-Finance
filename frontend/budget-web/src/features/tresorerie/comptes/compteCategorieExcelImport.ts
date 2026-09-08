import type { ImportCompteCategorieRawPayload } from './comptesService';
import type { XlsxSheetData } from '../banques/xlsxSheetReader';

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

type ColRole = 'idRelation' | 'dateDebut' | 'dateFin' | 'idCategorie' | 'idCompte' | 'ignore';

function mapHeader(folded: string): ColRole | null {
  if (!folded) return null;
  if (isIgnoredHeader(folded)) return 'ignore';
  if (
    folded === 'idtcompteaveccategorie' ||
    folded === 'idcomptecategorie' ||
    folded === 'idcompteaveccategorie'
  ) {
    return 'idRelation';
  }
  if (folded === 'datedebut') return 'dateDebut';
  if (folded === 'datefin') return 'dateFin';
  if (folded === 'idtcategoriecompte' || folded === 'idcategoriecompte') return 'idCategorie';
  if (folded === 'idcompte') return 'idCompte';
  return null;
}

export function resolveCompteCategorieExcelColumns(
  headers: string[],
): Record<Exclude<ColRole, 'ignore'>, number> {
  const cols: Partial<Record<Exclude<ColRole, 'ignore'>, number>> = {};
  headers.forEach((header, index) => {
    const role = mapHeader(foldHeader(header));
    if (!role || role === 'ignore') return;
    if (cols[role] == null) cols[role] = index;
  });

  const required: Array<Exclude<ColRole, 'ignore'>> = [
    'idRelation',
    'dateDebut',
    'idCategorie',
    'idCompte',
  ];
  const missing = required.filter((k) => cols[k] == null);
  if (missing.length) {
    throw new Error(
      'Colonnes Excel attendues introuvables : IDT_COMPTE_AVEC_CATEGORIE, Date_Debut, IDT_CATEGORIE_COMPTE, Id_Compte. La colonne « N° Enr. » est ignorée.',
    );
  }

  return {
    idRelation: cols.idRelation!,
    dateDebut: cols.dateDebut!,
    dateFin: cols.dateFin ?? -1,
    idCategorie: cols.idCategorie!,
    idCompte: cols.idCompte!,
  };
}

function cell(row: string[], index: number): string {
  if (index < 0) return '';
  return (row[index] ?? '').trim();
}

export function extraireLignesCompteCategoriesExcel(
  sheet: XlsxSheetData,
): ImportCompteCategorieRawPayload[] {
  const cols = resolveCompteCategorieExcelColumns(sheet.headers);
  return sheet.rows.map((row) => ({
    ligneExcel: row.rowNumber,
    idCompteCategorie: cell(row.values, cols.idRelation) || null,
    dateDebut: cell(row.values, cols.dateDebut),
    dateFin: cell(row.values, cols.dateFin),
    idCategorieCompte: cell(row.values, cols.idCategorie) || null,
    idCompte: cell(row.values, cols.idCompte) || null,
  }));
}
