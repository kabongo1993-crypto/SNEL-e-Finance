import DownloadIcon from '@mui/icons-material/Download';
import MoreVertIcon from '@mui/icons-material/MoreVert';
import PrintIcon from '@mui/icons-material/Print';
import RefreshIcon from '@mui/icons-material/Refresh';
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  IconButton,
  Menu,
  MenuItem,
  Skeleton,
  Tab,
  Tabs,
  useMediaQuery,
  useTheme,
} from '@mui/material';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { PageHeader, PrimaryButton, SecondaryButton } from '../../components';
import {
  fetchReferentielOrganisationnel,
  type ReferentielOrganisationnelSnapshot,
  type UniteBudgetaireOrganisation,
} from '../../services/apiClient';
import { BRAND_NAME } from '../../theme';
import { OrgChartPanel } from './components/OrgChartPanel';
import { OrgDetailPanel } from './components/OrgDetailPanel';
import { OrgFilters } from './components/OrgFilters';
import { OrgKpiStrip } from './components/OrgKpiStrip';
import { OrgListView } from './components/OrgListView';
import { OrgTreePanel } from './components/OrgTreePanel';
import {
  buildTree,
  collectExpandIds,
  filterTree,
  findNode,
  uniqueDepartements,
  uniqueTypes,
} from './orgUtils';

type LayoutMode = 'explorer' | 'liste';
type MobilePane = 'arbre' | 'organigramme' | 'details';

export function ReferentielOrganisationnelPage() {
  const theme = useTheme();
  const isDesktop = useMediaQuery(theme.breakpoints.up('lg'));

  const [snapshot, setSnapshot] = useState<ReferentielOrganisationnelSnapshot | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [search, setSearch] = useState('');
  const [typeFilter, setTypeFilter] = useState('all');
  const [departementFilter, setDepartementFilter] = useState('all');
  const [statutFilter, setStatutFilter] = useState('all');

  const [expanded, setExpanded] = useState<Set<number>>(new Set());
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [layoutMode, setLayoutMode] = useState<LayoutMode>('explorer');
  const [chartMode, setChartMode] = useState<'organigramme' | 'arbre'>('organigramme');
  const [mobilePane, setMobilePane] = useState<MobilePane>('arbre');
  const [menuAnchor, setMenuAnchor] = useState<null | HTMLElement>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await fetchReferentielOrganisationnel();
      setSnapshot(data);
      const roots = data.structures
        .filter((s) => s.typeStructure.toUpperCase() === 'ENTITE')
        .map((s) => s.idStructure);
      setExpanded(new Set(roots));
      const ddk = data.structures.find(
        (s) => s.typeStructure.toUpperCase() === 'ENTITE' && s.code.toUpperCase() === 'DDK',
      );
      if (ddk) {
        setSelectedId(ddk.idStructure);
        const dec = data.structures.find(
          (s) =>
            s.parentId === ddk.idStructure &&
            s.typeStructure.toUpperCase() === 'DEPARTEMENT' &&
            s.code.toUpperCase() === 'DEC',
        );
        if (dec) {
          setExpanded(new Set([...roots, ddk.idStructure, dec.idStructure]));
        }
      }
    } catch {
      setError(
        "Impossible de charger le référentiel organisationnel. Vérifiez que l'API est démarrée et connectée à BD_SNEL.",
      );
      setSnapshot(null);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const tree = useMemo(() => (snapshot ? buildTree(snapshot.structures) : []), [snapshot]);

  const filteredTree = useMemo(() => {
    if (!snapshot) return [];
    return filterTree(tree, snapshot.unitesBudgetaires, search, typeFilter, departementFilter);
  }, [tree, snapshot, search, typeFilter, departementFilter]);

  useEffect(() => {
    if (!search.trim() && typeFilter === 'all' && departementFilter === 'all') return;
    setExpanded(collectExpandIds(filteredTree));
  }, [search, typeFilter, departementFilter, filteredTree]);

  const selected = useMemo(
    () => snapshot?.structures.find((s) => s.idStructure === selectedId) ?? null,
    [snapshot, selectedId],
  );

  const selectedUbs: UniteBudgetaireOrganisation[] = useMemo(() => {
    if (!snapshot || selectedId == null) return [];
    return snapshot.unitesBudgetaires.filter((u) => u.structureId === selectedId);
  }, [snapshot, selectedId]);

  const childCount = useMemo(() => {
    if (selectedId == null) return 0;
    return findNode(tree, selectedId)?.children.length ?? 0;
  }, [tree, selectedId]);

  const types = useMemo(() => (snapshot ? uniqueTypes(snapshot.structures) : []), [snapshot]);
  const departements = useMemo(
    () => (snapshot ? uniqueDepartements(snapshot.structures) : []),
    [snapshot],
  );

  const toggle = (id: number) => {
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };

  const selectStructure = (id: number) => {
    setSelectedId(id);
    if (!isDesktop) setMobilePane('organigramme');
  };

  const resetFilters = () => {
    setSearch('');
    setTypeFilter('all');
    setDepartementFilter('all');
    setStatutFilter('all');
  };

  if (loading) {
    return (
      <Box>
        <Skeleton variant="text" width={360} height={36} />
        <Skeleton variant="rounded" height={72} sx={{ mt: 1.5 }} />
        <Box sx={{ display: 'grid', gridTemplateColumns: { lg: '1fr 2fr 1fr' }, gap: 1.5, mt: 1.5 }}>
          <Skeleton variant="rounded" height={480} />
          <Skeleton variant="rounded" height={480} />
          <Skeleton variant="rounded" height={480} />
        </Box>
        <Box sx={{ display: 'flex', justifyContent: 'center', py: 2 }}>
          <CircularProgress size={28} />
        </Box>
      </Box>
    );
  }

  if (error) {
    return (
      <Alert
        severity="error"
        action={
          <Button color="inherit" size="small" startIcon={<RefreshIcon />} onClick={() => void load()}>
            Réessayer
          </Button>
        }
      >
        {error}
      </Alert>
    );
  }

  if (!snapshot || snapshot.structures.length === 0) {
    return (
      <Alert
        severity="info"
        action={
          <Button color="inherit" size="small" onClick={() => void load()}>
            Actualiser
          </Button>
        }
      >
        Aucune structure organisationnelle trouvée dans BD_SNEL.
      </Alert>
    );
  }

  return (
    <Box>
      <PageHeader
        title="Référentiel organisationnel"
        subtitle="Exploration des structures et unités budgétaires"
        breadcrumbs={[
          { label: BRAND_NAME, to: '/dashboard' },
          { label: 'Référentiels', to: '/referentiels/organisationnel' },
          { label: 'Organisation' },
        ]}
        actions={
          <>
            <SecondaryButton startIcon={<DownloadIcon />} onClick={() => undefined}>
              Exporter
            </SecondaryButton>
            <SecondaryButton startIcon={<PrintIcon />} onClick={() => window.print()}>
              Imprimer
            </SecondaryButton>
            <PrimaryButton startIcon={<RefreshIcon />} onClick={() => void load()}>
              Actualiser
            </PrimaryButton>
            <IconButton
              size="small"
              onClick={(e) => setMenuAnchor(e.currentTarget)}
              aria-label="Plus d’actions"
            >
              <MoreVertIcon fontSize="small" />
            </IconButton>
            <Menu anchorEl={menuAnchor} open={Boolean(menuAnchor)} onClose={() => setMenuAnchor(null)}>
              <MenuItem
                onClick={() => {
                  setLayoutMode('liste');
                  setMenuAnchor(null);
                }}
              >
                Vue liste
              </MenuItem>
              <MenuItem
                onClick={() => {
                  setLayoutMode('explorer');
                  setMenuAnchor(null);
                }}
              >
                Vue explorateur
              </MenuItem>
            </Menu>
          </>
        }
      />

      <OrgKpiStrip compteurs={snapshot.compteurs} />

      <OrgFilters
        search={search}
        onSearchChange={setSearch}
        typeFilter={typeFilter}
        onTypeChange={setTypeFilter}
        departementFilter={departementFilter}
        onDepartementChange={setDepartementFilter}
        statutFilter={statutFilter}
        onStatutChange={setStatutFilter}
        types={types}
        departements={departements}
        onReset={resetFilters}
      />

      {layoutMode === 'liste' ? (
        <OrgListView
          structures={snapshot.structures}
          selectedId={selectedId}
          onSelect={(id) => {
            selectStructure(id);
            setLayoutMode('explorer');
          }}
          onBackToTree={() => setLayoutMode('explorer')}
        />
      ) : isDesktop ? (
        <Box
          sx={{
            display: 'grid',
            gridTemplateColumns: 'minmax(240px, 25%) minmax(0, 50%) minmax(240px, 25%)',
            gap: 1.5,
            height: 'calc(100vh - 320px)',
            minHeight: 520,
          }}
        >
          <OrgTreePanel
            nodes={filteredTree}
            expanded={expanded}
            selectedId={selectedId}
            onToggle={toggle}
            onSelect={selectStructure}
            onSwitchToList={() => setLayoutMode('liste')}
          />
          <OrgChartPanel
            structures={snapshot.structures}
            tree={tree}
            selectedId={selectedId}
            onSelect={selectStructure}
            mode={chartMode}
            onModeChange={setChartMode}
          />
          <OrgDetailPanel selected={selected} unites={selectedUbs} childCount={childCount} />
        </Box>
      ) : (
        <Box>
          <Tabs
            value={mobilePane}
            onChange={(_, v) => setMobilePane(v)}
            sx={{ mb: 1.25, minHeight: 36, '& .MuiTab-root': { minHeight: 36, textTransform: 'none' } }}
          >
            <Tab value="arbre" label="Arborescence" />
            <Tab value="organigramme" label="Organigramme" />
            <Tab value="details" label="Détails" />
          </Tabs>
          <Box sx={{ height: 520 }}>
            {mobilePane === 'arbre' && (
              <OrgTreePanel
                nodes={filteredTree}
                expanded={expanded}
                selectedId={selectedId}
                onToggle={toggle}
                onSelect={selectStructure}
                onSwitchToList={() => setLayoutMode('liste')}
              />
            )}
            {mobilePane === 'organigramme' && (
              <OrgChartPanel
                structures={snapshot.structures}
                tree={tree}
                selectedId={selectedId}
                onSelect={selectStructure}
                mode={chartMode}
                onModeChange={setChartMode}
              />
            )}
            {mobilePane === 'details' && (
              <OrgDetailPanel selected={selected} unites={selectedUbs} childCount={childCount} />
            )}
          </Box>
        </Box>
      )}
    </Box>
  );
}
