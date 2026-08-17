import type { EntityStatus } from '../types/status';

/** Shared mock filters — replace with API query params later. */
export interface PeriodFilter {
  exercice: string;
  periode: string;
  departementId: string | 'all';
  uniteBudgetaireId: string | 'all';
}

export const MOCK_EXERCICES = ['2024', '2025', '2026'];
export const MOCK_PERIODES = [
  { value: 'annee', label: 'Année complète' },
  { value: 't1', label: '1er trimestre' },
  { value: 't2', label: '2e trimestre' },
  { value: 't3', label: '3e trimestre' },
  { value: 't4', label: '4e trimestre' },
  { value: 'mois', label: 'Mois en cours' },
];

export const MOCK_DEPARTEMENTS = [
  { id: 'DEC', label: 'Direction Exploitation Commerciale' },
  { id: 'DFC', label: 'Direction Financière et Comptable' },
  { id: 'DRH', label: 'Direction des Ressources Humaines' },
  { id: 'DTI', label: 'Direction des Technologies' },
];

export const MOCK_UNITES = [
  { id: 'UB-001', label: 'UB Administration Centrale', departementId: 'DFC' },
  { id: 'UB-014', label: 'UB Exploitation Kinshasa', departementId: 'DEC' },
  { id: 'UB-028', label: 'UB Paie & Charges', departementId: 'DRH' },
  { id: 'UB-041', label: 'UB Systèmes & Réseaux', departementId: 'DTI' },
];

export function formatMontant(value: number, devise = 'CDF'): string {
  return `${new Intl.NumberFormat('fr-CD', {
    maximumFractionDigits: 0,
  }).format(value)} ${devise}`;
}

export function formatDateFr(iso: string): string {
  return new Intl.DateTimeFormat('fr-FR', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
  }).format(new Date(iso));
}

export interface MockPaiement {
  id: string;
  numero: string;
  date: string;
  demandeur: string;
  beneficiaire: string;
  objet: string;
  montant: number;
  devise: string;
  departement: string;
  uniteBudgetaire: string;
  statut: EntityStatus;
}

export interface MockEngagement {
  id: string;
  numero: string;
  date: string;
  objet: string;
  fournisseur: string;
  montant: number;
  budgetCode: string;
  disponible: number;
  statut: EntityStatus;
}

export interface MockLigneBudget {
  id: string;
  code: string;
  libelle: string;
  departement: string;
  uniteBudgetaire: string;
  budgetInitial: number;
  revisions: number;
  budgetDisponible: number;
  engage: number;
  execute: number;
  tauxExecution: number;
}

export interface MockMouvementTreso {
  id: string;
  date: string;
  reference: string;
  libelle: string;
  compte: string;
  type: 'encaissement' | 'decaissement';
  montant: number;
  soldeApres: number;
}

export interface MockUtilisateur {
  id: string;
  nom: string;
  email: string;
  role: string;
  departement: string;
  actif: boolean;
  derniereConnexion: string;
}
