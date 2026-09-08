import {
  filterNavGroups,
  canAccessPath,
} from './navAccess';
import { navGroups } from './navConfig';

function assert(cond: boolean, msg: string) {
  if (!cond) throw new Error(msg);
}

const demandeur = {
  permissions: ['paiements.lire', 'paiements.ecrire', 'paiements.soumettre'],
  roles: ['SERVICE_DEMANDEUR'],
};

const juniorDc = {
  permissions: ['paiements.lire', 'paiements.imputer_dc', 'paiements.controler_budget'],
  roles: ['GESTIONNAIRE_JUNIOR'],
};

const admin = {
  permissions: ['admin.all'],
  roles: ['ADMINISTRATEUR_SYSTEME'],
};

const preparateur = {
  permissions: ['previsions.ecrire', 'previsions.soumettre'],
  roles: ['SERVICE_DEMANDEUR'],
};

const filteredDemandeur = filterNavGroups(navGroups, demandeur);
assert(
  filteredDemandeur.some((g) => g.id === 'operations'),
  'demandeur doit voir Opérations',
);
assert(
  !filteredDemandeur.some((g) => g.id === 'administration'),
  'demandeur ne doit pas voir Administration',
);
assert(
  !filteredDemandeur.some((g) => g.id === 'referentiels'),
  'demandeur ne doit pas voir Référentiels',
);

const ops = filteredDemandeur.find((g) => g.id === 'operations')!;
const paiements = ops.children?.find((c) => c.label === 'Paiements');
assert(Boolean(paiements?.children?.some((c) => c.path === '/paiements')), 'mes demandes');
assert(
  !paiements?.children?.some((c) => c.path === '/paiements/junior-dc'),
  'pas junior DC pour demandeur',
);
assert(!ops.children?.some((c) => c.path === '/engagements'), 'pas engagements');

const filteredJunior = filterNavGroups(navGroups, juniorDc);
const opsJ = filteredJunior.find((g) => g.id === 'operations')!;
const payJ = opsJ.children?.find((c) => c.label === 'Paiements');
assert(Boolean(payJ?.children?.some((c) => c.path === '/paiements/junior-dc')), 'junior DC');
assert(!payJ?.children?.some((c) => c.path === '/paiements/charge-dpm'), 'pas charge DP');
assert(
  !payJ?.children?.some((c) => c.path === '/paiements/documents-etablis'),
  'pas documents établis pour junior',
);
assert(!canAccessPath('/paiements/documents-etablis', demandeur), 'deny docs etablis demandeur');

assert(canAccessPath('/paiements', demandeur), 'demandeur /paiements');
assert(!canAccessPath('/administration/utilisateurs', demandeur), 'deny admin');
assert(!canAccessPath('/paiements/junior-dc', demandeur), 'deny junior');
assert(canAccessPath('/paiements/junior-dc', juniorDc), 'allow junior');
assert(canAccessPath('/administration/utilisateurs', admin), 'admin all');

assert(
  canAccessPath('/budget/suivi-mes-previsions/12/34', preparateur),
  'preparateur consulte sa prevision',
);
assert(!canAccessPath('/budget/soumissions', preparateur), 'preparateur pas liste soumissions');
assert(!canAccessPath('/budget/soumissions/12/34', preparateur), 'preparateur pas detail soumissions');
assert(
  canAccessPath('/budget/soumissions/12/34', preparateur, '?from=mes'),
  'legacy from=mes sur soumissions',
);

const filteredAdmin = filterNavGroups(navGroups, admin);
assert(filteredAdmin.length === navGroups.length, 'admin voit tous les groupes');

console.log('navAccess.selftest: OK');
