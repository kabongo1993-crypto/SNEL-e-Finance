import type { MockPaiement } from './types';

/** Mock paiements — replace with API later. Isolated from BD_SNEL référentiel. */
export const mockPaiements: MockPaiement[] = [
  {
    id: '1',
    numero: 'PAY-2026-0842',
    date: '2026-08-15',
    demandeur: 'Jean Kabongo',
    beneficiaire: 'SARL Bureau Plus',
    objet: 'Fournitures de bureau — Administration centrale',
    montant: 12_500_000,
    devise: 'CDF',
    departement: 'DFC',
    uniteBudgetaire: 'UB-001',
    statut: 'en_validation',
  },
  {
    id: '2',
    numero: 'PAY-2026-0839',
    date: '2026-08-14',
    demandeur: 'Grace Ilunga',
    beneficiaire: 'TechNet Solutions',
    objet: 'Prestations maintenance SI',
    montant: 45_200_000,
    devise: 'CDF',
    departement: 'DTI',
    uniteBudgetaire: 'UB-041',
    statut: 'soumis',
  },
  {
    id: '3',
    numero: 'PAY-2026-0835',
    date: '2026-08-12',
    demandeur: 'Paul Mwamba',
    beneficiaire: 'Entreprise Kabila TP',
    objet: 'Travaux réhabilitation poste GCC',
    montant: 78_000_000,
    devise: 'CDF',
    departement: 'DEC',
    uniteBudgetaire: 'UB-014',
    statut: 'valide',
  },
  {
    id: '4',
    numero: 'PAY-2026-0828',
    date: '2026-08-08',
    demandeur: 'Sophie Kalala',
    beneficiaire: 'SOCOFER',
    objet: 'Fourniture pièces transformateurs',
    montant: 156_000_000,
    devise: 'CDF',
    departement: 'DEC',
    uniteBudgetaire: 'UB-014',
    statut: 'execute',
  },
  {
    id: '5',
    numero: 'PAY-2026-0821',
    date: '2026-08-05',
    demandeur: 'Jean Kabongo',
    beneficiaire: 'Imprimerie Nationale',
    objet: 'Impression documents budgétaires',
    montant: 4_800_000,
    devise: 'CDF',
    departement: 'DFC',
    uniteBudgetaire: 'UB-001',
    statut: 'rejete',
  },
  {
    id: '6',
    numero: 'PAY-2026-0815',
    date: '2026-08-01',
    demandeur: 'Grace Ilunga',
    beneficiaire: 'Cabinet Audit Congo',
    objet: 'Honoraires audit trimestriel',
    montant: 95_000_000,
    devise: 'CDF',
    departement: 'DFC',
    uniteBudgetaire: 'UB-001',
    statut: 'brouillon',
  },
  {
    id: '7',
    numero: 'PAY-2026-0802',
    date: '2026-07-28',
    demandeur: 'Paul Mwamba',
    beneficiaire: 'Total Energies RDC',
    objet: 'Carburant flotte exploitation',
    montant: 32_000_000,
    devise: 'CDF',
    departement: 'DEC',
    uniteBudgetaire: 'UB-014',
    statut: 'annule',
  },
  {
    id: '8',
    numero: 'PAY-2026-0790',
    date: '2026-07-22',
    demandeur: 'Sophie Kalala',
    beneficiaire: 'Mutuelle SNEL',
    objet: 'Cotisations sociales agents',
    montant: 210_000_000,
    devise: 'CDF',
    departement: 'DRH',
    uniteBudgetaire: 'UB-028',
    statut: 'execute',
  },
];

export function getPaiementById(id: string): MockPaiement | undefined {
  return mockPaiements.find((p) => p.id === id);
}

export const paiementCircuit = [
  { etape: 1, role: 'Demandeur', acteur: 'Jean Kabongo', date: '2026-08-15 09:12', action: 'Création', statut: 'fait' as const },
  { etape: 2, role: 'Chef de service', acteur: 'Mme Ndaya', date: '2026-08-15 11:40', action: 'Visa hiérarchique', statut: 'fait' as const },
  { etape: 3, role: 'Contrôle budgétaire', acteur: '—', date: '—', action: 'Contrôle disponibilité', statut: 'en_cours' as const },
  { etape: 4, role: 'Ordonnateur', acteur: '—', date: '—', action: 'Validation finale', statut: 'attente' as const },
  { etape: 5, role: 'Trésorerie', acteur: '—', date: '—', action: 'Exécution paiement', statut: 'attente' as const },
];

export const paiementPieces = [
  { id: 'pj1', nom: 'Facture_BureauPlus_0842.pdf', type: 'Facture', taille: '245 Ko', date: '2026-08-14' },
  { id: 'pj2', nom: 'Bon_commande_BC-441.pdf', type: 'Bon de commande', taille: '128 Ko', date: '2026-08-10' },
  { id: 'pj3', nom: 'PV_reception.pdf', type: 'PV réception', taille: '96 Ko', date: '2026-08-13' },
];

export const paiementCommentaires = [
  { id: 'c1', auteur: 'Mme Ndaya', date: '2026-08-15 11:42', texte: 'Pièces conformes. Transmis au contrôle budgétaire.' },
  { id: 'c2', auteur: 'Jean Kabongo', date: '2026-08-15 09:15', texte: 'Demande urgente — stock fournitures épuisé.' },
];
