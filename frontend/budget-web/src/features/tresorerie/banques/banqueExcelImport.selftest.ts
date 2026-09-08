import { analyserImportBanques, resolveBanqueExcelColumns } from './banqueExcelImport';

function assert(cond: boolean, msg: string) {
  if (!cond) throw new Error(`banqueExcelImport.selftest: ${msg}`);
}

const cols = resolveBanqueExcelColumns(['N° Enr.', 'ID_Banque', 'Libelle_Banque', 'Pays']);
assert(cols.idBanque === 1, 'ID_Banque colonne 1');
assert(cols.libelleBanque === 2, 'Libelle_Banque colonne 2');
assert(cols.pays === 3, 'Pays colonne 3');

let threw = false;
try {
  resolveBanqueExcelColumns(['N° Enr.', 'Pays']);
} catch {
  threw = true;
}
assert(threw, 'refuse un fichier sans ID_Banque / Libelle_Banque');

const preview = analyserImportBanques(
  'Banque.xlsx',
  {
    headers: ['N° Enr.', 'ID_Banque', 'Libelle_Banque', 'Pays'],
    rows: [
      { rowNumber: 2, values: ['1', 'AFRILAND', 'AFRILAND BANK', 'Rdc'] },
      { rowNumber: 3, values: ['2', 'ACCESSBANK', 'ACCESS BANK', 'Rdc'] },
      { rowNumber: 4, values: ['3', 'AFRILAND', 'AFRILAND BANK bis', 'Rdc'] },
      { rowNumber: 5, values: ['4', 'BCC', 'BCC', 'Rdc'] },
      { rowNumber: 6, values: ['5', '', 'SANS ID', 'Rdc'] },
      { rowNumber: 7, values: ['6', 'NOLABEL', '', 'Rdc'] },
      { rowNumber: 8, values: ['7', '  RAWBANK  ', 'Rawbank', ''] },
    ],
  },
  [{ idBanque: 'BCC' }],
);

assert(preview.lignesDetectees === 7, '7 lignes data (N° Enr. ignoré)');
assert(preview.lignes[0].idBanque === 'AFRILAND', 'casse ID conservée');
assert(preview.lignes[0].statut === 'a_importer', 'AFRILAND à importer');
assert(preview.lignes[2].statut === 'doublon_fichier', 'AFRILAND doublon fichier');
assert(preview.lignes[3].statut === 'deja_existante', 'BCC déjà existante');
assert(preview.lignes[4].statut === 'erreur', 'ID obligatoire');
assert(preview.lignes[5].statut === 'erreur', 'libellé obligatoire');
assert(preview.lignes[6].idBanque === 'RAWBANK', 'trim sans changer la casse');
assert(preview.lignes[6].pays === null, 'pays vide → null');
assert(preview.resume.aImporter === 3, '3 à importer (AFRILAND, ACCESSBANK, RAWBANK)');
assert(preview.resume.doublons === 1, '1 doublon');
assert(preview.resume.dejaExistantes === 1, '1 déjà existante');
assert(preview.resume.erreurs === 2, '2 erreurs');

console.log('banqueExcelImport.selftest: OK');
