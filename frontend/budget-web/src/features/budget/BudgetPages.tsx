import { Box, Grid, Typography } from '@mui/material';
import { useMemo, useState } from 'react';
import {
  Bar,
  BarChart,
  CartesianGrid,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import {
  ChartCard,
  DataTable,
  FilterBar,
  ModulePlaceholder,
  PageHeader,
  StatCard,
  type DataTableColumn,
} from '../../components';
import { budgetKpis, budgetPipelineChart, mockLignesBudget } from '../../mocks/budget';
import { formatMontant, type MockLigneBudget } from '../../mocks/types';

export function BudgetDashboardPage() {
  const [exercice, setExercice] = useState('2026');
  const [periode, setPeriode] = useState('annee');
  const [departement, setDepartement] = useState('all');
  const [unite, setUnite] = useState('all');

  return (
    <Box>
      <PageHeader
        title="Tableau de bord budget"
        subtitle="Suivi de la chaîne budgétaire : initial → révisé → engagé → exécuté → disponible."
        breadcrumbs={[
          { label: 'e-Finance', to: '/dashboard' },
          { label: 'Budget' },
        ]}
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
      <Grid container spacing={2} sx={{ mb: 2.5 }}>
        {[
          { title: 'Budget initial', value: budgetKpis.budgetInitial },
          { title: 'Révisé', value: budgetKpis.revise },
          { title: 'Engagé', value: budgetKpis.engage },
          { title: 'Exécuté', value: budgetKpis.execute },
          { title: 'Disponible', value: budgetKpis.disponible },
        ].map((kpi) => (
          <Grid key={kpi.title} size={{ xs: 12, sm: 6, md: 4, lg: 2.4 }}>
            <StatCard title={kpi.title} value={formatMontant(kpi.value)} />
          </Grid>
        ))}
      </Grid>
      <Grid container spacing={2.5} sx={{ alignItems: 'flex-start' }}>
        <Grid size={{ xs: 12, lg: 8 }} sx={{ minWidth: 0 }}>
          <BudgetSuiviTable embedded />
        </Grid>
        <Grid size={{ xs: 12, lg: 4 }} sx={{ minWidth: 0 }}>
          <ChartCard title="Pipeline budgétaire" subtitle="Milliards CDF (mock)" height={260}>
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={budgetPipelineChart} margin={{ top: 8, right: 8, left: -16, bottom: 0 }}>
                <CartesianGrid strokeDasharray="3 3" stroke="var(--ef-border-subtle)" vertical={false} />
                <XAxis dataKey="etape" tick={{ fontSize: 12 }} tickLine={false} axisLine={false} />
                <YAxis tick={{ fontSize: 12 }} tickLine={false} axisLine={false} />
                <Tooltip />
                <Bar dataKey="montant" fill="var(--ef-chart-1)" radius={[6, 6, 0, 0]} name="Montant" />
              </BarChart>
            </ResponsiveContainer>
          </ChartCard>
        </Grid>
      </Grid>
    </Box>
  );
}

function BudgetSuiviTable({ embedded }: { embedded?: boolean }) {
  const [search, setSearch] = useState('');
  const rows = useMemo(
    () =>
      mockLignesBudget.filter((l) => {
        if (!search) return true;
        const q = search.toLowerCase();
        return `${l.code} ${l.libelle} ${l.departement}`.toLowerCase().includes(q);
      }),
    [search],
  );

  const columns: DataTableColumn<MockLigneBudget>[] = [
    { id: 'code', label: 'Code', sortable: true, sortValue: (r) => r.code, mobile: 'title', render: (r) => <Typography variant="body2" sx={{ fontWeight: 700 }}>{r.code}</Typography> },
    { id: 'libelle', label: 'Libellé', mobile: 'subtitle', render: (r) => r.libelle },
    { id: 'dept', label: 'Département', mobile: 'meta', render: (r) => r.departement },
    { id: 'ub', label: 'UB', mobile: 'hidden', render: (r) => r.uniteBudgetaire },
    { id: 'initial', label: 'Budget initial', align: 'right', mobile: 'hidden', render: (r) => formatMontant(r.budgetInitial) },
    { id: 'rev', label: 'Révisions', align: 'right', mobile: 'hidden', render: (r) => formatMontant(r.revisions) },
    { id: 'dispo', label: 'Disponible', align: 'right', mobile: 'meta', render: (r) => formatMontant(r.budgetDisponible) },
    { id: 'eng', label: 'Engagé', align: 'right', mobile: 'hidden', render: (r) => formatMontant(r.engage) },
    { id: 'exe', label: 'Exécuté', align: 'right', mobile: 'hidden', render: (r) => formatMontant(r.execute) },
    { id: 'taux', label: 'Taux', align: 'right', sortable: true, sortValue: (r) => r.tauxExecution, mobile: 'meta', render: (r) => `${r.tauxExecution} %` },
  ];

  return (
    <Box>
      {!embedded && (
        <>
          <PageHeader
            title="Suivi budgétaire"
            subtitle="Consommation des lignes budgétaires (données mock)."
            breadcrumbs={[
              { label: 'e-Finance', to: '/dashboard' },
              { label: 'Budget', to: '/budget' },
              { label: 'Suivi' },
            ]}
          />
          <FilterBar search={search} onSearchChange={setSearch} showPeriodFilters />
        </>
      )}
      {embedded && (
        <Typography variant="subtitle1" sx={{ fontWeight: 700, mb: 1.5 }}>
          Lignes budgétaires
        </Typography>
      )}
      <DataTable columns={columns} rows={rows} actions={[{ id: 'voir', label: 'Voir', onClick: () => undefined }]} />
    </Box>
  );
}

export function BudgetSuiviPage() {
  return <BudgetSuiviTable />;
}

export function BudgetLignesPage() {
  return <BudgetSuiviPage />;
}

export function BudgetModulePage({
  title,
  pathLabel,
}: {
  title: string;
  pathLabel: string;
}) {
  return (
    <ModulePlaceholder
      title={title}
      subtitle="Écran budgétaire — maquette fonctionnelle."
      moduleLabel="Budget"
      breadcrumbs={[
        { label: 'e-Finance', to: '/dashboard' },
        { label: 'Budget', to: '/budget' },
        { label: pathLabel },
      ]}
    >
      <Typography color="text.secondary">
        Interface prête pour l’implémentation de « {title} ». Utilisez le suivi budgétaire pour visualiser
        le modèle de tableau cible.
      </Typography>
    </ModulePlaceholder>
  );
}
