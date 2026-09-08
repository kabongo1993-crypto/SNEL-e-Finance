/**

 * Self-test utilitaires taux de change.

 * Exécuter : npx --yes tsx src/features/taux-change/tauxChangeUtils.selftest.ts

 */

import {

  assertNoEditAction,

  buildApercuPaire,

  buildTauxChangeListQuery,

  calculerTauxInverseAffichage,

  formatPaireLibelle,

  paireKeyFromCodes,

  toTauxChangeRows,

  validateCreateTauxChangeForm,

} from './tauxChangeUtils';

import type { TauxChangeDto } from '../../services/apiClient';



function assert(cond: boolean, msg: string) {

  if (!cond) throw new Error(msg);

}



const devises = ['USD', 'CDF', 'EUR', 'CFA'];



const sampleDto = (id: number, dateEffet: string, tauxReference: number): TauxChangeDto => ({

  idTauxChange: id,

  deviseBase: 'USD',

  deviseQuote: 'CDF',

  tauxReference,

  dateEffet,

  statut: 'ACTIF',

  dateCreation: '2026-08-27T10:00:00',

  idUtilisateurCreation: 1,

  libelleUtilisateurCreation: 'admin',

  dateModification: null,

  idUtilisateurModification: null,

  libelleUtilisateurModification: null,

  estModifiable: true,

});



assert(formatPaireLibelle('EUR', 'USD') === 'EUR / USD', 'format paire');

assert(paireKeyFromCodes('EUR', 'USD') === 'EUR/USD', 'paire key');



const rows = toTauxChangeRows([

  sampleDto(2, '2026-09-01', 2450),

  sampleDto(1, '2026-08-01', 2400),

]);

assert(rows[0].idTauxChange === 2, 'sort desc date');



const q = buildTauxChangeListQuery({

  paireKey: 'USD/CDF',

  statut: 'ACTIF',

  dateEffetMin: '2026-08-01',

  dateEffetMax: '',

});

assert(q.deviseBase === 'USD', 'query base');

assert(q.deviseQuote === 'CDF', 'query quote');



assert(

  validateCreateTauxChangeForm(

    { deviseBase: 'USD', deviseQuote: 'CDF', tauxReference: '2450', dateEffet: '2026-09-01' },

    devises,

  ) === null,

  'accept valid form',

);



assert(

  validateCreateTauxChangeForm(

    { deviseBase: 'EUR', deviseQuote: 'USD', tauxReference: '3450', dateEffet: '2026-09-01' },

    devises,

  ) === null,

  'accept EUR/USD orientation chosen by user',

);



assert(

  validateCreateTauxChangeForm(

    { deviseBase: 'USD', deviseQuote: 'USD', tauxReference: '2450', dateEffet: '2026-09-01' },

    devises,

  ) !== null,

  'reject same devise',

);



assert(

  validateCreateTauxChangeForm(

    { deviseBase: 'USD', deviseQuote: 'CDF', tauxReference: '0', dateEffet: '2026-09-01' },

    devises,

  ) !== null,

  'reject zero taux',

);



const inverse = calculerTauxInverseAffichage(2450);

assert(inverse > 0 && inverse < 0.001, 'inverse 8 decimals range');



const apercu = buildApercuPaire(3450, 'EUR', 'USD');

assert(apercu !== null && apercu.direct.includes('EUR') && apercu.inverse.includes('USD'), 'apercu EUR/USD');



assert(assertNoEditAction([]), 'no row actions in table');

assert(!assertNoEditAction([{ id: 'desactiver' }]), 'desactiver removed from UI');



console.log('tauxChangeUtils.selftest OK');

