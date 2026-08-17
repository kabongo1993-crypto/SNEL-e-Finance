import AddIcon from '@mui/icons-material/Add';
import {
  Alert,
  Box,
  Button,
  Checkbox,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControlLabel,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { isAxiosError } from 'axios';
import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  DataTable,
  ErrorState,
  FilterBar,
  LoadingState,
  PageHeader,
  PrimaryButton,
  SecondaryButton,
  type DataTableColumn,
} from '../../components';
import {
  createDepartement,
  fetchDepartements,
  type Departement,
} from '../../services/apiClient';
import { BRAND_NAME } from '../../theme';
import { formatDateFr } from '../../mocks/types';

type DepartementRow = Departement & { id: string };

export function DepartementsPage() {
  const [rows, setRows] = useState<DepartementRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [success, setSuccess] = useState<string | null>(null);

  const [dialogOpen, setDialogOpen] = useState(false);
  const [code, setCode] = useState('');
  const [libelle, setLibelle] = useState('');
  const [actif, setActif] = useState(true);
  const [formError, setFormError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<{ code?: string; libelle?: string }>({});
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await fetchDepartements();
      setRows(data.map((d) => ({ ...d, id: String(d.idDepartement) })));
    } catch {
      setError('Impossible de charger les départements.');
      setRows([]);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return rows;
    return rows.filter(
      (r) => r.code.toLowerCase().includes(q) || r.libelle.toLowerCase().includes(q),
    );
  }, [rows, search]);

  const columns: DataTableColumn<DepartementRow>[] = [
    {
      id: 'code',
      label: 'Code',
      sortable: true,
      sortValue: (r) => r.code,
      render: (r) => (
        <Typography variant="body2" sx={{ fontFamily: 'ui-monospace, monospace', fontWeight: 700 }}>
          {r.code}
        </Typography>
      ),
    },
    {
      id: 'libelle',
      label: 'Libellé',
      sortable: true,
      sortValue: (r) => r.libelle,
      render: (r) => r.libelle,
    },
    {
      id: 'statut',
      label: 'Statut',
      render: (r) => (r.actif ? 'Actif' : 'Inactif'),
    },
    {
      id: 'date',
      label: 'Date de création',
      sortable: true,
      sortValue: (r) => r.dateCreation,
      render: (r) => formatDateFr(r.dateCreation),
    },
  ];

  const openCreate = () => {
    setCode('');
    setLibelle('');
    setActif(true);
    setFormError(null);
    setFieldErrors({});
    setDialogOpen(true);
  };

  const validate = () => {
    const next: { code?: string; libelle?: string } = {};
    const normalizedCode = code.trim();
    if (!normalizedCode) next.code = 'Le code est obligatoire.';
    if (!libelle.trim()) next.libelle = 'Le libellé est obligatoire.';
    setFieldErrors(next);
    return Object.keys(next).length === 0;
  };

  const handleSave = async () => {
    setFormError(null);
    setSuccess(null);
    if (!validate()) return;

    setSaving(true);
    try {
      await createDepartement({
        code: code.trim().toUpperCase(),
        libelle: libelle.trim(),
        actif,
      });
      setDialogOpen(false);
      setSuccess('Département créé avec succès.');
      await load();
    } catch (err) {
      if (isAxiosError(err)) {
        const message =
          (err.response?.data as { message?: string } | undefined)?.message ??
          'Impossible de créer le département.';
        setFormError(message);
      } else {
        setFormError('Impossible de créer le département.');
      }
    } finally {
      setSaving(false);
    }
  };

  return (
    <Box>
      <PageHeader
        title="Départements"
        subtitle="Référentiel des départements — données réelles BD_SNEL."
        breadcrumbs={[
          { label: BRAND_NAME, to: '/dashboard' },
          { label: 'Référentiels', to: '/referentiels/organisationnel' },
          { label: 'Départements' },
        ]}
        actions={
          <PrimaryButton startIcon={<AddIcon />} onClick={openCreate}>
            Nouveau département
          </PrimaryButton>
        }
      />

      {success && (
        <Alert severity="success" sx={{ mb: 1.5 }} onClose={() => setSuccess(null)}>
          {success}
        </Alert>
      )}

      <FilterBar
        search={search}
        onSearchChange={setSearch}
        searchPlaceholder="Rechercher par code ou libellé…"
      />

      {loading ? (
        <LoadingState label="Chargement des départements…" />
      ) : error ? (
        <ErrorState message={error} onRetry={() => void load()} />
      ) : (
        <DataTable
          columns={columns}
          rows={filtered}
          emptyTitle={
            search.trim()
              ? 'Aucun département ne correspond à votre recherche.'
              : 'Aucun département trouvé.'
          }
          emptyDescription={
            search.trim()
              ? 'Modifiez les critères de recherche.'
              : 'Créez un département pour commencer.'
          }
          actions={[
            {
              id: 'modifier',
              label: 'Modifier',
              onClick: () =>
                setSuccess('La modification des départements sera disponible dans une prochaine étape.'),
            },
          ]}
        />
      )}

      <Dialog open={dialogOpen} onClose={() => !saving && setDialogOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>Nouveau département</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            {formError && <Alert severity="error">{formError}</Alert>}
            <TextField
              required
              label="Code"
              value={code}
              onChange={(e) => setCode(e.target.value.toUpperCase())}
              error={Boolean(fieldErrors.code)}
              helperText={fieldErrors.code}
              disabled={saving}
              autoFocus
              slotProps={{ htmlInput: { maxLength: 30 } }}
            />
            <TextField
              required
              label="Libellé"
              value={libelle}
              onChange={(e) => setLibelle(e.target.value)}
              error={Boolean(fieldErrors.libelle)}
              helperText={fieldErrors.libelle}
              disabled={saving}
              slotProps={{ htmlInput: { maxLength: 200 } }}
            />
            <FormControlLabel
              control={
                <Checkbox
                  checked={actif}
                  onChange={(e) => setActif(e.target.checked)}
                  disabled={saving}
                />
              }
              label="Actif"
            />
          </Stack>
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2 }}>
          <SecondaryButton onClick={() => setDialogOpen(false)} disabled={saving}>
            Annuler
          </SecondaryButton>
          <Button variant="contained" onClick={() => void handleSave()} disabled={saving}>
            {saving ? 'Enregistrement…' : 'Enregistrer'}
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
