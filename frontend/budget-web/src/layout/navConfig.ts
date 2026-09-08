import DashboardOutlinedIcon from '@mui/icons-material/DashboardOutlined';
import AccountTreeOutlinedIcon from '@mui/icons-material/AccountTreeOutlined';
import AccountBalanceOutlinedIcon from '@mui/icons-material/AccountBalanceOutlined';
import AssessmentOutlinedIcon from '@mui/icons-material/AssessmentOutlined';
import SettingsOutlinedIcon from '@mui/icons-material/SettingsOutlined';
import WorkOutlineOutlinedIcon from '@mui/icons-material/WorkOutlineOutlined';
import type { SvgIconComponent } from '@mui/icons-material';
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
} from '../features/auth/permissions';

/** Entrée feuille (navigable) ou sous-rubrique parent (sans path). */
export interface NavItem {
  id?: string;
  label: string;
  /** Absent = rubrique parent non navigable (toggle uniquement). */
  path?: string;
  children?: NavItem[];
  /**
   * Permissions requises (OR). Absent = visible pour tout utilisateur authentifié.
   * `admin.all` / rôle admin couverts via `hasPerm`.
   */
  anyOf?: readonly string[];
  /** Compteur affiché à droite de l'entrée (dossiers en attente, etc.). */
  badge?: number;
}

export interface NavGroup {
  id: string;
  label: string;
  icon: SvgIconComponent;
  path?: string;
  children?: NavItem[];
  anyOf?: readonly string[];
  badge?: number;
}

/** Premier chemin feuille d’un nœud (sidebar repliée). */
export function getFirstNavLeafPath(item: NavItem | NavGroup): string | undefined {
  if ('path' in item && item.path && !item.children?.length) return item.path;
  if (item.children?.length) {
    for (const child of item.children) {
      const leaf = getFirstNavLeafPath(child);
      if (leaf) return leaf;
    }
  }
  if ('path' in item && item.path) return item.path;
  return undefined;
}

/** True si le pathname correspond à un descendant navigable. */
export function navItemMatchesPath(item: NavItem, pathname: string): boolean {
  if (item.path && (pathname === item.path || pathname.startsWith(`${item.path}/`))) {
    return true;
  }
  return item.children?.some((c) => navItemMatchesPath(c, pathname)) ?? false;
}

/** Navigation métier e-Finance — groupes logiques productifs. */
export const navGroups: NavGroup[] = [
  {
    id: 'dashboard',
    label: 'Tableau de bord',
    icon: DashboardOutlinedIcon,
    path: '/dashboard',
  },
  {
    id: 'operations',
    label: 'Opérations',
    icon: WorkOutlineOutlinedIcon,
    children: [
      {
        label: 'Paiements',
        children: [
          { label: 'Mes demandes', path: '/paiements', anyOf: PERMS_PAIEMENTS_ACCESS },
          {
            label: 'Demandes de paiement — Chargé DP',
            path: '/paiements/charge-dpm',
            anyOf: PERMS_CHARGE_DP,
          },
          {
            label: 'Documents établis',
            path: '/paiements/documents-etablis',
            anyOf: PERMS_CHARGE_DP,
          },
          { label: 'Junior DC', path: '/paiements/junior-dc', anyOf: ['paiements.imputer_dc'] },
          { label: 'Junior AE', path: '/paiements/junior-ae', anyOf: ['paiements.imputer_ae'] },
          { label: 'Junior BI', path: '/paiements/junior-bi', anyOf: ['paiements.imputer_bi'] },
          { label: 'Budget', path: '/paiements/budget', anyOf: PERMS_PAIEMENTS_BUDGET },
        ],
      },
      { label: 'Engagements', path: '/engagements', anyOf: PERMS_ADMIN_TECH },
    ],
  },
  {
    id: 'budget',
    label: 'Budget',
    icon: AccountBalanceOutlinedIcon,
    children: [
      { label: 'Types de budget', path: '/budget/types-budget', anyOf: PERMS_REFERENTIELS },
      { label: 'Versions budgétaires', path: '/budget/versions', anyOf: PERMS_VERSIONS_ACCESS },
      { label: 'Budgets', path: '/budget/lignes', anyOf: PERMS_ADMIN_TECH },
      { label: 'Prévisions', path: '/budget/previsions', anyOf: PERMS_PREVISIONS_ACCESS },
      { label: 'Soumissions', path: '/budget/soumissions', anyOf: PERMS_VERSIONS_WORKFLOW },
      {
        label: 'Ajustements budgétaires',
        path: '/budget/ajustements',
        anyOf: PERMS_AJUSTEMENTS_ACCESS,
      },
      {
        label: 'Historique',
        path: '/budget/historique-previsions',
        anyOf: [...PERMS_PREVISIONS_ACCESS, ...PERMS_VERSIONS_ACCESS],
      },
      {
        label: 'Suivi de mes prévisions',
        path: '/budget/suivi-mes-previsions',
        anyOf: PERMS_PREVISIONS_ACCESS,
      },
      { label: 'Répartition', path: '/budget/repartition', anyOf: PERMS_ADMIN_TECH },
      { label: 'Exécution', path: '/budget/execution', anyOf: PERMS_ADMIN_TECH },
    ],
  },
  {
    id: 'referentiels',
    label: 'Référentiels',
    icon: AccountTreeOutlinedIcon,
    children: [
      {
        label: 'Organisation',
        path: '/referentiels/organisationnel',
        anyOf: PERMS_REFERENTIELS,
      },
      { label: 'Départements', path: '/referentiels/departements', anyOf: PERMS_REFERENTIELS },
      { label: 'Structures', path: '/referentiels/structures', anyOf: PERMS_REFERENTIELS },
      {
        label: 'Unités budgétaires',
        path: '/referentiels/unites-budgetaires',
        anyOf: PERMS_REFERENTIELS,
      },
      { label: 'Exercices', path: '/referentiels/exercices', anyOf: PERMS_REFERENTIELS },
      { label: 'Devises', path: '/referentiels/devises', anyOf: PERMS_REFERENTIELS },
      { label: 'Cas de dossiers', path: '/referentiels/cas-dossiers', anyOf: PERMS_REFERENTIELS },
      {
        label: 'Instruments de paiement',
        path: '/referentiels/instruments-paiement',
        anyOf: PERMS_REFERENTIELS,
      },
      { label: 'Taux de change', path: '/referentiels/taux-change', anyOf: PERMS_REFERENTIELS },
      { label: 'Rubriques budgétaires', path: '/budget/rubriques', anyOf: PERMS_REFERENTIELS },
      { label: 'Items BI', path: '/budget/items-bi', anyOf: PERMS_REFERENTIELS },
    ],
  },
  {
    id: 'analyse',
    label: 'Analyse',
    icon: AssessmentOutlinedIcon,
    children: [
      {
        id: 'rapports',
        label: 'Rapports',
        children: [
          {
            label: 'Prévisions DC',
            path: '/rapports/previsions-dc',
            anyOf: PERMS_RAPPORTS_PREVISIONS,
          },
          {
            label: 'Prévisions DC consolidées',
            path: '/rapports/previsions-dc-consolide',
            anyOf: PERMS_RAPPORTS_PREVISIONS,
          },
          {
            label: "Prévisions AE — Actions d'exploitation",
            path: '/rapports/previsions-ae',
            anyOf: PERMS_RAPPORTS_PREVISIONS,
          },
          {
            label: 'Prévisions BI — Investissements',
            path: '/rapports/previsions-bi',
            anyOf: PERMS_RAPPORTS_PREVISIONS,
          },
        ],
      },
      {
        label: 'États financiers',
        path: '/rapports/situation-budgetaire',
        anyOf: PERMS_ADMIN_TECH,
      },
      { label: 'Historique', path: '/rapports/historique', anyOf: PERMS_ADMIN_TECH },
    ],
  },
  {
    id: 'administration',
    label: 'Administration',
    icon: SettingsOutlinedIcon,
    children: [
      {
        label: 'Utilisateurs',
        path: '/administration/utilisateurs',
        anyOf: PERMS_ADMIN_UTILISATEURS,
      },
      { label: 'Rôles', path: '/administration/roles', anyOf: PERMS_ADMIN_PROFILS },
      { label: 'Permissions', path: '/administration/permissions', anyOf: PERMS_ADMIN_PROFILS },
      { label: 'Paramètres', path: '/administration/parametres', anyOf: PERMS_ADMIN_TECH },
      { label: 'Audit', path: '/administration/audit', anyOf: PERMS_ADMIN_TECH },
    ],
  },
];
