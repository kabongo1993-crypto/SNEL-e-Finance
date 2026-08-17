import { Button, Grid, Paper, Typography } from '@mui/material';
import { Link as RouterLink } from 'react-router-dom';
import { PageHeader, StatCard } from '../../components';
import { mockPaiements } from '../../mocks/paiements';
import { formatMontant } from '../../mocks/types';
import { PaiementsListPage } from './PaiementsListPage';

export function PaiementsDashboardPage() {
  const counts = {
    total: mockPaiements.length,
    aValider: mockPaiements.filter((p) => p.statut === 'en_validation' || p.statut === 'soumis').length,
    valides: mockPaiements.filter((p) => p.statut === 'valide').length,
    executes: mockPaiements.filter((p) => p.statut === 'execute').length,
    montant: mockPaiements.reduce((s, p) => s + p.montant, 0),
  };

  return (
    <>
      <PageHeader
        title="Tableau de bord paiements"
        subtitle="Pilotage des demandes de paiement et des flux de validation."
        breadcrumbs={[
          { label: 'e-Finance', to: '/dashboard' },
          { label: 'Paiements' },
        ]}
        actions={
          <Button variant="contained" component={RouterLink} to="/paiements/nouveau">
            Nouveau paiement
          </Button>
        }
      />
      <Grid container spacing={2} sx={{ mb: 3 }}>
        <Grid size={{ xs: 12, sm: 6, md: 4, lg: 2.4 }}>
          <StatCard title="Demandes" value={String(counts.total)} />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, md: 4, lg: 2.4 }}>
          <StatCard title="À valider" value={String(counts.aValider)} />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, md: 4, lg: 2.4 }}>
          <StatCard title="Validés" value={String(counts.valides)} />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, md: 4, lg: 2.4 }}>
          <StatCard title="Exécutés" value={String(counts.executes)} />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, md: 4, lg: 2.4 }}>
          <StatCard title="Volume" value={formatMontant(counts.montant)} />
        </Grid>
      </Grid>
      <Paper sx={{ p: 2, mb: 2 }}>
        <Typography variant="body2" color="text.secondary">
          Accès rapide : demandes, validations, historique. Les données ci-dessous sont mockées.
        </Typography>
      </Paper>
      <Typography variant="h6" sx={{ mb: 1.5 }}>
        Demandes récentes
      </Typography>
      <PaiementsListPage title="Demandes" showNewButton={false} hideHeader />
    </>
  );
}

export { PaiementsListPage } from './PaiementsListPage';
export { PaiementDetailPage } from './PaiementDetailPage';
export { NouveauPaiementPage } from './NouveauPaiementPage';
