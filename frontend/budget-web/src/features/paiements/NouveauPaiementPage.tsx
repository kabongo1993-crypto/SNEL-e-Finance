import { DemandePaiementFormPage } from './DemandePaiementFormPage';

export function NouveauPaiementPage() {
  return <DemandePaiementFormPage mode="create" />;
}

export function ModifierDemandePaiementPage() {
  return <DemandePaiementFormPage mode="edit" />;
}
