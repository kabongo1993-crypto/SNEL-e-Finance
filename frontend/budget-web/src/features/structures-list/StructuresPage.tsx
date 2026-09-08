import AddIcon from '@mui/icons-material/Add';
import AccountTreeOutlinedIcon from '@mui/icons-material/AccountTreeOutlined';
import ViewListOutlinedIcon from '@mui/icons-material/ViewListOutlined';
import {
  Autocomplete,
  Box,
  Button,
  ButtonGroup,
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
  createStructure,
  deleteStructure,
  fetchStructures,
  updateStructure,
  type Structure,
} from '../../services/apiClient';
import { BRAND_NAME } from '../../theme';
import { useAuth, canWriteReferentiels } from '../auth';
import { getTypeVisual } from '../structures/orgUtils';
import { StructuresDetailPanel } from './StructuresDetailPanel';
import { StructuresTreeView } from './StructuresTreeView';
import {
  STRUCTURE_TYPES,
  apiErrorMessage,
  buildStructureTree,
  collectAncestorIds,
  extraireCodeAffichage,
  libelleParent,
  libelleStatut,
  type StructureTreeNode,
} from './structureUtils';

type StructureRow = Structure & { id: string };
type ViewMode = 'liste' | 'arbre';
type FormMode = 'create' | 'edit';

function filterTree(
  nodes: StructureTreeNode[],
  predicate: (s: Structure) => boolean,
): StructureTreeNode[] {
  const walk = (list: StructureTreeNode[]): StructureTreeNode[] => {
    const result: StructureTreeNode[] = [];
    for (const node of list) {
      const children = walk(node.children);
      if (predicate(node.structure) || children.length > 0) {
        result.push({ structure: node.structure, children });
      }
    }
    return result;
  };
  return walk(nodes);
}

export function StructuresPage() {
  const { user } = useAuth();
  const msgBox = useMsgBox();
  const canWrite = canWriteReferentiels(user);
  const [rows, setRows] = useState<StructureRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [typeFilter, setTypeFilter] = useState('all');
  const [statutFilter, setStatutFilter] = useState('all');
  const [viewMode, setViewMode] = useState<ViewMode>('liste');
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [expanded, setExpanded] = useState<Set<number>>(new Set());

  const [dialogOpen, setDialogOpen] = useState(false);
  const [formMode, setFormMode] = useState<FormMode>('create');
  const [editingId, setEditingId] = useState<number | null>(null);
  const [typeStructure, setTypeStructure] = useState('DEPARTEMENT');
  const [code, setCode] = useState('');
  const [libelle, setLibelle] = useState('');
  const [parentId, setParentId] = useState('');
  const [actif, setActif] = useState(true);
  const [fieldErrors, setFieldErrors] = useState<{
    typeStructure?: string;
    code?: string;
    libelle?: string;
  }>({});
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await fetchStructures();
      setRows(data.map((s) => ({ ...s, id: String(s.idStructure) })));
    } catch {
      setError('Impossible de charger les structures.');
      setRows([]);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    if (rows.length === 0 || expanded.size > 0) return;
    const roots = rows.filter((r) => r.parentId == null).map((r) => r.idStructure);
    setExpanded(new Set(roots));
  }, [rows, expanded.size]);

  const matchesFilters = useCallback(
    (s: Structure) => {
      const q = search.trim().toLowerCase();
      const typeLabel = getTypeVisual(s.typeStructure).label.toLowerCase();
      const matchesSearch =
        !q ||
        extraireCodeAffichage(s.code).toLowerCase().includes(q) ||
        s.code.toLowerCase().includes(q) ||
        s.libelle.toLowerCase().includes(q) ||
        s.typeStructure.toLowerCase().includes(q) ||
        typeLabel.includes(q);
      const matchesType = typeFilter === 'all' || s.typeStructure.toUpperCase() === typeFilter;
      const matchesStatut =
        statutFilter === 'all' ||
        (statutFilter === 'actif' && s.actif) ||
        (statutFilter === 'inactif' && !s.actif);
      return matchesSearch && matchesType && matchesStatut;
    },
    [search, typeFilter, statutFilter],
  );

  const filtered = useMemo(() => rows.filter(matchesFilters), [rows, matchesFilters]);

  const treeNodes = useMemo(() => {
    const tree = buildStructureTree(rows);
    if (search.trim() === '' && typeFilter === 'all' && statutFilter === 'all') return tree;
    return filterTree(tree, matchesFilters);
  }, [rows, search, typeFilter, statutFilter, matchesFilters]);

  const hasActiveFilter = search.trim() !== '' || typeFilter !== 'all' || statutFilter !== 'all';

  const selected = useMemo(
    () => rows.find((r) => r.idStructure === selectedId) ?? null,
    [rows, selectedId],
  );

  const parentOptions = useMemo(
    () =>
      rows
        .filter((s) => s.idStructure !== editingId)
        .slice()
        .sort((a, b) =>
          extraireCodeAffichage(a.code).localeCompare(extraireCodeAffichage(b.code), 'fr'),
        ),
    [rows, editingId],
  );
  const selectedParent = parentOptions.find((s) => String(s.idStructure) === parentId) ?? null;

  const columns: DataTableColumn<StructureRow>[] = [
    {
      id: 'code',
      label: 'Code',
      sortable: true,
      sortValue: (r) => extraireCodeAffichage(r.code),
      mobile: 'title',
      render: (r) => (
        <Typography variant="body2" sx={{ fontFamily: 'ui-monospace, monospace', fontWeight: 700 }}>
          {extraireCodeAffichage(r.code)}
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
      id: 'type',
      label: 'Type',
      sortable: true,
      sortValue: (r) => r.typeStructure,
      mobile: 'meta',
      render: (r) => {
        const visual = getTypeVisual(r.typeStructure);
        return (
          <Chip
            size="small"
            label={visual.label}
            sx={{ bgcolor: visual.soft, color: visual.color, fontWeight: 700 }}
          />
        );
      },
    },
    {
      id: 'parent',
      label: 'Structure parente',
      sortable: true,
      sortValue: (r) => libelleParent(r),
      mobile: 'meta',
      render: (r) => libelleParent(r),
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
    setTypeStructure('DEPARTEMENT');
    setCode('');
    setLibelle('');
    setParentId(selectedId != null ? String(selectedId) : '');
    setActif(true);
    setFieldErrors({});
    setDialogOpen(true);
  };

  const openEdit = (row: StructureRow) => {
    setFormMode('edit');
    setEditingId(row.idStructure);
    setTypeStructure(row.typeStructure.toUpperCase());
    setCode(row.code);
    setLibelle(row.libelle);
    setParentId(row.parentId != null ? String(row.parentId) : '');
    setActif(row.actif);
    setFieldErrors({});
    setDialogOpen(true);
  };

  const validate = () => {
    const next: { typeStructure?: string; code?: string; libelle?: string } = {};
    if (!typeStructure.trim()) next.typeStructure = 'Le type est obligatoire.';
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
        typeStructure,
        code: code.trim().toUpperCase(),
        libelle: libelle.trim(),
        parentId: typeStructure === 'ENTITE' || !parentId ? null : Number(parentId),
        actif,
      };
      if (formMode === 'edit' && editingId != null) {
        const updated = await updateStructure(editingId, payload);
        void msgBox.success('Structure modifiée avec succès.');
        setSelectedId(updated.idStructure);
      } else {
        const created = await createStructure(payload);
        void msgBox.success('Structure créée avec succès.');
        setSelectedId(created.idStructure);
      }
      setDialogOpen(false);
      await load();
    } catch (err) {
      void msgBox.error(
        isAxiosError(err)
          ? apiErrorMessage(err, 'Impossible d’enregistrer la structure.')
          : 'Impossible d’enregistrer la structure.',
      );
    } finally {
      setSaving(false);
    }
  };

  const handleSelect = (id: number) => {
    setSelectedId(id);
    setExpanded((prev) => {
      const next = new Set(prev);
      collectAncestorIds(rows, id).forEach((ancestor) => next.add(ancestor));
      return next;
    });
  };

  const toggleExpand = (id: number) => {
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };

  const runDelete = async (row: StructureRow) => {
    const ok = await msgBox.confirm({
      title: `Supprimer « ${row.libelle} » ?`,
      message: 'Cette action est définitive.',
      danger: true,
      confirmLabel: 'Supprimer',
    });
    if (!ok) return;
    try {
      await deleteStructure(row.idStructure);
      void msgBox.success(`Structure « ${row.libelle} » supprimée.`);
      setSelectedId((current) => (current === row.idStructure ? null : current));
      await load();
    } catch (err) {
      void msgBox.error(
        isAxiosError(err)
          ? apiErrorMessage(err, 'Impossible de terminer l’opération.')
          : 'Impossible de terminer l’opération.',
      );
    }
  };

  const runDesactiver = async (row: StructureRow) => {
    const ok = await msgBox.confirm({
      title: `Désactiver « ${row.libelle} » ?`,
      message: 'La structure restera dans le référentiel, mais sera marquée INACTIF.',
      confirmLabel: 'Désactiver',
    });
    if (!ok) return;
    try {
      const updated = await updateStructure(row.idStructure, {
        typeStructure: row.typeStructure,
        code: row.code,
        libelle: row.libelle,
        parentId: row.parentId,
        actif: false,
      });
      void msgBox.success(`Structure « ${updated.libelle} » désactivée.`);
      setSelectedId(updated.idStructure);
      await load();
    } catch (err) {
      void msgBox.error(
        isAxiosError(err)
          ? apiErrorMessage(err, 'Impossible de terminer l’opération.')
          : 'Impossible de terminer l’opération.',
      );
    }
  };

  const runActiver = async (row: StructureRow) => {
    const ok = await msgBox.confirm({
      title: `Activer « ${row.libelle} » ?`,
      message: 'La structure sera de nouveau marquée ACTIF.',
      confirmLabel: 'Activer',
    });
    if (!ok) return;
    try {
      const updated = await updateStructure(row.idStructure, {
        typeStructure: row.typeStructure,
        code: row.code,
        libelle: row.libelle,
        parentId: row.parentId,
        actif: true,
      });
      void msgBox.success(`Structure « ${updated.libelle} » activée.`);
      setSelectedId(updated.idStructure);
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
    ? 'Aucune structure ne correspond à votre recherche.'
    : 'Aucune structure trouvée.';
  const emptyDescription = hasActiveFilter
    ? 'Modifiez les critères de recherche ou les filtres.'
    : 'Créez une structure pour commencer.';

  return (
    <Box>
      <PageHeader
        title="Structures"
        subtitle="Référentiel des structures organisationnelles — données réelles BD_SNEL."
        breadcrumbs={[
          { label: BRAND_NAME, to: '/dashboard' },
          { label: 'Référentiels', to: '/referentiels/organisationnel' },
          { label: 'Structures' },
        ]}
        actions={
          <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
            <ButtonGroup size="small" variant="outlined">
              <Button
                variant={viewMode === 'liste' ? 'contained' : 'outlined'}
                startIcon={<ViewListOutlinedIcon />}
                onClick={() => setViewMode('liste')}
              >
                Vue liste
              </Button>
              <Button
                variant={viewMode === 'arbre' ? 'contained' : 'outlined'}
                startIcon={<AccountTreeOutlinedIcon />}
                onClick={() => setViewMode('arbre')}
              >
                Vue arbre
              </Button>
            </ButtonGroup>
            {canWrite && (
              <PrimaryButton startIcon={<AddIcon />} onClick={openCreate}>
                Nouvelle structure
              </PrimaryButton>
            )}
          </Stack>
        }
      />

      <FilterBar
        search={search}
        onSearchChange={setSearch}
        searchPlaceholder="Rechercher par code, libellé ou type…"
        extra={
          <>
            <SearchableSelect
              label="Type"
              value={typeFilter}
              onChange={setTypeFilter}
              allowEmpty
              options={[
                { value: 'all', label: 'Tous les types' },
                ...STRUCTURE_TYPES.map((type) => ({
                  value: type,
                  label: getTypeVisual(type).label,
                })),
              ]}
              width={160}
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
        <LoadingState label="Chargement des structures…" />
      ) : error ? (
        <ErrorState message={error} onRetry={() => void load()} />
      ) : (
        <MasterDetailLayout
          detailOpen={selectedId != null}
          detailTitle="Détails de la structure"
          onCloseDetail={() => setSelectedId(null)}
          detail={<StructuresDetailPanel selected={selected} />}
          master={
            viewMode === 'liste' ? (
              <DataTable
                columns={columns}
                rows={filtered}
                emptyTitle={emptyTitle}
                emptyDescription={emptyDescription}
                selectedRowId={selectedId != null ? String(selectedId) : null}
                onRowClick={(row) => handleSelect(row.idStructure)}
                defaultRowsPerPage={25}
                countLabel={(count) => (
                  <>
                    <strong>{count}</strong> structure{count > 1 ? 's' : ''}
                  </>
                )}
                actions={[
                  {
                    id: 'voir',
                    label: 'Voir',
                    onClick: (row) => handleSelect(row.idStructure),
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
            ) : (
              <Box>
                <PaperCount count={filtered.length} />
                <StructuresTreeView
                  nodes={treeNodes}
                  expanded={expanded}
                  selectedId={selectedId}
                  onToggle={toggleExpand}
                  onSelect={handleSelect}
                  emptyTitle={emptyTitle}
                  emptyDescription={emptyDescription}
                />
              </Box>
            )
          }
        />
      )}

      <Dialog open={dialogOpen} onClose={() => !saving && setDialogOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>{formMode === 'edit' ? 'Modifier la structure' : 'Nouvelle structure'}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <SearchableSelect
              required
              label="Type de structure"
              value={typeStructure}
              onChange={(next) => {
                setTypeStructure(next);
                if (next === 'ENTITE') setParentId('');
              }}
              error={Boolean(fieldErrors.typeStructure)}
              helperText={fieldErrors.typeStructure}
              disabled={saving}
              options={STRUCTURE_TYPES.map((type) => ({
                value: type,
                label: getTypeVisual(type).label,
              }))}
              fullWidth
            />
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
            <Autocomplete
              disabled={saving || typeStructure === 'ENTITE'}
              options={parentOptions}
              value={selectedParent}
              onChange={(_, value) => setParentId(value ? String(value.idStructure) : '')}
              getOptionLabel={(s) => `${extraireCodeAffichage(s.code)} — ${s.libelle}`}
              isOptionEqualToValue={(a, b) => a.idStructure === b.idStructure}
              renderInput={(params) => (
                <TextField
                  {...params}
                  label="Structure parente"
                  helperText={
                    typeStructure === 'ENTITE'
                      ? 'Une entité n’a pas de structure parente.'
                      : 'Sélectionnez une structure existante. Laissez vide s’il n’y a pas de parent.'
                  }
                />
              )}
              noOptionsText="Aucune structure"
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

function PaperCount({ count }: { count: number }) {
  return (
    <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
      <strong>{count}</strong> résultat{count > 1 ? 's' : ''}
    </Typography>
  );
}
