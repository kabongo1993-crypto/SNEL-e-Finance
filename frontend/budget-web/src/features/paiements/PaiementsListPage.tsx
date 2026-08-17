import AddIcon from '@mui/icons-material/Add';
import { Button, MenuItem, TextField, Typography } from '@mui/material';
import { useMemo, useState } from 'react';
import { Link as RouterLink, useNavigate } from 'react-router-dom';
import { DataTable, FilterBar, PageHeader, StatusBadge, type DataTableColumn } from '../../components';
import { mockPaiements } from '../../mocks/paiements';
import { formatDateFr, formatMontant, type MockPaiement } from '../../mocks/types';
import type { EntityStatus } from '../../types/status';
import { STATUS_LABELS } from '../../types/status';

interface PaiementsListPageProps {
  title: string;
  subtitle?: string;
  statusFilter?: EntityStatus | EntityStatus[];
  showNewButton?: boolean;
  /** When embedded in another page, hide the page header. */
  hideHeader?: boolean;
}

export function PaiementsListPage({
  title,
  subtitle = 'Suivi des demandes de paiement.',
  statusFilter,
  showNewButton = true,
  hideHeader = false,
}: PaiementsListPageProps) {
  const navigate = useNavigate();
  const [search, setSearch] = useState('');
  const [statut, setStatut] = useState<string>('all');
  const [exercice, setExercice] = useState('2026');
  const [periode, setPeriode] = useState('annee');
  const [departement, setDepartement] = useState('all');
  const [unite, setUnite] = useState('all');

  const rows = useMemo(() => {
    return mockPaiements.filter((p) => {
      if (statusFilter) {
        const allowed = Array.isArray(statusFilter) ? statusFilter : [statusFilter];
        if (!allowed.includes(p.statut)) return false;
      } else if (statut !== 'all' && p.statut !== statut) {
        return false;
      }
      if (departement !== 'all' && p.departement !== departement) return false;
      if (unite !== 'all' && p.uniteBudgetaire !== unite) return false;
      if (search) {
        const q = search.toLowerCase();
        const hay = `${p.numero} ${p.demandeur} ${p.beneficiaire} ${p.objet}`.toLowerCase();
        if (!hay.includes(q)) return false;
      }
      return true;
    });
  }, [search, statut, statusFilter, departement, unite]);

  const columns: DataTableColumn<MockPaiement>[] = [
    { id: 'numero', label: 'Numéro', sortable: true, sortValue: (r) => r.numero, render: (r) => <Typography variant="body2" sx={{ fontWeight: 700 }}>{r.numero}</Typography> },
    { id: 'date', label: 'Date', sortable: true, sortValue: (r) => r.date, render: (r) => formatDateFr(r.date) },
    { id: 'demandeur', label: 'Demandeur', render: (r) => r.demandeur },
    { id: 'beneficiaire', label: 'Bénéficiaire', render: (r) => r.beneficiaire },
    { id: 'objet', label: 'Objet', render: (r) => r.objet, width: 220 },
    { id: 'montant', label: 'Montant', align: 'right', sortable: true, sortValue: (r) => r.montant, render: (r) => formatMontant(r.montant, r.devise) },
    { id: 'devise', label: 'Devise', render: (r) => r.devise },
    { id: 'departement', label: 'Département', render: (r) => r.departement },
    { id: 'ub', label: 'UB', render: (r) => r.uniteBudgetaire },
    { id: 'statut', label: 'Statut', render: (r) => <StatusBadge status={r.statut} /> },
  ];

  return (
    <>
      {!hideHeader && (
        <PageHeader
          title={title}
          subtitle={subtitle}
          breadcrumbs={[
            { label: 'e-Finance', to: '/dashboard' },
            { label: 'Paiements', to: '/paiements' },
            { label: title },
          ]}
          actions={
            showNewButton ? (
              <Button variant="contained" startIcon={<AddIcon />} component={RouterLink} to="/paiements/nouveau">
                Nouveau paiement
              </Button>
            ) : undefined
          }
        />
      )}
      <FilterBar
        search={search}
        onSearchChange={setSearch}
        searchPlaceholder="N° , demandeur, bénéficiaire, objet…"
        showPeriodFilters
        exercice={exercice}
        onExerciceChange={setExercice}
        periode={periode}
        onPeriodeChange={setPeriode}
        departement={departement}
        onDepartementChange={setDepartement}
        unite={unite}
        onUniteChange={setUnite}
        extra={
          !statusFilter ? (
            <TextField
              select
              size="small"
              label="Statut"
              value={statut}
              onChange={(e) => setStatut(e.target.value)}
              sx={{ minWidth: 160 }}
            >
              <MenuItem value="all">Tous</MenuItem>
              {Object.entries(STATUS_LABELS).map(([k, v]) => (
                <MenuItem key={k} value={k}>
                  {v}
                </MenuItem>
              ))}
            </TextField>
          ) : undefined
        }
      />
      <DataTable
        columns={columns}
        rows={rows}
        selectable
        actions={[
          { id: 'voir', label: 'Voir', onClick: (r) => navigate(`/paiements/${r.id}`) },
          { id: 'modifier', label: 'Modifier', onClick: (r) => navigate(`/paiements/${r.id}`), hidden: (r) => !['brouillon', 'rejete'].includes(r.statut) },
          { id: 'valider', label: 'Valider', onClick: () => undefined, hidden: (r) => r.statut !== 'en_validation' },
          { id: 'rejeter', label: 'Rejeter', color: 'error', onClick: () => undefined, hidden: (r) => r.statut !== 'en_validation' },
          { id: 'historique', label: 'Historique', onClick: (r) => navigate(`/paiements/${r.id}`) },
        ]}
      />
    </>
  );
}
