import AddIcon from '@mui/icons-material/Add';
import {
  Box,
  Button,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  MenuItem,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  DataTable,
  ErrorState,
  FilterBar,
  LoadingState,
  PageHeader,
  PrimaryButton,
  SecondaryButton,
  useMsgBox,
  type DataTableColumn,
} from '../../../components';
import { canWriteReferentiels, useAuth } from '../../auth';
import {
  createProvince,
  fetchProvinces,
  updateProvince,
  type ProvinceDto,
} from './provincesService';

function apiErrorMessage(err: unknown, fallback: string): string {
  if (err && typeof err === 'object' && 'response' in err) {
    const data = (err as { response?: { data?: { title?: string; detail?: string; message?: string } } })
      .response?.data;
    return data?.detail || data?.title || data?.message || fallback;
  }
  if (err instanceof Error) return err.message;
  return fallback;
}

function formatDateTimeFr(iso: string | null | undefined): string {
  if (!iso) return '—';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '—';
  return d.toLocaleString('fr-FR', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

type ProvinceRow = ProvinceDto & { id: string };
type FormMode = 'create' | 'edit';

export function ProvincesPage() {
  const { user } = useAuth();
  const msgBox = useMsgBox();
  const canWrite = canWriteReferentiels(user);

  const [rows, setRows] = useState<ProvinceRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [statutFilter, setStatutFilter] = useState('all');

  const [dialogOpen, setDialogOpen] = useState(false);
  const [formMode, setFormMode] = useState<FormMode>('create');
  const [editingId, setEditingId] = useState<string | null>(null);
  const [idProvince, setIdProvince] = useState('');
  const [libelle, setLibelle] = useState('');
  const [actif, setActif] = useState(true);
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await fetchProvinces();
      setRows(data.map((p) => ({ ...p, id: p.idProvince })));
    } catch (err) {
      setError(apiErrorMessage(err, 'Impossible de charger les provinces.'));
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
    return rows.filter((r) => {
      const matchesSearch =
        !q ||
        r.idProvince.toLowerCase().includes(q) ||
        r.libelle.toLowerCase().includes(q);
      const matchesStatut =
        statutFilter === 'all' ||
        (statutFilter === 'actif' && r.actif) ||
        (statutFilter === 'inactif' && !r.actif);
      return matchesSearch && matchesStatut;
    });
  }, [rows, search, statutFilter]);

  const openCreate = () => {
    setFormMode('create');
    setEditingId(null);
    setIdProvince('');
    setLibelle('');
    setActif(true);
    setDialogOpen(true);
  };

  const openEdit = (row: ProvinceRow) => {
    setFormMode('edit');
    setEditingId(row.idProvince);
    setIdProvince(row.idProvince);
    setLibelle(row.libelle);
    setActif(row.actif);
    setDialogOpen(true);
  };

  const save = async () => {
    if (!idProvince.trim()) {
      void msgBox.error("L'identifiant province est obligatoire.");
      return;
    }
    if (!libelle.trim()) {
      void msgBox.error('Le libellé est obligatoire.');
      return;
    }
    setSaving(true);
    try {
      if (formMode === 'create') {
        await createProvince({
          idProvince: idProvince.trim(),
          libelle: libelle.trim(),
          actif,
        });
        void msgBox.success('Province créée.');
      } else if (editingId != null) {
        await updateProvince(editingId, {
          idProvince: idProvince.trim(),
          libelle: libelle.trim(),
          actif,
        });
        void msgBox.success('Province mise à jour.');
      }
      setDialogOpen(false);
      await load();
    } catch (err) {
      void msgBox.error(apiErrorMessage(err, 'Enregistrement impossible.'));
    } finally {
      setSaving(false);
    }
  };

  const runDesactiver = async (row: ProvinceRow) => {
    const ok = await msgBox.confirm({
      title: `Désactiver « ${row.libelle} » ?`,
      message:
        'La province restera dans le référentiel et sur les comptes existants, mais sera marquée inactive.',
      confirmLabel: 'Désactiver',
    });
    if (!ok) return;
    try {
      await updateProvince(row.idProvince, {
        idProvince: row.idProvince,
        libelle: row.libelle,
        actif: false,
      });
      void msgBox.success(`Province « ${row.libelle} » désactivée.`);
      await load();
    } catch (err) {
      void msgBox.error(apiErrorMessage(err, 'Action impossible.'));
    }
  };

  const runActiver = async (row: ProvinceRow) => {
    const ok = await msgBox.confirm({
      title: `Activer « ${row.libelle} » ?`,
      message: 'La province sera de nouveau proposée dans les écrans de paramétrage.',
      confirmLabel: 'Activer',
    });
    if (!ok) return;
    try {
      await updateProvince(row.idProvince, {
        idProvince: row.idProvince,
        libelle: row.libelle,
        actif: true,
      });
      void msgBox.success(`Province « ${row.libelle} » activée.`);
      await load();
    } catch (err) {
      void msgBox.error(apiErrorMessage(err, 'Action impossible.'));
    }
  };

  const columns: DataTableColumn<ProvinceRow>[] = [
    {
      id: 'id',
      label: 'ID Province',
      mobile: 'title',
      render: (r) => (
        <Typography sx={{ fontWeight: 700, fontFamily: 'ui-monospace, monospace' }}>
          {r.idProvince}
        </Typography>
      ),
    },
    {
      id: 'libelle',
      label: 'Libellé',
      mobile: 'subtitle',
      render: (r) => r.libelle,
    },
    {
      id: 'actif',
      label: 'Statut',
      mobile: 'meta',
      render: (r) => (
        <Chip
          size="small"
          label={r.actif ? 'Actif' : 'Inactif'}
          color={r.actif ? 'success' : 'default'}
          variant={r.actif ? 'outlined' : 'filled'}
        />
      ),
    },
    {
      id: 'dateCreation',
      label: 'Création',
      mobile: 'meta',
      render: (r) => formatDateTimeFr(r.dateCreation),
    },
    {
      id: 'dateModification',
      label: 'Modification',
      mobile: 'meta',
      render: (r) => formatDateTimeFr(r.dateModification),
    },
  ];

  return (
    <Box>
      <PageHeader
        title="Provinces"
        subtitle="Identifiants métier des provinces / sites rattachés aux comptes financiers."
        breadcrumbs={[
          { label: 'Trésorerie', to: '/tresorerie/dashboard' },
          { label: 'Référentiels' },
          { label: 'Provinces' },
        ]}
        actions={
          canWrite ? (
            <Button variant="contained" startIcon={<AddIcon />} onClick={openCreate}>
              Nouvelle province
            </Button>
          ) : undefined
        }
      />

      <FilterBar
        search={search}
        onSearchChange={setSearch}
        searchPlaceholder="Rechercher identifiant, libellé…"
        extra={
          <TextField
            select
            size="small"
            label="Statut"
            value={statutFilter}
            onChange={(e) => setStatutFilter(e.target.value)}
            sx={{ minWidth: 140 }}
          >
            <MenuItem value="all">Tous</MenuItem>
            <MenuItem value="actif">Actifs</MenuItem>
            <MenuItem value="inactif">Inactifs</MenuItem>
          </TextField>
        }
      />

      {loading && <LoadingState label="Chargement des provinces…" />}
      {error && !loading && <ErrorState message={error} onRetry={() => void load()} />}
      {!loading && !error && (
        <DataTable
          columns={columns}
          rows={filtered}
          emptyTitle="Aucune province"
          emptyDescription={
            rows.length === 0
              ? 'Le référentiel ne contient aucune province. Créez-en une pour commencer.'
              : 'Aucune province ne correspond aux critères sélectionnés.'
          }
          actions={
            canWrite
              ? [
                  {
                    id: 'modifier',
                    label: 'Modifier',
                    onClick: (row) => openEdit(row),
                  },
                  {
                    id: 'desactiver',
                    label: 'Désactiver',
                    hidden: (row) => !row.actif,
                    onClick: (row) => void runDesactiver(row),
                  },
                  {
                    id: 'activer',
                    label: 'Activer',
                    hidden: (row) => row.actif,
                    onClick: (row) => void runActiver(row),
                  },
                ]
              : undefined
          }
        />
      )}

      <Dialog open={dialogOpen} onClose={() => !saving && setDialogOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>{formMode === 'create' ? 'Nouvelle province' : 'Modifier la province'}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <TextField
              label="Identifiant province"
              value={idProvince}
              onChange={(e) => setIdProvince(e.target.value)}
              disabled={saving}
              required
              helperText={
                formMode === 'edit'
                  ? 'Identifiant métier. Modifiable uniquement si aucun compte financier ne l’utilise.'
                  : 'Identifiant métier unique (ex. KIN, KIND, KISA). Saisi par l’utilisateur, jamais généré.'
              }
              slotProps={{ htmlInput: { maxLength: 20 } }}
            />
            <TextField
              label="Libellé"
              value={libelle}
              onChange={(e) => setLibelle(e.target.value)}
              disabled={saving}
              required
              slotProps={{ htmlInput: { maxLength: 200 } }}
            />
            <TextField
              select
              label="Actif"
              value={actif ? '1' : '0'}
              onChange={(e) => setActif(e.target.value === '1')}
              disabled={saving}
            >
              <MenuItem value="1">Actif</MenuItem>
              <MenuItem value="0">Inactif</MenuItem>
            </TextField>
            <Typography variant="caption" color="text.secondary">
              Une province référencée par un compte financier ne peut pas être supprimée : désactivez-la.
              Les comptes existants conservent leur FK_Province.
            </Typography>
          </Stack>
        </DialogContent>
        <DialogActions>
          <SecondaryButton onClick={() => setDialogOpen(false)} disabled={saving}>
            Annuler
          </SecondaryButton>
          <PrimaryButton onClick={() => void save()} disabled={saving}>
            Enregistrer
          </PrimaryButton>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
