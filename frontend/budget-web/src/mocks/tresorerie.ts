import type { MockMouvementTreso } from './types';

export const tresorerieKpis = {
  soldeTotal: 18_450_000_000,
  entrees: 4_820_000_000,
  sorties: 3_610_000_000,
  soldePrevisionnel: 16_900_000_000,
};

export const mockComptes = [
  { id: 'c1', code: '5111', libelle: 'Compte principal BCC', solde: 12_200_000_000, devise: 'CDF' },
  { id: 'c2', code: '5120', libelle: 'Compte opérations courantes', solde: 4_150_000_000, devise: 'CDF' },
  { id: 'c3', code: '5210', libelle: 'Compte USD opérations', solde: 2_100_000, devise: 'USD' },
];

export const mockMouvements: MockMouvementTreso[] = [
  {
    id: 'm1',
    date: '2026-08-15',
    reference: 'ENC-2026-1204',
    libelle: 'Recouvrement facturation clients',
    compte: '5111',
    type: 'encaissement',
    montant: 890_000_000,
    soldeApres: 12_200_000_000,
  },
  {
    id: 'm2',
    date: '2026-08-14',
    reference: 'DEC-2026-0551',
    libelle: 'Virement salaires août',
    compte: '5120',
    type: 'decaissement',
    montant: 2_100_000_000,
    soldeApres: 4_150_000_000,
  },
  {
    id: 'm3',
    date: '2026-08-13',
    reference: 'DEC-2026-0548',
    libelle: 'Paiement SOCOFER PAY-2026-0828',
    compte: '5120',
    type: 'decaissement',
    montant: 156_000_000,
    soldeApres: 6_250_000_000,
  },
  {
    id: 'm4',
    date: '2026-08-12',
    reference: 'ENC-2026-1198',
    libelle: 'Subvention investissement',
    compte: '5111',
    type: 'encaissement',
    montant: 1_500_000_000,
    soldeApres: 11_310_000_000,
  },
  {
    id: 'm5',
    date: '2026-08-11',
    reference: 'DEC-2026-0540',
    libelle: 'Règlement Mutuelle SNEL',
    compte: '5120',
    type: 'decaissement',
    montant: 210_000_000,
    soldeApres: 6_406_000_000,
  },
];
