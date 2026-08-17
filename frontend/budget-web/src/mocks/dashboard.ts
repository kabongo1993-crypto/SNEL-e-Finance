/** Mock dashboard data — swap for API services later. Keep isolated from real référentiel API. */

export const dashboardKpis = {
  soldeDisponible: 18_450_000_000,
  encaissements: 4_820_000_000,
  decaissements: 3_610_000_000,
  engagements: 2_140_000_000,
  budgetConsomme: 62.4,
  budgetDisponible: 12_300_000_000,
};

export const evolutionEncaissements = [
  { mois: 'Jan', montant: 320 },
  { mois: 'Fév', montant: 410 },
  { mois: 'Mar', montant: 380 },
  { mois: 'Avr', montant: 450 },
  { mois: 'Mai', montant: 520 },
  { mois: 'Jun', montant: 490 },
  { mois: 'Jul', montant: 560 },
  { mois: 'Aoû', montant: 610 },
];

export const evolutionDecaissements = [
  { mois: 'Jan', montant: 280 },
  { mois: 'Fév', montant: 350 },
  { mois: 'Mar', montant: 310 },
  { mois: 'Avr', montant: 390 },
  { mois: 'Mai', montant: 420 },
  { mois: 'Jun', montant: 400 },
  { mois: 'Jul', montant: 470 },
  { mois: 'Aoû', montant: 510 },
];

export const budgetPipeline = [
  { etape: 'Initial', montant: 100 },
  { etape: 'Révisé', montant: 95 },
  { etape: 'Engagé', montant: 68 },
  { etape: 'Exécuté', montant: 52 },
  { etape: 'Disponible', montant: 27 },
];

export const repartitionDepartement = [
  { name: 'DEC', value: 28 },
  { name: 'DFC', value: 22 },
  { name: 'DRH', value: 15 },
  { name: 'DTI', value: 12 },
  { name: 'Autres', value: 23 },
];

export const repartitionNature = [
  { name: 'Fonctionnement', value: 42 },
  { name: 'Personnel', value: 31 },
  { name: 'Investissement', value: 18 },
  { name: 'Dettes', value: 9 },
];

export const dernieresOperations = [
  { id: '1', date: '2026-08-15', type: 'Paiement', reference: 'PAY-2026-0842', libelle: 'Fournitures bureau AC', montant: 12_500_000, statut: 'en_validation' as const },
  { id: '2', date: '2026-08-14', type: 'Engagement', reference: 'ENG-2026-0311', libelle: 'Maintenance transformateurs', montant: 185_000_000, statut: 'valide' as const },
  { id: '3', date: '2026-08-14', type: 'Encaissement', reference: 'ENC-2026-1204', libelle: 'Recouvrement facturation', montant: 890_000_000, statut: 'execute' as const },
  { id: '4', date: '2026-08-13', type: 'Paiement', reference: 'PAY-2026-0839', libelle: 'Prestations informatiques', montant: 45_200_000, statut: 'soumis' as const },
  { id: '5', date: '2026-08-12', type: 'Décaissement', reference: 'DEC-2026-0551', libelle: 'Virement salaires août', montant: 2_100_000_000, statut: 'execute' as const },
];

export const paiementsRecents = [
  { id: 'p1', numero: 'PAY-2026-0842', beneficiaire: 'SARL Bureau Plus', montant: 12_500_000, statut: 'en_validation' as const },
  { id: 'p2', numero: 'PAY-2026-0839', beneficiaire: 'TechNet Solutions', montant: 45_200_000, statut: 'soumis' as const },
  { id: 'p3', numero: 'PAY-2026-0835', beneficiaire: 'Entreprise Kabila TP', montant: 78_000_000, statut: 'valide' as const },
  { id: 'p4', numero: 'PAY-2026-0828', beneficiaire: 'SOCOFER', montant: 156_000_000, statut: 'execute' as const },
];

export const alertes = [
  { id: 'a1', niveau: 'warning' as const, message: '3 paiements en attente de validation depuis plus de 48 h.' },
  { id: 'a2', niveau: 'error' as const, message: 'Ligne budgétaire UB-014 — taux d’exécution à 94 %.' },
  { id: 'a3', niveau: 'info' as const, message: 'Rapprochement bancaire du compte principal non finalisé.' },
];

export const validationsEnAttente = [
  { id: 'v1', type: 'Paiement', reference: 'PAY-2026-0842', demandeur: 'M. Kabongo', montant: 12_500_000 },
  { id: 'v2', type: 'Engagement', reference: 'ENG-2026-0318', demandeur: 'Mme Ilunga', montant: 62_000_000 },
  { id: 'v3', type: 'Paiement', reference: 'PAY-2026-0840', demandeur: 'M. Mwamba', montant: 8_750_000 },
];
