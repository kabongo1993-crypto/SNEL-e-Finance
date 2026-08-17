import AddIcon from '@mui/icons-material/Add';
import { Box, Button, Grid, MenuItem, TextField, Typography } from '@mui/material';
import { useMemo, useState } from 'react';
import { Link as RouterLink, useNavigate } from 'react-router-dom';
import {
  DataTable,
  FilterBar,
  FormSection,
  ModulePlaceholder,
  PageHeader,
  StatusBadge,
  type DataTableColumn,
} from '../../components';
import { mockEngagements } from '../../mocks/engagements';
import { formatDateFr, formatMontant, MOCK_DEPARTEMENTS, type MockEngagement } from '../../mocks/types';
import type { EntityStatus } from '../../types/status';

interface EngagementsListProps {
  title: string;
  statusFilter?: EntityStatus | EntityStatus[];
  hideHeader?: boolean;
}

export function EngagementsListPage({ title, statusFilter, hideHeader }: EngagementsListProps) {
  const navigate = useNavigate();
  const [search, setSearch] = useState('');

  const rows = useMemo(() => {
    return mockEngagements.filter((e) => {
      if (statusFilter) {
        const allowed = Array.isArray(statusFilter) ? statusFilter : [statusFilter];
        if (!allowed.includes(e.statut)) return false;
      }
      if (!search) return true;
      const q = search.toLowerCase();
      return `${e.numero} ${e.objet} ${e.fournisseur}`.toLowerCase().includes(q);
    });
  }, [search, statusFilter]);

  const columns: DataTableColumn<MockEngagement>[] = [
    { id: 'numero', label: 'Numéro', sortable: true, sortValue: (r) => r.numero, render: (r) => <Typography variant="body2" sx={{ fontWeight: 700 }}>{r.numero}</Typography> },
    { id: 'date', label: 'Date', render: (r) => formatDateFr(r.date) },
    { id: 'objet', label: 'Objet', render: (r) => r.objet },
    { id: 'fournisseur', label: 'Fournisseur / Bénéficiaire', render: (r) => r.fournisseur },
    { id: 'montant', label: 'Montant', align: 'right', render: (r) => formatMontant(r.montant) },
    { id: 'budget', label: 'Budget', render: (r) => r.budgetCode },
    { id: 'dispo', label: 'Disponible', align: 'right', render: (r) => formatMontant(r.disponible) },
    { id: 'statut', label: 'Statut', render: (r) => <StatusBadge status={r.statut} /> },
  ];

  return (
    <Box>
      {!hideHeader && (
        <PageHeader
          title={title}
          subtitle="Suivi des engagements budgétaires (données mock)."
          breadcrumbs={[
            { label: 'e-Finance', to: '/dashboard' },
            { label: 'Engagements', to: '/engagements' },
            { label: title },
          ]}
          actions={
            <Button variant="contained" startIcon={<AddIcon />} component={RouterLink} to="/engagements/nouveau">
              Nouvel engagement
            </Button>
          }
        />
      )}
      <FilterBar search={search} onSearchChange={setSearch} showPeriodFilters />
      <DataTable
        columns={columns}
        rows={rows}
        selectable
        actions={[
          { id: 'voir', label: 'Voir', onClick: () => undefined },
          { id: 'valider', label: 'Valider', onClick: () => undefined, hidden: (r) => r.statut !== 'en_attente' && r.statut !== 'soumis' },
          { id: 'rejeter', label: 'Rejeter', color: 'error', onClick: () => undefined, hidden: (r) => r.statut !== 'en_attente' },
          { id: 'historique', label: 'Historique', onClick: () => navigate('/engagements/historique') },
        ]}
      />
    </Box>
  );
}

export function NouvelEngagementPage() {
  const navigate = useNavigate();
  const [form, setForm] = useState({
    date: '2026-08-16',
    objet: '',
    fournisseur: '',
    montant: '',
    departement: '',
    budget: '',
    commentaire: '',
  });

  return (
    <Box>
      <PageHeader
        title="Nouvel engagement"
        subtitle="Création d’un engagement sur une ligne budgétaire."
        breadcrumbs={[
          { label: 'e-Finance', to: '/dashboard' },
          { label: 'Engagements', to: '/engagements' },
          { label: 'Nouveau' },
        ]}
      />
      <FormSection
        title="Informations de l’engagement"
        actions={
          <>
            <Button component={RouterLink} to="/engagements">
              Annuler
            </Button>
            <Button variant="outlined" onClick={() => navigate('/engagements')}>
              Enregistrer
            </Button>
            <Button variant="contained" onClick={() => navigate('/engagements')}>
              Soumettre
            </Button>
          </>
        }
      >
        <Grid container spacing={2}>
          <Grid size={{ xs: 12, sm: 4 }}>
            <TextField
              fullWidth
              type="date"
              label="Date"
              value={form.date}
              onChange={(e) => setForm({ ...form, date: e.target.value })}
              slotProps={{ inputLabel: { shrink: true } }}
            />
          </Grid>
          <Grid size={{ xs: 12, sm: 4 }}>
            <TextField
              fullWidth
              required
              label="Montant"
              value={form.montant}
              onChange={(e) => setForm({ ...form, montant: e.target.value })}
            />
          </Grid>
          <Grid size={{ xs: 12, sm: 4 }}>
            <TextField
              select
              fullWidth
              label="Département"
              value={form.departement}
              onChange={(e) => setForm({ ...form, departement: e.target.value })}
            >
              {MOCK_DEPARTEMENTS.map((d) => (
                <MenuItem key={d.id} value={d.id}>
                  {d.id}
                </MenuItem>
              ))}
            </TextField>
          </Grid>
          <Grid size={{ xs: 12, sm: 6 }}>
            <TextField
              fullWidth
              required
              label="Fournisseur / Bénéficiaire"
              value={form.fournisseur}
              onChange={(e) => setForm({ ...form, fournisseur: e.target.value })}
            />
          </Grid>
          <Grid size={{ xs: 12, sm: 6 }}>
            <TextField
              fullWidth
              label="Code budget"
              value={form.budget}
              onChange={(e) => setForm({ ...form, budget: e.target.value })}
            />
          </Grid>
          <Grid size={{ xs: 12 }}>
            <TextField
              fullWidth
              required
              multiline
              minRows={2}
              label="Objet"
              value={form.objet}
              onChange={(e) => setForm({ ...form, objet: e.target.value })}
            />
          </Grid>
          <Grid size={{ xs: 12 }}>
            <TextField
              fullWidth
              multiline
              minRows={2}
              label="Commentaire"
              value={form.commentaire}
              onChange={(e) => setForm({ ...form, commentaire: e.target.value })}
            />
          </Grid>
        </Grid>
      </FormSection>
    </Box>
  );
}

export function EngagementsModulePage({ title, pathLabel }: { title: string; pathLabel: string }) {
  return (
    <ModulePlaceholder
      title={title}
      subtitle="Écran engagements — maquette fonctionnelle."
      moduleLabel="Engagements"
      breadcrumbs={[
        { label: 'e-Finance', to: '/dashboard' },
        { label: 'Engagements', to: '/engagements' },
        { label: pathLabel },
      ]}
    />
  );
}
