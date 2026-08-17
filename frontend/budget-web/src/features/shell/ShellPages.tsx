import DownloadIcon from '@mui/icons-material/Download';
import PrintIcon from '@mui/icons-material/Print';
import {
  Box,
  Button,
  Grid,
  MenuItem,
  Paper,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { useMemo, useState } from 'react';
import { Link as RouterLink } from 'react-router-dom';
import { DataTable, FilterBar, ModulePlaceholder, PageHeader, ThemeSettingsPanel, type DataTableColumn } from '../../components';
import {
  mockAuditLog,
  mockCircuits,
  mockDevises,
  mockModesPaiement,
  mockNatures,
  mockRapports,
  mockRoles,
  mockSources,
  mockUtilisateurs,
} from '../../mocks/administration';
import { MOCK_EXERCICES, formatMontant } from '../../mocks/types';

export function RapportsPage() {
  const [selected, setSelected] = useState(mockRapports[0]?.id ?? '');
  const [exercice, setExercice] = useState('2026');
  const [periode, setPeriode] = useState('annee');
  const rapport = mockRapports.find((r) => r.id === selected);

  return (
    <Box>
      <PageHeader
        title="Rapports financiers"
        subtitle="Sélectionnez un rapport, définissez la période, puis affichez ou exportez."
        breadcrumbs={[
          { label: 'e-Finance', to: '/dashboard' },
          { label: 'Rapports' },
        ]}
        actions={
          <>
            <Button variant="outlined" startIcon={<DownloadIcon />}>
              Excel
            </Button>
            <Button variant="outlined" startIcon={<DownloadIcon />}>
              PDF
            </Button>
            <Button variant="outlined" startIcon={<PrintIcon />}>
              Imprimer
            </Button>
            <Button variant="contained">Afficher</Button>
          </>
        }
      />
      <FilterBar
        showPeriodFilters
        exercice={exercice}
        onExerciceChange={setExercice}
        periode={periode}
        onPeriodeChange={setPeriode}
      />
      <Grid container spacing={2}>
        <Grid size={{ xs: 12, md: 4 }}>
          <Paper sx={{ p: 2 }}>
            <Typography sx={{ fontWeight: 700,  mb: 1.5 }}>
              Catalogue
            </Typography>
            <Stack spacing={1}>
              {mockRapports.map((r) => (
                <Paper
                  key={r.id}
                  onClick={() => setSelected(r.id)}
                  sx={{
                    p: 1.5,
                    cursor: 'pointer',
                    borderColor: selected === r.id ? 'secondary.main' : 'divider',
                    bgcolor: selected === r.id ? 'action.selected' : 'background.paper',
                  }}
                >
                  <Typography variant="caption" color="text.secondary">
                    {r.categorie}
                  </Typography>
                  <Typography variant="body2" sx={{ fontWeight: 700 }}>
                    {r.titre}
                  </Typography>
                  <Typography variant="caption" color="text.secondary">
                    {r.description}
                  </Typography>
                </Paper>
              ))}
            </Stack>
          </Paper>
        </Grid>
        <Grid size={{ xs: 12, md: 8 }}>
          <Paper sx={{ p: 3, minHeight: 360 }}>
            <Typography variant="h6">{rapport?.titre ?? 'Rapport'}</Typography>
            <Typography color="text.secondary" sx={{ mt: 1, mb: 3 }}>
              {rapport?.description} — aperçu maquette pour l’exercice {exercice}.
            </Typography>
            <Box
              sx={{
                border: '1px dashed',
                borderColor: 'divider',
                borderRadius: 2,
                py: 8,
                textAlign: 'center',
                bgcolor: 'action.hover',
              }}
            >
              <Typography sx={{ fontWeight: 600 }}>Zone d’aperçu du rapport</Typography>
              <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>
                Les graphiques et tableaux détaillés seront branchés sur l’API de reporting.
              </Typography>
              <Typography variant="caption" color="text.secondary" sx={{ mt: 2, display: 'block' }}>
                Exemple de total mock : {formatMontant(18_450_000_000)}
              </Typography>
            </Box>
          </Paper>
        </Grid>
      </Grid>
    </Box>
  );
}

export function RapportModulePage({ title }: { title: string }) {
  return (
    <ModulePlaceholder
      title={title}
      subtitle="Rapport dédié — utilisez le catalogue pour la sélection complète."
      moduleLabel="Rapports"
      breadcrumbs={[
        { label: 'e-Finance', to: '/dashboard' },
        { label: 'Rapports', to: '/rapports' },
        { label: title },
      ]}
      actions={
        <>
          <Button variant="outlined" startIcon={<DownloadIcon />}>
            Excel
          </Button>
          <Button variant="outlined" startIcon={<DownloadIcon />}>
            PDF
          </Button>
          <Button variant="contained">Afficher</Button>
        </>
      }
    />
  );
}

export function ReferentielTablePage({
  title,
  description,
  columns,
  rows,
}: {
  title: string;
  description: string;
  columns: DataTableColumn<{ id: string; [key: string]: string | number | boolean }>[];
  rows: { id: string; [key: string]: string | number | boolean }[];
}) {
  const [search, setSearch] = useState('');
  const filtered = useMemo(() => {
    if (!search) return rows;
    const q = search.toLowerCase();
    return rows.filter((r) => JSON.stringify(r).toLowerCase().includes(q));
  }, [rows, search]);

  return (
    <Box>
      <PageHeader
        title={title}
        subtitle={description}
        breadcrumbs={[
          { label: 'e-Finance', to: '/dashboard' },
          { label: 'Référentiels', to: '/referentiels/organisationnel' },
          { label: title },
        ]}
      />
      <FilterBar search={search} onSearchChange={setSearch} />
      <DataTable columns={columns} rows={filtered} />
    </Box>
  );
}

export function ReferentielExercicesPage() {
  return (
    <ReferentielTablePage
      title="Exercices"
      description="Exercices budgétaires (mock)."
      columns={[
        { id: 'id', label: 'Exercice', render: (r) => String(r.id) },
        { id: 'statut', label: 'Statut', render: (r) => String(r.statut) },
      ]}
      rows={MOCK_EXERCICES.map((ex) => ({
        id: ex,
        statut: ex === '2026' ? 'Ouvert' : 'Clôturé',
      }))}
    />
  );
}

export function ReferentielNaturesPage() {
  return (
    <ReferentielTablePage
      title="Natures de dépenses"
      description="Nomenclature des natures (mock)."
      columns={[
        { id: 'id', label: 'Code', render: (r) => String(r.id) },
        { id: 'libelle', label: 'Libellé', render: (r) => String(r.libelle) },
      ]}
      rows={mockNatures.map((n) => ({ id: n.code, libelle: n.libelle }))}
    />
  );
}

export function ReferentielSourcesPage() {
  return (
    <ReferentielTablePage
      title="Sources de financement"
      description="Sources de financement (mock)."
      columns={[
        { id: 'id', label: 'Code', render: (r) => String(r.id) },
        { id: 'libelle', label: 'Libellé', render: (r) => String(r.libelle) },
      ]}
      rows={mockSources.map((n) => ({ id: n.code, libelle: n.libelle }))}
    />
  );
}

export function ReferentielModesPaiementPage() {
  return (
    <ReferentielTablePage
      title="Modes de paiement"
      description="Modes de règlement (mock)."
      columns={[
        { id: 'id', label: 'Code', render: (r) => String(r.id) },
        { id: 'libelle', label: 'Libellé', render: (r) => String(r.libelle) },
      ]}
      rows={mockModesPaiement.map((n) => ({ id: n.code, libelle: n.libelle }))}
    />
  );
}

export function ReferentielDevisesPage() {
  return (
    <ReferentielTablePage
      title="Devises"
      description="Devises de travail (mock)."
      columns={[
        { id: 'id', label: 'Code', render: (r) => String(r.id) },
        { id: 'libelle', label: 'Libellé', render: (r) => String(r.libelle) },
        { id: 'symbole', label: 'Symbole', render: (r) => String(r.symbole) },
      ]}
      rows={mockDevises.map((n) => ({ id: n.code, libelle: n.libelle, symbole: n.symbole }))}
    />
  );
}

export function ReferentielStructuresPlaceholderPage() {
  return (
    <ModulePlaceholder
      title="Structures"
      subtitle="Vue listée des structures. Pour l’exploration arborescente réelle, utilisez le référentiel organisationnel."
      moduleLabel="Référentiels"
      breadcrumbs={[
        { label: 'e-Finance', to: '/dashboard' },
        { label: 'Référentiels', to: '/referentiels/organisationnel' },
        { label: 'Structures' },
      ]}
      actions={
        <Button variant="contained" component={RouterLink} to="/referentiels/organisationnel">
          Ouvrir le référentiel organisationnel
        </Button>
      }
    />
  );
}

export function ReferentielUbPlaceholderPage() {
  return (
    <ModulePlaceholder
      title="Unités budgétaires"
      subtitle="Liste dédiée des UB. Les UB réelles restent accessibles via le référentiel organisationnel (API BD_SNEL)."
      moduleLabel="Référentiels"
      breadcrumbs={[
        { label: 'e-Finance', to: '/dashboard' },
        { label: 'Référentiels', to: '/referentiels/organisationnel' },
        { label: 'Unités budgétaires' },
      ]}
      actions={
        <Button variant="contained" component={RouterLink} to="/referentiels/organisationnel">
          Voir dans l’organisationnel
        </Button>
      }
    />
  );
}

export function AdminUtilisateursPage() {
  return (
    <Box>
      <PageHeader
        title="Utilisateurs"
        subtitle="Gestion des comptes e-Finance (mock)."
        breadcrumbs={[
          { label: 'e-Finance', to: '/dashboard' },
          { label: 'Administration', to: '/administration/utilisateurs' },
          { label: 'Utilisateurs' },
        ]}
        actions={<Button variant="contained">Nouvel utilisateur</Button>}
      />
      <DataTable
        columns={[
          { id: 'nom', label: 'Nom', render: (r) => r.nom },
          { id: 'email', label: 'Email', render: (r) => r.email },
          { id: 'role', label: 'Rôle', render: (r) => r.role },
          { id: 'dept', label: 'Département', render: (r) => r.departement },
          { id: 'actif', label: 'Actif', render: (r) => (r.actif ? 'Oui' : 'Non') },
          { id: 'last', label: 'Dernière connexion', render: (r) => r.derniereConnexion },
        ]}
        rows={mockUtilisateurs}
        actions={[
          { id: 'voir', label: 'Voir', onClick: () => undefined },
          { id: 'modifier', label: 'Modifier', onClick: () => undefined },
        ]}
      />
    </Box>
  );
}

export function AdminRolesPage() {
  return (
    <Box>
      <PageHeader
        title="Rôles"
        subtitle="Référentiel des rôles applicatifs (mock)."
        breadcrumbs={[
          { label: 'e-Finance', to: '/dashboard' },
          { label: 'Administration' },
          { label: 'Rôles' },
        ]}
      />
      <DataTable
        columns={[
          { id: 'code', label: 'Code', render: (r) => r.code },
          { id: 'libelle', label: 'Libellé', render: (r) => r.libelle },
          { id: 'users', label: 'Utilisateurs', align: 'right', render: (r) => r.utilisateurs },
        ]}
        rows={mockRoles}
      />
    </Box>
  );
}

export function AdminCircuitsPage() {
  return (
    <Box>
      <PageHeader
        title="Circuits de validation"
        subtitle="Paramétrage des workflows (mock)."
        breadcrumbs={[
          { label: 'e-Finance', to: '/dashboard' },
          { label: 'Administration' },
          { label: 'Circuits' },
        ]}
      />
      <DataTable
        columns={[
          { id: 'nom', label: 'Circuit', render: (r) => r.nom },
          { id: 'etapes', label: 'Étapes', align: 'right', render: (r) => r.etapes },
          { id: 'actif', label: 'Actif', render: (r) => (r.actif ? 'Oui' : 'Non') },
        ]}
        rows={mockCircuits}
      />
    </Box>
  );
}

export function AdminAuditPage() {
  return (
    <Box>
      <PageHeader
        title="Journal d'audit"
        subtitle="Traçabilité des actions sensibles (mock)."
        breadcrumbs={[
          { label: 'e-Finance', to: '/dashboard' },
          { label: 'Administration' },
          { label: 'Audit' },
        ]}
      />
      <DataTable
        columns={[
          { id: 'date', label: 'Date', render: (r) => r.date },
          { id: 'user', label: 'Utilisateur', render: (r) => r.utilisateur },
          { id: 'action', label: 'Action', render: (r) => r.action },
          { id: 'objet', label: 'Objet', render: (r) => r.objet },
          { id: 'detail', label: 'Détail', render: (r) => r.detail },
        ]}
        rows={mockAuditLog}
      />
    </Box>
  );
}

export function AdminModulePage({ title }: { title: string }) {
  const isParametres = title === 'Paramètres';
  return (
    <ModulePlaceholder
      title={title}
      subtitle="Écran d’administration — maquette fonctionnelle."
      moduleLabel="Administration"
      breadcrumbs={[
        { label: 'e-Finance', to: '/dashboard' },
        { label: 'Administration', to: '/administration/utilisateurs' },
        { label: title },
      ]}
    >
      {isParametres ? (
        <ThemeSettingsPanel />
      ) : (
        <Stack spacing={2} direction={{ xs: 'column', sm: 'row' }}>
          <TextField select label="Environnement" size="small" defaultValue="dev" sx={{ minWidth: 180 }}>
            <MenuItem value="dev">Développement</MenuItem>
            <MenuItem value="uat">Recette</MenuItem>
            <MenuItem value="prod">Production</MenuItem>
          </TextField>
          <TextField label="Paramètre" size="small" placeholder="Clé de configuration" sx={{ flex: 1 }} />
        </Stack>
      )}
    </ModulePlaceholder>
  );
}
