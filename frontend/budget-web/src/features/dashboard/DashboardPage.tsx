import AccountBalanceWalletOutlinedIcon from '@mui/icons-material/AccountBalanceWalletOutlined';
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
import { useState } from 'react';
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
import { ChartCard, FilterBar, PageHeader, StatCard, StatusBadge } from '../../components';
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

const CHART = [
  'var(--ef-chart-1)',
  'var(--ef-chart-2)',
  'var(--ef-chart-3)',
  'var(--ef-chart-4)',
  'var(--ef-chart-2)',
];

export function DashboardPage() {
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

      <Grid container spacing={1.5} sx={{ mb: 2 }}>
        <Grid size={{ xs: 12, sm: 6, md: 3 }}>
          <StatCard
            title="Solde disponible"
            value={formatMontant(dashboardKpis.soldeDisponible)}
            icon={<AccountBalanceWalletOutlinedIcon fontSize="small" />}
            trend={{ value: '+4,2 % vs période précédente', direction: 'up' }}
          />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, md: 3 }}>
          <StatCard
            title="Encaissements"
            value={formatMontant(dashboardKpis.encaissements)}
            icon={<TrendingUpIcon fontSize="small" />}
            trend={{ value: '+8,1 %', direction: 'up' }}
          />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, md: 3 }}>
          <StatCard
            title="Décaissements"
            value={formatMontant(dashboardKpis.decaissements)}
            icon={<PaymentsOutlinedIcon fontSize="small" />}
            trend={{ value: '+2,4 %', direction: 'up' }}
          />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, md: 3 }}>
          <StatCard
            title="Budget consommé"
            value={`${dashboardKpis.budgetConsomme} %`}
            subtitle={`Disponible ${formatMontant(dashboardKpis.budgetDisponible)}`}
          />
        </Grid>
      </Grid>

      <Grid container spacing={1.5} sx={{ mb: 2 }}>
        <Grid size={{ xs: 12 }}>
          <ChartCard title="Évolution financière" subtitle="Encaissements vs décaissements (milliards CDF — mock)">
            <ResponsiveContainer width="100%" height={280}>
              <LineChart data={evolutionCombine}>
                <CartesianGrid strokeDasharray="3 3" stroke="var(--ef-border)" />
                <XAxis dataKey="mois" tick={{ fontSize: 12 }} />
                <YAxis tick={{ fontSize: 12 }} />
                <Tooltip />
                <Legend />
                <Line
                  type="monotone"
                  dataKey="encaissements"
                  stroke="var(--ef-success)"
                  strokeWidth={2}
                  dot={false}
                  name="Encaissements"
                />
                <Line
                  type="monotone"
                  dataKey="decaissements"
                  stroke="var(--ef-warning)"
                  strokeWidth={2}
                  dot={false}
                  name="Décaissements"
                />
              </LineChart>
            </ResponsiveContainer>
          </ChartCard>
        </Grid>
      </Grid>

      <Grid container spacing={1.5} sx={{ mb: 2 }}>
        <Grid size={{ xs: 12, md: 5 }}>
          <ChartCard title="Exécution budgétaire" subtitle="Pipeline initial → disponible">
            <ResponsiveContainer width="100%" height={240}>
              <BarChart data={budgetPipeline}>
                <CartesianGrid strokeDasharray="3 3" stroke="var(--ef-border)" />
                <XAxis dataKey="etape" tick={{ fontSize: 11 }} />
                <YAxis tick={{ fontSize: 12 }} />
                <Tooltip />
                <Bar dataKey="montant" fill="var(--ef-chart-1)" radius={[3, 3, 0, 0]} name="%" />
              </BarChart>
            </ResponsiveContainer>
          </ChartCard>
        </Grid>
        <Grid size={{ xs: 12, md: 3.5 }}>
          <ChartCard title="Répartition par département">
            <ResponsiveContainer width="100%" height={240}>
              <PieChart>
                <Pie
                  data={repartitionDepartement}
                  dataKey="value"
                  nameKey="name"
                  innerRadius={48}
                  outerRadius={80}
                  paddingAngle={2}
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
        </Grid>
        <Grid size={{ xs: 12, md: 3.5 }}>
          <Paper sx={{ p: 1.75, height: '100%' }}>
            <Stack direction="row" spacing={1} sx={{ alignItems: 'center', mb: 1.25 }}>
              <WarningAmberIcon color="warning" fontSize="small" />
              <Typography sx={{ fontWeight: 700 }}>Alertes & validations</Typography>
            </Stack>
            <Stack spacing={1} sx={{ mb: 1.5 }}>
              {alertes.map((a) => (
                <Alert key={a.id} severity={a.niveau} variant="outlined" sx={{ py: 0.25 }}>
                  {a.message}
                </Alert>
              ))}
            </Stack>
            <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 650 }}>
              À valider
            </Typography>
            <List dense disablePadding>
              {validationsEnAttente.map((v) => (
                <ListItem
                  key={v.id}
                  component={RouterLink}
                  to={v.type === 'Paiement' ? '/paiements/a-valider' : '/engagements/en-attente'}
                  sx={{
                    px: 0.5,
                    borderRadius: 1,
                    textDecoration: 'none',
                    color: 'inherit',
                    '&:hover': { bgcolor: 'action.hover' },
                  }}
                >
                  <ListItemText
                    primary={`${v.reference} · ${formatMontant(v.montant)}`}
                    secondary={`${v.type} — ${v.demandeur}`}
                    slotProps={{
                      primary: { variant: 'body2', sx: { fontWeight: 600 } },
                      secondary: { variant: 'caption' },
                    }}
                  />
                </ListItem>
              ))}
            </List>
          </Paper>
        </Grid>
      </Grid>

      <Paper sx={{ p: 1.75 }}>
        <Typography sx={{ fontWeight: 700, mb: 1.25 }}>Opérations récentes</Typography>
        <Stack spacing={0.5}>
          {dernieresOperations.map((op) => (
            <Stack
              key={op.id}
              direction={{ xs: 'column', sm: 'row' }}
              spacing={1}
              sx={{
                justifyContent: 'space-between',
                alignItems: { sm: 'center' },
                py: 0.85,
                borderBottom: '1px solid',
                borderColor: 'divider',
              }}
            >
              <Box>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>
                  {op.reference} · {op.type}
                </Typography>
                <Typography variant="caption" color="text.secondary">
                  {op.date} — {op.libelle}
                </Typography>
              </Box>
              <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center' }}>
                <Typography variant="body2" sx={{ fontWeight: 700 }}>
                  {formatMontant(op.montant)}
                </Typography>
                <StatusBadge status={op.statut as EntityStatus} />
              </Stack>
            </Stack>
          ))}
        </Stack>
      </Paper>
    </Box>
  );
}
