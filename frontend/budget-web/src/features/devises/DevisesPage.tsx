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

import { useLocation } from 'react-router-dom';

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

} from '../../components';

import {

  createDevise,

  fetchDevises,

  updateDevise,

  type DeviseDto,

} from '../../services/apiClient';

import { BRAND_NAME } from '../../theme';

import { workspaceReferentielBreadcrumbs } from '../../layout/workspace';

import { useAuth, canWriteReferentiels } from '../auth';



type DeviseRow = DeviseDto & { id: string };

type FormMode = 'create' | 'edit';



function apiErrorMessage(err: unknown, fallback: string): string {

  if (err && typeof err === 'object' && 'response' in err) {

    const data = (err as { response?: { data?: { title?: string; detail?: string; message?: string } } })

      .response?.data;

    return data?.detail || data?.title || data?.message || fallback;

  }

  if (err instanceof Error) return err.message;

  return fallback;

}



export function DevisesPage() {

  const { user } = useAuth();

  const location = useLocation();

  const msgBox = useMsgBox();

  const canWrite = canWriteReferentiels(user);

  const [rows, setRows] = useState<DeviseRow[]>([]);

  const [loading, setLoading] = useState(true);

  const [error, setError] = useState<string | null>(null);

  const [search, setSearch] = useState('');

  const [statutFilter, setStatutFilter] = useState('all');



  const [dialogOpen, setDialogOpen] = useState(false);

  const [formMode, setFormMode] = useState<FormMode>('create');

  const [editingId, setEditingId] = useState<number | null>(null);

  const [code, setCode] = useState('');

  const [libelle, setLibelle] = useState('');

  const [symbole, setSymbole] = useState('');

  const [actif, setActif] = useState(true);

  const [saving, setSaving] = useState(false);



  const load = useCallback(async () => {

    setLoading(true);

    setError(null);

    try {

      const data = await fetchDevises(false);

      setRows(data.map((d) => ({ ...d, id: String(d.idDevise) })));

    } catch {

      setError('Impossible de charger les devises.');

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

        r.code.toLowerCase().includes(q) ||

        r.libelle.toLowerCase().includes(q) ||

        (r.symbole ?? '').toLowerCase().includes(q);

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

    setCode('');

    setLibelle('');

    setSymbole('');

    setActif(true);

    setDialogOpen(true);

  };



  const openEdit = (row: DeviseRow) => {

    setFormMode('edit');

    setEditingId(row.idDevise);

    setCode(row.code);

    setLibelle(row.libelle);

    setSymbole(row.symbole ?? '');

    setActif(row.actif);

    setDialogOpen(true);

  };



  const save = async () => {

    setSaving(true);

    try {

      if (formMode === 'create') {

        if (!code.trim() || code.trim().length !== 3) {

          void msgBox.error('Le code doit comporter exactement 3 caractères (ex. EUR).');

          return;

        }

        if (!libelle.trim()) {

          void msgBox.error('Le libellé est obligatoire.');

          return;

        }

        await createDevise({

          code: code.trim().toUpperCase(),

          libelle: libelle.trim(),

          symbole: symbole.trim() || null,

          actif,

        });

        void msgBox.success('Devise créée.');

      } else if (editingId != null) {

        if (!libelle.trim()) {

          void msgBox.error('Le libellé est obligatoire.');

          return;

        }

        await updateDevise(editingId, {

          libelle: libelle.trim(),

          symbole: symbole.trim() || null,

          actif,

        });

        void msgBox.success('Devise mise à jour.');

      }

      setDialogOpen(false);

      await load();

    } catch (err) {

      void msgBox.error(apiErrorMessage(err, 'Enregistrement impossible.'));

    } finally {

      setSaving(false);

    }

  };



  const toggleActif = async (row: DeviseRow) => {

    try {

      await updateDevise(row.idDevise, {

        libelle: row.libelle,

        symbole: row.symbole,

        actif: !row.actif,

      });

      void msgBox.success(row.actif ? 'Devise désactivée.' : 'Devise activée.');

      await load();

    } catch (err) {

      void msgBox.error(apiErrorMessage(err, 'Action impossible.'));

    }

  };



  const columns: DataTableColumn<DeviseRow>[] = [

    {

      id: 'code',

      label: 'Code',

      mobile: 'title',

      render: (r) => <Typography sx={{ fontWeight: 700 }}>{r.code}</Typography>,

    },

    { id: 'libelle', label: 'Libellé', mobile: 'subtitle', render: (r) => r.libelle },

    { id: 'symbole', label: 'Symbole', mobile: 'meta', render: (r) => r.symbole ?? '—' },

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

        title="Devises"

        subtitle="Référentiel des devises utilisées par les demandes de paiement."

        breadcrumbs={workspaceReferentielBreadcrumbs(location.pathname, 'Devises', BRAND_NAME)}

        actions={

          canWrite ? (

            <Button variant="contained" startIcon={<AddIcon />} onClick={openCreate}>

              Nouvelle devise

            </Button>

          ) : undefined

        }

      />



      <FilterBar

        search={search}

        onSearchChange={setSearch}

        searchPlaceholder="Rechercher code, libellé…"

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



      {loading && <LoadingState />}

      {error && !loading && <ErrorState message={error} onRetry={() => void load()} />}

      {!loading && !error && (

        <DataTable

          columns={columns}

          rows={filtered}

          actions={

            canWrite

              ? [

                  {

                    id: 'modifier',

                    label: 'Modifier',

                    onClick: (row) => openEdit(row),

                  },

                  {

                    id: 'toggle',

                    label: 'Activer / désactiver',

                    onClick: (row) => void toggleActif(row),

                  },

                ]

              : undefined

          }

        />

      )}



      <Dialog open={dialogOpen} onClose={() => !saving && setDialogOpen(false)} fullWidth maxWidth="sm">

        <DialogTitle>{formMode === 'create' ? 'Nouvelle devise' : 'Modifier la devise'}</DialogTitle>

        <DialogContent>

          <Stack spacing={2} sx={{ mt: 1 }}>

            <TextField

              label="Code"

              value={code}

              onChange={(e) => setCode(e.target.value.toUpperCase())}

              disabled={formMode === 'edit' || saving}

              required

              helperText="Code ISO à 3 lettres (EUR, pas EURO)"

              slotProps={{ htmlInput: { maxLength: 3 } }}

            />

            <TextField

              label="Libellé"

              value={libelle}

              onChange={(e) => setLibelle(e.target.value)}

              disabled={saving}

              required

            />

            <TextField

              label="Symbole"

              value={symbole}

              onChange={(e) => setSymbole(e.target.value)}

              disabled={saving}

            />

            <TextField

              select

              label="Statut"

              value={actif ? '1' : '0'}

              onChange={(e) => setActif(e.target.value === '1')}

              disabled={saving}

            >

              <MenuItem value="1">Active</MenuItem>

              <MenuItem value="0">Inactive</MenuItem>

            </TextField>

            <Typography variant="caption" color="text.secondary">

              Une devise déjà utilisée par une demande ne peut pas être supprimée : désactivez-la

              pour qu’elle ne soit plus proposée aux nouvelles demandes.

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


