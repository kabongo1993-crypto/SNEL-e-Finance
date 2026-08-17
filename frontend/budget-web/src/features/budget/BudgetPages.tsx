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
      <Grid container spacing={2} sx={{ mb: 2 }}>
        <Grid size={{ xs: 12, sm: 6, md: 2.4 }}>
          <StatCard title="Budget initial" value={formatMontant(budgetKpis.budgetInitial)} />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, md: 2.4 }}>
          <StatCard title="Révisé" value={formatMontant(budgetKpis.revise)} />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, md: 2.4 }}>
          <StatCard title="Engagé" value={formatMontant(budgetKpis.engage)} />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, md: 2.4 }}>
          <StatCard title="Exécuté" value={formatMontant(budgetKpis.execute)} />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, md: 2.4 }}>
          <StatCard title="Disponible" value={formatMontant(budgetKpis.disponible)} />
        </Grid>
      </Grid>
      <Grid container spacing={2} sx={{ mb: 2 }}>
        <Grid size={{ xs: 12, md: 6 }}>
          <ChartCard title="Pipeline budgétaire" subtitle="Milliards CDF (mock)">
            <ResponsiveContainer width="100%" height={260}>
              <BarChart data={budgetPipelineChart}>
                <CartesianGrid strokeDasharray="3 3" stroke="var(--ef-border)" />
                <XAxis dataKey="etape" tick={{ fontSize: 12 }} />
                <YAxis tick={{ fontSize: 12 }} />
                <Tooltip />
                <Bar dataKey="montant" fill="var(--ef-chart-1)" radius={[3, 3, 0, 0]} name="Montant" />
              </BarChart>
            </ResponsiveContainer>
          </ChartCard>
        </Grid>
        <Grid size={{ xs: 12, md: 6 }}>
          <BudgetSuiviTable embedded />
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
    { id: 'code', label: 'Code', sortable: true, sortValue: (r) => r.code, render: (r) => <Typography variant="body2" sx={{ fontWeight: 700 }}>{r.code}</Typography> },
    { id: 'libelle', label: 'Libellé', render: (r) => r.libelle },
    { id: 'dept', label: 'Département', render: (r) => r.departement },
    { id: 'ub', label: 'UB', render: (r) => r.uniteBudgetaire },
    { id: 'initial', label: 'Budget initial', align: 'right', render: (r) => formatMontant(r.budgetInitial) },
    { id: 'rev', label: 'Révisions', align: 'right', render: (r) => formatMontant(r.revisions) },
    { id: 'dispo', label: 'Disponible', align: 'right', render: (r) => formatMontant(r.budgetDisponible) },
    { id: 'eng', label: 'Engagé', align: 'right', render: (r) => formatMontant(r.engage) },
    { id: 'exe', label: 'Exécuté', align: 'right', render: (r) => formatMontant(r.execute) },
    { id: 'taux', label: 'Taux', align: 'right', sortable: true, sortValue: (r) => r.tauxExecution, render: (r) => `${r.tauxExecution} %` },
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
        <Typography sx={{ fontWeight: 700,  mb: 1.5 }}>
          Lignes budgétaires
        </Typography>
      )}
      {!embedded && null}
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
