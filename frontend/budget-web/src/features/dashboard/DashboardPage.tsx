import AccountBalanceWalletOutlinedIcon from '@mui/icons-material/AccountBalanceWalletOutlined';
import DonutLargeOutlinedIcon from '@mui/icons-material/DonutLargeOutlined';
import PaymentsOutlinedIcon from '@mui/icons-material/PaymentsOutlined';
import TrendingUpIcon from '@mui/icons-material/TrendingUp';
import WarningAmberIcon from '@mui/icons-material/WarningAmber';
import {
  Alert,
  Box,
  Grid,
  List,
  ListItem,
  ListItemText,
  Paper,
  Stack,
  Typography,
} from '@mui/material';
import { useMemo, useState } from 'react';
import { Link as RouterLink } from 'react-router-dom';
import {
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  Legend,
  Line,
  LineChart,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import { ChartCard, FilterBar, KpiRow, PageHeader, StatCard, StatusBadge } from '../../components';
import { MsgBoxSmokeTest } from '../../components/msgbox/MsgBoxSmokeTest';
import {
  alertes,
  budgetPipeline,
  dashboardKpis,
  dernieresOperations,
  evolutionDecaissements,
  evolutionEncaissements,
  repartitionDepartement,
  validationsEnAttente,
} from '../../mocks/dashboard';
import { formatMontant } from '../../mocks/types';
import { BRAND_NAME } from '../../theme';
import type { EntityStatus } from '../../types/status';
import { useAuth, hasAnyPerm, PERMS_ADMIN_TECH, PERMS_PAIEMENTS_ACCESS } from '../auth';

const CHART = [
  'var(--ef-chart-1)',
  'var(--ef-chart-2)',
  'var(--ef-chart-3)',
  'var(--ef-chart-4)',
  'var(--ef-chart-2)',
];

export function DashboardPage() {
  const { user } = useAuth();
  const canPaiements = hasAnyPerm(user, PERMS_PAIEMENTS_ACCESS);
  const canEngagements = hasAnyPerm(user, PERMS_ADMIN_TECH);
  const validationsVisibles = useMemo(
    () =>
      validationsEnAttente.filter((v) =>
        v.type === 'Paiement' ? canPaiements : canEngagements,
      ),
    [canPaiements, canEngagements],
  );
  const [exercice, setExercice] = useState('2026');
  const [periode, setPeriode] = useState('annee');
  const [departement, setDepartement] = useState('all');
  const [unite, setUnite] = useState('all');

  const evolutionCombine = evolutionEncaissements.map((e, i) => ({
    mois: e.mois,
    encaissements: e.montant,
    decaissements: evolutionDecaissements[i]?.montant ?? 0,
  }));

  return (
    <Box>
      <PageHeader
        title="Tableau de bord"
        subtitle="Vue synthétique de la situation financière, budgétaire et des validations en cours."
        breadcrumbs={[{ label: BRAND_NAME, to: '/dashboard' }, { label: 'Tableau de bord' }]}
      />

      <MsgBoxSmokeTest />

      <FilterBar
        showPeriodFilters
        exercice={exercice}
        onExerciceChange={setExercice}
        periode={periode}
        onPeriodeChange={setPeriode}
        departement={departement}
        onDepartementChange={setDepartement}
        unite={unite}
        onUniteChange={setUnite}
      />

      <KpiRow>
        <StatCard
          title="Solde disponible"
          value={formatMontant(dashboardKpis.soldeDisponible)}
          icon={<AccountBalanceWalletOutlinedIcon />}
          trend={{ value: '+4,2 %', direction: 'up', comparison: 'vs. période précédente' }}
        />
        <StatCard
          title="Encaissements"
          value={formatMontant(dashboardKpis.encaissements)}
          icon={<TrendingUpIcon />}
          trend={{ value: '+8,1 %', direction: 'up', comparison: 'vs. période précédente' }}
        />
        <StatCard
          title="Décaissements"
          value={formatMontant(dashboardKpis.decaissements)}
          icon={<PaymentsOutlinedIcon />}
          trend={{ value: '+2,4 %', direction: 'up', comparison: 'vs. période précédente' }}
        />
        <StatCard
          title="Budget consommé"
          value={`${dashboardKpis.budgetConsomme} %`}
          icon={<DonutLargeOutlinedIcon />}
          subtitle={`Disponible ${formatMontant(dashboardKpis.budgetDisponible)}`}
        />
      </KpiRow>

      {/* Colonne principale large + colonne latérale de widgets, comme la maquette. */}
      <Grid container spacing={2.5} sx={{ alignItems: 'flex-start' }}>
        <Grid size={{ xs: 12, lg: 8 }} sx={{ minWidth: 0 }}>
          <Stack spacing={2.5}>
            <ChartCard
              title="Évolution financière"
              subtitle="Encaissements vs décaissements (milliards CDF — mock)"
              height={300}
            >
              <ResponsiveContainer width="100%" height="100%">
                <LineChart data={evolutionCombine} margin={{ top: 8, right: 8, left: -16, bottom: 0 }}>
                  <CartesianGrid strokeDasharray="3 3" stroke="var(--ef-border-subtle)" vertical={false} />
                  <XAxis dataKey="mois" tick={{ fontSize: 12 }} tickLine={false} axisLine={false} />
                  <YAxis tick={{ fontSize: 12 }} tickLine={false} axisLine={false} />
                  <Tooltip />
                  <Legend />
                  <Line
                    type="monotone"
                    dataKey="encaissements"
                    stroke="var(--ef-success)"
                    strokeWidth={2.5}
                    dot={false}
                    name="Encaissements"
                  />
                  <Line
                    type="monotone"
                    dataKey="decaissements"
                    stroke="var(--ef-warning)"
                    strokeWidth={2.5}
                    dot={false}
                    name="Décaissements"
                  />
                </LineChart>
              </ResponsiveContainer>
            </ChartCard>

            <ChartCard title="Exécution budgétaire" subtitle="Pipeline initial → disponible" height={240}>
              <ResponsiveContainer width="100%" height="100%">
                <BarChart data={budgetPipeline} margin={{ top: 8, right: 8, left: -16, bottom: 0 }}>
                  <CartesianGrid strokeDasharray="3 3" stroke="var(--ef-border-subtle)" vertical={false} />
                  <XAxis dataKey="etape" tick={{ fontSize: 11 }} tickLine={false} axisLine={false} />
                  <YAxis tick={{ fontSize: 12 }} tickLine={false} axisLine={false} />
                  <Tooltip />
                  <Bar dataKey="montant" fill="var(--ef-chart-1)" radius={[6, 6, 0, 0]} name="%" />
                </BarChart>
              </ResponsiveContainer>
            </ChartCard>

            <Paper sx={{ p: { xs: 2, sm: 2.5 } }}>
              <Typography variant="subtitle1" sx={{ fontWeight: 700, mb: 2 }}>
                Opérations récentes
              </Typography>
              <Stack>
                {dernieresOperations.map((op) => (
                  <Stack
                    key={op.id}
                    direction={{ xs: 'column', sm: 'row' }}
                    spacing={1}
                    sx={{
                      justifyContent: 'space-between',
                      alignItems: { sm: 'center' },
                      py: 1.25,
                      borderBottom: '1px solid',
                      borderColor: 'var(--ef-border-subtle)',
                      '&:last-of-type': { borderBottom: 'none', pb: 0 },
                    }}
                  >
                    <Box sx={{ minWidth: 0 }}>
                      <Typography variant="body2" sx={{ fontWeight: 650 }}>
                        {op.reference} · {op.type}
                      </Typography>
                      <Typography variant="caption" color="text.secondary">
                        {op.date} — {op.libelle}
                      </Typography>
                    </Box>
                    <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center', flexShrink: 0 }}>
                      <Typography variant="body2" sx={{ fontWeight: 700 }}>
                        {formatMontant(op.montant)}
                      </Typography>
                      <StatusBadge status={op.statut as EntityStatus} />
                    </Stack>
                  </Stack>
                ))}
              </Stack>
            </Paper>
          </Stack>
        </Grid>

        <Grid size={{ xs: 12, lg: 4 }} sx={{ minWidth: 0 }}>
          <Stack spacing={2.5}>
            <ChartCard title="Répartition par département" height={240}>
              <ResponsiveContainer width="100%" height="100%">
                <PieChart>
                  <Pie
                    data={repartitionDepartement}
                    dataKey="value"
                    nameKey="name"
                    innerRadius={52}
                    outerRadius={84}
                    paddingAngle={3}
                    stroke="none"
                  >
                    {repartitionDepartement.map((_, i) => (
                      <Cell key={i} fill={CHART[i % CHART.length]} />
                    ))}
                  </Pie>
                  <Tooltip />
                  <Legend />
                </PieChart>
              </ResponsiveContainer>
            </ChartCard>

            <Paper sx={{ p: { xs: 2, sm: 2.5 } }}>
              <Stack direction="row" spacing={1} sx={{ alignItems: 'center', mb: 2 }}>
                <WarningAmberIcon color="warning" fontSize="small" />
                <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
                  Alertes &amp; validations
                </Typography>
              </Stack>
              <Stack spacing={1} sx={{ mb: 2 }}>
                {alertes.map((a) => (
                  <Alert key={a.id} severity={a.niveau} variant="outlined" sx={{ py: 0.25 }}>
                    {a.message}
                  </Alert>
                ))}
              </Stack>
              <Typography variant="overline" color="text.secondary">
                À valider
              </Typography>
              <List dense disablePadding sx={{ mt: 0.5 }}>
                {validationsVisibles.map((v) => (
                  <ListItem
                    key={v.id}
                    component={RouterLink}
                    to={v.type === 'Paiement' ? '/paiements?statut=EN_VALIDATION_N1' : '/engagements/en-attente'}
                    sx={{
                      px: 1,
                      borderRadius: 'var(--ef-radius)',
                      textDecoration: 'none',
                      color: 'inherit',
                      '&:hover': { bgcolor: 'action.hover' },
                    }}
                  >
                    <ListItemText
                      primary={`${v.reference} · ${formatMontant(v.montant)}`}
                      secondary={`${v.type} — ${v.demandeur}`}
                      slotProps={{
                        primary: { variant: 'body2', sx: { fontWeight: 650 } },
                        secondary: { variant: 'caption' },
                      }}
                    />
                  </ListItem>
                ))}
              </List>
            </Paper>
          </Stack>
        </Grid>
      </Grid>
    </Box>
  );
}
