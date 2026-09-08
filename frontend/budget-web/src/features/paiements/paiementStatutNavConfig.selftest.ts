/**
 * Segments navigation statut DPM par permissions.
 * npx --yes tsx src/features/paiements/paiementStatutNavConfig.selftest.ts
 */
import {
  getAllowedStatutsFromNavItems,
  getStatutDropdownOptions,
  getStatutNavItems,
  normalizeStatutNavValue,
  resolveStatutNavContext,
} from './paiementStatutNavConfig.ts';
import { STATUTS_DPM } from './paiementUtils.ts';

function assert(cond: unknown, msg: string): void {
  if (!cond) throw new Error(msg);
}

const demandeur = {
  permissions: ['paiements.lire', 'paiements.ecrire', 'paiements.soumettre', 'paiements.envoyer_validation'],
};
const valideur = {
  permissions: [
    'paiements.lire',
    'paiements.valider_n1',
    'paiements.valider_n2',
    'paiements.rejeter_validation_entite',
  ],
};
const budget = {
  permissions: ['paiements.lire', 'paiements.imputer_dc', 'paiements.controler_budget', 'paiements.viser_budget'],
};
const junior = { permissions: ['paiements.lire', 'paiements.imputer_dc'] };
const admin = { permissions: ['admin.all', 'paiements.lire'] };
const sansPerm = { permissions: [] as string[] };

assert(resolveStatutNavContext(demandeur) === 'demandeur', 'contexte demandeur');
assert(resolveStatutNavContext(valideur) === 'valideur', 'contexte valideur');
assert(resolveStatutNavContext(budget) === 'budget', 'contexte budget');
assert(resolveStatutNavContext(junior) === 'budget', 'contexte junior → budget');
assert(resolveStatutNavContext(admin) === 'admin', 'contexte admin');
assert(resolveStatutNavContext(sansPerm) === 'minimal', 'contexte minimal');

const navDemandeur = getStatutNavItems(demandeur).map((i) => i.value);
assert(navDemandeur[0] === '', 'demandeur : Toutes en premier');
assert(navDemandeur.includes('BROUILLON'), 'demandeur : brouillons');
assert(navDemandeur.includes('EN_VALIDATION_N1'), 'demandeur : N1');
assert(navDemandeur.includes('SOUMISE'), 'demandeur : soumises');
assert(navDemandeur.includes('EN_TRAITEMENT_DPM'), 'demandeur : traitement DPM');
assert(navDemandeur.includes('EN_CONTROLE_BUDGETAIRE'), 'demandeur : contrôle budgétaire');
assert(
  navDemandeur.indexOf('SOUMISE') === navDemandeur.indexOf('VALIDEE_ENTITE') + 1,
  'demandeur : soumises après validées entité',
);
assert(
  navDemandeur.indexOf('EN_TRAITEMENT_DPM') === navDemandeur.indexOf('SOUMISE') + 1,
  'demandeur : traitement après soumises',
);

const navValideur = getStatutNavItems(valideur).map((i) => i.value);
assert(navValideur.includes('EN_VALIDATION_N1'), 'valideur : N1');
assert(navValideur.includes('SOUMISE'), 'valideur : soumises');
assert(navValideur.includes('EN_TRAITEMENT_DPM'), 'valideur : traitement DPM');
assert(navValideur.includes('EN_CONTROLE_BUDGETAIRE'), 'valideur : contrôle');
assert(navValideur.includes('A_CORRIGER'), 'valideur : à corriger');
assert(navValideur.includes('VISEE_BUDGETAIREMENT'), 'valideur : visées');
assert(!navValideur.includes('BROUILLON'), 'valideur : pas brouillons (saisisseur uniquement)');

const navBudget = getStatutNavItems(budget).map((i) => i.value);
assert(navBudget.includes('EN_VALIDATION_N1'), 'budget : N1');
assert(navBudget.includes('SOUMISE'), 'budget : soumises');
assert(navBudget.includes('EN_TRAITEMENT_DPM'), 'budget : traitement DPM');
assert(navBudget.includes('EN_CONTROLE_BUDGETAIRE'), 'budget : contrôle');
assert(!navBudget.includes('BROUILLON'), 'budget : pas brouillons');

const navJunior = getStatutNavItems(junior).map((i) => i.value);
assert(navJunior.includes('EN_CONTROLE_BUDGETAIRE'), 'junior : contrôle');
assert(navJunior.includes('EN_VALIDATION_N1'), 'junior : N1 visible');
assert(!navJunior.includes('BROUILLON'), 'junior : pas brouillons');
assert(navJunior.length === 9, 'junior : 8 statuts circuit + Toutes');

const navAdmin = getStatutNavItems(admin);
assert(navAdmin.length === STATUTS_DPM.length + 1, 'admin : tous statuts + Toutes');

const navMinimal = getStatutNavItems(sansPerm);
assert(navMinimal.length === 1 && navMinimal[0]!.value === '', 'sans perm : Toutes seule');

assert(normalizeStatutNavValue('BROUILLON') === 'BROUILLON', 'normalise BROUILLON');
assert(normalizeStatutNavValue('') === '', 'normalise vide');
assert(normalizeStatutNavValue('TOUS') === '', 'normalise TOUS');
assert(normalizeStatutNavValue('INVALIDE') === '', 'statut inconnu → Toutes');

const chargeDp = {
  permissions: ['paiements.lire', 'paiements.charge_dpm', 'paiements.reception_budget'],
};
const navChargeDp = getStatutNavItems(chargeDp, 'charge-dpm').map((i) => i.value);
assert(navChargeDp.length === STATUTS_DPM.length + 1, 'charge DP : tous statuts + Toutes');
assert(navChargeDp[0] === '', 'charge DP : Toutes');
assert(navChargeDp.includes('SOUMISE'), 'charge DP : soumises');
assert(navChargeDp.includes('EN_TRAITEMENT_DPM'), 'charge DP : traitement DPM');
assert(navChargeDp.includes('EN_CONTROLE_BUDGETAIRE'), 'charge DP : contrôle budgétaire');
assert(navChargeDp.includes('A_CORRIGER'), 'charge DP : à corriger');
assert(navChargeDp.includes('EN_VALIDATION_N1'), 'charge DP : validation N1');
assert(navChargeDp.includes('EN_VALIDATION_N2'), 'charge DP : validation N2');
assert(navChargeDp.includes('VALIDEE_ENTITE'), 'charge DP : validée entité');
assert(navChargeDp.includes('VISEE_BUDGETAIREMENT'), 'charge DP : visées');
assert(navChargeDp.includes('BROUILLON'), 'charge DP : brouillons');
assert(
  navChargeDp.indexOf('SOUMISE') === navChargeDp.indexOf('VALIDEE_ENTITE') + 1,
  'charge DP : soumises après validées entité',
);

const chargeAvecEcrire = {
  permissions: ['paiements.lire', 'paiements.charge_dpm', 'paiements.ecrire'],
};
assert(
  getStatutNavItems(chargeAvecEcrire, 'mes-demandes')
    .map((i) => i.value)
    .includes('BROUILLON'),
  'mes-demandes charge+ecrire : statuts demandeur inchangés',
);

assert(
  normalizeStatutNavValue('SOUMISE', { allowed: ['SOUMISE', 'EN_TRAITEMENT_DPM'] }) === 'SOUMISE',
  'allowed SOUMISE',
);
assert(
  normalizeStatutNavValue('BROUILLON', { allowed: ['SOUMISE', 'EN_TRAITEMENT_DPM'] }) === '',
  'allowed rejette BROUILLON',
);

const dropdownDemandeur = getStatutDropdownOptions(getStatutNavItems(demandeur));
const dropdownValues = dropdownDemandeur.map((o) => o.value);
assert(dropdownValues.includes('EN_TRAITEMENT_DPM'), 'dropdown demandeur : traitement DPM');
assert(dropdownValues.includes('BROUILLON'), 'dropdown demandeur : brouillons');
assert(dropdownValues.includes('EN_CONTROLE_BUDGETAIRE'), 'dropdown demandeur : contrôle budgétaire');

const chargeDpItems = getStatutNavItems(chargeDp, 'charge-dpm');
assert(getAllowedStatutsFromNavItems(chargeDpItems).length === STATUTS_DPM.length, 'charge DP : tous statuts hors Toutes');
const dropdownCharge = getStatutDropdownOptions(chargeDpItems);
assert(dropdownCharge.length === STATUTS_DPM.length + 1, 'dropdown charge DP : tous statuts');

const adminItems = getStatutNavItems(admin);
assert(getStatutDropdownOptions(adminItems).length === STATUTS_DPM.length + 1, 'admin : tous statuts');

const allowedDemandeur = getAllowedStatutsFromNavItems(getStatutNavItems(demandeur));
assert(
  normalizeStatutNavValue('EN_TRAITEMENT_DPM', { allowed: allowedDemandeur }) === 'EN_TRAITEMENT_DPM',
  'URL statut traitement autorisé demandeur',
);

console.log('paiementStatutNavConfig.selftest OK');
