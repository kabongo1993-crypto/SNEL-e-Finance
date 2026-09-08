import AddIcon from '@mui/icons-material/Add';

import LockOpenOutlinedIcon from '@mui/icons-material/LockOpenOutlined';

import LockOutlinedIcon from '@mui/icons-material/LockOutlined';

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

  createExercice,

  deleteExercice,

  fetchExercices,

  updateExercice,

  type Exercice,

} from '../../services/apiClient';

import { BRAND_NAME } from '../../theme';

import { useAuth, canWriteReferentiels } from '../auth';

import { ExercicesDetailPanel } from './ExercicesDetailPanel';

import {

  STATUTS_EXERCICE,

  apiErrorMessage,

  extraireDateIso,

  formatDateExercice,

} from './exerciceUtils';



type ExerciceRow = Exercice & { id: string };

type FormMode = 'create' | 'edit';



function anneeCourante(): number {

  return new Date().getFullYear();

}



function estOuvert(statut: string): boolean {

  return statut.toUpperCase() === 'OUVERT';

}



export function ExercicesPage() {

  const { user } = useAuth();

  const msgBox = useMsgBox();

  const canWrite = canWriteReferentiels(user);

  const [rows, setRows] = useState<ExerciceRow[]>([]);

  const [loading, setLoading] = useState(true);

  const [error, setError] = useState<string | null>(null);

  const [search, setSearch] = useState('');

  const [statutFilter, setStatutFilter] = useState('all');

  const [selectedId, setSelectedId] = useState<number | null>(null);



  const [dialogOpen, setDialogOpen] = useState(false);

  const [formMode, setFormMode] = useState<FormMode>('create');

  const [editingId, setEditingId] = useState<number | null>(null);

  const [annee, setAnnee] = useState(String(anneeCourante()));

  const [statut, setStatut] = useState('OUVERT');

  const [dateOuverture, setDateOuverture] = useState('');

  const [dateCloture, setDateCloture] = useState('');

  const [fieldErrors, setFieldErrors] = useState<{

    annee?: string;

    statut?: string;

    dates?: string;

  }>({});

  const [saving, setSaving] = useState(false);



  const load = useCallback(async () => {

    setLoading(true);

    setError(null);

    try {

      const data = await fetchExercices();

      setRows(data.map((e) => ({ ...e, id: String(e.idExercice) })));

    } catch {

      setError('Impossible de charger les exercices.');

      setRows([]);

    } finally {

      setLoading(false);

    }

  }, []);



  useEffect(() => {

    void load();

  }, [load]);



  const filtered = useMemo(() => {

    const q = search.trim();

    return rows.filter((r) => {

      const matchesSearch = !q || String(r.annee).includes(q);

      const matchesStatut = statutFilter === 'all' || r.statut.toUpperCase() === statutFilter;

      return matchesSearch && matchesStatut;

    });

  }, [rows, search, statutFilter]);



  const selected = useMemo(

    () => rows.find((r) => r.idExercice === selectedId) ?? null,

    [rows, selectedId],

  );



  const hasActiveFilter = search.trim() !== '' || statutFilter !== 'all';



  const columns: DataTableColumn<ExerciceRow>[] = [

    {

      id: 'annee',

      label: 'Année',

      sortable: true,

      sortValue: (r) => r.annee,

      mobile: 'title',

      render: (r) => (

        <Typography sx={{ fontWeight: 800, fontSize: '1.05rem', letterSpacing: '-0.02em' }}>

          {r.annee}

        </Typography>

      ),

    },

    {

      id: 'statut',

      label: 'Statut',

      sortable: true,

      sortValue: (r) => r.statut,

      mobile: 'meta',

      render: (r) => {

        const ouvert = estOuvert(r.statut);

        return (

          <Chip

            size="small"

            label={r.statut}

            color={ouvert ? 'success' : 'default'}

            variant={ouvert ? 'outlined' : 'filled'}

            sx={{ fontWeight: 700, minWidth: 88 }}

          />

        );

      },

    },

    {

      id: 'ouverture',

      label: 'Date d’ouverture',

      sortable: true,

      sortValue: (r) => r.dateOuverture ?? '',

      mobile: 'meta',

      render: (r) => formatDateExercice(r.dateOuverture),

    },

    {

      id: 'cloture',

      label: 'Date de clôture',

      sortable: true,

      sortValue: (r) => r.dateCloture ?? '',

      mobile: 'meta',

      render: (r) => formatDateExercice(r.dateCloture),

    },

  ];



  const resetFormErrors = () => {

    setFieldErrors({});

  };



  const openCreate = () => {

    const year = anneeCourante();

    setFormMode('create');

    setEditingId(null);

    setAnnee(String(year));

    setStatut('OUVERT');

    setDateOuverture(`${year}-01-01`);

    setDateCloture('');

    resetFormErrors();

    setDialogOpen(true);

  };



  const openEdit = (row: ExerciceRow) => {

    setFormMode('edit');

    setEditingId(row.idExercice);

    setAnnee(String(row.annee));

    setStatut(row.statut.toUpperCase());

    setDateOuverture(extraireDateIso(row.dateOuverture) ?? '');

    setDateCloture(extraireDateIso(row.dateCloture) ?? '');

    resetFormErrors();

    setDialogOpen(true);

  };



  const validate = () => {

    const next: { annee?: string; statut?: string; dates?: string } = {};

    const year = Number(annee);

    if (!annee.trim() || !Number.isInteger(year)) {

      next.annee = 'L’année est obligatoire et doit être un entier.';

    } else if (year < 2000 || year > 2100) {

      next.annee = 'L’année doit être comprise entre 2000 et 2100.';

    }

    if (!statut || (statut !== 'OUVERT' && statut !== 'CLOTURE')) {

      next.statut = 'Le statut doit être OUVERT ou CLOTURE.';

    }

    const ouverture = extraireDateIso(dateOuverture);

    const cloture = extraireDateIso(dateCloture);

    if (ouverture && cloture && cloture < ouverture) {

      next.dates = 'La date de clôture ne peut pas être antérieure à la date d’ouverture.';

    }

    setFieldErrors(next);

    return Object.keys(next).length === 0;

  };



  const handleSave = async () => {

    if (!validate()) return;



    setSaving(true);

    try {

      const payload = {

        annee: Number(annee),

        statut,

        dateOuverture: extraireDateIso(dateOuverture),

        dateCloture: extraireDateIso(dateCloture),

      };

      const saved =

        formMode === 'edit' && editingId != null

          ? await updateExercice(editingId, payload)

          : await createExercice(payload);

      setDialogOpen(false);

      void msgBox.success(

        formMode === 'edit' ? 'Exercice modifié avec succès.' : 'Exercice créé avec succès.',

      );

      await load();

      setSelectedId(saved.idExercice);

    } catch (err) {

      void msgBox.error(

        isAxiosError(err)

          ? apiErrorMessage(err, 'Impossible d’enregistrer l’exercice.')

          : 'Impossible d’enregistrer l’exercice.',

      );

    } finally {

      setSaving(false);

    }

  };



  const runDelete = async (row: ExerciceRow) => {

    const ok = await msgBox.confirm({

      title: `Supprimer l’exercice ${row.annee} ?`,

      message: 'Cette action est définitive.',

      danger: true,

      confirmLabel: 'Supprimer',

    });

    if (!ok) return;

    try {

      await deleteExercice(row.idExercice);

      void msgBox.success(`Exercice ${row.annee} supprimé avec succès.`);

      setSelectedId((current) => (current === row.idExercice ? null : current));

      await load();

    } catch (err) {

      void msgBox.error(

        isAxiosError(err)

          ? apiErrorMessage(err, 'Impossible de terminer l’opération.')

          : 'Impossible de terminer l’opération.',

      );

    }

  };



  const runCloturer = async (row: ExerciceRow) => {

    const ok = await msgBox.confirm({

      title: `Clôturer l’exercice ${row.annee} ?`,

      message:

        'Cette opération passera l’exercice de OUVERT à CLOTURE et renseignera la date de clôture (date du jour si elle est vide).',

      confirmLabel: 'Clôturer',

    });

    if (!ok) return;

    try {

      const updated = await updateExercice(row.idExercice, {

        annee: row.annee,

        statut: 'CLOTURE',

        dateOuverture: extraireDateIso(row.dateOuverture),

        dateCloture: extraireDateIso(row.dateCloture),

      });

      void msgBox.success(`Exercice ${updated.annee} clôturé.`);

      setSelectedId(updated.idExercice);

      await load();

    } catch (err) {

      void msgBox.error(

        isAxiosError(err)

          ? apiErrorMessage(err, 'Impossible de terminer l’opération.')

          : 'Impossible de terminer l’opération.',

      );

    }

  };



  const runRouvrir = async (row: ExerciceRow) => {

    const ok = await msgBox.confirm({

      title: `Rouvrir l’exercice ${row.annee} ?`,

      message:

        'Cette opération passera l’exercice de CLOTURE à OUVERT. La date de clôture existante est conservée.',

      confirmLabel: 'Rouvrir',

    });

    if (!ok) return;

    try {

      const updated = await updateExercice(row.idExercice, {

        annee: row.annee,

        statut: 'OUVERT',

        dateOuverture: extraireDateIso(row.dateOuverture),

        dateCloture: extraireDateIso(row.dateCloture),

      });

      void msgBox.success(`Exercice ${updated.annee} rouvert.`);

      setSelectedId(updated.idExercice);

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

    ? 'Aucun exercice ne correspond à votre recherche.'

    : 'Aucun exercice trouvé.';

  const emptyDescription = hasActiveFilter

    ? 'Modifiez les critères de recherche ou le filtre de statut.'

    : 'Créez un exercice pour commencer.';



  return (

    <Box>

      <PageHeader

        title="Exercices budgétaires"

        subtitle="Gestion des exercices budgétaires de SNEL."

        breadcrumbs={[

          { label: BRAND_NAME, to: '/dashboard' },

          { label: 'Référentiels', to: '/referentiels/organisationnel' },

          { label: 'Exercices' },

        ]}

        actions={

          canWrite ? (

            <PrimaryButton startIcon={<AddIcon />} onClick={openCreate}>

              Nouvel exercice

            </PrimaryButton>

          ) : undefined

        }

      />



      <FilterBar

        search={search}

        onSearchChange={setSearch}

        searchPlaceholder="Rechercher une année…"

        extra={

          <TextField select size="small"

            label="Statut"

            value={statutFilter}

            onChange={(e) => setStatutFilter(e.target.value)}

            sx={{ minWidth: 160 }}

          >

            <MenuItem value="all">Tous</MenuItem>

            {STATUTS_EXERCICE.map((s) => (

              <MenuItem key={s} value={s}>

                {s}

              </MenuItem>

            ))}

          </TextField>

        }

      />



      {loading ? (

        <LoadingState label="Chargement des exercices…" />

      ) : error ? (

        <ErrorState message={error} onRetry={() => void load()} />

      ) : (

        <MasterDetailLayout

          detailOpen={selectedId != null}

          detailTitle="Détails de l’exercice"

          onCloseDetail={() => setSelectedId(null)}

          detail={<ExercicesDetailPanel selected={selected} />}

          master={

            <DataTable

              columns={columns}

              rows={filtered}

              emptyTitle={emptyTitle}

              emptyDescription={emptyDescription}

              selectedRowId={selectedId != null ? String(selectedId) : null}

              onRowClick={(row) => setSelectedId(row.idExercice)}

              countLabel={(count) => (

                <>

                  <strong>{count}</strong> exercice{count > 1 ? 's' : ''}

                </>

              )}

              actions={[

                {

                  id: 'voir',

                  label: 'Voir',

                  onClick: (row) => setSelectedId(row.idExercice),

                },

                {

                  id: 'modifier',

                  label: 'Modifier',

                  hidden: () => !canWrite,

                  onClick: (row) => openEdit(row),

                },

                {

                  id: 'cloturer',

                  label: 'Clôturer',

                  icon: <LockOutlinedIcon fontSize="small" />,

                  hidden: (row) => !canWrite || !estOuvert(row.statut),

                  onClick: (row) => void runCloturer(row),

                },

                {

                  id: 'rouvrir',

                  label: 'Rouvrir',

                  icon: <LockOpenOutlinedIcon fontSize="small" />,

                  hidden: (row) => !canWrite || estOuvert(row.statut),

                  onClick: (row) => void runRouvrir(row),

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

        <DialogTitle>

          {formMode === 'edit' ? 'Modifier l’exercice' : 'Nouvel exercice budgétaire'}

        </DialogTitle>

        <DialogContent>

          <Stack spacing={2} sx={{ mt: 1 }}>

            <TextField

              required

              type="number"

              label="Année"

              value={annee}

              onChange={(e) => {

                const next = e.target.value;

                setAnnee(next);

                const year = Number(next);

                if (formMode === 'create' && Number.isInteger(year) && year >= 2000 && year <= 2100) {

                  setDateOuverture((prev) =>

                    !prev || prev.endsWith('-01-01') ? `${year}-01-01` : prev,

                  );

                }

              }}

              error={Boolean(fieldErrors.annee)}

              helperText={fieldErrors.annee}

              disabled={saving}

              autoFocus

              slotProps={{ htmlInput: { min: 2000, max: 2100, step: 1 } }}

            />

            <TextField select required

              label="Statut"

              value={statut}

              onChange={(e) => setStatut(e.target.value)}

              error={Boolean(fieldErrors.statut)}

              helperText={fieldErrors.statut}

              disabled={saving}

            >

              {STATUTS_EXERCICE.map((s) => (

                <MenuItem key={s} value={s}>

                  {s}

                </MenuItem>

              ))}

            </TextField>

            <TextField

              type="date"

              label="Date d’ouverture"

              value={dateOuverture}

              onChange={(e) => setDateOuverture(e.target.value)}

              disabled={saving}

              slotProps={{ inputLabel: { shrink: true } }}

            />

            <TextField

              type="date"

              label="Date de clôture"

              value={dateCloture}

              onChange={(e) => setDateCloture(e.target.value)}

              error={Boolean(fieldErrors.dates)}

              helperText={fieldErrors.dates}

              disabled={saving}

              slotProps={{ inputLabel: { shrink: true } }}

            />

          </Stack>

        </DialogContent>

        <DialogActions sx={{ px: 3, pb: 2 }}>

          <SecondaryButton onClick={() => setDialogOpen(false)} disabled={saving}>

            Annuler

          </SecondaryButton>

          <Button variant="contained" onClick={() => void handleSave()} disabled={saving}>

            {saving

              ? 'Enregistrement…'

              : formMode === 'edit'

                ? 'Enregistrer les modifications'

                : 'Créer'}

          </Button>

        </DialogActions>

      </Dialog>

    </Box>

  );

}


