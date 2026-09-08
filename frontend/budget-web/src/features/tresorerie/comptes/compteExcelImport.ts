import type { ImportCompteRawPayload } from './comptesService';
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
    folded === 'idtutilisateur' ||
    folded === 'idutilisateur' ||
    folded === 'n' ||
    folded === 'no'
  );
}

type ColRole =
  | 'idCompte'
  | 'numeroCompte'
  | 'libelleCompte'
  | 'direction'
  | 'banque'
  | 'typeCompte'
  | 'devise'
  | 'dateCreation'
  | 'dateCloture'
  | 'etat'
  | 'idtProvince'
  | 'ignore';

function mapHeader(folded: string): ColRole | null {
  if (!folded) return null;
  if (isIgnoredHeader(folded)) return 'ignore';
  if (folded === 'idcompte' || folded === 'id_compte') return 'idCompte';
  if (folded === 'numerocompte' || folded === 'numerodecompte') return 'numeroCompte';
  if (folded === 'libellecompte' || folded === 'libelle') return 'libelleCompte';
  if (folded === 'direction') return 'direction';
  if (folded === 'banque') return 'banque';
  if (folded === 'typecompte' || folded === 'typedecompte') return 'typeCompte';
  if (folded === 'devise') return 'devise';
  if (folded === 'datecreation') return 'dateCreation';
  if (folded === 'datecloture') return 'dateCloture';
  if (folded === 'etat') return 'etat';
  if (folded === 'idtprovince' || folded === 'idprovince' || folded === 'province') return 'idtProvince';
  return null;
}

export function resolveCompteExcelColumns(headers: string[]): Record<Exclude<ColRole, 'ignore'>, number> {
  const cols: Partial<Record<Exclude<ColRole, 'ignore'>, number>> = {};

  headers.forEach((header, index) => {
    const role = mapHeader(foldHeader(header));
    if (!role || role === 'ignore') return;
    if (cols[role] == null) cols[role] = index;
  });

  const required: Array<Exclude<ColRole, 'ignore'>> = [
    'idCompte',
    'numeroCompte',
    'libelleCompte',
    'direction',
    'banque',
    'typeCompte',
    'devise',
  ];
  const missing = required.filter((k) => cols[k] == null);
  if (missing.length) {
    throw new Error(
      'Colonnes Excel attendues introuvables : Id_Compte, Numero_Compte, Libelle_Compte, Direction, Banque, Type_Compte, Devise. La colonne « N° Enr. » est ignorée.',
    );
  }

  return {
    idCompte: cols.idCompte!,
    numeroCompte: cols.numeroCompte!,
    libelleCompte: cols.libelleCompte!,
    direction: cols.direction!,
    banque: cols.banque!,
    typeCompte: cols.typeCompte!,
    devise: cols.devise!,
    dateCreation: cols.dateCreation ?? -1,
    dateCloture: cols.dateCloture ?? -1,
    etat: cols.etat ?? -1,
    idtProvince: cols.idtProvince ?? -1,
  };
}

function cell(row: string[], index: number): string {
  if (index < 0) return '';
  return (row[index] ?? '').trim();
}

export function extraireLignesComptesExcel(sheet: XlsxSheetData): ImportCompteRawPayload[] {
  const cols = resolveCompteExcelColumns(sheet.headers);
  return sheet.rows.map((row) => {
    const province = cell(row.values, cols.idtProvince);
    return {
      ligneExcel: row.rowNumber,
      idCompte: cell(row.values, cols.idCompte) || null,
      numeroCompte: cell(row.values, cols.numeroCompte),
      libelleCompte: cell(row.values, cols.libelleCompte),
      direction: cell(row.values, cols.direction),
      banque: cell(row.values, cols.banque),
      typeCompte: cell(row.values, cols.typeCompte),
      devise: cell(row.values, cols.devise),
      dateCreation: cell(row.values, cols.dateCreation),
      dateCloture: cell(row.values, cols.dateCloture),
      etat: cell(row.values, cols.etat),
      idtProvince: province.length ? province : null,
    };
  });
}
