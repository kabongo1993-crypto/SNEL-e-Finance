import DashboardOutlinedIcon from '@mui/icons-material/DashboardOutlined';
import AccountTreeOutlinedIcon from '@mui/icons-material/AccountTreeOutlined';
import AccountBalanceOutlinedIcon from '@mui/icons-material/AccountBalanceOutlined';
import AssessmentOutlinedIcon from '@mui/icons-material/AssessmentOutlined';
import SettingsOutlinedIcon from '@mui/icons-material/SettingsOutlined';
import WorkOutlineOutlinedIcon from '@mui/icons-material/WorkOutlineOutlined';
import type { SvgIconComponent } from '@mui/icons-material';

export interface NavItem {
  label: string;
  path: string;
}

export interface NavGroup {
  id: string;
  label: string;
  icon: SvgIconComponent;
  path?: string;
  children?: NavItem[];
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
      { label: 'Paiements', path: '/paiements' },
      { label: 'Engagements', path: '/engagements' },
      { label: 'Trésorerie', path: '/tresorerie' },
    ],
  },
  {
    id: 'budget',
    label: 'Budget',
    icon: AccountBalanceOutlinedIcon,
    children: [
      { label: 'Budgets', path: '/budget/lignes' },
      { label: 'Prévisions', path: '/budget/previsions' },
      { label: 'Répartition', path: '/budget/repartition' },
      { label: 'Exécution', path: '/budget/execution' },
    ],
  },
  {
    id: 'referentiels',
    label: 'Référentiels',
    icon: AccountTreeOutlinedIcon,
    children: [
      { label: 'Organisation', path: '/referentiels/organisationnel' },
      { label: 'Départements', path: '/referentiels/departements' },
      { label: 'Structures', path: '/referentiels/structures' },
      { label: 'Unités budgétaires', path: '/referentiels/unites-budgetaires' },
      { label: 'Exercices', path: '/referentiels/exercices' },
      { label: 'Natures de dépenses', path: '/referentiels/natures' },
    ],
  },
  {
    id: 'analyse',
    label: 'Analyse',
    icon: AssessmentOutlinedIcon,
    children: [
      { label: 'Rapports', path: '/rapports' },
      { label: 'États financiers', path: '/rapports/situation-budgetaire' },
      { label: 'Historique', path: '/rapports/historique' },
    ],
  },
  {
    id: 'administration',
    label: 'Administration',
    icon: SettingsOutlinedIcon,
    children: [
      { label: 'Utilisateurs', path: '/administration/utilisateurs' },
      { label: 'Rôles', path: '/administration/roles' },
      { label: 'Permissions', path: '/administration/permissions' },
      { label: 'Paramètres', path: '/administration/parametres' },
      { label: 'Audit', path: '/administration/audit' },
    ],
  },
];
