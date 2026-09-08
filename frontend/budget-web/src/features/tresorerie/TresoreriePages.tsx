import AssignmentTurnedInOutlinedIcon from '@mui/icons-material/AssignmentTurnedInOutlined';
import CancelOutlinedIcon from '@mui/icons-material/CancelOutlined';
import DrawOutlinedIcon from '@mui/icons-material/DrawOutlined';
import HourglassTopOutlinedIcon from '@mui/icons-material/HourglassTopOutlined';
import InboxOutlinedIcon from '@mui/icons-material/InboxOutlined';
import PendingActionsOutlinedIcon from '@mui/icons-material/PendingActionsOutlined';
import { Box, Grid, Paper, Typography } from '@mui/material';
import { useMemo } from 'react';
import { KpiRow, ModulePlaceholder, PageHeader, StatCard } from '../../components';
import type { TresorerieDashboardStat, TresorerieDocumentStatutId } from '../../mocks/tresorerieDashboard';
import { fetchTresorerieDashboardSnapshot } from './tresorerieDashboardService';

const STAT_ICONS: Record<TresorerieDocumentStatutId, typeof InboxOutlinedIcon> = {
  documents_recus: InboxOutlinedIcon,
  a_traiter: PendingActionsOutlinedIcon,
  en_signature: DrawOutlinedIcon,
  rejetes: CancelOutlinedIcon,
  payes: AssignmentTurnedInOutlinedIcon,
  encours: HourglassTopOutlinedIcon,
};

function formatStatValue(value: number): string {
  return value.toLocaleString('fr-FR');
}

function DashboardStatCard({ stat }: { stat: TresorerieDashboardStat }) {
  const Icon = STAT_ICONS[stat.id];
  return (
    <StatCard
      title={stat.label}
      value={formatStatValue(stat.value)}
      subtitle={stat.hint}
      icon={<Icon />}
    />
  );
}

export function TresorerieDashboardPage() {
  const snapshot = useMemo(() => fetchTresorerieDashboardSnapshot(), []);

  return (
    <Box>
      <PageHeader
        title="Tableau de bord"
        subtitle="Vue d'ensemble de l'activité trésorerie (données mock)."
        breadcrumbs={[
          { label: 'Trésorerie', to: '/tresorerie/dashboard' },
          { label: 'Tableau de bord' },
        ]}
      />

      <KpiRow columns={3}>
        {snapshot.stats.map((stat) => (
          <DashboardStatCard key={stat.id} stat={stat} />
        ))}
      </KpiRow>

      <Grid container spacing={2.5} sx={{ mt: 0.5 }}>
        <Grid size={{ xs: 12, lg: 7 }}>
          <Paper sx={{ p: 2.5, bgcolor: 'var(--ef-surface)' }}>
            <Typography variant="subtitle1" sx={{ fontWeight: 700, mb: 1 }}>
              Activité récente
            </Typography>
            <Typography variant="body2" color="text.secondary">
              Les flux détaillés (réception DPM, traitement, signature, règlement) seront affichés ici
              une fois les écrans de traitement connectés aux services métier.
            </Typography>
          </Paper>
        </Grid>
        <Grid size={{ xs: 12, lg: 5 }}>
          <Paper sx={{ p: 2.5, bgcolor: 'var(--ef-surface)' }}>
            <Typography variant="subtitle1" sx={{ fontWeight: 700, mb: 1 }}>
              Rappels
            </Typography>
            <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>
              Les statuts ci-dessus sont des indicateurs — ils seront utilisés comme filtres dans les
              écrans de liste (Décaissements, Suivi des paiements), pas comme entrées de menu.
            </Typography>
            <Typography variant="caption" color="text.secondary">
              Dernière mise à jour (mock) :{' '}
              {new Date(snapshot.generatedAt).toLocaleString('fr-FR', {
                dateStyle: 'medium',
                timeStyle: 'short',
              })}
            </Typography>
          </Paper>
        </Grid>
      </Grid>
    </Box>
  );
}

export function TresorerieMouvementsPage() {
  return (
    <ModulePlaceholder
      title="Mouvements de trésorerie"
      subtitle="Journal des encaissements et décaissements — écran historique, à reconnecter."
      moduleLabel="Trésorerie"
      breadcrumbs={[
        { label: 'Trésorerie', to: '/tresorerie/dashboard' },
        { label: 'Mouvements' },
      ]}
    />
  );
}

export function TresorerieModulePage({
  title,
  pathLabel,
  parentLabel,
}: {
  title: string;
  pathLabel: string;
  parentLabel?: string;
}) {
  return (
    <ModulePlaceholder
      title={title}
      subtitle="Module Trésorerie — écran en cours de construction."
      moduleLabel="Trésorerie"
      breadcrumbs={[
        { label: 'Trésorerie', to: '/tresorerie/dashboard' },
        ...(parentLabel ? [{ label: parentLabel }] : []),
        { label: pathLabel },
      ]}
    />
  );
}
