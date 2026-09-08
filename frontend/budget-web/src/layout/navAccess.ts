import {
  PERMS_ADMIN_PROFILS,
  PERMS_ADMIN_TECH,
  PERMS_ADMIN_UTILISATEURS,
  PERMS_AJUSTEMENTS_ACCESS,
  PERMS_CHARGE_DP,
  PERMS_PAIEMENTS_ACCESS,
  PERMS_PAIEMENTS_BUDGET,
  PERMS_PREVISIONS_ACCESS,
  PERMS_RAPPORTS_PREVISIONS,
  PERMS_REFERENTIELS,
  PERMS_VERSIONS_ACCESS,
  PERMS_VERSIONS_WORKFLOW,
  type AuthzUser,
  hasAnyPerm,
} from '../features/auth/permissions';
import { navGroups, type NavGroup, type NavItem } from './navConfig';
import { tresorerieNavGroups } from './tresorerieNavConfig';
import { getWorkspaceFromPath, type Workspace } from './workspace';

/** Filtre récursif : garde les feuilles autorisées et les parents qui ont encore des enfants. */
export function filterNavItem(item: NavItem, user: AuthzUser): NavItem | null {
  if (item.children?.length) {
    const children = item.children
      .map((c) => filterNavItem(c, user))
      .filter((c): c is NavItem => c != null);
    if (!children.length) return null;
    return { ...item, children };
  }

  if (item.anyOf && !hasAnyPerm(user, item.anyOf)) return null;
  return item;
}

export function filterNavGroups(groups: NavGroup[], user: AuthzUser): NavGroup[] {
  return groups
    .map((group) => {
      if (group.children?.length) {
        const children = group.children
          .map((c) => filterNavItem(c, user))
          .filter((c): c is NavItem => c != null);
        if (!children.length) return null;
        return { ...group, children };
      }
      if (group.anyOf && !hasAnyPerm(user, group.anyOf)) return null;
      return group;
    })
    .filter((g): g is NavGroup => g != null);
}

function navGroupsForWorkspace(workspace: Workspace): NavGroup[] {
  return workspace === 'tresorerie' ? tresorerieNavGroups : navGroups;
}

/** Navigation visible pour l’utilisateur courant et l’espace métier actif. */
export function getVisibleNavGroups(user: AuthzUser, workspace: Workspace = 'budget'): NavGroup[] {
  return filterNavGroups(navGroupsForWorkspace(workspace), user);
}

/** Navigation visible déduite du pathname courant. */
export function getVisibleNavGroupsForPath(user: AuthzUser, pathname: string): NavGroup[] {
  return getVisibleNavGroups(user, getWorkspaceFromPath(pathname));
}

type PathRule = { prefix: string; anyOf: readonly string[] };

/**
 * Règles d’accès deep-link (plus spécifique d’abord).
 * Chemins hors liste = accessibles si authentifié (ex. dashboard).
 */
const PATH_RULES: PathRule[] = [
  { prefix: '/paiements/documents-etablis', anyOf: PERMS_CHARGE_DP },
  { prefix: '/paiements/charge-dpm', anyOf: PERMS_CHARGE_DP },
  { prefix: '/paiements/junior-dc', anyOf: ['paiements.imputer_dc'] },
  { prefix: '/paiements/junior-ae', anyOf: ['paiements.imputer_ae'] },
  { prefix: '/paiements/junior-bi', anyOf: ['paiements.imputer_bi'] },
  { prefix: '/paiements/budget', anyOf: PERMS_PAIEMENTS_BUDGET },
  { prefix: '/paiements/nouveau', anyOf: ['paiements.ecrire'] },
  { prefix: '/paiements', anyOf: PERMS_PAIEMENTS_ACCESS },

  { prefix: '/engagements', anyOf: PERMS_ADMIN_TECH },
  { prefix: '/tresorerie', anyOf: PERMS_ADMIN_TECH },

  { prefix: '/budget/types-budget', anyOf: PERMS_REFERENTIELS },
  { prefix: '/budget/versions', anyOf: PERMS_VERSIONS_ACCESS },
  { prefix: '/budget/previsions', anyOf: PERMS_PREVISIONS_ACCESS },
  { prefix: '/budget/soumissions', anyOf: PERMS_VERSIONS_WORKFLOW },
  { prefix: '/budget/ajustements', anyOf: PERMS_AJUSTEMENTS_ACCESS },
  {
    prefix: '/budget/historique-previsions',
    anyOf: [...PERMS_PREVISIONS_ACCESS, ...PERMS_VERSIONS_ACCESS],
  },
  { prefix: '/budget/suivi-mes-previsions', anyOf: PERMS_PREVISIONS_ACCESS },
  { prefix: '/budget/rubriques', anyOf: PERMS_REFERENTIELS },
  { prefix: '/budget/items-bi', anyOf: PERMS_REFERENTIELS },
  { prefix: '/budget/import-syscohada', anyOf: PERMS_REFERENTIELS },
  { prefix: '/budget/lignes', anyOf: PERMS_ADMIN_TECH },
  { prefix: '/budget/repartition', anyOf: PERMS_ADMIN_TECH },
  { prefix: '/budget/execution', anyOf: PERMS_ADMIN_TECH },
  { prefix: '/budget/engagements', anyOf: PERMS_ADMIN_TECH },
  { prefix: '/budget/suivi', anyOf: PERMS_ADMIN_TECH },
  { prefix: '/budget/historique', anyOf: PERMS_ADMIN_TECH },
  {
    prefix: '/budget',
    anyOf: [...PERMS_PREVISIONS_ACCESS, ...PERMS_VERSIONS_ACCESS, ...PERMS_AJUSTEMENTS_ACCESS],
  },

  { prefix: '/referentiels', anyOf: PERMS_REFERENTIELS },
  { prefix: '/referentiel-organisationnel', anyOf: PERMS_REFERENTIELS },

  { prefix: '/rapports/previsions-dc-consolide', anyOf: PERMS_RAPPORTS_PREVISIONS },
  { prefix: '/rapports/previsions-dc', anyOf: PERMS_RAPPORTS_PREVISIONS },
  { prefix: '/rapports/previsions-ae', anyOf: PERMS_RAPPORTS_PREVISIONS },
  { prefix: '/rapports/previsions-bi', anyOf: PERMS_RAPPORTS_PREVISIONS },
  { prefix: '/rapports/situation-budgetaire', anyOf: PERMS_ADMIN_TECH },
  { prefix: '/rapports/historique', anyOf: PERMS_ADMIN_TECH },
  { prefix: '/rapports', anyOf: PERMS_RAPPORTS_PREVISIONS },

  { prefix: '/administration/utilisateurs', anyOf: PERMS_ADMIN_UTILISATEURS },
  { prefix: '/administration/roles', anyOf: PERMS_ADMIN_PROFILS },
  { prefix: '/administration/permissions', anyOf: PERMS_ADMIN_PROFILS },
  { prefix: '/administration', anyOf: PERMS_ADMIN_TECH },
];

function matchPathRule(pathname: string): PathRule | null {
  const normalized =
    pathname.length > 1 && pathname.endsWith('/') ? pathname.slice(0, -1) : pathname;

  for (const rule of PATH_RULES) {
    if (normalized === rule.prefix || normalized.startsWith(`${rule.prefix}/`)) {
      return rule;
    }
  }
  return null;
}

/** Accès autorisé au pathname (deep link). Dashboard et chemins sans règle = OK. */
export function canAccessPath(pathname: string, user: AuthzUser, search = ''): boolean {
  if (pathname === '/' || pathname === '/dashboard' || pathname.startsWith('/dashboard/')) {
    return true;
  }
  if (/^\/paiements\/[^/]+\/modifier/.test(pathname)) {
    return hasAnyPerm(user, ['paiements.ecrire']);
  }
  const normalized =
    pathname.length > 1 && pathname.endsWith('/') ? pathname.slice(0, -1) : pathname;
  if (/^\/budget\/soumissions\/\d+\/\d+$/.test(normalized)) {
    const params = new URLSearchParams(search.startsWith('?') ? search.slice(1) : search);
    if (params.get('from') === 'mes') {
      return hasAnyPerm(user, PERMS_PREVISIONS_ACCESS);
    }
  }
  const rule = matchPathRule(pathname);
  if (!rule) return true;
  return hasAnyPerm(user, rule.anyOf);
}
