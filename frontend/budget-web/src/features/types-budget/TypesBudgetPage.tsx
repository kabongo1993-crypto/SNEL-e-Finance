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

  createTypeBudget,

  deleteTypeBudget,

  fetchTypesBudget,

  updateTypeBudget,

  type TypeBudget,

} from '../../services/apiClient';

import { BRAND_NAME } from '../../theme';

import { useAuth, canWriteReferentiels } from '../auth';

import { TypesBudgetDetailPanel } from './TypesBudgetDetailPanel';

import { apiErrorMessage, libelleStatut } from './typeBudgetUtils';



type TypeBudgetRow = TypeBudget & { id: string };

type FormMode = 'create' | 'edit';



export function TypesBudgetPage() {

  const { user } = useAuth();

  const msgBox = useMsgBox();

  const canWrite = canWriteReferentiels(user);

  const [rows, setRows] = useState<TypeBudgetRow[]>([]);

  const [loading, setLoading] = useState(true);

  const [error, setError] = useState<string | null>(null);

  const [search, setSearch] = useState('');

  const [statutFilter, setStatutFilter] = useState('all');

  const [selectedId, setSelectedId] = useState<number | null>(null);



  const [dialogOpen, setDialogOpen] = useState(false);

  const [formMode, setFormMode] = useState<FormMode>('create');

  const [editingId, setEditingId] = useState<number | null>(null);

  const [codeType, setCodeType] = useState('');

  const [libelle, setLibelle] = useState('');

  const [ordreAffichage, setOrdreAffichage] = useState('');

  const [actif, setActif] = useState(true);

  const [fieldErrors, setFieldErrors] = useState<{

    codeType?: string;

    libelle?: string;

    ordreAffichage?: string;

  }>({});

  const [saving, setSaving] = useState(false);



  const load = useCallback(async () => {

    setLoading(true);

    setError(null);

    try {

      const data = await fetchTypesBudget();

      setRows(data.map((t) => ({ ...t, id: String(t.idTypeBudget) })));

    } catch {

      setError('Impossible de charger les types de budget.');

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

        r.codeType.toLowerCase().includes(q) ||

        r.libelle.toLowerCase().includes(q) ||

        String(r.ordreAffichage).includes(q);

      const matchesStatut =

        statutFilter === 'all' ||

        (statutFilter === 'actif' && r.actif) ||

        (statutFilter === 'inactif' && !r.actif);

      return matchesSearch && matchesStatut;

    });

  }, [rows, search, statutFilter]);



  const selected = useMemo(

    () => rows.find((r) => r.idTypeBudget === selectedId) ?? null,

    [rows, selectedId],

  );



  const hasActiveFilter = search.trim() !== '' || statutFilter !== 'all';



  const columns: DataTableColumn<TypeBudgetRow>[] = [

    {

      id: 'codeType',

      label: 'Code',

      sortable: true,

      sortValue: (r) => r.codeType,

      mobile: 'title',

      render: (r) => (

        <Typography variant="body2" sx={{ fontFamily: 'ui-monospace, monospace', fontWeight: 700 }}>

          {r.codeType}

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

      id: 'ordre',

      label: 'Ordre',

      sortable: true,

      sortValue: (r) => r.ordreAffichage,

      mobile: 'meta',

      render: (r) => r.ordreAffichage,

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

  ];



  const openCreate = () => {

    setFormMode('create');

    setEditingId(null);

    setCodeType('');

    setLibelle('');

    setOrdreAffichage('');

    setActif(true);

    setFieldErrors({});

    setDialogOpen(true);

  };



  const openEdit = (row: TypeBudgetRow) => {

    setFormMode('edit');

    setEditingId(row.idTypeBudget);

    setCodeType(row.codeType);

    setLibelle(row.libelle);

    setOrdreAffichage(String(row.ordreAffichage));

    setActif(row.actif);

    setFieldErrors({});

    setDialogOpen(true);

  };



  const validate = () => {

    const next: { codeType?: string; libelle?: string; ordreAffichage?: string } = {};

    if (!codeType.trim()) next.codeType = 'Le code est obligatoire.';

    if (!libelle.trim()) next.libelle = 'Le libellé est obligatoire.';

    if (!ordreAffichage.trim()) next.ordreAffichage = 'L’ordre d’affichage est obligatoire.';

    else if (!/^-?\d+$/.test(ordreAffichage.trim())) {

      next.ordreAffichage = 'L’ordre d’affichage doit être un nombre entier.';

    }

    setFieldErrors(next);

    return Object.keys(next).length === 0;

  };



  const handleSave = async () => {

    if (!validate()) return;



    setSaving(true);

    try {

      const payload = {

        codeType: codeType.trim().toUpperCase(),

        libelle: libelle.trim(),

        ordreAffichage: Number(ordreAffichage),

        actif,

      };

      if (formMode === 'edit' && editingId != null) {

        const updated = await updateTypeBudget(editingId, payload);

        void msgBox.success('Type de budget modifié avec succès.');

        setSelectedId(updated.idTypeBudget);

      } else {

        const created = await createTypeBudget(payload);

        void msgBox.success('Type de budget créé avec succès.');

        setSelectedId(created.idTypeBudget);

      }

      setDialogOpen(false);

      await load();

    } catch (err) {

      void msgBox.error(

        isAxiosError(err)

          ? apiErrorMessage(err, 'Impossible d’enregistrer le type de budget.')

          : 'Impossible d’enregistrer le type de budget.',

      );

    } finally {

      setSaving(false);

    }

  };



  const runDelete = async (row: TypeBudgetRow) => {

    const ok = await msgBox.confirm({

      title: `Supprimer « ${row.libelle} » ?`,

      message: 'Cette action est définitive.',

      danger: true,

      confirmLabel: 'Supprimer',

    });

    if (!ok) return;

    try {

      await deleteTypeBudget(row.idTypeBudget);

      void msgBox.success(`Type de budget « ${row.libelle} » supprimé.`);

      setSelectedId((current) => (current === row.idTypeBudget ? null : current));

      await load();

    } catch (err) {

      void msgBox.error(

        isAxiosError(err)

          ? apiErrorMessage(err, 'Impossible de terminer l’opération.')

          : 'Impossible de terminer l’opération.',

      );

    }

  };



  const runDesactiver = async (row: TypeBudgetRow) => {

    const ok = await msgBox.confirm({

      title: `Désactiver « ${row.libelle} » ?`,

      message: 'Le type de budget restera dans le référentiel, mais sera marqué INACTIF.',

      confirmLabel: 'Désactiver',

    });

    if (!ok) return;

    try {

      const updated = await updateTypeBudget(row.idTypeBudget, {

        codeType: row.codeType,

        libelle: row.libelle,

        ordreAffichage: row.ordreAffichage,

        actif: false,

      });

      void msgBox.success(`Type de budget « ${updated.libelle} » désactivé.`);

      setSelectedId(updated.idTypeBudget);

      await load();

    } catch (err) {

      void msgBox.error(

        isAxiosError(err)

          ? apiErrorMessage(err, 'Impossible de terminer l’opération.')

          : 'Impossible de terminer l’opération.',

      );

    }

  };



  const runActiver = async (row: TypeBudgetRow) => {

    const ok = await msgBox.confirm({

      title: `Activer « ${row.libelle} » ?`,

      message: 'Le type de budget sera de nouveau marqué ACTIF.',

      confirmLabel: 'Activer',

    });

    if (!ok) return;

    try {

      const updated = await updateTypeBudget(row.idTypeBudget, {

        codeType: row.codeType,

        libelle: row.libelle,

        ordreAffichage: row.ordreAffichage,

        actif: true,

      });

      void msgBox.success(`Type de budget « ${updated.libelle} » activé.`);

      setSelectedId(updated.idTypeBudget);

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

    ? 'Aucun type de budget ne correspond à votre recherche.'

    : 'Aucun type de budget trouvé.';

  const emptyDescription = hasActiveFilter

    ? 'Modifiez les critères de recherche ou le filtre de statut.'

    : 'Créez un type de budget pour commencer.';



  return (

    <Box>

      <PageHeader

        title="Types de budget"

        subtitle="Référentiel des types de budget — données réelles BD_SNEL."

        breadcrumbs={[

          { label: BRAND_NAME, to: '/dashboard' },

          { label: 'Budget', to: '/budget' },

          { label: 'Types de budget' },

        ]}

        actions={

          canWrite ? (

            <PrimaryButton startIcon={<AddIcon />} onClick={openCreate}>

              Nouveau type de budget

            </PrimaryButton>

          ) : undefined

        }

      />



      <FilterBar

        search={search}

        onSearchChange={setSearch}

        searchPlaceholder="Rechercher par code, libellé ou ordre…"

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

        <LoadingState label="Chargement des types de budget…" />

      ) : error ? (

        <ErrorState message={error} onRetry={() => void load()} />

      ) : (

        <MasterDetailLayout

          detailOpen={selectedId != null}

          detailTitle="Détails du type de budget"

          onCloseDetail={() => setSelectedId(null)}

          detail={<TypesBudgetDetailPanel selected={selected} />}

          master={

            <DataTable

              columns={columns}

              rows={filtered}

              emptyTitle={emptyTitle}

              emptyDescription={emptyDescription}

              selectedRowId={selectedId != null ? String(selectedId) : null}

              onRowClick={(row) => setSelectedId(row.idTypeBudget)}

              countLabel={(count) => (

                <>

                  <strong>{count}</strong> type{count > 1 ? 's' : ''} de budget

                </>

              )}

              actions={[

                {

                  id: 'voir',

                  label: 'Voir',

                  onClick: (row) => setSelectedId(row.idTypeBudget),

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

        <DialogTitle>

          {formMode === 'edit' ? 'Modifier le type de budget' : 'Nouveau type de budget'}

        </DialogTitle>

        <DialogContent>

          <Stack spacing={2} sx={{ mt: 1 }}>

            <TextField

              required

              label="Code"

              value={codeType}

              onChange={(e) => setCodeType(e.target.value.toUpperCase())}

              error={Boolean(fieldErrors.codeType)}

              helperText={fieldErrors.codeType}

              disabled={saving}

              autoFocus

              slotProps={{ htmlInput: { maxLength: 10 } }}

            />

            <TextField

              required

              label="Libellé"

              value={libelle}

              onChange={(e) => setLibelle(e.target.value)}

              error={Boolean(fieldErrors.libelle)}

              helperText={fieldErrors.libelle}

              disabled={saving}

              slotProps={{ htmlInput: { maxLength: 100 } }}

            />

            <TextField

              required

              type="number"

              label="Ordre d’affichage"

              value={ordreAffichage}

              onChange={(e) => setOrdreAffichage(e.target.value)}

              error={Boolean(fieldErrors.ordreAffichage)}

              helperText={fieldErrors.ordreAffichage}

              disabled={saving}

              slotProps={{ htmlInput: { step: 1 } }}

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


