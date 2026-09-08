import AddIcon from '@mui/icons-material/Add';
import AccountTreeOutlinedIcon from '@mui/icons-material/AccountTreeOutlined';
import ViewListOutlinedIcon from '@mui/icons-material/ViewListOutlined';
import {
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
import {
  createItemBI,
  deleteItemBI,
  fetchItemsBI,
  setItemBIActif,
  updateItemBI,
  type ItemBI,
} from '../../services/apiClient';
import { BRAND_NAME } from '../../theme';
import { useAuth, canWriteReferentiels } from '../auth';
import { ItemsBIDetailPanel } from './ItemsBIDetailPanel';
import { ItemsBITreeView } from './ItemsBITreeView';
import {
  apiErrorMessage,
  buildItemBITree,
  collectAncestorIds,
  formatDateFr,
  libelleParent,
  libelleStatut,
  type ItemBITreeNode,
} from './itemBIUtils';

type ItemRow = ItemBI & { id: string };
type ViewMode = 'liste' | 'arbre';
type FormMode = 'create' | 'edit';

function filterTree(nodes: ItemBITreeNode[], predicate: (r: ItemBI) => boolean): ItemBITreeNode[] {
  const walk = (list: ItemBITreeNode[]): ItemBITreeNode[] => {
    const result: ItemBITreeNode[] = [];
    for (const node of list) {
      const children = walk(node.children);
      if (predicate(node.item) || children.length > 0) {
        result.push({ item: node.item, children });
      }
    }
    return result;
  };
  return walk(nodes);
}

export function ItemsBIPage() {
  const { user } = useAuth();
  const msgBox = useMsgBox();
  const canWrite = canWriteReferentiels(user);
  const [rows, setRows] = useState<ItemRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [statutFilter, setStatutFilter] = useState('all');
  const [viewMode, setViewMode] = useState<ViewMode>('liste');
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [expanded, setExpanded] = useState<Set<number>>(new Set());

  const [dialogOpen, setDialogOpen] = useState(false);
  const [formMode, setFormMode] = useState<FormMode>('create');
  const [editingId, setEditingId] = useState<number | null>(null);
  const [codeItem, setCodeItem] = useState('');
  const [libelle, setLibelle] = useState('');
  const [parentId, setParentId] = useState('');
  const [categorie, setCategorie] = useState('');
  const [actif, setActif] = useState(true);
  const [fieldErrors, setFieldErrors] = useState<{ codeItem?: string; libelle?: string }>({});
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await fetchItemsBI();
      setRows(data.map((r) => ({ ...r, id: String(r.idItemBI) })));
    } catch {
      setError('Impossible de charger les items BI.');
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
    setExpanded(new Set(rows.filter((r) => r.parentId == null).map((r) => r.idItemBI)));
  }, [rows, expanded.size]);

  const matchesFilters = useCallback(
    (r: ItemBI) => {
      const q = search.trim().toLowerCase();
      const matchesSearch =
        !q ||
        r.codeItem.toLowerCase().includes(q) ||
        r.libelle.toLowerCase().includes(q) ||
        (r.categorie ?? '').toLowerCase().includes(q);
      const matchesStatut =
        statutFilter === 'all' ||
        (statutFilter === 'actif' && r.actif) ||
        (statutFilter === 'inactif' && !r.actif);
      return matchesSearch && matchesStatut;
    },
    [search, statutFilter],
  );

  const filtered = useMemo(() => rows.filter(matchesFilters), [rows, matchesFilters]);
  const treeNodes = useMemo(() => {
    const tree = buildItemBITree(rows);
    if (search.trim() === '' && statutFilter === 'all') return tree;
    return filterTree(tree, matchesFilters);
  }, [rows, search, statutFilter, matchesFilters]);

  const selected = useMemo(() => rows.find((r) => r.idItemBI === selectedId) ?? null, [rows, selectedId]);
  const hasActiveFilter = search.trim() !== '' || statutFilter !== 'all';
  const parentOptions = useMemo(
    () =>
      rows.filter((r) => r.idItemBI !== editingId).slice().sort((a, b) => a.codeItem.localeCompare(b.codeItem, 'fr')),
    [rows, editingId],
  );

  const columns: DataTableColumn<ItemRow>[] = [
    {
      id: 'code',
      label: 'Code',
      sortable: true,
      sortValue: (r) => r.codeItem,
      mobile: 'title',
      render: (r) => (
        <Typography variant="body2" sx={{ fontFamily: 'ui-monospace, monospace', fontWeight: 700 }}>
          {r.codeItem}
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
      id: 'parent',
      label: 'Parent',
      sortable: true,
      sortValue: (r) => libelleParent(r),
      mobile: 'meta',
      render: (r) => libelleParent(r),
    },
    {
      id: 'categorie',
      label: 'Catégorie',
      sortable: true,
      sortValue: (r) => r.categorie ?? '',
      mobile: 'meta',
      render: (r) => r.categorie ?? '—',
    },
    {
      id: 'niveau',
      label: 'Niveau',
      sortable: true,
      sortValue: (r) => r.niveau,
      mobile: 'hidden',
      render: (r) => r.niveau,
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
      label: 'Création',
      sortable: true,
      sortValue: (r) => r.dateCreation,
      mobile: 'hidden',
      render: (r) => formatDateFr(r.dateCreation),
    },
  ];

  const openCreate = () => {
    setFormMode('create');
    setEditingId(null);
    setCodeItem('');
    setLibelle('');
    setParentId(selectedId != null ? String(selectedId) : '');
    setCategorie('');
    setActif(true);
    setFieldErrors({});
    setDialogOpen(true);
  };

  const openEdit = (row: ItemRow) => {
    setFormMode('edit');
    setEditingId(row.idItemBI);
    setCodeItem(row.codeItem);
    setLibelle(row.libelle);
    setParentId(row.parentId != null ? String(row.parentId) : '');
    setCategorie(row.categorie ?? '');
    setActif(row.actif);
    setFieldErrors({});
    setDialogOpen(true);
  };

  const validate = () => {
    const next: { codeItem?: string; libelle?: string } = {};
    if (!codeItem.trim()) next.codeItem = 'Le code est obligatoire.';
    if (!libelle.trim()) next.libelle = 'Le libellé est obligatoire.';
    setFieldErrors(next);
    return Object.keys(next).length === 0;
  };

  const handleSave = async () => {
    if (!validate()) return;
    setSaving(true);
    try {
      const payload = {
        codeItem: codeItem.trim().toUpperCase(),
        libelle: libelle.trim(),
        parentId: parentId ? Number(parentId) : null,
        categorie: categorie.trim() || null,
        actif,
      };
      if (formMode === 'edit' && editingId != null) {
        const updated = await updateItemBI(editingId, payload);
        void msgBox.success('Item BI modifié avec succès.');
        setSelectedId(updated.idItemBI);
      } else {
        const created = await createItemBI(payload);
        void msgBox.success('Item BI créé avec succès.');
        setSelectedId(created.idItemBI);
      }
      setDialogOpen(false);
      await load();
    } catch (err) {
      void msgBox.error(
        isAxiosError(err) ? apiErrorMessage(err, 'Impossible d’enregistrer l’item BI.') : 'Impossible d’enregistrer l’item BI.',
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

  const runDelete = async (row: ItemRow) => {
    const ok = await msgBox.confirm({
      title: `Supprimer « ${row.libelle} » ?`,
      message:
        'Cette action est définitive. Si l’item est utilisé par des prévisions ou possède des enfants, la suppression sera refusée.',
      danger: true,
      confirmLabel: 'Supprimer',
    });
    if (!ok) return;
    try {
      await deleteItemBI(row.idItemBI);
      void msgBox.success(`Item BI « ${row.libelle} » supprimé.`);
      setSelectedId((current) => (current === row.idItemBI ? null : current));
      await load();
    } catch (err) {
      void msgBox.error(
        isAxiosError(err) ? apiErrorMessage(err, 'Impossible de terminer l’opération.') : 'Impossible de terminer l’opération.',
      );
    }
  };

  const runDesactiver = async (row: ItemRow) => {
    const ok = await msgBox.confirm({
      title: `Désactiver « ${row.libelle} » ?`,
      message: 'L’item restera dans le référentiel, mais sera marqué INACTIF.',
      confirmLabel: 'Désactiver',
    });
    if (!ok) return;
    try {
      const updated = await setItemBIActif(row.idItemBI, false);
      void msgBox.success(`Item BI « ${updated.libelle} » désactivé.`);
      setSelectedId(updated.idItemBI);
      await load();
    } catch (err) {
      void msgBox.error(
        isAxiosError(err) ? apiErrorMessage(err, 'Impossible de terminer l’opération.') : 'Impossible de terminer l’opération.',
      );
    }
  };

  const runActiver = async (row: ItemRow) => {
    const ok = await msgBox.confirm({
      title: `Activer « ${row.libelle} » ?`,
      message: 'L’item sera de nouveau marqué ACTIF.',
      confirmLabel: 'Activer',
    });
    if (!ok) return;
    try {
      const updated = await setItemBIActif(row.idItemBI, true);
      void msgBox.success(`Item BI « ${updated.libelle} » activé.`);
      setSelectedId(updated.idItemBI);
      await load();
    } catch (err) {
      void msgBox.error(
        isAxiosError(err) ? apiErrorMessage(err, 'Impossible de terminer l’opération.') : 'Impossible de terminer l’opération.',
      );
    }
  };

  const emptyTitle = hasActiveFilter
    ? 'Aucun item BI ne correspond à votre recherche.'
    : 'Aucun item BI trouvé.';
  const emptyDescription = hasActiveFilter
    ? 'Modifiez les critères de recherche ou le filtre de statut.'
    : 'Créez un item du budget d’investissement pour commencer.';

  return (
    <Box>
      <PageHeader
        title="Items BI"
        subtitle="Catalogue hiérarchique du Budget d’Investissement — données réelles BD_SNEL."
        breadcrumbs={[
          { label: BRAND_NAME, to: '/dashboard' },
          { label: 'Référentiels', to: '/referentiels/organisationnel' },
          { label: 'Items BI' },
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
                Nouvel item BI
              </PrimaryButton>
            )}
          </Stack>
        }
      />

      <FilterBar
        search={search}
        onSearchChange={setSearch}
        searchPlaceholder="Rechercher par code, libellé ou catégorie…"
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
        <LoadingState label="Chargement des items BI…" />
      ) : error ? (
        <ErrorState message={error} onRetry={() => void load()} />
      ) : (
        <MasterDetailLayout
          detailOpen={selectedId != null}
          detailTitle="Détails de l'item BI"
          onCloseDetail={() => setSelectedId(null)}
          detail={<ItemsBIDetailPanel selected={selected} />}
          master={
            viewMode === 'arbre' ? (
              <ItemsBITreeView
                nodes={treeNodes}
                expanded={expanded}
                selectedId={selectedId}
                onToggle={toggleExpand}
                onSelect={handleSelect}
                emptyTitle={emptyTitle}
                emptyDescription={emptyDescription}
              />
            ) : (
              <DataTable
                columns={columns}
                rows={filtered}
                emptyTitle={emptyTitle}
                emptyDescription={emptyDescription}
                selectedRowId={selectedId != null ? String(selectedId) : null}
                onRowClick={(row) => handleSelect(row.idItemBI)}
                countLabel={(count) => (
                  <>
                    <strong>{count}</strong> item{count > 1 ? 's' : ''} BI
                  </>
                )}
                actions={[
                  { id: 'voir', label: 'Voir', onClick: (row) => handleSelect(row.idItemBI) },
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
            )
          }
        />
      )}

      <Dialog open={dialogOpen} onClose={() => !saving && setDialogOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>{formMode === 'edit' ? 'Modifier l’item BI' : 'Nouvel item BI'}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <TextField
              required
              label="Code"
              value={codeItem}
              onChange={(e) => setCodeItem(e.target.value.toUpperCase())}
              error={Boolean(fieldErrors.codeItem)}
              helperText={fieldErrors.codeItem}
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
              slotProps={{ htmlInput: { maxLength: 300 } }}
            />
            <SearchableSelect
              label="Item parent"
              value={parentId}
              onChange={setParentId}
              disabled={saving}
              allowEmpty
              options={[
                { value: '', label: 'Aucun (racine, niveau 0)' },
                ...parentOptions.map((r) => ({
                  value: String(r.idItemBI),
                  label: `${'· '.repeat(r.niveau)}${r.codeItem} — ${r.libelle}`,
                })),
              ]}
              helperText="Optionnel — le niveau est calculé automatiquement."
              placeholder="Rechercher un item parent…"
              fullWidth
            />
            <TextField
              label="Catégorie"
              value={categorie}
              onChange={(e) => setCategorie(e.target.value)}
              disabled={saving}
              helperText="Optionnel — texte libre (aucune liste imposée par la base)."
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
