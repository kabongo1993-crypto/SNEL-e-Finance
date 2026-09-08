/**
 * Règles UI DPM demandeur.
 * npx --yes tsx src/features/paiements/paiementUtils.selftest.ts
 */
import {
  matchesJuniorCodeTypeBudget,
  STATUTS_DPM,
  calculerMontantUsd,
  canSupprimerBrouillon,
  estModifiableDemandeur,
  labelStatutDpm,
  normalizeStatutDpm,
  totalImputationsOk,
} from './paiementUtils.ts';

function assert(cond: unknown, msg: string): void {
  if (!cond) throw new Error(msg);
}

assert(STATUTS_DPM.includes('BROUILLON'), 'statut brouillon');
assert(STATUTS_DPM.includes('EN_TRAITEMENT_DPM'), 'statut traitement dpm');
assert(STATUTS_DPM.includes('VISEE_BUDGETAIREMENT'), 'statut visa');
assert(!STATUTS_DPM.includes('PAYEE' as never), 'pas de statut trésorerie');
assert(!STATUTS_DPM.includes('ORIENTEE_DC' as never), 'pas de statut oriente');

assert(normalizeStatutDpm('  soumise ') === 'SOUMISE', 'normalize');
assert(labelStatutDpm('A_CORRIGER').toLowerCase().includes('demandeur'), 'label retour demandeur');

assert(estModifiableDemandeur('BROUILLON'), 'brouillon editable');
assert(estModifiableDemandeur('A_CORRIGER'), 'a corriger editable');
assert(!estModifiableDemandeur('SOUMISE'), 'soumise lock');
assert(!estModifiableDemandeur('EN_CONTROLE_BUDGETAIRE'), 'controle lock');
assert(!estModifiableDemandeur('VISEE_BUDGETAIREMENT'), 'visa lock');

const writer = { permissions: ['paiements.ecrire'] };
assert(canSupprimerBrouillon(writer, 'BROUILLON'), 'supprimer brouillon');
assert(!canSupprimerBrouillon(writer, 'SOUMISE'), 'pas supprimer soumise');
assert(!canSupprimerBrouillon({ permissions: ['paiements.lire'] }, 'BROUILLON'), 'pas supprimer sans ecrire');

assert(calculerMontantUsd(2800, 2800) === 1, 'usd = brut/taux');
assert(calculerMontantUsd(100, 1) === 100, 'usd identity');
assert(totalImputationsOk(100, 100), 'totaux egaux');
assert(totalImputationsOk(100.004, 100), 'tolerance');
assert(!totalImputationsOk(90, 100), 'ecart bloque');

assert(matchesJuniorCodeTypeBudget('DC', 'DC'), 'junior DC accepte DC');
assert(matchesJuniorCodeTypeBudget(' dc ', 'DC'), 'junior DC trim');
assert(!matchesJuniorCodeTypeBudget('AE', 'DC'), 'junior DC rejette AE');
assert(matchesJuniorCodeTypeBudget('AE', 'AE'), 'junior AE accepte AE');
assert(matchesJuniorCodeTypeBudget('BI', 'BI'), 'junior BI accepte BI');
assert(!matchesJuniorCodeTypeBudget(null, 'DC'), 'junior rejette null');
assert(!matchesJuniorCodeTypeBudget(undefined, 'AE'), 'junior rejette undefined');
assert(!matchesJuniorCodeTypeBudget('', 'BI'), 'junior rejette vide');
assert(!matchesJuniorCodeTypeBudget('XX', 'DC'), 'junior rejette code inconnu');

console.log('paiementUtils.selftest OK');
