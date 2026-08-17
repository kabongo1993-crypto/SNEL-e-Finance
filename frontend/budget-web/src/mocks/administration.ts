import type { MockUtilisateur } from './types';

export const mockUtilisateurs: MockUtilisateur[] = [
  {
    id: 'u1',
    nom: 'Jean Kabongo',
    email: 'j.kabongo@snel.cd',
    role: 'Demandeur',
    departement: 'DFC',
    actif: true,
    derniereConnexion: '2026-08-16 08:42',
  },
  {
    id: 'u2',
    nom: 'Grace Ilunga',
    email: 'g.ilunga@snel.cd',
    role: 'Contrôleur budgétaire',
    departement: 'DFC',
    actif: true,
    derniereConnexion: '2026-08-16 09:10',
  },
  {
    id: 'u3',
    nom: 'Paul Mwamba',
    email: 'p.mwamba@snel.cd',
    role: 'Ordonnateur',
    departement: 'DEC',
    actif: true,
    derniereConnexion: '2026-08-15 17:22',
  },
  {
    id: 'u4',
    nom: 'Sophie Kalala',
    email: 's.kalala@snel.cd',
    role: 'Trésorier',
    departement: 'DFC',
    actif: true,
    derniereConnexion: '2026-08-16 07:55',
  },
  {
    id: 'u5',
    nom: 'Admin Système',
    email: 'admin@snel.cd',
    role: 'Administrateur',
    departement: 'DTI',
    actif: false,
    derniereConnexion: '2026-07-01 12:00',
  },
];

export const mockRoles = [
  { id: 'r1', code: 'DEMANDEUR', libelle: 'Demandeur', utilisateurs: 42 },
  { id: 'r2', code: 'CONTROLEUR', libelle: 'Contrôleur budgétaire', utilisateurs: 8 },
  { id: 'r3', code: 'ORDONNATEUR', libelle: 'Ordonnateur', utilisateurs: 5 },
  { id: 'r4', code: 'TRESORIER', libelle: 'Trésorier', utilisateurs: 4 },
  { id: 'r5', code: 'ADMIN', libelle: 'Administrateur', utilisateurs: 2 },
];

export const mockCircuits = [
  { id: 'ci1', nom: 'Circuit paiements standard', etapes: 5, actif: true },
  { id: 'ci2', nom: 'Circuit engagements', etapes: 4, actif: true },
  { id: 'ci3', nom: 'Circuit urgences (< 10 M CDF)', etapes: 3, actif: false },
];

export const mockAuditLog = [
  { id: 'l1', date: '2026-08-16 09:12', utilisateur: 'g.ilunga', action: 'VALIDATION', objet: 'PAY-2026-0835', detail: 'Paiement validé' },
  { id: 'l2', date: '2026-08-16 08:55', utilisateur: 'j.kabongo', action: 'CREATION', objet: 'PAY-2026-0842', detail: 'Nouvelle demande' },
  { id: 'l3', date: '2026-08-15 17:30', utilisateur: 's.kalala', action: 'EXECUTION', objet: 'PAY-2026-0828', detail: 'Paiement exécuté' },
  { id: 'l4', date: '2026-08-15 14:10', utilisateur: 'p.mwamba', action: 'REJET', objet: 'ENG-2026-0290', detail: 'Pièces insuffisantes' },
];

export const mockRapports = [
  { id: 'rp1', categorie: 'Financier', titre: 'Situation financière consolidée', description: 'Vue d’ensemble soldes, flux et engagements.' },
  { id: 'rp2', categorie: 'Budget', titre: 'Situation budgétaire', description: 'Consommation par département et UB.' },
  { id: 'rp3', categorie: 'Trésorerie', titre: 'Situation de trésorerie', description: 'Soldes comptes et prévisions de liquidité.' },
  { id: 'rp4', categorie: 'Paiements', titre: 'État des paiements', description: 'Suivi des demandes par statut.' },
  { id: 'rp5', categorie: 'Budget', titre: 'Exécution budgétaire', description: 'Taux d’exécution et écarts.' },
  { id: 'rp6', categorie: 'Engagements', titre: 'État des engagements', description: 'Engagements ouverts et restes à liquider.' },
  { id: 'rp7', categorie: 'Audit', titre: 'Historique des opérations', description: 'Journal des opérations sur la période.' },
];

/** Placeholder référentiels (hors organisationnel réel). */
export const mockDevises = [
  { code: 'CDF', libelle: 'Franc congolais', symbole: 'FC' },
  { code: 'USD', libelle: 'Dollar américain', symbole: '$' },
  { code: 'EUR', libelle: 'Euro', symbole: '€' },
];

export const mockModesPaiement = [
  { code: 'VIR', libelle: 'Virement bancaire' },
  { code: 'CHQ', libelle: 'Chèque' },
  { code: 'ESP', libelle: 'Espèces' },
];

export const mockNatures = [
  { code: 'FONCT', libelle: 'Fonctionnement' },
  { code: 'PERS', libelle: 'Personnel' },
  { code: 'INV', libelle: 'Investissement' },
  { code: 'DETTE', libelle: 'Service de la dette' },
];

export const mockSources = [
  { code: 'REC', libelle: 'Recettes propres' },
  { code: 'SUB', libelle: 'Subventions' },
  { code: 'EMP', libelle: 'Emprunts' },
];
