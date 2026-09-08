import AddIcon from '@mui/icons-material/Add';
import {
  Autocomplete,
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
  SearchableSelect,
  SecondaryButton,
  useMsgBox,
  type DataTableColumn,
} from '../../components';
import { formatDateFr } from '../../mocks/types';
import {
  createUniteBudgetaire,
  deleteUniteBudgetaire,
  fetchDepartements,
  fetchStructures,
  fetchUnitesBudgetaires,
  updateUniteBudgetaire,
  type Departement,
  type Structure,
  type UniteBudgetaire,
} from '../../services/apiClient';
import { BRAND_NAME } from '../../theme';
import { useAuth, canWriteReferentiels } from '../auth';
import { UnitesBudgetairesDetailPanel } from './UnitesBudgetairesDetailPanel';
import {
  apiErrorMessage,
  extraireCodeAffichage,
  libelleDepartement,
  libelleStatut,
  libelleStructure,
  structureAppartientAuDepartement,
} from './ubUtils';

type UniteRow = UniteBudgetaire & { id: string };
type FormMode = 'create' | 'edit';

export function UnitesBudgetairesPage() {
  const { user } = useAuth();
  const msgBox = useMsgBox();
  const canWrite = canWriteReferentiels(user);
  const [rows, setRows] = useState<UniteRow[]>([]);
  const [departements, setDepartements] = useState<Departement[]>([]);
  const [structures, setStructures] = useState<Structure[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [deptFilter, setDeptFilter] = useState('all');
  const [structureFilter, setStructureFilter] = useState('all');
  const [statutFilter, setStatutFilter] = useState('all');
  const [selectedId, setSelectedId] = useState<number | null>(null);

  const [dialogOpen, setDialogOpen] = useState(false);
  const [formMode, setFormMode] = useState<FormMode>('create');
  const [editingId, setEditingId] = useState<number | null>(null);
  const [codeUB, setCodeUB] = useState('');
  const [libelle, setLibelle] = useState('');
  const [formDeptId, setFormDeptId] = useState('');
  const [formStructureId, setFormStructureId] = useState('');
  const [actif, setActif] = useState(true);
  const [fieldErrors, setFieldErrors] = useState<{
    codeUB?: string;
    libelle?: string;
    departement?: string;
    structure?: string;
  }>({});
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [unites, depts, structs] = await Promise.all([
        fetchUnitesBudgetaires(),
        fetchDepartements(),
        fetchStructures(),
      ]);
      setRows(unites.map((u) => ({ ...u, id: String(u.idUB) })));
      setDepartements(depts);
      setStructures(structs);
    } catch {
      setError('Impossible de charger les unités budgétaires.');
      setRows([]);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const structuresPourFiltre = useMemo(() => {
    if (deptFilter === 'all') return structures;
    const dept = departements.find((d) => String(d.idDepartement) === deptFilter);
    if (!dept) return structures;
    return structures.filter((s) => structureAppartientAuDepartement(s, structures, dept.code));
  }, [structures, departements, deptFilter]);

  const structuresPourFormulaire = useMemo(() => {
    if (!formDeptId) return [];
    const dept = departements.find((d) => String(d.idDepartement) === formDeptId);
    if (!dept) return [];
    return structures.filter((s) => structureAppartientAuDepartement(s, structures, dept.code));
  }, [structures, departements, formDeptId]);

  useEffect(() => {
    if (structureFilter === 'all') return;
    const stillVisible = structuresPourFiltre.some((s) => String(s.idStructure) === structureFilter);
    if (!stillVisible) setStructureFilter('all');
  }, [structuresPourFiltre, structureFilter]);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return rows.filter((r) => {
      const matchesSearch =
        !q ||
        r.codeUB.toLowerCase().includes(q) ||
        r.libelle.toLowerCase().includes(q) ||
        r.departementCode.toLowerCase().includes(q) ||
        r.departementLibelle.toLowerCase().includes(q) ||
        r.structureCode.toLowerCase().includes(q) ||
        extraireCodeAffichage(r.structureCode).toLowerCase().includes(q) ||
        r.structureLibelle.toLowerCase().includes(q);
      const matchesDept = deptFilter === 'all' || String(r.idDepartement) === deptFilter;
      const matchesStructure = structureFilter === 'all' || String(r.idStructure) === structureFilter;
      const matchesStatut =
        statutFilter === 'all' ||
        (statutFilter === 'actif' && r.actif) ||
        (statutFilter === 'inactif' && !r.actif);
      return matchesSearch && matchesDept && matchesStructure && matchesStatut;
    });
  }, [rows, search, deptFilter, structureFilter, statutFilter]);

  const selected = useMemo(
    () => rows.find((r) => r.idUB === selectedId) ?? null,
    [rows, selectedId],
  );

  const hasActiveFilter =
    search.trim() !== '' || deptFilter !== 'all' || structureFilter !== 'all' || statutFilter !== 'all';

  const columns: DataTableColumn<UniteRow>[] = [
    {
      id: 'codeUB',
      label: 'Code UB',
      sortable: true,
      sortValue: (r) => r.codeUB,
      mobile: 'title',
      render: (r) => (
        <Typography variant="body2" sx={{ fontFamily: 'ui-monospace, monospace', fontWeight: 700 }}>
          {r.codeUB}
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
      id: 'departement',
      label: 'Département',
      sortable: true,
      sortValue: (r) => r.departementCode,
      mobile: 'meta',
      render: (r) => libelleDepartement(r.departementCode, r.departementLibelle),
    },
    {
      id: 'structure',
      label: 'Structure',
      sortable: true,
      sortValue: (r) => extraireCodeAffichage(r.structureCode),
      mobile: 'meta',
      render: (r) => libelleStructure(r.structureCode, r.structureLibelle),
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
      mobile: 'hidden',
      render: (r) => formatDateFr(r.dateCreation),
    },
  ];

  const openCreate = () => {
    setFormMode('create');
    setEditingId(null);
    setCodeUB('');
    setLibelle('');
    setFormDeptId('');
    setFormStructureId('');
    setActif(true);
    setFieldErrors({});
    setDialogOpen(true);
  };

  const openEdit = (row: UniteRow) => {
    setFormMode('edit');
    setEditingId(row.idUB);
    setCodeUB(row.codeUB);
    setLibelle(row.libelle);
    setFormDeptId(String(row.idDepartement));
    setFormStructureId(String(row.idStructure));
    setActif(row.actif);
    setFieldErrors({});
    setDialogOpen(true);
  };

  const validate = () => {
    const next: { codeUB?: string; libelle?: string; departement?: string; structure?: string } = {};
    if (!codeUB.trim()) next.codeUB = 'Le code UB est obligatoire.';
    if (!libelle.trim()) next.libelle = 'Le libellé est obligatoire.';
    if (!formDeptId) next.departement = 'Le département est obligatoire.';
    if (!formStructureId) next.structure = 'La structure organisationnelle est obligatoire.';
    setFieldErrors(next);
    return Object.keys(next).length === 0;
  };

  const handleSave = async () => {
    if (!validate()) return;

    setSaving(true);
    try {
      const payload = {
        codeUB: codeUB.trim().toUpperCase(),
        libelle: libelle.trim(),
        idDepartement: Number(formDeptId),
        idStructure: Number(formStructureId),
        actif,
      };
      if (formMode === 'edit' && editingId != null) {
        const updated = await updateUniteBudgetaire(editingId, payload);
        void msgBox.success('Unité budgétaire modifiée avec succès.');
        setSelectedId(updated.idUB);
      } else {
        const created = await createUniteBudgetaire(payload);
        void msgBox.success('Unité budgétaire créée avec succès.');
        setSelectedId(created.idUB);
      }
      setDialogOpen(false);
      await load();
    } catch (err) {
      void msgBox.error(
        isAxiosError(err)
          ? apiErrorMessage(err, 'Impossible d’enregistrer l’unité budgétaire.')
          : 'Impossible d’enregistrer l’unité budgétaire.',
      );
    } finally {
      setSaving(false);
    }
  };

  const runDelete = async (row: UniteRow) => {
    const ok = await msgBox.confirm({
      title: `Supprimer « ${row.libelle} » ?`,
      message: 'Cette action est définitive.',
      danger: true,
      confirmLabel: 'Supprimer',
    });
    if (!ok) return;
    try {
      await deleteUniteBudgetaire(row.idUB);
      void msgBox.success(`Unité budgétaire « ${row.libelle} » supprimée.`);
      setSelectedId((current) => (current === row.idUB ? null : current));
      await load();
    } catch (err) {
      void msgBox.error(
        isAxiosError(err)
          ? apiErrorMessage(err, 'Impossible de terminer l’opération.')
          : 'Impossible de terminer l’opération.',
      );
    }
  };

  const runDesactiver = async (row: UniteRow) => {
    const ok = await msgBox.confirm({
      title: `Désactiver « ${row.libelle} » ?`,
      message: 'L’unité budgétaire restera dans le référentiel, mais sera marquée INACTIF.',
      confirmLabel: 'Désactiver',
    });
    if (!ok) return;
    try {
      const updated = await updateUniteBudgetaire(row.idUB, {
        codeUB: row.codeUB,
        libelle: row.libelle,
        idDepartement: row.idDepartement,
        idStructure: row.idStructure,
        actif: false,
      });
      void msgBox.success(`Unité budgétaire « ${updated.libelle} » désactivée.`);
      setSelectedId(updated.idUB);
      await load();
    } catch (err) {
      void msgBox.error(
        isAxiosError(err)
          ? apiErrorMessage(err, 'Impossible de terminer l’opération.')
          : 'Impossible de terminer l’opération.',
      );
    }
  };

  const runActiver = async (row: UniteRow) => {
    const ok = await msgBox.confirm({
      title: `Activer « ${row.libelle} » ?`,
      message: 'L’unité budgétaire sera de nouveau marquée ACTIF.',
      confirmLabel: 'Activer',
    });
    if (!ok) return;
    try {
      const updated = await updateUniteBudgetaire(row.idUB, {
        codeUB: row.codeUB,
        libelle: row.libelle,
        idDepartement: row.idDepartement,
        idStructure: row.idStructure,
        actif: true,
      });
      void msgBox.success(`Unité budgétaire « ${updated.libelle} » activée.`);
      setSelectedId(updated.idUB);
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
    ? 'Aucune unité budgétaire ne correspond à votre recherche.'
    : 'Aucune unité budgétaire trouvée.';
  const emptyDescription = hasActiveFilter
    ? 'Modifiez les critères de recherche ou les filtres.'
    : 'Créez une unité budgétaire pour commencer.';

  const structureOptions = structuresPourFiltre
    .slice()
    .sort((a, b) => extraireCodeAffichage(a.code).localeCompare(extraireCodeAffichage(b.code), 'fr'));
  const selectedStructureFilter =
    structureOptions.find((s) => String(s.idStructure) === structureFilter) ?? null;

  const formStructureOptions = structuresPourFormulaire
    .slice()
    .sort((a, b) => extraireCodeAffichage(a.code).localeCompare(extraireCodeAffichage(b.code), 'fr'));
  const selectedFormStructure =
    formStructureOptions.find((s) => String(s.idStructure) === formStructureId) ?? null;

  return (
    <Box>
      <PageHeader
        title="Unités budgétaires"
        subtitle="Référentiel des unités budgétaires — données réelles BD_SNEL."
        breadcrumbs={[
          { label: BRAND_NAME, to: '/dashboard' },
          { label: 'Référentiels', to: '/referentiels/organisationnel' },
          { label: 'Unités budgétaires' },
        ]}
        actions={
          canWrite ? (
            <PrimaryButton startIcon={<AddIcon />} onClick={openCreate}>
              Nouvelle unité budgétaire
            </PrimaryButton>
          ) : undefined
        }
      />

      <FilterBar
        search={search}
        onSearchChange={setSearch}
        searchPlaceholder="Rechercher par code UB, libellé, département ou structure…"
        extra={
          <>
            <SearchableSelect
              label="Département"
              value={deptFilter}
              onChange={setDeptFilter}
              allowEmpty
              options={[
                { value: 'all', label: 'Tous les départements' },
                ...departements.map((d) => ({
                  value: String(d.idDepartement),
                  label: `${d.code} — ${d.libelle}`,
                })),
              ]}
              width={180}
            />
            <Autocomplete
              size="small"
              sx={{ minWidth: 280 }}
              options={structureOptions}
              value={selectedStructureFilter}
              onChange={(_, value) => setStructureFilter(value ? String(value.idStructure) : 'all')}
              getOptionLabel={(s) => libelleStructure(s.code, s.libelle)}
              isOptionEqualToValue={(a, b) => a.idStructure === b.idStructure}
              renderInput={(params) => (
                <TextField {...params} label="Structure" placeholder="Toutes les structures" />
              )}
              noOptionsText="Aucune structure"
            />
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
          </>
        }
      />

      {loading ? (
        <LoadingState label="Chargement des unités budgétaires…" />
      ) : error ? (
        <ErrorState message={error} onRetry={() => void load()} />
      ) : (
        <MasterDetailLayout
          detailOpen={selectedId != null}
          detailTitle="Détails de l’unité budgétaire"
          onCloseDetail={() => setSelectedId(null)}
          detail={<UnitesBudgetairesDetailPanel selected={selected} />}
          master={
            <DataTable
              columns={columns}
              rows={filtered}
              emptyTitle={emptyTitle}
              emptyDescription={emptyDescription}
              selectedRowId={selectedId != null ? String(selectedId) : null}
              onRowClick={(row) => setSelectedId(row.idUB)}
              defaultRowsPerPage={25}
              countLabel={(count) => (
                <>
                  <strong>{count}</strong> unité{count > 1 ? 's' : ''} budgétaire{count > 1 ? 's' : ''}
                </>
              )}
              actions={[
                {
                  id: 'voir',
                  label: 'Voir',
                  onClick: (row) => setSelectedId(row.idUB),
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
          {formMode === 'edit' ? 'Modifier l’unité budgétaire' : 'Nouvelle unité budgétaire'}
        </DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <TextField
              required
              label="Code UB"
              value={codeUB}
              onChange={(e) => setCodeUB(e.target.value.toUpperCase())}
              error={Boolean(fieldErrors.codeUB)}
              helperText={fieldErrors.codeUB}
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
            <SearchableSelect
              required
              label="Département"
              value={formDeptId}
              onChange={(v) => {
                setFormDeptId(v);
                setFormStructureId('');
              }}
              error={Boolean(fieldErrors.departement)}
              helperText={fieldErrors.departement}
              disabled={saving}
              allowEmpty
              options={[
                { value: '', label: 'Sélectionner un département' },
                ...departements.map((d) => ({
                  value: String(d.idDepartement),
                  label: `${d.code} — ${d.libelle}`,
                })),
              ]}
              placeholder="Rechercher un département…"
              fullWidth
            />
            <Autocomplete
              disabled={saving || !formDeptId}
              options={formStructureOptions}
              value={selectedFormStructure}
              onChange={(_, value) => setFormStructureId(value ? String(value.idStructure) : '')}
              getOptionLabel={(s) => libelleStructure(s.code, s.libelle)}
              isOptionEqualToValue={(a, b) => a.idStructure === b.idStructure}
              renderInput={(params) => (
                <TextField
                  {...params}
                  required
                  label="Structure organisationnelle"
                  error={Boolean(fieldErrors.structure)}
                  helperText={
                    fieldErrors.structure ??
                    (formDeptId
                      ? 'Structures rattachées au département sélectionné.'
                      : 'Sélectionnez d’abord un département.')
                  }
                />
              )}
              noOptionsText="Aucune structure compatible"
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
