import AddIcon from '@mui/icons-material/Add';
import AccountTreeOutlinedIcon from '@mui/icons-material/AccountTreeOutlined';
import ViewListOutlinedIcon from '@mui/icons-material/ViewListOutlined';
import {
  Box,
  Button,
  ButtonGroup,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  MenuItem,
  Stack,
  TextField,
} from '@mui/material';
import { isAxiosError } from 'axios';
import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  ErrorState,
  FilterBar,
  LoadingState,
  MasterDetailLayout,
  PageHeader,
  PrimaryButton,
  SecondaryButton,
  useMsgBox,
} from '../../components';
import {
  createRubriqueBudgetaire,
  deleteRubriqueBudgetaire,
  fetchGroupesRubriquesBudgetaires,
  fetchRubriquesBudgetaires,
  setRubriqueBudgetaireActif,
  updateRubriqueBudgetaire,
  type GroupeRubriqueBudgetaire,
  type RubriqueBudgetaire,
} from '../../services/apiClient';
import { BRAND_NAME } from '../../theme';
import { useAuth, canWriteReferentiels } from '../auth';
import {
  RubriqueGroupeNiveau1Legend,
  RubriqueGroupeNiveau1List,
  RubriqueGroupeNiveau1Tree,
  RubriqueHierarchySelect,
  buildRubriqueGroupesNiveau1,
  countRubriquesInGroupes,
  filterGroupesNiveau1ForReferentiel,
} from './hierarchy';
import { RubriquesDetailPanel } from './RubriquesDetailPanel';
import { apiErrorMessage } from './rubriqueUtils';

type RubriqueRow = RubriqueBudgetaire & { id: string };
type ViewMode = 'liste' | 'arbre';
type FormMode = 'create' | 'edit';

export function RubriquesBudgetairesPage() {
  const { user } = useAuth();
  const msgBox = useMsgBox();
  const canWrite = canWriteReferentiels(user);
  const [rows, setRows] = useState<RubriqueRow[]>([]);
  const [groupesRef, setGroupesRef] = useState<GroupeRubriqueBudgetaire[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [statutFilter, setStatutFilter] = useState('all');
  const [viewMode, setViewMode] = useState<ViewMode>('arbre');
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [expanded, setExpanded] = useState<Set<number>>(new Set());
  const [expandedManual, setExpandedManual] = useState(false);

  const [dialogOpen, setDialogOpen] = useState(false);
  const [formMode, setFormMode] = useState<FormMode>('create');
  const [editingId, setEditingId] = useState<number | null>(null);
  const [codeRB, setCodeRB] = useState('');
  const [libelle, setLibelle] = useState('');
  const [parentId, setParentId] = useState('');
  const [actif, setActif] = useState(true);
  const [fieldErrors, setFieldErrors] = useState<{ codeRB?: string; libelle?: string }>({});
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [data, groupes] = await Promise.all([
        fetchRubriquesBudgetaires(),
        fetchGroupesRubriquesBudgetaires(),
      ]);
      setRows(data.map((r) => ({ ...r, id: String(r.idRB) })));
      setGroupesRef(groupes);
    } catch {
      setError('Impossible de charger les rubriques budgétaires.');
      setRows([]);
      setGroupesRef([]);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const matchesStatut = useCallback(
    (r: RubriqueBudgetaire) =>
      statutFilter === 'all' ||
      (statutFilter === 'actif' && r.actif) ||
      (statutFilter === 'inactif' && !r.actif),
    [statutFilter],
  );

  const hasActiveFilter = search.trim() !== '' || statutFilter !== 'all';

  const groupesAll = useMemo(
    () => buildRubriqueGroupesNiveau1(rows, groupesRef),
    [rows, groupesRef],
  );

  const groupes = useMemo(
    () =>
      filterGroupesNiveau1ForReferentiel(groupesAll, {
        search,
        matchesStatut,
      }),
    [groupesAll, search, matchesStatut],
  );

  const totalRbVisible = useMemo(() => countRubriquesInGroupes(groupes), [groupes]);

  useEffect(() => {
    if (groupesAll.length === 0) return;
    if (hasActiveFilter) {
      setExpanded(new Set(groupes.map((g) => g.idGroupeRB)));
      return;
    }
    if (!expandedManual) {
      setExpanded(new Set(groupesAll.map((g) => g.idGroupeRB)));
    }
  }, [groupesAll, groupes, hasActiveFilter, expandedManual]);

  const selected = useMemo(() => rows.find((r) => r.idRB === selectedId) ?? null, [rows, selectedId]);

  const openCreate = () => {
    setFormMode('create');
    setEditingId(null);
    setCodeRB('');
    setLibelle('');
    setParentId(selectedId != null ? String(selected?.parentId ?? selectedId) : '');
    setActif(true);
    setFieldErrors({});
    setDialogOpen(true);
  };

  const openEdit = (row: RubriqueRow) => {
    setFormMode('edit');
    setEditingId(row.idRB);
    setCodeRB(row.codeRB);
    setLibelle(row.libelle);
    setParentId(row.parentId != null ? String(row.parentId) : '');
    setActif(row.actif);
    setFieldErrors({});
    setDialogOpen(true);
  };

  const validate = () => {
    const next: { codeRB?: string; libelle?: string } = {};
    if (!codeRB.trim()) next.codeRB = 'Le code est obligatoire.';
    if (!libelle.trim()) next.libelle = 'Le libellé est obligatoire.';
    setFieldErrors(next);
    return Object.keys(next).length === 0;
  };

  const handleSave = async () => {
    if (!validate()) return;
    setSaving(true);
    try {
      const payload = {
        codeRB: codeRB.trim().toUpperCase(),
        libelle: libelle.trim(),
        parentId: parentId ? Number(parentId) : null,
        actif,
      };
      if (formMode === 'edit' && editingId != null) {
        const updated = await updateRubriqueBudgetaire(editingId, payload);
        void msgBox.success('Rubrique budgétaire modifiée avec succès.');
        setSelectedId(updated.idRB);
      } else {
        const created = await createRubriqueBudgetaire(payload);
        void msgBox.success('Rubrique budgétaire créée avec succès.');
        setSelectedId(created.idRB);
      }
      setDialogOpen(false);
      await load();
    } catch (err) {
      void msgBox.error(
        isAxiosError(err)
          ? apiErrorMessage(err, 'Impossible d’enregistrer la rubrique budgétaire.')
          : 'Impossible d’enregistrer la rubrique budgétaire.',
      );
    } finally {
      setSaving(false);
    }
  };

  const handleSelect = (id: number) => {
    setSelectedId(id);
    const rb = rows.find((r) => r.idRB === id);
    if (rb?.idGroupeRB != null) {
      setExpanded((prev) => {
        const next = new Set(prev);
        next.add(rb.idGroupeRB!);
        return next;
      });
    }
  };

  const toggleExpand = (idGroupeRB: number) => {
    setExpandedManual(true);
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(idGroupeRB)) next.delete(idGroupeRB);
      else next.add(idGroupeRB);
      return next;
    });
  };

  const runDelete = async (row: RubriqueRow) => {
    const ok = await msgBox.confirm({
      title: `Supprimer « ${row.libelle} » ?`,
      message:
        'Cette action est définitive. Si la rubrique est utilisée par des prévisions ou possède des enfants, la suppression sera refusée.',
      danger: true,
      confirmLabel: 'Supprimer',
    });
    if (!ok) return;
    try {
      await deleteRubriqueBudgetaire(row.idRB);
      void msgBox.success(`Rubrique « ${row.libelle} » supprimée.`);
      setSelectedId((current) => (current === row.idRB ? null : current));
      await load();
    } catch (err) {
      void msgBox.error(
        isAxiosError(err)
          ? apiErrorMessage(err, 'Impossible de terminer l’opération.')
          : 'Impossible de terminer l’opération.',
      );
    }
  };

  const runDesactiver = async (row: RubriqueRow) => {
    const ok = await msgBox.confirm({
      title: `Désactiver « ${row.libelle} » ?`,
      message: 'La rubrique restera dans le référentiel, mais sera marquée INACTIF.',
      confirmLabel: 'Désactiver',
    });
    if (!ok) return;
    try {
      const updated = await setRubriqueBudgetaireActif(row.idRB, false);
      void msgBox.success(`Rubrique « ${updated.libelle} » désactivée.`);
      setSelectedId(updated.idRB);
      await load();
    } catch (err) {
      void msgBox.error(
        isAxiosError(err)
          ? apiErrorMessage(err, 'Impossible de terminer l’opération.')
          : 'Impossible de terminer l’opération.',
      );
    }
  };

  const runActiver = async (row: RubriqueRow) => {
    const ok = await msgBox.confirm({
      title: `Activer « ${row.libelle} » ?`,
      message: 'La rubrique sera de nouveau marquée ACTIF.',
      confirmLabel: 'Activer',
    });
    if (!ok) return;
    try {
      const updated = await setRubriqueBudgetaireActif(row.idRB, true);
      void msgBox.success(`Rubrique « ${updated.libelle} » activée.`);
      setSelectedId(updated.idRB);
      await load();
    } catch (err) {
      void msgBox.error(
        isAxiosError(err)
          ? apiErrorMessage(err, 'Impossible de terminer l’opération.')
          : 'Impossible de terminer l’opération.',
      );
    }
  };

  const listActions = [
    { id: 'voir', label: 'Voir', onClick: (row: RubriqueBudgetaire) => handleSelect(row.idRB) },
    {
      id: 'modifier',
      label: 'Modifier',
      hidden: () => !canWrite,
      onClick: (row: RubriqueBudgetaire) => openEdit(row as RubriqueRow),
    },
    {
      id: 'desactiver',
      label: 'Désactiver',
      hidden: (row: RubriqueBudgetaire) => !canWrite || !row.actif,
      onClick: (row: RubriqueBudgetaire) => void runDesactiver(row as RubriqueRow),
    },
    {
      id: 'activer',
      label: 'Activer',
      hidden: (row: RubriqueBudgetaire) => !canWrite || row.actif,
      onClick: (row: RubriqueBudgetaire) => void runActiver(row as RubriqueRow),
    },
    {
      id: 'supprimer',
      label: 'Supprimer',
      color: 'error' as const,
      hidden: () => !canWrite,
      onClick: (row: RubriqueBudgetaire) => void runDelete(row as RubriqueRow),
    },
  ];

  const emptyTitle = hasActiveFilter
    ? 'Aucune rubrique ne correspond à votre recherche.'
    : 'Aucune rubrique budgétaire trouvée.';
  const emptyDescription = hasActiveFilter
    ? 'Modifiez les critères de recherche ou le filtre de statut.'
    : 'Créez une rubrique budgétaire pour commencer.';

  return (
    <Box>
      <PageHeader
        title="Rubriques budgétaires"
        subtitle="Affichage métier : Groupe niveau 1 → RB (sections techniques masquées)."
        breadcrumbs={[
          { label: BRAND_NAME, to: '/dashboard' },
          { label: 'Référentiels', to: '/referentiels/organisationnel' },
          { label: 'Rubriques budgétaires' },
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
                Nouvelle rubrique
              </PrimaryButton>
            )}
          </Stack>
        }
      />

      <FilterBar
        search={search}
        onSearchChange={setSearch}
        searchPlaceholder="Rechercher par code, libellé ou groupe…"
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

      <Box sx={{ mb: 1.25, display: 'flex', gap: 2, flexWrap: 'wrap', alignItems: 'center' }}>
        <RubriqueGroupeNiveau1Legend />
        {!loading && !error && (
          <Box component="span" sx={{ typography: 'caption', color: 'text.secondary' }}>
            {groupes.length} groupe{groupes.length > 1 ? 's' : ''} · {totalRbVisible} RB
            {hasActiveFilter ? ' (filtrés)' : ''}
          </Box>
        )}
      </Box>

      {loading ? (
        <LoadingState label="Chargement des rubriques budgétaires…" />
      ) : error ? (
        <ErrorState message={error} onRetry={() => void load()} />
      ) : (
        <MasterDetailLayout
          detailOpen={selectedId != null}
          detailTitle="Détails de la rubrique"
          onCloseDetail={() => setSelectedId(null)}
          detail={<RubriquesDetailPanel selected={selected} />}
          master={
            viewMode === 'arbre' ? (
              <RubriqueGroupeNiveau1Tree
                groupes={groupes}
                expanded={expanded}
                selectedId={selectedId}
                onToggle={toggleExpand}
                onSelect={handleSelect}
                emptyTitle={emptyTitle}
                emptyDescription={emptyDescription}
              />
            ) : (
              <RubriqueGroupeNiveau1List
                groupes={groupes}
                selectedId={selectedId}
                onSelect={handleSelect}
                emptyTitle={emptyTitle}
                emptyDescription={emptyDescription}
                actions={listActions}
              />
            )
          }
        />
      )}

      <Dialog open={dialogOpen} onClose={() => !saving && setDialogOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>
          {formMode === 'edit' ? 'Modifier la rubrique budgétaire' : 'Nouvelle rubrique budgétaire'}
        </DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <TextField
              required
              label="Code"
              value={codeRB}
              onChange={(e) => setCodeRB(e.target.value.toUpperCase())}
              error={Boolean(fieldErrors.codeRB)}
              helperText={fieldErrors.codeRB}
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
            <RubriqueHierarchySelect
              label="Section technique parente"
              value={parentId}
              onChange={setParentId}
              rubriques={rows}
              excludeId={editingId}
              disabled={saving}
              helperText="Optionnel — section technique (0010, 0011…). Le groupe niveau 1 est géré séparément."
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
