export type EntityStatus =
  | 'brouillon'
  | 'soumis'
  | 'en_attente'
  | 'en_validation'
  | 'valide'
  | 'rejete'
  | 'execute'
  | 'annule';

export const STATUS_LABELS: Record<EntityStatus, string> = {
  brouillon: 'Brouillon',
  soumis: 'Soumis',
  en_attente: 'En attente',
  en_validation: 'En validation',
  valide: 'Validé',
  rejete: 'Rejeté',
  execute: 'Exécuté',
  annule: 'Annulé',
};

export type StatusTone = 'default' | 'info' | 'warning' | 'success' | 'error' | 'secondary';

export const STATUS_TONES: Record<EntityStatus, StatusTone> = {
  brouillon: 'default',
  soumis: 'info',
  en_attente: 'warning',
  en_validation: 'warning',
  valide: 'success',
  rejete: 'error',
  execute: 'secondary',
  annule: 'default',
};
