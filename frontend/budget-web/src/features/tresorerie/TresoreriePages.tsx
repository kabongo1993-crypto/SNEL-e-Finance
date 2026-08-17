import { Box, Grid, Typography } from '@mui/material';
import { useMemo, useState } from 'react';
import { Chip } from '@mui/material';
import {
  DataTable,
  FilterBar,
  ModulePlaceholder,
  PageHeader,
  StatCard,
  type DataTableColumn,
} from '../../components';
import { mockComptes, mockMouvements, tresorerieKpis } from '../../mocks/tresorerie';
import { formatDateFr, formatMontant, type MockMouvementTreso } from '../../mocks/types';

export function TresorerieDashboardPage() {
  return (
    <Box>
      <PageHeader
        title="Tableau de bord trésorerie"
        subtitle="Situation de liquidité et flux de caisse (données mock)."
        breadcrumbs={[
          { label: 'e-Finance', to: '/dashboard' },
          { label: 'Trésorerie' },
        ]}
      />
      <Grid container spacing={2} sx={{ mb: 2 }}>
        <Grid size={{ xs: 12, sm: 6, md: 3 }}>
          <StatCard title="Solde total" value={formatMontant(tresorerieKpis.soldeTotal)} />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, md: 3 }}>
          <StatCard title="Entrées" value={formatMontant(tresorerieKpis.entrees)} />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, md: 3 }}>
          <StatCard title="Sorties" value={formatMontant(tresorerieKpis.sorties)} />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, md: 3 }}>
          <StatCard
            title="Solde prévisionnel"
            value={formatMontant(tresorerieKpis.soldePrevisionnel)}
          />
        </Grid>
      </Grid>
      <Typography variant="h6" sx={{ mb: 1.5 }}>
        Comptes
      </Typography>
      <DataTable
        columns={[
          { id: 'code', label: 'Code', render: (r) => <Typography variant="body2" sx={{ fontWeight: 700 }}>{r.code}</Typography> },
          { id: 'libelle', label: 'Libellé', render: (r) => r.libelle },
          { id: 'solde', label: 'Solde', align: 'right', render: (r) => formatMontant(r.solde, r.devise) },
          { id: 'devise', label: 'Devise', render: (r) => r.devise },
        ]}
        rows={mockComptes}
      />
      <Typography variant="h6" sx={{ mt: 3, mb: 1.5 }}>
        Derniers mouvements
      </Typography>
      <MouvementsTable hideHeader />
    </Box>
  );
}

function MouvementsTable({ hideHeader }: { hideHeader?: boolean }) {
  const [search, setSearch] = useState('');
  const rows = useMemo(
    () =>
      mockMouvements.filter((m) => {
        if (!search) return true;
        const q = search.toLowerCase();
        return `${m.reference} ${m.libelle} ${m.compte}`.toLowerCase().includes(q);
      }),
    [search],
  );

  const columns: DataTableColumn<MockMouvementTreso>[] = [
    { id: 'date', label: 'Date', sortable: true, sortValue: (r) => r.date, render: (r) => formatDateFr(r.date) },
    { id: 'ref', label: 'Référence', render: (r) => <Typography variant="body2" sx={{ fontWeight: 700 }}>{r.reference}</Typography> },
    { id: 'libelle', label: 'Libellé', render: (r) => r.libelle },
    { id: 'compte', label: 'Compte', render: (r) => r.compte },
    {
      id: 'type',
      label: 'Type',
      render: (r) => (
        <Chip
          size="small"
          label={r.type === 'encaissement' ? 'Encaissement' : 'Décaissement'}
          color={r.type === 'encaissement' ? 'success' : 'warning'}
          variant="outlined"
        />
      ),
    },
    {
      id: 'montant',
      label: 'Montant',
      align: 'right',
      render: (r) => (
        <Typography
          variant="body2"
         
          color={r.type === 'encaissement' ? 'success.main' : 'warning.main'}
         sx={{ fontWeight: 700 }}>
          {r.type === 'encaissement' ? '+' : '−'}
          {formatMontant(r.montant)}
        </Typography>
      ),
    },
    { id: 'solde', label: 'Solde après', align: 'right', render: (r) => formatMontant(r.soldeApres) },
  ];

  return (
    <Box>
      {!hideHeader && (
        <>
          <PageHeader
            title="Mouvements de trésorerie"
            subtitle="Journal des encaissements et décaissements (mock)."
            breadcrumbs={[
              { label: 'e-Finance', to: '/dashboard' },
              { label: 'Trésorerie', to: '/tresorerie' },
              { label: 'Mouvements' },
            ]}
          />
          <FilterBar search={search} onSearchChange={setSearch} showPeriodFilters />
        </>
      )}
      <DataTable columns={columns} rows={rows} />
    </Box>
  );
}

export function TresorerieMouvementsPage() {
  return <MouvementsTable />;
}

export function TresorerieModulePage({ title, pathLabel }: { title: string; pathLabel: string }) {
  return (
    <ModulePlaceholder
      title={title}
      subtitle="Écran trésorerie — maquette fonctionnelle."
      moduleLabel="Trésorerie"
      breadcrumbs={[
        { label: 'e-Finance', to: '/dashboard' },
        { label: 'Trésorerie', to: '/tresorerie' },
        { label: pathLabel },
      ]}
    />
  );
}
