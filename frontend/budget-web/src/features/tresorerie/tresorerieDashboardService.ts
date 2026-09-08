import {
  mockTresorerieDashboardSnapshot,
  type TresorerieDashboardSnapshot,
} from '../../mocks/tresorerieDashboard';

/**
 * Point d'accès unique aux indicateurs du tableau de bord Trésorerie.
 * Remplacer l'implémentation par un appel API sans modifier les composants.
 */
export function fetchTresorerieDashboardSnapshot(): TresorerieDashboardSnapshot {
  return mockTresorerieDashboardSnapshot;
}
