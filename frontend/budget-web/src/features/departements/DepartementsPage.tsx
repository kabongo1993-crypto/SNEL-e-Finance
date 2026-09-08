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

import { isAxiosError } from 'axios';

import { useCallback, useEffect, useMemo, useState } from 'react';

import {

  DataTable,

  ErrorState,

  FilterBar,

  LoadingState,

  MasterDetailLayout,

  PageHeader,

  PrimaryButton,

  SecondaryButton,

  useMsgBox,

  type DataTableColumn,

} from '../../components';

import {

  createDepartement,

  deleteDepartement,

  fetchDepartements,

  updateDepartement,

  type Departement,

} from '../../services/apiClient';

import { BRAND_NAME } from '../../theme';

import { formatDateFr } from '../../mocks/types';

import { useAuth, canWriteReferentiels } from '../auth';

import { DepartementsDetailPanel } from './DepartementsDetailPanel';

import { apiErrorMessage, libelleStatut } from './deptUtils';



type DepartementRow = Departement & { id: string };

type FormMode = 'create' | 'edit';



export function DepartementsPage() {

  const { user } = useAuth();

  const msgBox = useMsgBox();

  const canWrite = canWriteReferentiels(user);

  const [rows, setRows] = useState<DepartementRow[]>([]);

  const [loading, setLoading] = useState(true);

  const [error, setError] = useState<string | null>(null);

  const [search, setSearch] = useState('');

  const [statutFilter, setStatutFilter] = useState('all');

  const [selectedId, setSelectedId] = useState<number | null>(null);



  const [dialogOpen, setDialogOpen] = useState(false);

  const [formMode, setFormMode] = useState<FormMode>('create');

  const [editingId, setEditingId] = useState<number | null>(null);

  const [code, setCode] = useState('');

  const [libelle, setLibelle] = useState('');

  const [actif, setActif] = useState(true);

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

    return rows.filter((r) => {

      const matchesSearch = !q || r.code.toLowerCase().includes(q) || r.libelle.toLowerCase().includes(q);

      const matchesStatut =

        statutFilter === 'all' ||

        (statutFilter === 'actif' && r.actif) ||

        (statutFilter === 'inactif' && !r.actif);

      return matchesSearch && matchesStatut;

    });

  }, [rows, search, statutFilter]);



  const selected = useMemo(

    () => rows.find((r) => r.idDepartement === selectedId) ?? null,

    [rows, selectedId],

  );



  const hasActiveFilter = search.trim() !== '' || statutFilter !== 'all';



  const columns: DataTableColumn<DepartementRow>[] = [

    {

      id: 'code',

      label: 'Code',

      sortable: true,

      sortValue: (r) => r.code,

      mobile: 'title',

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

      mobile: 'subtitle',

      render: (r) => r.libelle,

    },

    {

      id: 'statut',

      label: 'Statut',

      sortable: true,

      sortValue: (r) => libelleStatut(r.actif),

      mobile: 'meta',

      render: (r) => (

        <Chip

          size="small"

          label={libelleStatut(r.actif)}

          color={r.actif ? 'success' : 'default'}

          variant={r.actif ? 'outlined' : 'filled'}

          sx={{ fontWeight: 700, minWidth: 88 }}

        />

      ),

    },

    {

      id: 'date',

      label: 'Date de création',

      sortable: true,

      sortValue: (r) => r.dateCreation,

      mobile: 'meta',

      render: (r) => formatDateFr(r.dateCreation),

    },

  ];



  const openCreate = () => {

    setFormMode('create');

    setEditingId(null);

    setCode('');

    setLibelle('');

    setActif(true);

    setFieldErrors({});

    setDialogOpen(true);

  };



  const openEdit = (row: DepartementRow) => {

    setFormMode('edit');

    setEditingId(row.idDepartement);

    setCode(row.code);

    setLibelle(row.libelle);

    setActif(row.actif);

    setFieldErrors({});

    setDialogOpen(true);

  };



  const validate = () => {

    const next: { code?: string; libelle?: string } = {};

    if (!code.trim()) next.code = 'Le code est obligatoire.';

    if (!libelle.trim()) next.libelle = 'Le libellé est obligatoire.';

    setFieldErrors(next);

    return Object.keys(next).length === 0;

  };



  const handleSave = async () => {

    if (!validate()) return;



    setSaving(true);

    try {

      const payload = {

        code: code.trim().toUpperCase(),

        libelle: libelle.trim(),

        actif,

      };

      if (formMode === 'edit' && editingId != null) {

        const updated = await updateDepartement(editingId, payload);

        void msgBox.success('Département modifié avec succès.');

        setSelectedId(updated.idDepartement);

      } else {

        const created = await createDepartement(payload);

        void msgBox.success('Département créé avec succès.');

        setSelectedId(created.idDepartement);

      }

      setDialogOpen(false);

      await load();

    } catch (err) {

      void msgBox.error(

        isAxiosError(err)

          ? apiErrorMessage(err, 'Impossible d’enregistrer le département.')

          : 'Impossible d’enregistrer le département.',

      );

    } finally {

      setSaving(false);

    }

  };



  const runDelete = async (row: DepartementRow) => {

    const ok = await msgBox.confirm({

      title: `Supprimer « ${row.libelle} » ?`,

      message: 'Cette action est définitive.',

      danger: true,

      confirmLabel: 'Supprimer',

    });

    if (!ok) return;

    try {

      await deleteDepartement(row.idDepartement);

      void msgBox.success(`Département « ${row.libelle} » supprimé.`);

      setSelectedId((current) => (current === row.idDepartement ? null : current));

      await load();

    } catch (err) {

      void msgBox.error(

        isAxiosError(err)

          ? apiErrorMessage(err, 'Impossible de terminer l’opération.')

          : 'Impossible de terminer l’opération.',

      );

    }

  };



  const runDesactiver = async (row: DepartementRow) => {

    const ok = await msgBox.confirm({

      title: `Désactiver « ${row.libelle} » ?`,

      message: 'Le département restera dans le référentiel, mais sera marqué INACTIF.',

      confirmLabel: 'Désactiver',

    });

    if (!ok) return;

    try {

      const updated = await updateDepartement(row.idDepartement, {

        code: row.code,

        libelle: row.libelle,

        actif: false,

      });

      void msgBox.success(`Département « ${updated.libelle} » désactivé.`);

      setSelectedId(updated.idDepartement);

      await load();

    } catch (err) {

      void msgBox.error(

        isAxiosError(err)

          ? apiErrorMessage(err, 'Impossible de terminer l’opération.')

          : 'Impossible de terminer l’opération.',

      );

    }

  };



  const runActiver = async (row: DepartementRow) => {

    const ok = await msgBox.confirm({

      title: `Activer « ${row.libelle} » ?`,

      message: 'Le département sera de nouveau marqué ACTIF.',

      confirmLabel: 'Activer',

    });

    if (!ok) return;

    try {

      const updated = await updateDepartement(row.idDepartement, {

        code: row.code,

        libelle: row.libelle,

        actif: true,

      });

      void msgBox.success(`Département « ${updated.libelle} » activé.`);

      setSelectedId(updated.idDepartement);

      await load();

    } catch (err) {

      void msgBox.error(

        isAxiosError(err)

          ? apiErrorMessage(err, 'Impossible de terminer l’opération.')

          : 'Impossible de terminer l’opération.',

      );

    }

  };



  const emptyTitle = hasActiveFilter

    ? 'Aucun département ne correspond à votre recherche.'

    : 'Aucun département trouvé.';

  const emptyDescription = hasActiveFilter

    ? 'Modifiez les critères de recherche ou le filtre de statut.'

    : 'Créez un département pour commencer.';



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

          canWrite ? (

            <PrimaryButton startIcon={<AddIcon />} onClick={openCreate}>

              Nouveau département

            </PrimaryButton>

          ) : undefined

        }

      />



      <FilterBar

        search={search}

        onSearchChange={setSearch}

        searchPlaceholder="Rechercher par code ou libellé…"

        extra={

          <TextField select size="small"

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



      {loading ? (

        <LoadingState label="Chargement des départements…" />

      ) : error ? (

        <ErrorState message={error} onRetry={() => void load()} />

      ) : (

        <MasterDetailLayout

          detailOpen={selectedId != null}

          detailTitle="Détails du département"

          onCloseDetail={() => setSelectedId(null)}

          detail={<DepartementsDetailPanel selected={selected} />}

          master={

            <DataTable

              columns={columns}

              rows={filtered}

              emptyTitle={emptyTitle}

              emptyDescription={emptyDescription}

              selectedRowId={selectedId != null ? String(selectedId) : null}

              onRowClick={(row) => setSelectedId(row.idDepartement)}

              countLabel={(count) => (

                <>

                  <strong>{count}</strong> département{count > 1 ? 's' : ''}

                </>

              )}

              actions={[

                {

                  id: 'voir',

                  label: 'Voir',

                  onClick: (row) => setSelectedId(row.idDepartement),

                },

                {

                  id: 'modifier',

                  label: 'Modifier',

                  hidden: () => !canWrite,

                  onClick: (row) => openEdit(row),

                },

                {

                  id: 'desactiver',

                  label: 'Désactiver',

                  hidden: (row) => !canWrite || !row.actif,

                  onClick: (row) => void runDesactiver(row),

                },

                {

                  id: 'activer',

                  label: 'Activer',

                  hidden: (row) => !canWrite || row.actif,

                  onClick: (row) => void runActiver(row),

                },

                {

                  id: 'supprimer',

                  label: 'Supprimer',

                  color: 'error',

                  hidden: () => !canWrite,

                  onClick: (row) => void runDelete(row),

                },

              ]}

            />

          }

        />

      )}



      <Dialog open={dialogOpen} onClose={() => !saving && setDialogOpen(false)} fullWidth maxWidth="sm">

        <DialogTitle>{formMode === 'edit' ? 'Modifier le département' : 'Nouveau département'}</DialogTitle>

        <DialogContent>

          <Stack spacing={2} sx={{ mt: 1 }}>

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

            <TextField select label="Statut"

              value={actif ? 'ACTIF' : 'INACTIF'}

              onChange={(e) => setActif(e.target.value === 'ACTIF')}

              disabled={saving}

            >

              <MenuItem value="ACTIF">ACTIF</MenuItem>

              <MenuItem value="INACTIF">INACTIF</MenuItem>

            </TextField>

          </Stack>

        </DialogContent>

        <DialogActions sx={{ px: 3, pb: 2 }}>

          <SecondaryButton onClick={() => setDialogOpen(false)} disabled={saving}>

            Annuler

          </SecondaryButton>

          <Button variant="contained" onClick={() => void handleSave()} disabled={saving}>

            {saving ? 'Enregistrement…' : formMode === 'edit' ? 'Enregistrer' : 'Créer'}

          </Button>

        </DialogActions>

      </Dialog>

    </Box>

  );

}


