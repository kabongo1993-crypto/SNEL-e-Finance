import DashboardOutlinedIcon from '@mui/icons-material/DashboardOutlined';
import AccountBalanceWalletOutlinedIcon from '@mui/icons-material/AccountBalanceWalletOutlined';
import PaymentsOutlinedIcon from '@mui/icons-material/PaymentsOutlined';
import EventNoteOutlinedIcon from '@mui/icons-material/EventNoteOutlined';
import TrackChangesOutlinedIcon from '@mui/icons-material/TrackChangesOutlined';
import SearchOutlinedIcon from '@mui/icons-material/SearchOutlined';
import InsightsOutlinedIcon from '@mui/icons-material/InsightsOutlined';
import AccountBalanceOutlinedIcon from '@mui/icons-material/AccountBalanceOutlined';
import AccountTreeOutlinedIcon from '@mui/icons-material/AccountTreeOutlined';
import SettingsOutlinedIcon from '@mui/icons-material/SettingsOutlined';
import { PERMS_ADMIN_TECH } from '../features/auth/permissions';
import type { NavGroup } from './navConfig';

/** Navigation dédiée à l'espace Trésorerie (séparée du menu Budget). */
export const tresorerieNavGroups: NavGroup[] = [
  {
    id: 'tresorerie-dashboard',
    label: 'Tableau de bord',
    icon: DashboardOutlinedIcon,
    path: '/tresorerie/dashboard',
    anyOf: PERMS_ADMIN_TECH,
  },
  {
    id: 'tresorerie-modules',
    label: 'Trésorerie',
    icon: AccountBalanceWalletOutlinedIcon,
    anyOf: PERMS_ADMIN_TECH,
    children: [
      {
        id: 'decaissements',
        label: 'Décaissements',
        path: '/tresorerie/decaissements',
        anyOf: PERMS_ADMIN_TECH,
      },
      {
        id: 'programmation-caisse',
        label: 'Programmation caisse',
        path: '/tresorerie/programmation-caisse',
        anyOf: PERMS_ADMIN_TECH,
      },
      {
        id: 'suivi-paiements',
        label: 'Suivi des paiements',
        path: '/tresorerie/suivi-paiements',
        anyOf: PERMS_ADMIN_TECH,
      },
      {
        id: 'recherche',
        label: 'Recherche',
        path: '/tresorerie/recherche',
        anyOf: PERMS_ADMIN_TECH,
      },
      {
        id: 'analyses',
        label: 'Analyses',
        path: '/tresorerie/analyses',
        anyOf: PERMS_ADMIN_TECH,
      },
      {
        id: 'comptes-financiers',
        label: 'Comptes financiers',
        path: '/tresorerie/comptes-financiers',
        anyOf: PERMS_ADMIN_TECH,
      },
      {
        id: 'configuration',
        label: 'Configuration',
        path: '/tresorerie/configuration',
        anyOf: PERMS_ADMIN_TECH,
      },
    ],
  },
  {
    id: 'tresorerie-referentiels',
    label: 'Référentiels',
    icon: AccountTreeOutlinedIcon,
    anyOf: PERMS_ADMIN_TECH,
    children: [
      {
        id: 'banques',
        label: 'Banques',
        path: '/tresorerie/referentiels/banques',
        anyOf: PERMS_ADMIN_TECH,
      },
      {
        id: 'comptes',
        label: 'Comptes',
        path: '/tresorerie/referentiels/comptes',
        anyOf: PERMS_ADMIN_TECH,
      },
      {
        id: 'categories-comptes',
        label: 'Catégories de comptes',
        path: '/tresorerie/referentiels/categories-comptes',
        anyOf: PERMS_ADMIN_TECH,
      },
      {
        id: 'directions',
        label: 'Directions',
        path: '/tresorerie/referentiels/directions',
        anyOf: PERMS_ADMIN_TECH,
      },
      {
        id: 'groupes-types-comptes',
        label: 'Groupes de types de comptes',
        path: '/tresorerie/referentiels/groupes-types-comptes',
        anyOf: PERMS_ADMIN_TECH,
      },
      {
        id: 'provinces',
        label: 'Provinces',
        path: '/tresorerie/referentiels/provinces',
        anyOf: PERMS_ADMIN_TECH,
      },
      {
        id: 'types-comptes',
        label: 'Types de comptes',
        path: '/tresorerie/referentiels/types-comptes',
        anyOf: PERMS_ADMIN_TECH,
      },
      {
        id: 'devises',
        label: 'Devises',
        path: '/tresorerie/referentiels/devises',
        anyOf: PERMS_ADMIN_TECH,
      },
      {
        id: 'taux-change',
        label: 'Taux de change',
        path: '/tresorerie/referentiels/taux-change',
        anyOf: PERMS_ADMIN_TECH,
      },
    ],
  },
];

/** Icônes associées aux entrées (dashboard KPI, documentation interne). */
export const tresorerieNavIcons = {
  dashboard: DashboardOutlinedIcon,
  decaissements: PaymentsOutlinedIcon,
  programmationCaisse: EventNoteOutlinedIcon,
  suiviPaiements: TrackChangesOutlinedIcon,
  recherche: SearchOutlinedIcon,
  analyses: InsightsOutlinedIcon,
  comptesFinanciers: AccountBalanceOutlinedIcon,
  configuration: SettingsOutlinedIcon,
} as const;
