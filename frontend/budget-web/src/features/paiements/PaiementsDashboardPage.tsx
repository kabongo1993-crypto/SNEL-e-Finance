import { PaiementsListPage } from './PaiementsListPage';

/** Point d’entrée Opérations → Paiements : liste « Mes demandes ». */
export function PaiementsDashboardPage() {
  return <PaiementsListPage title="Mes demandes de paiement" hideHeader />;
}

export { PaiementsListPage } from './PaiementsListPage';
