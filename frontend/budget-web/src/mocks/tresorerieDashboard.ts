/** Identifiants métier des indicateurs d'activité trésorerie (filtres futurs, pas des routes). */
export type TresorerieDocumentStatutId =
  | 'documents_recus'
  | 'a_traiter'
  | 'en_signature'
  | 'rejetes'
  | 'payes'
  | 'encours';

export interface TresorerieDashboardStat {
  id: TresorerieDocumentStatutId;
  label: string;
  value: number;
  /** Sous-titre optionnel affiché sous la valeur. */
  hint?: string;
}

export interface TresorerieDashboardSnapshot {
  stats: TresorerieDashboardStat[];
  /** Horodatage fictif — remplacé par la réponse API. */
  generatedAt: string;
}

/** Données fictives du tableau de bord Trésorerie. */
export const mockTresorerieDashboardSnapshot: TresorerieDashboardSnapshot = {
  generatedAt: '2026-09-07T10:30:00+01:00',
  stats: [
    { id: 'documents_recus', label: 'Documents reçus', value: 24, hint: 'DPM transmis à la Trésorerie' },
    { id: 'a_traiter', label: 'À traiter', value: 12, hint: 'En attente de traitement' },
    { id: 'en_signature', label: 'En signature', value: 8, hint: 'Circuit de validation en cours' },
    { id: 'rejetes', label: 'Rejetés', value: 3, hint: 'À corriger ou archiver' },
    { id: 'payes', label: 'Payés', value: 36, hint: 'Règlements effectués' },
    { id: 'encours', label: 'Encours de paiement', value: 17, hint: 'Ordres émis, non soldés' },
  ],
};
