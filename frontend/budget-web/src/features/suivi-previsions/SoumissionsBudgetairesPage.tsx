import AddIcon from '@mui/icons-material/Add';
import RemoveIcon from '@mui/icons-material/Remove';
import {
  Box,
  Button,
  Chip,
  Collapse,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  IconButton,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from '@mui/material';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ErrorState, FilterZone, KpiRow, LoadingState, PageHeader, SearchableSelect, SecondaryButton, StatCard, useMsgBox } from '../../components';
import {
  controlerDepartementPrevisions,
  controlerPrevisionUb,
  fetchDepartements,
  fetchExercices,
  fetchSoumissionsBudgetaires,
  fetchVersionsBudgetaires,
  rejeterDepartementPrevisions,
  rejeterPrevisionUb,
  validerDepartementPrevisions,
  validerPrevisionUb,
  type Departement,
  type Exercice,
  type SuiviPrevisionListe,
  type SuiviPrevisionUbResume,
  type VersionBudgetaire,
  type WorkflowDepartementBulkResult,
} from '../../services/apiClient';
import { BRAND_NAME } from '../../theme';
import { useAuth } from '../auth';
import {
  DocumentActionResultDialog,
  buildDocumentActionResult,
  type DocumentActionResultState,
} from '../documents-previsions';
import { apiErrorMessage } from '../rubriques-budgetaires/rubriqueUtils';
import {
  formatDateSuivi,
  formatMontantUsd,
  hasPerm,
  messageResultatBulkDepartement,
  normalizeStatut,
  peutControlerUb,
  peutRejeterUb,
  peutValiderUb,
  statutChipSx,
  versionLabel,
} from './suiviUtils';

/**
 * Colonnes secondaires masquées sous `md` : sur téléphone on garde l'UB, le
 * total, le statut et les actions. Le détail par type revient dès `md`.
 */
const CELLULE_DETAIL = { display: { xs: 'none', md: 'table-cell' } } as const;

type FiltreRapide = 'TOUTES' | 'SOUMISE' | 'CONTROLEE' | 'VALIDEE' | 'REJETEE' | 'BROUILLON';
type DeptAction = 'controler' | 'valider' | 'rejeter';

type DeptGroup = {
  key: string;
  idVersion: number;
  numeroVersion: number;
  idDepartement: number;
  libelleDepartement: string;
  codeDepartement: string;
  lignes: SuiviPrevisionUbResume[];
  nbUb: number;
  brouillons: number;
  soumises: number;
  controlees: number;
  validees: number;
  rejetees: number;
  montantDC: number;
  montantAE: number;
  montantBI: number;
  montantTotal: number;
  nbControllables: number;
  nbValidables: number;
  nbRejetables: number;
};

function groupByDepartementVersion(lignes: SuiviPrevisionUbResume[]): DeptGroup[] {
  const map = new Map<string, DeptGroup>();
  for (const row of lignes) {
    const key = `${row.idVersion}-${row.idDepartement}`;
    let g = map.get(key);
    if (!g) {
      g = {
        key,
        idVersion: row.idVersion,
        numeroVersion: row.numeroVersion,
        idDepartement: row.idDepartement,
        libelleDepartement: row.libelleDepartement,
        codeDepartement: row.codeDepartement,
        lignes: [],
        nbUb: 0,
        brouillons: 0,
        soumises: 0,
        controlees: 0,
        validees: 0,
        rejetees: 0,
        montantDC: 0,
        montantAE: 0,
        montantBI: 0,
        montantTotal: 0,
        nbControllables: 0,
        nbValidables: 0,
        nbRejetables: 0,
      };
      map.set(key, g);
    }
    g.lignes.push(row);
    g.nbUb += 1;
    g.montantDC += row.montantDC;
    g.montantAE += row.montantAE;
    g.montantBI += row.montantBI;
    g.montantTotal += row.montantTotal;
    const s = normalizeStatut(row.statut);
    if (s === 'BROUILLON') g.brouillons += 1;
    else if (s === 'SOUMISE') {
      g.soumises += 1;
      g.nbControllables += 1;
      g.nbRejetables += 1;
    } else if (s === 'CONTROLEE') {
      g.controlees += 1;
      g.nbValidables += 1;
      g.nbRejetables += 1;
    } else if (s === 'VALIDEE') g.validees += 1;
    else if (s === 'REJETEE') g.rejetees += 1;
  }
  return [...map.values()].sort(
    (a, b) =>
      a.libelleDepartement.localeCompare(b.libelleDepartement, 'fr') ||
      a.numeroVersion - b.numeroVersion,
  );
}

function MontantCell({ value }: { value: number }) {
  return (
    <Typography variant="body2" sx={{ fontVariantNumeric: 'tabular-nums', whiteSpace: 'nowrap' }}>
      {formatMontantUsd(value)}
    </Typography>
  );
}

export function SoumissionsBudgetairesPage() {
  const navigate = useNavigate();
  const { user } = useAuth();
  const msgBox = useMsgBox();
  const canAccess =
    hasPerm(user, 'versions.controler') ||
    hasPerm(user, 'versions.valider') ||
    hasPerm(user, 'versions.rejeter') ||
    hasPerm(user, 'admin.all');
  const canControlerDept = hasPerm(user, 'versions.controler');
  const canValiderDept = hasPerm(user, 'versions.valider');
  const canRejeterDept = hasPerm(user, 'versions.rejeter');

  const [exercices, setExercices] = useState<Exercice[]>([]);
  const [versions, setVersions] = useState<VersionBudgetaire[]>([]);
  const [departements, setDepartements] = useState<Departement[]>([]);
  const [idExercice, setIdExercice] = useState('');
  const [idVersion, setIdVersion] = useState('');
  const [idDepartement, setIdDepartement] = useState('');
  const [searchUb, setSearchUb] = useState('');
  const [filtreRapide, setFiltreRapide] = useState<FiltreRapide>('TOUTES');
  const [data, setData] = useState<SuiviPrevisionListe | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [busyKey, setBusyKey] = useState<string | null>(null);
  const [expanded, setExpanded] = useState<Record<string, boolean>>({});

  const [rejectUb, setRejectUb] = useState<SuiviPrevisionUbResume | null>(null);
  const [rejectUbMotif, setRejectUbMotif] = useState('');
  const [rejectUbError, setRejectUbError] = useState<string | null>(null);

  const [deptDialog, setDeptDialog] = useState<{ group: DeptGroup; action: DeptAction } | null>(null);
  const [deptMotif, setDeptMotif] = useState('');
  const [deptError, setDeptError] = useState<string | null>(null);
  const [docResult, setDocResult] = useState<DocumentActionResultState | null>(null);

  const versionsFiltrees = useMemo(
    () => versions.filter((v) => !idExercice || String(v.idExercice) === idExercice),
    [versions, idExercice],
  );

  const groupes = useMemo(() => groupByDepartementVersion(data?.lignes ?? []), [data]);

  const syntheseGlobale = useMemo(() => {
    const lignes = data?.lignes ?? [];
    let montantDC = 0;
    let montantAE = 0;
    let montantBI = 0;
    for (const l of lignes) {
      montantDC += l.montantDC;
      montantAE += l.montantAE;
      montantBI += l.montantBI;
    }
    return {
      nbDepartements: groupes.length,
      nbUb: lignes.length,
      validees: data?.compteurs.validees ?? 0,
      rejetees: data?.compteurs.rejetees ?? 0,
      soumises: data?.compteurs.soumises ?? 0,
      controlees: data?.compteurs.controlees ?? 0,
      brouillons: data?.compteurs.brouillons ?? 0,
      montantDC,
      montantAE,
      montantBI,
      montantTotal: montantDC + montantAE + montantBI,
    };
  }, [data, groupes.length]);

  useEffect(() => {
    setExpanded((prev) => {
      const next = { ...prev };
      for (const g of groupes) {
        if (next[g.key] === undefined) next[g.key] = true;
      }
      return next;
    });
  }, [groupes]);

  useEffect(() => {
    void (async () => {
      try {
        const [ex, ver, dep] = await Promise.all([
          fetchExercices(),
          fetchVersionsBudgetaires(),
          fetchDepartements(),
        ]);
        setExercices(ex);
        setVersions(ver);
        setDepartements(dep.filter((d) => d.actif));
        if (ex[0]) setIdExercice(String(ex[0].idExercice));
      } catch (err) {
        setError(apiErrorMessage(err, 'Impossible de charger les référentiels.'));
      }
    })();
  }, []);

  const load = useCallback(async () => {
    if (!canAccess) {
      setLoading(false);
      return;
    }
    setLoading(true);
    setError(null);
    try {
      const result = await fetchSoumissionsBudgetaires({
        idExercice: idExercice ? Number(idExercice) : undefined,
        idVersion: idVersion ? Number(idVersion) : undefined,
        idDepartement: idDepartement ? Number(idDepartement) : undefined,
        statut: filtreRapide === 'TOUTES' ? undefined : filtreRapide,
        searchUb: searchUb.trim() || undefined,
      });
      setData(result);
    } catch (err) {
      setError(apiErrorMessage(err, 'Impossible de charger les soumissions.'));
      setData(null);
    } finally {
      setLoading(false);
    }
  }, [canAccess, idExercice, idVersion, idDepartement, filtreRapide, searchUb]);

  useEffect(() => {
    void load();
  }, [load]);

  const patchLigneStatut = (
    idV: number,
    idU: number,
    statut: string,
    extra?: Partial<SuiviPrevisionUbResume>,
  ) => {
    setData((prev) => {
      if (!prev) return prev;
      const lignes = prev.lignes.map((l) =>
        l.idVersion === idV && l.idUB === idU ? { ...l, statut, ...extra } : l,
      );
      const compteurs = {
        brouillons: lignes.filter((l) => normalizeStatut(l.statut) === 'BROUILLON').length,
        soumises: lignes.filter((l) => normalizeStatut(l.statut) === 'SOUMISE').length,
        controlees: lignes.filter((l) => normalizeStatut(l.statut) === 'CONTROLEE').length,
        validees: lignes.filter((l) => normalizeStatut(l.statut) === 'VALIDEE').length,
        rejetees: lignes.filter((l) => normalizeStatut(l.statut) === 'REJETEE').length,
      };
      return { ...prev, lignes, compteurs };
    });
  };

  const appliquerBulk = (group: DeptGroup, result: WorkflowDepartementBulkResult) => {
    setData((prev) => {
      if (!prev) return prev;
      const byUb = new Map(result.details.map((d) => [d.idUB, d]));
      const lignes = prev.lignes.map((l) => {
        if (l.idVersion !== group.idVersion || l.idDepartement !== group.idDepartement) return l;
        const d = byUb.get(l.idUB);
        if (!d || d.outcome === 'IGNORE' || !d.statutApres) return l;
        return { ...l, statut: d.statutApres };
      });
      const compteurs = {
        brouillons: lignes.filter((x) => normalizeStatut(x.statut) === 'BROUILLON').length,
        soumises: lignes.filter((x) => normalizeStatut(x.statut) === 'SOUMISE').length,
        controlees: lignes.filter((x) => normalizeStatut(x.statut) === 'CONTROLEE').length,
        validees: lignes.filter((x) => normalizeStatut(x.statut) === 'VALIDEE').length,
        rejetees: lignes.filter((x) => normalizeStatut(x.statut) === 'REJETEE').length,
      };
      return { ...prev, lignes, compteurs };
    });
  };

  const runControler = async (row: SuiviPrevisionUbResume) => {
    const key = `c-${row.idVersion}-${row.idUB}`;
    setBusyKey(key);
    try {
      const wf = await controlerPrevisionUb(row.idVersion, row.idUB);
      patchLigneStatut(row.idVersion, row.idUB, wf.statut);
      void msgBox.success(`UB ${row.codeUB} contrôlée.`);
      const result = buildDocumentActionResult(wf.document, 'CTL');
      if (result) setDocResult(result);
    } catch (err) {
      void msgBox.error(apiErrorMessage(err, 'Échec du contrôle.'));
    } finally {
      setBusyKey(null);
    }
  };

  const runValider = async (row: SuiviPrevisionUbResume) => {
    const key = `v-${row.idVersion}-${row.idUB}`;
    setBusyKey(key);
    try {
      const wf = await validerPrevisionUb(row.idVersion, row.idUB);
      patchLigneStatut(row.idVersion, row.idUB, wf.statut);
      void msgBox.success(`UB ${row.codeUB} validée.`);
      const result = buildDocumentActionResult(wf.document, 'VAL');
      if (result) setDocResult(result);
    } catch (err) {
      void msgBox.error(apiErrorMessage(err, 'Échec de la validation.'));
    } finally {
      setBusyKey(null);
    }
  };

  const runRejeterUb = async () => {
    if (!rejectUb) return;
    const motif = rejectUbMotif.trim();
    if (!motif) {
      setRejectUbError('Le motif du rejet est obligatoire.');
      return;
    }
    setBusyKey(`r-${rejectUb.idVersion}-${rejectUb.idUB}`);
    setRejectUbError(null);
    try {
      const wf = await rejeterPrevisionUb(rejectUb.idVersion, rejectUb.idUB, motif);
      patchLigneStatut(rejectUb.idVersion, rejectUb.idUB, wf.statut, {
        motifRejet: wf.motifRejet,
        dateRejet: wf.dateRejet,
        nomUtilisateurRejet: wf.nomUtilisateurRejet,
      });
      void msgBox.success(`UB ${rejectUb.codeUB} rejetée.`);
      const result = buildDocumentActionResult(wf.document, 'REJ');
      if (result) setDocResult(result);
      setRejectUb(null);
      setRejectUbMotif('');
    } catch (err) {
      void msgBox.error(apiErrorMessage(err, 'Échec du rejet.'));
    } finally {
      setBusyKey(null);
    }
  };

  const concernesPourAction = (g: DeptGroup, action: DeptAction) => {
    if (action === 'controler') return g.nbControllables;
    if (action === 'valider') return g.nbValidables;
    return g.nbRejetables;
  };

  const runDeptAction = async () => {
    if (!deptDialog) return;
    const { group, action } = deptDialog;
    if (action === 'rejeter') {
      const motif = deptMotif.trim();
      if (!motif) {
        setDeptError('Le motif du rejet est obligatoire.');
        return;
      }
    }
    setBusyKey(`d-${action}-${group.key}`);
    setDeptError(null);
    try {
      let result: WorkflowDepartementBulkResult;
      let label: string;
      let docKind: 'SUB' | 'REJ' | 'CTL' | 'VAL';
      if (action === 'controler') {
        result = await controlerDepartementPrevisions(group.idVersion, group.idDepartement);
        label = 'Contrôle terminé.';
        docKind = 'CTL';
      } else if (action === 'valider') {
        result = await validerDepartementPrevisions(group.idVersion, group.idDepartement);
        label = 'Validation terminée.';
        docKind = 'VAL';
      } else {
        result = await rejeterDepartementPrevisions(
          group.idVersion,
          group.idDepartement,
          deptMotif.trim(),
        );
        label = 'Rejet terminé.';
        docKind = 'REJ';
      }
      appliquerBulk(group, result);
      void msgBox.success(messageResultatBulkDepartement(label, result.traitees, result.ignorees, result.details));
      const docResultState = buildDocumentActionResult(result.document, docKind);
      if (docResultState) setDocResult(docResultState);
      setDeptDialog(null);
      setDeptMotif('');
    } catch (err) {
      void msgBox.error(apiErrorMessage(err, "Échec de l'action département."));
    } finally {
      setBusyKey(null);
    }
  };

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

  if (!canAccess) {
    return (
      <Box>
        <PageHeader
          title="Soumissions budgétaires"
          breadcrumbs={[
            { label: BRAND_NAME, to: '/dashboard' },
            { label: 'Budget' },
            { label: 'Soumissions' },
          ]}
        />
        <ErrorState
          title="Accès refusé"
          message="Permissions versions.controler, versions.valider ou versions.rejeter requises."
        />
      </Box>
    );
  }

  const deptActionTitle =
    deptDialog?.action === 'controler'
      ? 'Contrôler le département'
      : deptDialog?.action === 'valider'
        ? 'Valider le département'
        : 'Rejeter le département';

  return (
    <Box>
      <PageHeader
        title="Soumissions budgétaires"
        subtitle="Traitement par Département (masse) et par UB — montants en USD."
        breadcrumbs={[
          { label: BRAND_NAME, to: '/dashboard' },
          { label: 'Budget' },
          { label: 'Soumissions budgétaires' },
        ]}
      />

      <KpiRow columns={5}>
        {(
          [
            ['DÉPARTEMENTS', syntheseGlobale.nbDepartements],
            ['UB', syntheseGlobale.nbUb],
            ['VALIDÉES', syntheseGlobale.validees],
            ['REJETÉES', syntheseGlobale.rejetees],
            ['À CONTRÔLER', syntheseGlobale.soumises],
          ] as const
        ).map(([label, value]) => (
          <StatCard key={label} title={label} value={String(value)} />
        ))}
      </KpiRow>

      <Paper sx={{ p: 1.5, mb: 2 }}>
        <Stack
          direction={{ xs: 'column', md: 'row' }}
          spacing={2}
          sx={{ justifyContent: 'space-between', flexWrap: 'wrap' }}
        >
          <Box>
            <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 650 }}>
              TOTAL BUDGET (filtre courant)
            </Typography>
            <Stack direction="row" spacing={2} useFlexGap sx={{ flexWrap: 'wrap', mt: 0.5 }}>
              <Typography variant="body2">DC {formatMontantUsd(syntheseGlobale.montantDC)}</Typography>
              <Typography variant="body2">AE {formatMontantUsd(syntheseGlobale.montantAE)}</Typography>
              <Typography variant="body2">BI {formatMontantUsd(syntheseGlobale.montantBI)}</Typography>
              <Typography variant="body2" sx={{ fontWeight: 700 }}>
                TOTAL {formatMontantUsd(syntheseGlobale.montantTotal)}
              </Typography>
            </Stack>
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
          columns={{ xs: 1, sm: 2, md: 3, lg: 3, xl: 3 }}
          search={
            <TextField
              size="small"
              fullWidth
              label="Recherche UB"
              value={searchUb}
              onChange={(e) => setSearchUb(e.target.value)}
            />
          }
        >
          <SearchableSelect
            label="Exercice"
            value={idExercice}
            onChange={(v) => {
              setIdExercice(v);
              setIdVersion('');
            }}
            fullWidth
            options={exercices.map((ex) => ({
              value: String(ex.idExercice),
              label: String(ex.annee),
            }))}
          />
          <SearchableSelect
            label="Version"
            value={idVersion}
            onChange={setIdVersion}
            allowEmpty
            fullWidth
            options={[
              { value: '', label: 'Toutes' },
              ...versionsFiltrees.map((v) => ({
                value: String(v.idVersion),
                label: `${versionLabel(v.numeroVersion, v.libelle)}${v.statut ? ` · ${normalizeStatut(v.statut)}` : ''}`,
              })),
            ]}
          />
          <SearchableSelect
            label="Département"
            value={idDepartement}
            onChange={setIdDepartement}
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
        </FilterZone>
        <Stack direction="row" spacing={0.75} useFlexGap sx={{ flexWrap: 'wrap', mt: 1.25 }}>
          {(
            [
              ['TOUTES', 'Toutes'],
              ['SOUMISE', 'À contrôler'],
              ['CONTROLEE', 'Contrôlées'],
              ['VALIDEE', 'Validées'],
              ['REJETEE', 'Rejetées'],
              ['BROUILLON', 'Brouillons'],
            ] as const
          ).map(([key, label]) => (
            <Chip
              key={key}
              size="small"
              label={label}
              clickable
              color={filtreRapide === key ? 'primary' : 'default'}
              variant={filtreRapide === key ? 'filled' : 'outlined'}
              onClick={() => setFiltreRapide(key)}
            />
          ))}
        </Stack>
      </Paper>

      {error && <ErrorState title="Erreur" message={error} onRetry={() => void load()} />}
      {loading && <LoadingState label="Chargement des soumissions…" />}

      {!loading && data && groupes.length === 0 && (
        <Paper sx={{ p: 3 }}>
          <Typography color="text.secondary" sx={{ textAlign: 'center' }}>
            Aucune soumission pour ces filtres.
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
                      {versionLabel(g.numeroVersion)} · {g.nbUb} UB
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
                      {g.validees > 0 && (
                        <Chip size="small" label={`${g.validees} Validées`} sx={statutChipSx('VALIDEE')} />
                      )}
                      {g.controlees > 0 && (
                        <Chip size="small" label={`${g.controlees} Contrôlées`} sx={statutChipSx('CONTROLEE')} />
                      )}
                      {g.soumises > 0 && (
                        <Chip size="small" label={`${g.soumises} Soumises`} sx={statutChipSx('SOUMISE')} />
                      )}
                      {g.rejetees > 0 && (
                        <Chip size="small" label={`${g.rejetees} Rejetées`} sx={statutChipSx('REJETEE')} />
                      )}
                      {g.brouillons > 0 && (
                        <Chip size="small" label={`${g.brouillons} Brouillons`} sx={statutChipSx('BROUILLON')} />
                      )}
                    </Stack>
                    <Stack direction="row" spacing={0.75} useFlexGap sx={{ flexWrap: 'wrap', mt: 1.25 }}>
                      {canControlerDept && g.nbControllables > 0 && (
                        <Button
                          size="small"
                          variant="contained"
                          disabled={Boolean(busyKey)}
                          onClick={() => {
                            setDeptDialog({ group: g, action: 'controler' });
                            setDeptMotif('');
                            setDeptError(null);
                          }}
                        >
                          Contrôler le département
                        </Button>
                      )}
                      {canValiderDept && g.nbValidables > 0 && (
                        <Button
                          size="small"
                          variant="contained"
                          disabled={Boolean(busyKey)}
                          onClick={() => {
                            setDeptDialog({ group: g, action: 'valider' });
                            setDeptMotif('');
                            setDeptError(null);
                          }}
                        >
                          Valider le département
                        </Button>
                      )}
                      {canRejeterDept && g.nbRejetables > 0 && (
                        <Button
                          size="small"
                          color="error"
                          variant="outlined"
                          disabled={Boolean(busyKey)}
                          onClick={() => {
                            setDeptDialog({ group: g, action: 'rejeter' });
                            setDeptMotif('');
                            setDeptError(null);
                          }}
                        >
                          Rejeter le département
                        </Button>
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
                        <TableCell>UB</TableCell>
                        <TableCell align="right" sx={CELLULE_DETAIL}>
                          DC
                        </TableCell>
                        <TableCell align="right" sx={CELLULE_DETAIL}>
                          AE
                        </TableCell>
                        <TableCell align="right" sx={CELLULE_DETAIL}>
                          BI
                        </TableCell>
                        <TableCell align="right">Total</TableCell>
                        <TableCell>Statut</TableCell>
                        <TableCell sx={CELLULE_DETAIL}>Date soumission</TableCell>
                        <TableCell sx={CELLULE_DETAIL}>Soumis par</TableCell>
                        <TableCell align="right">Action</TableCell>
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {g.lignes.map((row) => {
                        const rowBusy =
                          busyKey === `c-${row.idVersion}-${row.idUB}` ||
                          busyKey === `v-${row.idVersion}-${row.idUB}` ||
                          busyKey === `r-${row.idVersion}-${row.idUB}`;
                        return (
                          <TableRow key={`${row.idVersion}-${row.idUB}`} hover>
                            <TableCell>
                              <Typography variant="body2" sx={{ fontWeight: 650 }}>
                                {row.codeUB}
                              </Typography>
                              <Typography variant="caption" color="text.secondary">
                                {row.libelleUB}
                              </Typography>
                            </TableCell>
                            <TableCell align="right" sx={CELLULE_DETAIL}>
                              <MontantCell value={row.montantDC} />
                            </TableCell>
                            <TableCell align="right" sx={CELLULE_DETAIL}>
                              <MontantCell value={row.montantAE} />
                            </TableCell>
                            <TableCell align="right" sx={CELLULE_DETAIL}>
                              <MontantCell value={row.montantBI} />
                            </TableCell>
                            <TableCell align="right" sx={{ fontWeight: 700 }}>
                              <MontantCell value={row.montantTotal} />
                            </TableCell>
                            <TableCell>
                              <Chip
                                size="small"
                                label={normalizeStatut(row.statut)}
                                variant="outlined"
                                sx={{ height: 22, fontWeight: 700, ...statutChipSx(row.statut) }}
                              />
                            </TableCell>
                            <TableCell sx={CELLULE_DETAIL}>{formatDateSuivi(row.dateSoumission)}</TableCell>
                            <TableCell sx={CELLULE_DETAIL}>
                              <Typography variant="body2">{row.nomUtilisateurSoumission ?? '—'}</Typography>
                            </TableCell>
                            <TableCell align="right">
                              <Stack
                                direction="row"
                                spacing={0.75}
                                useFlexGap
                                sx={{ flexWrap: 'wrap', justifyContent: 'flex-end' }}
                              >
                                {peutControlerUb(row.statut, user) && (
                                  <Button
                                    size="small"
                                    variant="contained"
                                    disabled={rowBusy}
                                    onClick={() => void runControler(row)}
                                  >
                                    Contrôler
                                  </Button>
                                )}
                                {peutValiderUb(row.statut, user) && (
                                  <Button
                                    size="small"
                                    variant="contained"
                                    disabled={rowBusy}
                                    onClick={() => void runValider(row)}
                                  >
                                    Valider
                                  </Button>
                                )}
                                {peutRejeterUb(row.statut, user) && (
                                  <Button
                                    size="small"
                                    color="error"
                                    variant="outlined"
                                    disabled={rowBusy}
                                    onClick={() => {
                                      setRejectUb(row);
                                      setRejectUbMotif('');
                                      setRejectUbError(null);
                                    }}
                                  >
                                    Rejeter
                                  </Button>
                                )}
                                <SecondaryButton
                                  size="small"
                                  onClick={() =>
                                    navigate(`/budget/soumissions/${row.idVersion}/${row.idUB}`)
                                  }
                                >
                                  Voir
                                </SecondaryButton>
                              </Stack>
                            </TableCell>
                          </TableRow>
                        );
                      })}
                    </TableBody>
                  </Table>
                </TableContainer>
              </Collapse>
            </Paper>
          );
        })}

      <Dialog open={Boolean(rejectUb)} onClose={() => !busyKey && setRejectUb(null)} fullWidth maxWidth="sm">
        <DialogTitle>Rejeter la prévision</DialogTitle>
        <DialogContent>
          {rejectUb && (
            <Stack spacing={1} sx={{ mb: 2, mt: 0.5 }}>
              <Typography variant="body2">
                Département : <strong>{rejectUb.libelleDepartement}</strong>
              </Typography>
              <Typography variant="body2">
                UB : <strong>{rejectUb.codeUB}</strong> — {rejectUb.libelleUB}
              </Typography>
              <Typography variant="body2">DC : {formatMontantUsd(rejectUb.montantDC)}</Typography>
              <Typography variant="body2">AE : {formatMontantUsd(rejectUb.montantAE)}</Typography>
              <Typography variant="body2">BI : {formatMontantUsd(rejectUb.montantBI)}</Typography>
              <Typography variant="body2" sx={{ fontWeight: 700 }}>
                Total : {formatMontantUsd(rejectUb.montantTotal)}
              </Typography>
            </Stack>
          )}
          <TextField
            autoFocus
            fullWidth
            multiline
            minRows={3}
            label="Motif du rejet *"
            value={rejectUbMotif}
            onChange={(e) => setRejectUbMotif(e.target.value)}
            error={Boolean(rejectUbError)}
            helperText={rejectUbError}
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setRejectUb(null)} disabled={Boolean(busyKey)}>
            Annuler
          </Button>
          <Button color="error" variant="contained" disabled={Boolean(busyKey)} onClick={() => void runRejeterUb()}>
            Rejeter
          </Button>
        </DialogActions>
      </Dialog>

      <Dialog
        open={Boolean(deptDialog)}
        onClose={() => !busyKey && setDeptDialog(null)}
        fullWidth
        maxWidth="sm"
      >
        <DialogTitle>{deptActionTitle}</DialogTitle>
        <DialogContent>
          {deptDialog && (
            <Stack spacing={1} sx={{ mb: 2, mt: 0.5 }}>
              <Typography variant="body2">
                Département : <strong>{deptDialog.group.libelleDepartement}</strong>
              </Typography>
              <Typography variant="body2">
                Version : {versionLabel(deptDialog.group.numeroVersion)}
              </Typography>
              <Typography variant="body2">
                UB concernées :{' '}
                <strong>{concernesPourAction(deptDialog.group, deptDialog.action)}</strong> /{' '}
                {deptDialog.group.nbUb}
              </Typography>
              <Typography variant="body2">DC : {formatMontantUsd(deptDialog.group.montantDC)}</Typography>
              <Typography variant="body2">AE : {formatMontantUsd(deptDialog.group.montantAE)}</Typography>
              <Typography variant="body2">BI : {formatMontantUsd(deptDialog.group.montantBI)}</Typography>
              <Typography variant="body2" sx={{ fontWeight: 700 }}>
                TOTAL : {formatMontantUsd(deptDialog.group.montantTotal)}
              </Typography>
              {deptDialog.action === 'controler' && (
                <Typography variant="caption" color="text.secondary">
                  Seules les UB SOUMISES passeront en CONTROLEE. VALIDEE / REJETEE / BROUILLON inchangées.
                </Typography>
              )}
              {deptDialog.action === 'valider' && (
                <Typography variant="caption" color="text.secondary">
                  Seules les UB CONTROLEES seront validées. Les SOUMISES ne sont pas validées automatiquement.
                </Typography>
              )}
              {deptDialog.action === 'rejeter' && (
                <Typography variant="caption" color="text.secondary">
                  SOUMISE et CONTROLEE → REJETEE. Les UB déjà VALIDÉES ne seront pas touchées.
                </Typography>
              )}
            </Stack>
          )}
          {deptDialog?.action === 'rejeter' && (
            <TextField
              autoFocus
              fullWidth
              multiline
              minRows={3}
              label="Motif du rejet *"
              value={deptMotif}
              onChange={(e) => setDeptMotif(e.target.value)}
              error={Boolean(deptError)}
              helperText={deptError}
            />
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDeptDialog(null)} disabled={Boolean(busyKey)}>
            Annuler
          </Button>
          <Button
            color={deptDialog?.action === 'rejeter' ? 'error' : 'primary'}
            variant="contained"
            disabled={Boolean(busyKey)}
            onClick={() => void runDeptAction()}
          >
            {deptActionTitle}
          </Button>
        </DialogActions>
      </Dialog>

      <DocumentActionResultDialog
        open={!!docResult}
        state={docResult}
        onClose={() => setDocResult(null)}
      />
    </Box>
  );
}
