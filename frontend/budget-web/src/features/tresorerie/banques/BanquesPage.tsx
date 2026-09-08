import AddIcon from '@mui/icons-material/Add';
import FileUploadOutlinedIcon from '@mui/icons-material/FileUploadOutlined';
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
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
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
import { analyserImportBanques, lignesAImporter, type BanqueImportPreview } from './banqueExcelImport';
import { BanquesImportDialog } from './BanquesImportDialog';
import {
  createBanque,
  fetchBanques,
  importBanques,
  updateBanque,
  type BanqueDto,
} from './banquesService';
import { readFirstXlsxSheet } from './xlsxSheetReader';

function apiErrorMessage(err: unknown, fallback: string): string {
  if (err && typeof err === 'object' && 'response' in err) {
    const data = (err as { response?: { data?: { title?: string; detail?: string; message?: string } } })
      .response?.data;
    return data?.detail || data?.title || data?.message || fallback;
  }
  if (err instanceof Error) return err.message;
  return fallback;
}

type BanqueRow = BanqueDto & { id: string };
type FormMode = 'create' | 'edit';

function normalizePays(value: string): string | null {
  const trimmed = value.trim();
  return trimmed.length ? trimmed : null;
}

export function BanquesPage() {
  const { user } = useAuth();
  const msgBox = useMsgBox();
  const canWrite = canWriteReferentiels(user);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const [rows, setRows] = useState<BanqueRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [statutFilter, setStatutFilter] = useState('all');

  const [dialogOpen, setDialogOpen] = useState(false);
  const [formMode, setFormMode] = useState<FormMode>('create');
  const [editingId, setEditingId] = useState<string | null>(null);
  const [idBanque, setIdBanque] = useState('');
  const [libelleBanque, setLibelleBanque] = useState('');
  const [pays, setPays] = useState('');
  const [actif, setActif] = useState(true);
  const [saving, setSaving] = useState(false);

  const [importPreview, setImportPreview] = useState<BanqueImportPreview | null>(null);
  const [importOpen, setImportOpen] = useState(false);
  const [importing, setImporting] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await fetchBanques();
      setRows(data.map((b) => ({ ...b, id: b.idBanque })));
    } catch (err) {
      setError(apiErrorMessage(err, 'Impossible de charger les banques.'));
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
        r.idBanque.toLowerCase().includes(q) ||
        r.libelleBanque.toLowerCase().includes(q) ||
        (r.pays ?? '').toLowerCase().includes(q);
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
    setIdBanque('');
    setLibelleBanque('');
    setPays('');
    setActif(true);
    setDialogOpen(true);
  };

  const openEdit = (row: BanqueRow) => {
    setFormMode('edit');
    setEditingId(row.idBanque);
    setIdBanque(row.idBanque);
    setLibelleBanque(row.libelleBanque);
    setPays(row.pays ?? '');
    setActif(row.actif);
    setDialogOpen(true);
  };

  const save = async () => {
    if (formMode === 'create' && !idBanque.trim()) {
      void msgBox.error('L’ID Banque est obligatoire (référence historique, ex. RAWBANK).');
      return;
    }
    if (!libelleBanque.trim()) {
      void msgBox.error('Le libellé de la banque est obligatoire.');
      return;
    }
    setSaving(true);
    try {
      if (formMode === 'create') {
        await createBanque({
          idBanque: idBanque.trim(),
          libelleBanque: libelleBanque.trim(),
          pays: normalizePays(pays),
          actif,
        });
        void msgBox.success('Banque créée.');
      } else if (editingId != null) {
        await updateBanque(editingId, {
          libelleBanque: libelleBanque.trim(),
          pays: normalizePays(pays),
          actif,
        });
        void msgBox.success('Banque mise à jour.');
      }
      setDialogOpen(false);
      await load();
    } catch (err) {
      void msgBox.error(apiErrorMessage(err, 'Enregistrement impossible.'));
    } finally {
      setSaving(false);
    }
  };

  const runDesactiver = async (row: BanqueRow) => {
    const ok = await msgBox.confirm({
      title: `Désactiver « ${row.libelleBanque} » ?`,
      message: 'La banque restera dans le référentiel, mais sera marquée inactive.',
      confirmLabel: 'Désactiver',
    });
    if (!ok) return;
    try {
      await updateBanque(row.idBanque, {
        libelleBanque: row.libelleBanque,
        pays: row.pays,
        actif: false,
      });
      void msgBox.success(`Banque « ${row.libelleBanque} » désactivée.`);
      await load();
    } catch (err) {
      void msgBox.error(apiErrorMessage(err, 'Action impossible.'));
    }
  };

  const runActiver = async (row: BanqueRow) => {
    const ok = await msgBox.confirm({
      title: `Activer « ${row.libelleBanque} » ?`,
      message: 'La banque sera de nouveau proposée dans les écrans de paramétrage.',
      confirmLabel: 'Activer',
    });
    if (!ok) return;
    try {
      await updateBanque(row.idBanque, {
        libelleBanque: row.libelleBanque,
        pays: row.pays,
        actif: true,
      });
      void msgBox.success(`Banque « ${row.libelleBanque} » activée.`);
      await load();
    } catch (err) {
      void msgBox.error(apiErrorMessage(err, 'Action impossible.'));
    }
  };

  const onPickExcel = async (file: File | undefined) => {
    if (!file) return;
    try {
      const sheet = await readFirstXlsxSheet(file);
      const preview = analyserImportBanques(file.name, sheet, rows);
      setImportPreview(preview);
      setImportOpen(true);
    } catch (err) {
      void msgBox.error(apiErrorMessage(err, 'Lecture du fichier Excel impossible.'));
    } finally {
      if (fileInputRef.current) fileInputRef.current.value = '';
    }
  };

  const confirmImport = async () => {
    if (!importPreview) return;
    const items = lignesAImporter(importPreview);
    if (!items.length) return;
    const ok = await msgBox.confirm({
      title: 'Confirmer l’import',
      message: `Importer ${items.length} banque${items.length > 1 ? 's' : ''} ? Les doublons et erreurs ne seront pas enregistrés.`,
      confirmLabel: 'Importer',
    });
    if (!ok) return;
    setImporting(true);
    try {
      const result = await importBanques(items);
      const suffix = result.crees > 1 ? 's' : '';
      void msgBox.success(
        result.crees > 0
          ? `${result.crees} banque${suffix} importée${suffix}.`
          : 'Aucune banque créée (doublons ou déjà existantes).',
      );
      setImportOpen(false);
      setImportPreview(null);
      await load();
    } catch (err) {
      void msgBox.error(apiErrorMessage(err, 'Import impossible.'));
    } finally {
      setImporting(false);
    }
  };

  const columns: DataTableColumn<BanqueRow>[] = [
    {
      id: 'idBanque',
      label: 'ID Banque',
      mobile: 'title',
      render: (r) => (
        <Typography sx={{ fontWeight: 700, fontFamily: 'ui-monospace, monospace' }}>{r.idBanque}</Typography>
      ),
    },
    {
      id: 'libelle',
      label: 'Libellé',
      mobile: 'subtitle',
      render: (r) => r.libelleBanque,
    },
    {
      id: 'pays',
      label: 'Pays',
      mobile: 'meta',
      render: (r) => r.pays ?? '—',
    },
    {
      id: 'actif',
      label: 'Statut',
      mobile: 'meta',
      render: (r) => (
        <Chip
          size="small"
          label={r.actif ? 'Active' : 'Inactive'}
          color={r.actif ? 'success' : 'default'}
          variant={r.actif ? 'outlined' : 'filled'}
        />
      ),
    },
  ];

  return (
    <Box>
      <PageHeader
        title="Banques"
        subtitle="Référentiel des banques et établissements financiers utilisés par la Trésorerie."
        breadcrumbs={[
          { label: 'Trésorerie', to: '/tresorerie/dashboard' },
          { label: 'Référentiels' },
          { label: 'Banques' },
        ]}
        actions={
          canWrite ? (
            <>
              <input
                ref={fileInputRef}
                type="file"
                accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                hidden
                onChange={(e) => void onPickExcel(e.target.files?.[0])}
              />
              <SecondaryButton
                startIcon={<FileUploadOutlinedIcon />}
                onClick={() => fileInputRef.current?.click()}
              >
                Importer depuis Excel
              </SecondaryButton>
              <Button variant="contained" startIcon={<AddIcon />} onClick={openCreate}>
                Nouvelle banque
              </Button>
            </>
          ) : undefined
        }
      />

      <FilterBar
        search={search}
        onSearchChange={setSearch}
        searchPlaceholder="Rechercher ID, libellé, pays…"
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
            <MenuItem value="actif">Actives</MenuItem>
            <MenuItem value="inactif">Inactives</MenuItem>
          </TextField>
        }
      />

      {loading && <LoadingState label="Chargement des banques…" />}
      {error && !loading && <ErrorState message={error} onRetry={() => void load()} />}
      {!loading && !error && (
      <DataTable
        columns={columns}
        rows={filtered}
        emptyTitle="Aucune banque"
        emptyDescription={
          rows.length === 0
            ? 'Le référentiel ne contient aucune banque. Créez-en une ou importez un fichier Excel.'
            : 'Aucune banque ne correspond aux critères sélectionnés.'
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
        <DialogTitle>{formMode === 'create' ? 'Nouvelle banque' : 'Modifier la banque'}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <TextField
              label="ID Banque"
              value={idBanque}
              onChange={(e) => setIdBanque(e.target.value)}
              disabled={formMode === 'edit' || saving}
              required={formMode === 'create'}
              helperText={
                formMode === 'create'
                  ? 'Référence historique SNEL (ex. RAWBANK). Non générée automatiquement.'
                  : 'Référence de la banque — non modifiable (liens vers les comptes).'
              }
              slotProps={{ htmlInput: { maxLength: 50 } }}
            />
            <TextField
              label="Libellé de la banque"
              value={libelleBanque}
              onChange={(e) => setLibelleBanque(e.target.value)}
              disabled={saving}
              required
              slotProps={{ htmlInput: { maxLength: 200 } }}
            />
            <TextField
              label="Pays"
              value={pays}
              onChange={(e) => setPays(e.target.value)}
              disabled={saving}
              slotProps={{ htmlInput: { maxLength: 100 } }}
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
              Une banque déjà utilisée par un compte ne peut pas être supprimée : désactivez-la
              pour qu’elle ne soit plus proposée.
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

      <BanquesImportDialog
        open={importOpen}
        preview={importPreview}
        importing={importing}
        onClose={() => {
          setImportOpen(false);
          setImportPreview(null);
        }}
        onConfirm={() => void confirmImport()}
      />
    </Box>
  );
}
