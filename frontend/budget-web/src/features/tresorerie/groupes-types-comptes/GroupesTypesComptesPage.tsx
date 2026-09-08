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
  createGroupeTypeCompte,
  fetchGroupesTypesComptes,
  updateGroupeTypeCompte,
  type GroupeTypeCompteDto,
} from './groupesTypesComptesService';

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

type GroupeRow = GroupeTypeCompteDto & { id: string };
type FormMode = 'create' | 'edit';

export function GroupesTypesComptesPage() {
  const { user } = useAuth();
  const msgBox = useMsgBox();
  const canWrite = canWriteReferentiels(user);

  const [rows, setRows] = useState<GroupeRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [statutFilter, setStatutFilter] = useState('all');

  const [dialogOpen, setDialogOpen] = useState(false);
  const [formMode, setFormMode] = useState<FormMode>('create');
  const [editingId, setEditingId] = useState<number | null>(null);
  const [libelle, setLibelle] = useState('');
  const [actif, setActif] = useState(true);
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await fetchGroupesTypesComptes();
      setRows(data.map((g) => ({ ...g, id: String(g.idGroupeTypeCompte) })));
    } catch (err) {
      setError(apiErrorMessage(err, 'Impossible de charger les groupes de types de comptes.'));
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
        String(r.idGroupeTypeCompte).includes(q) ||
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
    setLibelle('');
    setActif(true);
    setDialogOpen(true);
  };

  const openEdit = (row: GroupeRow) => {
    setFormMode('edit');
    setEditingId(row.idGroupeTypeCompte);
    setLibelle(row.libelle);
    setActif(row.actif);
    setDialogOpen(true);
  };

  const save = async () => {
    if (!libelle.trim()) {
      void msgBox.error('Le libellé est obligatoire.');
      return;
    }
    setSaving(true);
    try {
      if (formMode === 'create') {
        await createGroupeTypeCompte({ libelle: libelle.trim(), actif });
        void msgBox.success('Groupe créé.');
      } else if (editingId != null) {
        await updateGroupeTypeCompte(editingId, { libelle: libelle.trim(), actif });
        void msgBox.success('Groupe mis à jour.');
      }
      setDialogOpen(false);
      await load();
    } catch (err) {
      void msgBox.error(apiErrorMessage(err, 'Enregistrement impossible.'));
    } finally {
      setSaving(false);
    }
  };

  const runDesactiver = async (row: GroupeRow) => {
    const ok = await msgBox.confirm({
      title: `Désactiver « ${row.libelle} » ?`,
      message: 'Le groupe restera dans le référentiel, mais sera marqué inactif.',
      confirmLabel: 'Désactiver',
    });
    if (!ok) return;
    try {
      await updateGroupeTypeCompte(row.idGroupeTypeCompte, {
        libelle: row.libelle,
        actif: false,
      });
      void msgBox.success(`Groupe « ${row.libelle} » désactivé.`);
      await load();
    } catch (err) {
      void msgBox.error(apiErrorMessage(err, 'Action impossible.'));
    }
  };

  const runActiver = async (row: GroupeRow) => {
    const ok = await msgBox.confirm({
      title: `Activer « ${row.libelle} » ?`,
      message: 'Le groupe sera de nouveau proposé dans les écrans de paramétrage.',
      confirmLabel: 'Activer',
    });
    if (!ok) return;
    try {
      await updateGroupeTypeCompte(row.idGroupeTypeCompte, {
        libelle: row.libelle,
        actif: true,
      });
      void msgBox.success(`Groupe « ${row.libelle} » activé.`);
      await load();
    } catch (err) {
      void msgBox.error(apiErrorMessage(err, 'Action impossible.'));
    }
  };

  const columns: DataTableColumn<GroupeRow>[] = [
    {
      id: 'id',
      label: 'ID',
      mobile: 'meta',
      render: (r) => (
        <Typography sx={{ fontWeight: 700, fontFamily: 'ui-monospace, monospace' }}>
          {r.idGroupeTypeCompte}
        </Typography>
      ),
    },
    {
      id: 'libelle',
      label: 'Libellé',
      mobile: 'title',
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
        title="Groupes de types de comptes"
        subtitle="Regroupements fonctionnels des types de comptes financiers."
        breadcrumbs={[
          { label: 'Trésorerie', to: '/tresorerie/dashboard' },
          { label: 'Référentiels' },
          { label: 'Groupes de types de comptes' },
        ]}
        actions={
          canWrite ? (
            <Button variant="contained" startIcon={<AddIcon />} onClick={openCreate}>
              Nouveau groupe
            </Button>
          ) : undefined
        }
      />

      <FilterBar
        search={search}
        onSearchChange={setSearch}
        searchPlaceholder="Rechercher ID, libellé…"
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

      {loading && <LoadingState label="Chargement des groupes…" />}
      {error && !loading && <ErrorState message={error} onRetry={() => void load()} />}
      {!loading && !error && (
        <DataTable
          columns={columns}
          rows={filtered}
          emptyTitle="Aucun groupe"
          emptyDescription={
            rows.length === 0
              ? 'Le référentiel ne contient aucun groupe. Créez-en un pour commencer.'
              : 'Aucun groupe ne correspond aux critères sélectionnés.'
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
        <DialogTitle>{formMode === 'create' ? 'Nouveau groupe' : 'Modifier le groupe'}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            {formMode === 'edit' && editingId != null && (
              <TextField
                label="ID"
                value={editingId}
                disabled
                helperText="Identifiant technique généré par SQL Server — non modifiable."
              />
            )}
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
              Un groupe référencé par un type de compte ne peut pas être supprimé : désactivez-le
              pour qu’il ne soit plus proposé.
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
