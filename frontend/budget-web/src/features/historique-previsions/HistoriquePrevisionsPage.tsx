import AddIcon from '@mui/icons-material/Add';
import RemoveIcon from '@mui/icons-material/Remove';
import {
  Alert,
  Box,
  Button,
  Chip,
  Collapse,
  Dialog,
  DialogContent,
  DialogTitle,
  IconButton,
  Pagination,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  ToggleButton,
  ToggleButtonGroup,
  Typography,
} from '@mui/material';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { ErrorState, FilterZone, LoadingState, PageHeader, SearchableSelect, SecondaryButton } from '../../components';
import {
  fetchDepartements,
  fetchDocumentPrevisionByAudit,
  downloadDocumentPrevisionPdf,
  fetchExercices,
  fetchHistoriquePrevisions,
  fetchHistoriquePrevisionsTimeline,
  fetchUnitesBudgetaires,
  fetchVersionsBudgetaires,
  type Departement,
  type Exercice,
  type HistoriquePrevisionEvenement,
  type HistoriquePrevisionPage,
  type HistoriquePrevisionTimeline,
  type UniteBudgetaire,
  type VersionBudgetaire,
} from '../../services/apiClient';
import { BRAND_NAME } from '../../theme';
import {
  DocumentActionResultDialog,
  buildDocumentActionResult,
  historiqueActionHasDocument,
  openDocumentPdfBlob,
  type DocumentActionResultState,
} from '../documents-previsions';
import { apiErrorMessage } from '../rubriques-budgetaires/rubriqueUtils';
import {
  formatDateSuivi,
  formatMontantUsd,
  normalizeStatut,
  statutChipSx,
  versionLabel,
} from '../suivi-previsions/suiviUtils';

/**
 * Colonnes secondaires du journal masquées sous `md` : sur téléphone on garde
 * la date, l'UB, l'action, le nouveau statut, le montant et le document.
 * Le journal reprend toutes ses colonnes dès `md`.
 */
const CELLULE_DETAIL = { display: { xs: 'none', md: 'table-cell' } } as const;

const ACTIONS = ['', 'SOUMISSION', 'CONTROLE', 'VALIDATION', 'REJET', 'REOUVERTURE'] as const;
const STATUTS = ['', 'BROUILLON', 'SOUMISE', 'CONTROLEE', 'VALIDEE', 'REJETEE'] as const;
const TYPES = ['', 'DC', 'AE', 'BI'] as const;

type DeptGroup = {
  key: string;
  idVersion: number;
  numeroVersion: number;
  libelleVersion: string | null;
  anneeExercice: number;
  idDepartement: number;
  codeDepartement: string;
  libelleDepartement: string;
  evenements: HistoriquePrevisionEvenement[];
  nbEvenements: number;
  nbUb: number;
  soumissions: number;
  controles: number;
  validations: number;
  rejets: number;
  reopenings: number;
  montantDC: number;
  montantAE: number;
  montantBI: number;
  montantTotal: number;
};

function groupByDepartementVersion(items: HistoriquePrevisionEvenement[]): DeptGroup[] {
  const map = new Map<string, DeptGroup>();
  for (const row of items) {
    const key = `${row.idVersion}-${row.idDepartement}`;
    let g = map.get(key);
    if (!g) {
      g = {
        key,
        idVersion: row.idVersion,
        numeroVersion: row.numeroVersion,
        libelleVersion: row.libelleVersion,
        anneeExercice: row.anneeExercice,
        idDepartement: row.idDepartement,
        codeDepartement: row.codeDepartement,
        libelleDepartement: row.libelleDepartement,
        evenements: [],
        nbEvenements: 0,
        nbUb: 0,
        soumissions: 0,
        controles: 0,
        validations: 0,
        rejets: 0,
        reopenings: 0,
        montantDC: 0,
        montantAE: 0,
        montantBI: 0,
        montantTotal: 0,
      };
      map.set(key, g);
    }
    g.evenements.push(row);
    g.nbEvenements += 1;
    const a = normalizeStatut(row.action);
    if (a === 'SOUMISSION' || a === 'RESOUMISSION') g.soumissions += 1;
    else if (a === 'CONTROLE') g.controles += 1;
    else if (a === 'VALIDATION') g.validations += 1;
    else if (a === 'REJET') g.rejets += 1;
    else if (a === 'REOUVERTURE') g.reopenings += 1;
  }

  for (const g of map.values()) {
    g.evenements.sort(
      (a, b) => new Date(b.dateHeure).getTime() - new Date(a.dateHeure).getTime() || b.idAudit - a.idAudit,
    );
    const ubSeen = new Set<number>();
    const ubMontants = new Map<number, HistoriquePrevisionEvenement>();
    for (const e of g.evenements) {
      ubSeen.add(e.idUB);
      if (!ubMontants.has(e.idUB)) ubMontants.set(e.idUB, e);
    }
    g.nbUb = ubSeen.size;
    let dc = 0;
    let ae = 0;
    let bi = 0;
    for (const e of ubMontants.values()) {
      dc += e.montantDC;
      ae += e.montantAE;
      bi += e.montantBI;
    }
    g.montantDC = dc;
    g.montantAE = ae;
    g.montantBI = bi;
    g.montantTotal = dc + ae + bi;
  }

  return [...map.values()].sort(
    (a, b) =>
      a.libelleDepartement.localeCompare(b.libelleDepartement, 'fr') ||
      a.numeroVersion - b.numeroVersion,
  );
}

function formatDateHeure(iso: string): string {
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

function timelinePhrase(ev: HistoriquePrevisionEvenement): string {
  const u = ev.nomUtilisateur;
  switch (normalizeStatut(ev.action)) {
    case 'SOUMISSION':
      return `Soumise par ${u}`;
    case 'CONTROLE':
      return `Contrôlée par ${u}`;
    case 'VALIDATION':
      return `Validée par ${u}`;
    case 'REJET':
      return `Rejetée par ${u}`;
    case 'REOUVERTURE':
      return `Réouverte par ${u}`;
    default:
      return `${ev.actionLibelle} — ${u}`;
  }
}

export function HistoriquePrevisionsPage() {
  const [exercices, setExercices] = useState<Exercice[]>([]);
  const [versions, setVersions] = useState<VersionBudgetaire[]>([]);
  const [departements, setDepartements] = useState<Departement[]>([]);
  const [ubs, setUbs] = useState<UniteBudgetaire[]>([]);

  const [idExercice, setIdExercice] = useState('');
  const [idVersion, setIdVersion] = useState('');
  const [idDepartement, setIdDepartement] = useState('');
  const [idUB, setIdUB] = useState('');
  const [type, setType] = useState('');
  const [action, setAction] = useState('');
  const [statut, setStatut] = useState('');
  const [dateDebut, setDateDebut] = useState('');
  const [dateFin, setDateFin] = useState('');
  const [search, setSearch] = useState('');
  const [monHistorique, setMonHistorique] = useState(true);
  const [page, setPage] = useState(1);
  const pageSize = 100;

  const [data, setData] = useState<HistoriquePrevisionPage | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [expanded, setExpanded] = useState<Record<string, boolean>>({});

  const [timeline, setTimeline] = useState<HistoriquePrevisionTimeline | null>(null);
  const [timelineOpen, setTimelineOpen] = useState(false);
  const [timelineLoading, setTimelineLoading] = useState(false);
  const [docResult, setDocResult] = useState<DocumentActionResultState | null>(null);
  const [docBusyAudit, setDocBusyAudit] = useState<number | null>(null);
  const [docError, setDocError] = useState<string | null>(null);
  const [docRefs, setDocRefs] = useState<Record<number, string>>({});

  const openDocumentForEvent = async (ev: HistoriquePrevisionEvenement, mode: 'dialog' | 'pdf' = 'dialog') => {
    if (!historiqueActionHasDocument(ev.action)) return;
    setDocBusyAudit(ev.idAudit);
    setDocError(null);
    try {
      const doc = await fetchDocumentPrevisionByAudit(ev.idAudit);
      if (!doc) {
        setDocError('Aucun document associé à cet événement.');
        return;
      }
      setDocRefs((prev) => ({ ...prev, [ev.idAudit]: doc.reference }));
      if (mode === 'pdf') {
        const blob = await downloadDocumentPrevisionPdf(doc.idDocument);
        openDocumentPdfBlob(blob);
        return;
      }
      const kind = (doc.typeDocument as 'SUB' | 'REJ' | 'CTL' | 'VAL') || 'SUB';
      const result = buildDocumentActionResult(doc, kind);
      if (result) setDocResult(result);
    } catch (err) {
      setDocError(apiErrorMessage(err, 'Impossible de charger le document.'));
    } finally {
      setDocBusyAudit(null);
    }
  };

  const versionsFiltrees = useMemo(
    () => versions.filter((v) => !idExercice || String(v.idExercice) === idExercice),
    [versions, idExercice],
  );

  const ubsFiltrees = useMemo(
    () => ubs.filter((u) => !idDepartement || String(u.idDepartement) === idDepartement),
    [ubs, idDepartement],
  );

  const groupes = useMemo(() => groupByDepartementVersion(data?.items ?? []), [data?.items]);

  useEffect(() => {
    const items = data?.items ?? [];
    const audits = items
      .filter((e) => historiqueActionHasDocument(e.action) && !docRefs[e.idAudit])
      .map((e) => e.idAudit);
    if (audits.length === 0) return;
    let cancelled = false;
    void (async () => {
      const next: Record<number, string> = {};
      await Promise.all(
        audits.slice(0, 40).map(async (idAudit) => {
          try {
            const doc = await fetchDocumentPrevisionByAudit(idAudit);
            if (doc?.reference) next[idAudit] = doc.reference;
          } catch {
            /* ignore missing docs */
          }
        }),
      );
      if (!cancelled && Object.keys(next).length > 0) {
        setDocRefs((prev) => ({ ...prev, ...next }));
      }
    })();
    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps -- refresh refs when page data changes
  }, [data]);

  useEffect(() => {
    void (async () => {
      try {
        const [ex, ver, dep, ubList] = await Promise.all([
          fetchExercices(),
          fetchVersionsBudgetaires(),
          fetchDepartements(),
          fetchUnitesBudgetaires(),
        ]);
        setExercices(ex);
        setVersions(ver);
        setDepartements(dep.filter((d) => d.actif));
        setUbs(ubList.filter((u) => u.actif));
        if (ex[0]) setIdExercice(String(ex[0].idExercice));
      } catch (err) {
        setError(apiErrorMessage(err, 'Impossible de charger les référentiels.'));
      }
    })();
  }, []);

  const resetFiltres = () => {
    setIdExercice(exercices[0] ? String(exercices[0].idExercice) : '');
    setIdVersion('');
    setIdDepartement('');
    setIdUB('');
    setType('');
    setAction('');
    setStatut('');
    setDateDebut('');
    setDateFin('');
    setSearch('');
    setMonHistorique(true);
    setPage(1);
  };

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await fetchHistoriquePrevisions({
        exerciseId: idExercice ? Number(idExercice) : undefined,
        versionId: idVersion ? Number(idVersion) : undefined,
        departementId: idDepartement ? Number(idDepartement) : undefined,
        ubId: idUB ? Number(idUB) : undefined,
        type: type || undefined,
        action: action || undefined,
        statut: statut || undefined,
        dateDebut: dateDebut || undefined,
        dateFin: dateFin || undefined,
        search: search.trim() || undefined,
        monHistorique,
        page,
        pageSize,
      });
      setData(result);
    } catch (err) {
      setError(apiErrorMessage(err, "Impossible de charger l'historique."));
      setData(null);
    } finally {
      setLoading(false);
    }
  }, [
    idExercice,
    idVersion,
    idDepartement,
    idUB,
    type,
    action,
    statut,
    dateDebut,
    dateFin,
    search,
    monHistorique,
    page,
  ]);

  useEffect(() => {
    void load();
  }, [load]);

  const developperTout = () => {
    const next: Record<string, boolean> = {};
    for (const g of groupes) next[g.key] = true;
    setExpanded(next);
  };

  const reduireTout = () => {
    const next: Record<string, boolean> = {};
    for (const g of groupes) next[g.key] = false;
    setExpanded(next);
  };

  const openTimeline = async (row: HistoriquePrevisionEvenement) => {
    setTimelineOpen(true);
    setTimelineLoading(true);
    setTimeline(null);
    try {
      const tl = await fetchHistoriquePrevisionsTimeline(row.idVersion, row.idUB);
      setTimeline(tl);
    } catch (err) {
      setError(apiErrorMessage(err, 'Impossible de charger la timeline.'));
      setTimelineOpen(false);
    } finally {
      setTimelineLoading(false);
    }
  };

  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1;

  return (
    <Box>
      <PageHeader
        title="Historique de mes prévisions"
        subtitle="Rupture par département — transitions Version × UB (journal d'audit)."
        breadcrumbs={[
          { label: BRAND_NAME, to: '/dashboard' },
          { label: 'Budget' },
          { label: 'Historique' },
        ]}
      />

      {data?.peutVoirToutes ? (
        <ToggleButtonGroup
          exclusive
          size="small"
          value={monHistorique ? 'mine' : 'all'}
          onChange={(_, v) => {
            if (!v) return;
            setMonHistorique(v === 'mine');
            setPage(1);
          }}
          sx={{ mb: 2 }}
        >
          <ToggleButton value="mine">Mon historique</ToggleButton>
          <ToggleButton value="all">Toutes les prévisions</ToggleButton>
        </ToggleButtonGroup>
      ) : (
        <Alert severity="info" sx={{ py: 0.5, mb: 2 }}>
          Affichage limité à votre historique (JWT) — prévisions et actions auxquelles vous êtes lié.
        </Alert>
      )}

      <Paper sx={{ p: 1.5, mb: 2 }}>
        <Stack
          direction={{ xs: 'column', md: 'row' }}
          spacing={2}
          sx={{ justifyContent: 'space-between', flexWrap: 'wrap' }}
        >
          <Box>
            <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 650 }}>
              SYNTHÈSE (page courante)
            </Typography>
            <Typography variant="body2" sx={{ mt: 0.5 }}>
              {groupes.length} département{groupes.length > 1 ? 's' : ''} · {data?.items.length ?? 0}{' '}
              événement{(data?.items.length ?? 0) > 1 ? 's' : ''}
              {data ? ` · total filtré ${data.totalCount}` : ''}
            </Typography>
          </Box>
          <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: 'wrap' }}>
            <SecondaryButton size="small" onClick={developperTout}>
              Développer tout
            </SecondaryButton>
            <SecondaryButton size="small" onClick={reduireTout}>
              Réduire tout
            </SecondaryButton>
          </Stack>
        </Stack>
      </Paper>

      <Paper sx={{ p: 1.5, mb: 2 }}>
        <FilterZone
          search={
            <TextField
              size="small"
              fullWidth
              label="Recherche"
              placeholder="Code UB, désignation, département…"
              value={search}
              onChange={(e) => {
                setSearch(e.target.value);
                setPage(1);
              }}
            />
          }
          actions={
            <SecondaryButton onClick={resetFiltres}>Réinitialiser les filtres</SecondaryButton>
          }
        >
          <SearchableSelect
            label="Exercice"
            value={idExercice}
            onChange={(v) => {
              setIdExercice(v);
              setIdVersion('');
              setPage(1);
            }}
            allowEmpty
            fullWidth
            options={[
              { value: '', label: 'Tous' },
              ...exercices.map((ex) => ({
                value: String(ex.idExercice),
                label: String(ex.annee),
              })),
            ]}
          />
          <SearchableSelect
            label="Version"
            value={idVersion}
            onChange={(v) => {
              setIdVersion(v);
              setPage(1);
            }}
            allowEmpty
            fullWidth
            options={[
              { value: '', label: 'Toutes' },
              ...versionsFiltrees.map((v) => ({
                value: String(v.idVersion),
                label: versionLabel(v.numeroVersion, v.libelle),
              })),
            ]}
          />
          <SearchableSelect
            label="Département"
            value={idDepartement}
            onChange={(v) => {
              setIdDepartement(v);
              setIdUB('');
              setPage(1);
            }}
            allowEmpty
            fullWidth
            options={[
              { value: '', label: 'Tous' },
              ...departements.map((d) => ({
                value: String(d.idDepartement),
                label: `${d.code} — ${d.libelle}`,
              })),
            ]}
          />
          <SearchableSelect
            label="UB"
            value={idUB}
            onChange={(v) => {
              setIdUB(v);
              setPage(1);
            }}
            allowEmpty
            fullWidth
            options={[
              { value: '', label: 'Toutes' },
              ...ubsFiltrees.map((u) => ({
                value: String(u.idUB),
                label: `${u.codeUB} — ${u.libelle}`,
              })),
            ]}
          />
          <SearchableSelect
            label="Type"
            value={type}
            onChange={(v) => {
              setType(v);
              setPage(1);
            }}
            allowEmpty
            fullWidth
            options={TYPES.map((t) => ({
              value: t,
              label: t || 'Tous',
            }))}
          />
          <SearchableSelect
            label="Action"
            value={action}
            onChange={(v) => {
              setAction(v);
              setPage(1);
            }}
            allowEmpty
            fullWidth
            options={ACTIONS.map((a) => ({
              value: a,
              label: a || 'Toutes',
            }))}
          />
          <SearchableSelect
            label="Statut"
            value={statut}
            onChange={(v) => {
              setStatut(v);
              setPage(1);
            }}
            allowEmpty
            fullWidth
            options={STATUTS.map((s) => ({
              value: s,
              label: s || 'Tous',
            }))}
          />
          <TextField
            size="small"
            fullWidth
            type="date"
            label="Date début"
            slotProps={{ inputLabel: { shrink: true } }}
            value={dateDebut}
            onChange={(e) => {
              setDateDebut(e.target.value);
              setPage(1);
            }}
          />
          <TextField
            size="small"
            fullWidth
            type="date"
            label="Date fin"
            slotProps={{ inputLabel: { shrink: true } }}
            value={dateFin}
            onChange={(e) => {
              setDateFin(e.target.value);
              setPage(1);
            }}
          />
        </FilterZone>
      </Paper>

      {error && <ErrorState message={error} onRetry={() => void load()} />}
      {loading && <LoadingState label="Chargement de l'historique…" />}

      {!loading && data && groupes.length === 0 && (
        <Paper sx={{ p: 3, mb: 2 }}>
          <Typography color="text.secondary" sx={{ textAlign: 'center' }}>
            Aucun événement d&apos;audit pour ces filtres. L&apos;historique ne contient que les
            transitions réellement enregistrées dans JOURNAL_AUDIT.
          </Typography>
        </Paper>
      )}

      {!loading &&
        groupes.map((g) => {
          const isOpen = expanded[g.key] !== false;
          return (
            <Paper key={g.key} sx={{ mb: 2, overflow: 'hidden' }}>
              <Box
                sx={{
                  px: 2,
                  py: 1.5,
                  bgcolor: 'var(--ef-surface-secondary)',
                  borderBottom: isOpen ? '1px solid var(--ef-border)' : 'none',
                }}
              >
                <Stack
                  direction="row"
                  spacing={1}
                  sx={{ justifyContent: 'space-between', alignItems: 'flex-start' }}
                >
                  <Box sx={{ minWidth: 0, flex: 1 }}>
                    <Typography variant="h6" sx={{ fontWeight: 700 }}>
                      {g.libelleDepartement}
                    </Typography>
                    <Typography variant="body2" color="text.secondary">
                      {versionLabel(g.numeroVersion, g.libelleVersion ?? undefined)} ·{' '}
                      {g.anneeExercice} · {g.nbUb} UB · {g.nbEvenements} événement
                      {g.nbEvenements > 1 ? 's' : ''}
                    </Typography>
                    <Stack
                      direction={{ xs: 'column', sm: 'row' }}
                      spacing={{ xs: 0.25, sm: 2 }}
                      useFlexGap
                      sx={{ flexWrap: 'wrap', mt: 0.75 }}
                    >
                      <Typography variant="body2">DC {formatMontantUsd(g.montantDC)}</Typography>
                      <Typography variant="body2">AE {formatMontantUsd(g.montantAE)}</Typography>
                      <Typography variant="body2">BI {formatMontantUsd(g.montantBI)}</Typography>
                      <Typography variant="body2" sx={{ fontWeight: 700 }}>
                        TOTAL {formatMontantUsd(g.montantTotal)}
                      </Typography>
                    </Stack>
                    <Stack direction="row" spacing={0.75} useFlexGap sx={{ flexWrap: 'wrap', mt: 1 }}>
                      {g.validations > 0 && (
                        <Chip size="small" label={`${g.validations} Validations`} sx={statutChipSx('VALIDEE')} />
                      )}
                      {g.controles > 0 && (
                        <Chip size="small" label={`${g.controles} Contrôles`} sx={statutChipSx('CONTROLEE')} />
                      )}
                      {g.soumissions > 0 && (
                        <Chip size="small" label={`${g.soumissions} Soumissions`} sx={statutChipSx('SOUMISE')} />
                      )}
                      {g.rejets > 0 && (
                        <Chip size="small" label={`${g.rejets} Rejets`} sx={statutChipSx('REJETEE')} />
                      )}
                      {g.reopenings > 0 && (
                        <Chip
                          size="small"
                          label={`${g.reopenings} Réouvertures`}
                          sx={statutChipSx('BROUILLON')}
                        />
                      )}
                    </Stack>
                  </Box>
                  <IconButton
                    size="small"
                    aria-label={isOpen ? 'Réduire' : 'Développer'}
                    onClick={() => setExpanded((p) => ({ ...p, [g.key]: !isOpen }))}
                  >
                    {isOpen ? <RemoveIcon /> : <AddIcon />}
                  </IconButton>
                </Stack>
              </Box>

              <Collapse in={isOpen} unmountOnExit>
                <TableContainer sx={{ overflowX: 'auto' }}>
                  <Table size="small">
                    <TableHead>
                      <TableRow>
                        <TableCell>DATE</TableCell>
                        <TableCell>UB</TableCell>
                        <TableCell sx={CELLULE_DETAIL}>TYPE</TableCell>
                        <TableCell>ACTION</TableCell>
                        <TableCell sx={CELLULE_DETAIL}>ANCIEN STATUT</TableCell>
                        <TableCell>NOUVEAU STATUT</TableCell>
                        <TableCell sx={CELLULE_DETAIL}>UTILISATEUR</TableCell>
                        <TableCell align="right">MONTANT</TableCell>
                        <TableCell sx={CELLULE_DETAIL}>MOTIF</TableCell>
                        <TableCell sx={CELLULE_DETAIL}>RÉFÉRENCE</TableCell>
                        <TableCell>DOCUMENT</TableCell>
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {g.evenements.map((row) => (
                        <TableRow
                          key={`${row.idAudit}-${row.idUB}-${row.portee}`}
                          hover
                          sx={{ cursor: 'pointer' }}
                          onClick={() => void openTimeline(row)}
                        >
                          <TableCell>{formatDateSuivi(row.dateHeure)}</TableCell>
                          <TableCell>
                            <Typography variant="body2" sx={{ fontWeight: 650 }}>
                              {row.codeUB}
                            </Typography>
                            <Typography variant="caption" color="text.secondary">
                              {row.libelleUB}
                            </Typography>
                          </TableCell>
                          <TableCell sx={CELLULE_DETAIL}>{row.typePrevision ?? 'UB'}</TableCell>
                          <TableCell>
                            <Chip size="small" label={row.action} variant="outlined" />
                            {row.portee === 'DEPARTEMENT' && (
                              <Typography
                                variant="caption"
                                color="text.secondary"
                                sx={{ display: 'block' }}
                              >
                                Portée département
                              </Typography>
                            )}
                          </TableCell>
                          <TableCell sx={CELLULE_DETAIL}>
                            {row.ancienStatut ? (
                              <Chip
                                size="small"
                                label={row.ancienStatut}
                                variant="outlined"
                                sx={{ height: 22, fontWeight: 700, ...statutChipSx(row.ancienStatut) }}
                              />
                            ) : (
                              '—'
                            )}
                          </TableCell>
                          <TableCell>
                            {row.nouveauStatut ? (
                              <Chip
                                size="small"
                                label={row.nouveauStatut}
                                variant="outlined"
                                sx={{ height: 22, fontWeight: 700, ...statutChipSx(row.nouveauStatut) }}
                              />
                            ) : (
                              '—'
                            )}
                          </TableCell>
                          <TableCell sx={CELLULE_DETAIL}>{row.nomUtilisateur}</TableCell>
                          <TableCell
                            align="right"
                            sx={{ fontVariantNumeric: 'tabular-nums', whiteSpace: 'nowrap', fontWeight: 700 }}
                          >
                            {formatMontantUsd(row.montantTotal)}
                          </TableCell>
                          <TableCell sx={CELLULE_DETAIL}>{row.motif?.trim() || '—'}</TableCell>
                          <TableCell sx={{ ...CELLULE_DETAIL, maxWidth: 220 }}>
                            {docRefs[row.idAudit] ? (
                              <Typography variant="caption" sx={{ fontFamily: 'ui-monospace, monospace' }}>
                                {docRefs[row.idAudit]}
                              </Typography>
                            ) : historiqueActionHasDocument(row.action) ? (
                              <Typography variant="caption" color="text.secondary">
                                …
                              </Typography>
                            ) : (
                              '—'
                            )}
                          </TableCell>
                          <TableCell onClick={(e) => e.stopPropagation()}>
                            {historiqueActionHasDocument(row.action) ? (
                              <Button
                                size="small"
                                variant="outlined"
                                disabled={docBusyAudit === row.idAudit}
                                onClick={() => void openDocumentForEvent(row, 'pdf')}
                              >
                                {docBusyAudit === row.idAudit ? '…' : 'Voir PDF'}
                              </Button>
                            ) : (
                              '—'
                            )}
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </TableContainer>
              </Collapse>
            </Paper>
          );
        })}

      {!loading && data && totalPages > 1 && (
        <Stack sx={{ alignItems: 'center', py: 1, mb: 2 }}>
          <Pagination
            count={totalPages}
            page={page}
            onChange={(_, p) => setPage(p)}
            color="primary"
            size="small"
          />
        </Stack>
      )}

      <Dialog open={timelineOpen} onClose={() => setTimelineOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>
          {timeline ? `${timeline.codeUB} — ${timeline.libelleUB}` : "Détail de l'historique"}
        </DialogTitle>
        <DialogContent dividers>
          {timelineLoading && <LoadingState />}
          {timeline && (
            <Stack spacing={2}>
              <Typography variant="body2" color="text.secondary">
                {timeline.libelleDepartement} — V{timeline.numeroVersion} — {timeline.anneeExercice}
              </Typography>
              <Typography variant="caption" color="text.secondary">
                Statut actuel : {timeline.statutActuel} · DC {formatMontantUsd(timeline.montantDC)} · AE{' '}
                {formatMontantUsd(timeline.montantAE)} · BI {formatMontantUsd(timeline.montantBI)} · TOTAL{' '}
                {formatMontantUsd(timeline.montantTotal)}
              </Typography>
              {timeline.evenements.length === 0 ? (
                <Alert severity="warning">
                  Aucune transition conservée dans JOURNAL_AUDIT pour cette UB (historique non inventé).
                </Alert>
              ) : (
                <Stack spacing={2} sx={{ borderLeft: '2px solid', borderColor: 'divider', pl: 2 }}>
                  {timeline.evenements.map((ev) => (
                    <Box key={`${ev.idAudit}-${ev.nouveauStatut}`}>
                      <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
                        <Box
                          sx={{
                            width: 10,
                            height: 10,
                            borderRadius: '50%',
                            bgcolor: 'primary.main',
                            ml: '-21px',
                          }}
                        />
                        <Chip
                          size="small"
                          label={ev.nouveauStatut ?? ev.action}
                          sx={statutChipSx(ev.nouveauStatut ?? '')}
                        />
                      </Stack>
                      <Typography variant="body2" sx={{ mt: 0.5 }}>
                        {formatDateHeure(ev.dateHeure)}
                      </Typography>
                      <Typography variant="body2">{timelinePhrase(ev)}</Typography>
                      {ev.portee === 'DEPARTEMENT' && (
                        <Typography variant="caption" color="text.secondary">
                          Action portée département
                        </Typography>
                      )}
                      {ev.motif && (
                        <Typography variant="body2" color="error.main">
                          Motif : {ev.motif}
                        </Typography>
                      )}
                      {historiqueActionHasDocument(ev.action) && (
                        <Stack direction="row" spacing={1} sx={{ mt: 0.5, alignItems: 'center' }}>
                          <Typography variant="caption" color="text.secondary" sx={{ fontFamily: 'monospace' }}>
                            {ev.action}
                          </Typography>
                          <Button
                            size="small"
                            variant="text"
                            disabled={docBusyAudit === ev.idAudit}
                            onClick={() => void openDocumentForEvent(ev, 'dialog')}
                          >
                            Voir PDF
                          </Button>
                        </Stack>
                      )}
                    </Box>
                  ))}
                </Stack>
              )}
            </Stack>
          )}
        </DialogContent>
      </Dialog>

      {docError && (
        <Alert severity="warning" sx={{ position: 'fixed', bottom: 16, right: 16, zIndex: 1400 }} onClose={() => setDocError(null)}>
          {docError}
        </Alert>
      )}

      <DocumentActionResultDialog
        open={!!docResult}
        state={docResult}
        onClose={() => setDocResult(null)}
      />
    </Box>
  );
}
