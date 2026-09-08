import AddIcon from '@mui/icons-material/Add';
import EditOutlinedIcon from '@mui/icons-material/EditOutlined';
import RestartAltIcon from '@mui/icons-material/RestartAlt';
import SaveOutlinedIcon from '@mui/icons-material/SaveOutlined';
import SearchIcon from '@mui/icons-material/Search';
import ReorderIcon from '@mui/icons-material/Reorder';
import {
  Alert,
  Autocomplete,
  Box,
  Button,
  Chip,
  Collapse,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  InputAdornment,
  Paper,
  Stack,
  Tab,
  Tabs,
  TextField,
  Typography,
} from '@mui/material';
import { useCallback, useDeferredValue, useEffect, useMemo, useRef, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import {
  ErrorState,
  FilterFields,
  LoadingState,
  PageHeader,
  PrimaryButton,
  SecondaryButton,
  SearchableSelect,
  AmountField,
  useMsgBox,
} from '../../components';
import {
  createGroupeItemAE,
  controlerPrevisionUb,
  fetchExercices,
  fetchGroupesItemAE,
  fetchGroupesRubriquesBudgetaires,
  fetchItemsBI,
  fetchModesPrevision,
  fetchActionsExploitation,
  fetchPrevisionGrillePage,
  fetchPrevisionsBudgetaires,
  fetchTypesBudget,
  fetchUnitesBudgetaires,
  fetchVersionsBudgetaires,
  rejeterPrevisionUb,
  reouvrirPrevisionUb,
  annulerSoumissionPrevisionUb,
  sauvegarderGrillePrevision,
  soumettrePrevisionUb,
  validerPrevisionUb,
  type GroupeItemAE,
  type GroupeRubriqueBudgetaire,
  type ItemBI,
  type ModePrevision,
  type TypeBudget,
  type UniteBudgetaire,
  type VersionBudgetaire,
  type WorkflowPrevisionUb,
  type Exercice,
} from '../../services/apiClient';
import { BRAND_NAME } from '../../theme';
import { useAuth } from '../auth';
import { ClassementAeDialog } from './ClassementAeDialog';
import {
  DocumentActionResultDialog,
  buildDocumentActionResult,
  type DocumentActionResultState,
} from '../documents-previsions';
import { formatGroupeNiveau1 } from '../rubriques-budgetaires/hierarchy';
import { apiErrorMessage } from '../rubriques-budgetaires/rubriqueUtils';
import { ouvrirViaGrillePrevisions } from '../suivi-previsions/suiviUtils';
import { PrevisionGrilleVirtual, type DraftLigne } from './PrevisionGrilleVirtual';
import { getCachedReferentiel } from './referentielCache';
import {
  agregatSections,
  filtrerGroupesReplies,
  contexteComplet,
  cumulRepartitions,
  formatMontantBudget,
  hasRepartitionNonVide,
  montantAnnuelAffiche,
  parseMontantInput,
  repartirMontantSur12Mois,
  resolveContexteDepuisPrevisions,
  setMoisMontant,
} from './previsionUtils';

const PAGE_SIZE = 100;
const CONTEXTE_OUVERT_KEY = 'budget-previsions-contexte-ouvert';
const ONGLETS_TYPE = ['DC', 'AE', 'BI'] as const;
type OngletTypeBudget = (typeof ONGLETS_TYPE)[number];

function normalizeStatut(statut: string): string {
  return (statut ?? '').trim().toUpperCase();
}

function statutChipSx(statut: string): { bgcolor: string; color: string; borderColor: string } {
  switch (normalizeStatut(statut)) {
    case 'BROUILLON':
      return {
        bgcolor: 'var(--ef-surface-secondary)',
        color: 'var(--ef-text-secondary)',
        borderColor: 'var(--ef-border)',
      };
    case 'SOUMISE':
      return {
        bgcolor: 'var(--ef-info-soft)',
        color: 'var(--ef-primary)',
        borderColor: 'var(--ef-border)',
      };
    case 'CONTROLEE':
      return {
        bgcolor: 'var(--ef-warning-soft)',
        color: 'var(--ef-warning)',
        borderColor: 'var(--ef-border)',
      };
    case 'VALIDEE':
      return {
        bgcolor: 'var(--ef-success-soft)',
        color: 'var(--ef-success)',
        borderColor: 'var(--ef-border)',
      };
    case 'REJETEE':
      return {
        bgcolor: 'var(--ef-danger-soft)',
        color: 'var(--ef-danger)',
        borderColor: 'var(--ef-border)',
      };
    default:
      return {
        bgcolor: 'var(--ef-surface-secondary)',
        color: 'var(--ef-text-secondary)',
        borderColor: 'var(--ef-border)',
      };
  }
}

function hasPerm(user: { permissions?: string[]; roles?: string[] } | null | undefined, permission: string): boolean {
  if (!user) return false;
  if (user.roles?.some((r) => r.toLowerCase().includes('admin'))) return true;
  return (user.permissions ?? []).some((p) => p === permission || p === 'admin.all');
}

function mergePages(prev: DraftLigne[], incoming: DraftLigne[]): DraftLigne[] {
  const keys = new Set(
    prev.map(
      (l) =>
        `${l.idGroupeRB ?? ''}|${l.estSection ? 'G' : 'R'}|${l.idRB ?? ''}|${l.detailBI ?? ''}|${l.idPrevision ?? ''}`,
    ),
  );
  const extra = incoming.filter(
    (l) =>
      !keys.has(
        `${l.idGroupeRB ?? ''}|${l.estSection ? 'G' : 'R'}|${l.idRB ?? ''}|${l.detailBI ?? ''}|${l.idPrevision ?? ''}`,
      ),
  );
  return [...prev, ...extra];
}

function cacheKey(parts: {
  idVersion: string;
  idTypeBudget: string;
  idModePrevision: string;
  idUB: string;
  actionAE: string;
  idItemBI: string;
  search: string;
  filtre: string;
  idGroupeRB: string;
  page: number;
}): string {
  return [
    parts.idVersion,
    parts.idTypeBudget,
    parts.idModePrevision,
    parts.idUB,
    parts.actionAE,
    parts.idItemBI,
    parts.search,
    parts.filtre,
    parts.idGroupeRB,
    parts.page,
  ].join('|');
}

function formatUbOption(u: UniteBudgetaire): string {
  return `${u.codeUB} — ${u.libelle}`;
}

export function PrevisionsBudgetairesPage() {
  const { user } = useAuth();
  const msgBox = useMsgBox();
  const [searchParams] = useSearchParams();
  const [exercices, setExercices] = useState<Exercice[]>([]);
  const [versions, setVersions] = useState<VersionBudgetaire[]>([]);
  const [types, setTypes] = useState<TypeBudget[]>([]);
  const [modes, setModes] = useState<ModePrevision[]>([]);
  const [ubs, setUbs] = useState<UniteBudgetaire[]>([]);
  const [groupesRB, setGroupesRB] = useState<GroupeRubriqueBudgetaire[]>([]);
  const [itemsBI, setItemsBI] = useState<ItemBI[]>([]);
  const [groupesAE, setGroupesAE] = useState<GroupeItemAE[]>([]);
  const [actionsAEOptions, setActionsAEOptions] = useState<string[]>([]);

  const [idExercice, setIdExercice] = useState('');
  const [idVersion, setIdVersion] = useState('');
  const [idTypeBudget, setIdTypeBudget] = useState('');
  const [idModePrevision, setIdModePrevision] = useState('');
  const [idUB, setIdUB] = useState('');
  const [actionAE, setActionAE] = useState('');
  const [idGroupeAE, setIdGroupeAE] = useState('');
  const [idItemBI, setIdItemBI] = useState('');
  const [nouveauGroupe, setNouveauGroupe] = useState('');
  const [classementAeOpen, setClassementAeOpen] = useState(false);

  const [loadingRefs, setLoadingRefs] = useState(true);
  const [loadingGrille, setLoadingGrille] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [lignes, setLignes] = useState<DraftLigne[]>([]);
  const [statutVersion, setStatutVersion] = useState('');
  const [modifiable, setModifiable] = useState(false);
  const [codeMode, setCodeMode] = useState('ANNUEL');
  const [codeType, setCodeType] = useState('DC');
  const [nouveauDetailBI, setNouveauDetailBI] = useState('');
  const [search, setSearch] = useState('');
  const [filtre, setFiltre] = useState<'toutes' | 'avec' | 'sans'>('toutes');
  const [idGroupeFiltre, setIdGroupeFiltre] = useState('');
  const [groupesReplies, setGroupesReplies] = useState<Set<number>>(() => new Set());
  const [page, setPage] = useState(1);
  const [hasNextPage, setHasNextPage] = useState(false);
  const [totalCount, setTotalCount] = useState(0);
  const [loadingMore, setLoadingMore] = useState(false);
  const pageCacheRef = useRef(new Map<string, DraftLigne[]>());
  const deferredSearch = useDeferredValue(search);
  const deferredActionAE = useDeferredValue(actionAE);

  const [repartDialog, setRepartDialog] = useState<{
    idRB: number;
    codeRB: string;
    libelle: string;
    montantRaw: string;
  } | null>(null);
  const [repartError, setRepartError] = useState<string | null>(null);
  const [workflowBusy, setWorkflowBusy] = useState(false);
  const [rejectOpen, setRejectOpen] = useState(false);
  const [rejectMotif, setRejectMotif] = useState('');
  const [rejectError, setRejectError] = useState<string | null>(null);
  const [docResult, setDocResult] = useState<DocumentActionResultState | null>(null);
  const [contexteOuvert, setContexteOuvert] = useState(() => {
    try {
      return localStorage.getItem(CONTEXTE_OUVERT_KEY) === '1';
    } catch {
      return false;
    }
  });

  const setContexteVisible = (ouvert: boolean) => {
    setContexteOuvert(ouvert);
    try {
      localStorage.setItem(CONTEXTE_OUVERT_KEY, ouvert ? '1' : '0');
    } catch {
      /* ignore */
    }
  };

  const versionsFiltrees = useMemo(
    () => versions.filter((v) => String(v.idExercice) === idExercice),
    [versions, idExercice],
  );

  const typeCourant = types.find((t) => String(t.idTypeBudget) === idTypeBudget);
  const codeTypeSel = (typeCourant?.codeType ?? codeType).toUpperCase();
  const navigationParOnglets = ouvrirViaGrillePrevisions(user);
  const ongletTypeActif: OngletTypeBudget = ONGLETS_TYPE.includes(codeTypeSel as OngletTypeBudget)
    ? (codeTypeSel as OngletTypeBudget)
    : 'DC';

  const ready = contexteComplet({
    idExercice,
    idVersion,
    idTypeBudget,
    idModePrevision,
    idUB,
    codeType: codeTypeSel,
    actionAE: deferredActionAE,
    idItemBI,
  });

  const contexteKeyBase = useMemo(
    () => ({
      idVersion,
      idTypeBudget,
      idModePrevision,
      idUB,
      actionAE: codeTypeSel === 'AE' ? deferredActionAE.trim() : '',
      idItemBI: codeTypeSel === 'BI' ? idItemBI : '',
      search: deferredSearch.trim().toLowerCase(),
      filtre,
      idGroupeRB: idGroupeFiltre,
    }),
    [
      idVersion,
      idTypeBudget,
      idModePrevision,
      idUB,
      codeTypeSel,
      deferredActionAE,
      idItemBI,
      deferredSearch,
      filtre,
      idGroupeFiltre,
    ],
  );

  const ubSelectionnee = useMemo(
    () => ubs.find((u) => String(u.idUB) === idUB) ?? null,
    [ubs, idUB],
  );

  const groupesRBOrdonnes = useMemo(
    () =>
      [...groupesRB]
        .filter((g) => g.actif)
        .sort((a, b) => {
          if (a.ordreAffichage !== b.ordreAffichage) return a.ordreAffichage - b.ordreAffichage;
          const byCode = a.codeGroupe.localeCompare(b.codeGroupe, 'fr');
          if (byCode !== 0) return byCode;
          return a.libelle.localeCompare(b.libelle, 'fr');
        }),
    [groupesRB],
  );

  const loadRefs = useCallback(async () => {
    setLoadingRefs(true);
    setError(null);
    try {
      // Référentiels stables en cache session ; items-bi / groupes-ae chargés à la demande (type BI/AE).
      const [ex, ver, ty, mo, ub, grRb] = await Promise.all([
        getCachedReferentiel('exercices', fetchExercices),
        getCachedReferentiel('versions', fetchVersionsBudgetaires),
        getCachedReferentiel('types', fetchTypesBudget),
        getCachedReferentiel('modes', fetchModesPrevision),
        getCachedReferentiel('ubs-accessibles', () => fetchUnitesBudgetaires({ accessibles: true })),
        getCachedReferentiel('groupes-rb', fetchGroupesRubriquesBudgetaires),
      ]);
      setExercices(ex);
      setVersions(ver);
      setTypes(ty.filter((t) => t.actif));
      setModes(mo.filter((m) => m.actif));
      setUbs(ub.filter((u) => u.actif));
      setGroupesRB(grRb);
      const qEx = searchParams.get('exercice');
      const qVer = searchParams.get('version');
      const qUb = searchParams.get('ub');
      const qType = searchParams.get('type');
      const qMode = searchParams.get('mode');
      if (qEx && ex.some((e) => String(e.idExercice) === qEx)) setIdExercice(qEx);
      else if (ex[0]) setIdExercice(String(ex[0].idExercice));
      if (qVer && ver.some((v) => String(v.idVersion) === qVer)) setIdVersion(qVer);
      if (qUb && ub.some((u) => String(u.idUB) === qUb)) setIdUB(qUb);

      const typesActifs = ty.filter((t) => t.actif);
      const modesActifs = mo.filter((m) => m.actif);
      const typeFromUrl =
        qType && typesActifs.some((t) => String(t.idTypeBudget) === qType) ? qType : '';
      const modeFromUrl =
        qMode && modesActifs.some((m) => String(m.idModePrevision) === qMode) ? qMode : '';
      const deepLinkUb = Boolean(qVer && qUb);
      const appliquerDefauts = () => {
        if (typesActifs[0]) setIdTypeBudget(String(typesActifs[0].idTypeBudget));
        if (modesActifs[0]) setIdModePrevision(String(modesActifs[0].idModePrevision));
      };

      if (typeFromUrl) setIdTypeBudget(typeFromUrl);
      if (modeFromUrl) setIdModePrevision(modeFromUrl);

      if (deepLinkUb && !(typeFromUrl && modeFromUrl)) {
        try {
          let previsions = await fetchPrevisionsBudgetaires({
            idVersion: Number(qVer),
            idUB: Number(qUb),
          });
          if (typeFromUrl) {
            previsions = previsions.filter((p) => String(p.idTypeBudget) === typeFromUrl);
          }
          const ctx = resolveContexteDepuisPrevisions(previsions);
          if (ctx) {
            if (!typeFromUrl) setIdTypeBudget(String(ctx.idTypeBudget));
            if (!modeFromUrl) setIdModePrevision(String(ctx.idModePrevision));
            setCodeType(ctx.codeType);
            setCodeMode(ctx.codeMode);
            if (ctx.codeType === 'AE' && ctx.libelleItemAE?.trim()) {
              setActionAE(ctx.libelleItemAE.trim());
            } else {
              setActionAE('');
            }
            if (ctx.codeType === 'BI' && ctx.idItemBI) {
              setIdItemBI(String(ctx.idItemBI));
            } else {
              setIdItemBI('');
            }
            setIdGroupeAE(ctx.idGroupeItemAE ? String(ctx.idGroupeItemAE) : '');
          } else if (!typeFromUrl || !modeFromUrl) {
            if (!typeFromUrl && typesActifs[0]) setIdTypeBudget(String(typesActifs[0].idTypeBudget));
            if (!modeFromUrl && modesActifs[0]) setIdModePrevision(String(modesActifs[0].idModePrevision));
          }
        } catch {
          if (!typeFromUrl || !modeFromUrl) appliquerDefauts();
        }
      } else if (!deepLinkUb) {
        appliquerDefauts();
      } else if (!typeFromUrl || !modeFromUrl) {
        appliquerDefauts();
      }
    } catch (err) {
      setError(apiErrorMessage(err, 'Impossible de charger les référentiels de prévision.'));
    } finally {
      setLoadingRefs(false);
    }
  }, [searchParams]);

  useEffect(() => {
    void loadRefs();
  }, [loadRefs]);

  useEffect(() => {
    if (!idExercice) return;
    const belongs =
      idVersion && versions.some((v) => String(v.idVersion) === idVersion && String(v.idExercice) === idExercice);
    if (belongs) return;
    const first = versions.find((v) => String(v.idExercice) === idExercice);
    setIdVersion(first ? String(first.idVersion) : '');
  }, [idExercice, versions, idVersion]);

  const switchTypeOnglet = useCallback(
    async (code: OngletTypeBudget) => {
      const type = types.find((t) => t.codeType.toUpperCase() === code);
      if (!type) return;

      setIdTypeBudget(String(type.idTypeBudget));
      setCodeType(code);

      if (code === 'DC') {
        setActionAE('');
        setIdItemBI('');
        setIdGroupeAE('');
      } else if (code === 'AE') {
        setIdItemBI('');
      } else {
        setActionAE('');
        setIdGroupeAE('');
      }

      if (!idVersion || !idUB) return;

      try {
        let previsions = await fetchPrevisionsBudgetaires({
          idVersion: Number(idVersion),
          idUB: Number(idUB),
        });
        previsions = previsions.filter((p) => p.codeType.toUpperCase() === code);
        const ctx = resolveContexteDepuisPrevisions(previsions);
        if (!ctx) return;

        setIdModePrevision(String(ctx.idModePrevision));
        setCodeMode(ctx.codeMode);
        if (code === 'AE' && ctx.libelleItemAE?.trim()) {
          setActionAE(ctx.libelleItemAE.trim());
        }
        if (code === 'BI' && ctx.idItemBI) {
          setIdItemBI(String(ctx.idItemBI));
        }
        setIdGroupeAE(ctx.idGroupeItemAE ? String(ctx.idGroupeItemAE) : '');
      } catch {
        /* conserve le mode courant si la résolution échoue */
      }
    },
    [types, idVersion, idUB],
  );

  useEffect(() => {
    if (codeTypeSel !== 'BI') return;
    let cancelled = false;
    void (async () => {
      try {
        const it = await getCachedReferentiel('items-bi', fetchItemsBI);
        if (!cancelled) setItemsBI(it.filter((i) => i.actif));
      } catch {
        if (!cancelled) setItemsBI([]);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [codeTypeSel]);

  useEffect(() => {
    if (codeTypeSel !== 'AE') return;
    let cancelled = false;
    void (async () => {
      try {
        const gr = await getCachedReferentiel('groupes-ae', fetchGroupesItemAE);
        if (!cancelled) setGroupesAE(gr.filter((g) => g.actif));
      } catch {
        if (!cancelled) setGroupesAE([]);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [codeTypeSel]);

  useEffect(() => {
    if (codeTypeSel !== 'AE' || !idExercice) {
      setActionsAEOptions([]);
      return;
    }
    let cancelled = false;
    void (async () => {
      try {
        const actions = await fetchActionsExploitation({ idExercice: Number(idExercice) });
        if (!cancelled) setActionsAEOptions(actions);
      } catch {
        if (!cancelled) setActionsAEOptions([]);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [codeTypeSel, idExercice]);

  // Reset pagination / cache when contexte métier change.
  useEffect(() => {
    setPage(1);
    setLignes([]);
    setHasNextPage(false);
    setTotalCount(0);
    pageCacheRef.current.clear();
  }, [contexteKeyBase]);

  const chargerPage = useCallback(
    async (pageToLoad: number, append: boolean) => {
      if (!ready) {
        setLignes([]);
        return;
      }

      const key = cacheKey({ ...contexteKeyBase, page: pageToLoad });
      const cached = pageCacheRef.current.get(key);
      if (cached) {
        setLignes((prev) => (append ? mergePages(prev, cached) : cached));
        setPage(pageToLoad);
        return;
      }

      if (append) setLoadingMore(true);
      else setLoadingGrille(true);
      setError(null);

      try {
        const grille = await fetchPrevisionGrillePage({
          idVersion: Number(idVersion),
          idTypeBudget: Number(idTypeBudget),
          idModePrevision: Number(idModePrevision),
          idUB: Number(idUB),
          libelleItemAE: codeTypeSel === 'AE' ? deferredActionAE.trim() : undefined,
          idGroupeItemAE: idGroupeAE ? Number(idGroupeAE) : null,
          idItemBI: codeTypeSel === 'BI' ? Number(idItemBI) : null,
          page: pageToLoad,
          pageSize: PAGE_SIZE,
          search: deferredSearch.trim() || undefined,
          filtre,
          idGroupeRB: idGroupeFiltre ? Number(idGroupeFiltre) : null,
        });

        const mapped = grille.lignes.map((l) => {
          const reps = l.repartitions ?? [];
          const montant = montantAnnuelAffiche(l.montantAnnuel, reps);
          return {
            ...l,
            montantAnnuel: montant,
            cumulMensuel: cumulRepartitions(reps),
            repartitions: reps,
            dirty: false,
          };
        });
        pageCacheRef.current.set(key, mapped);
        setLignes((prev) => (append ? mergePages(prev, mapped) : mapped));
        // Statut opérationnel Version×UB renvoyé par l'API grille (plus l'agrégat VERSION).
        const statutUb = grille.statutVersion;
        const s = normalizeStatut(statutUb);
        setStatutVersion(statutUb);
        setModifiable(s === 'BROUILLON' || s === 'REJETEE' || grille.modificationAutorisee);
        setCodeMode(grille.codeMode.toUpperCase());
        setCodeType(grille.codeType.toUpperCase());
        setHasNextPage(grille.hasNextPage);
        setTotalCount(grille.totalCount);
        setPage(pageToLoad);
      } catch (err) {
        if (!append) setLignes([]);
        setError(apiErrorMessage(err, 'Impossible de charger la grille de prévision.'));
      } finally {
        setLoadingGrille(false);
        setLoadingMore(false);
      }
    },
    [
      ready,
      contexteKeyBase,
      idVersion,
      idTypeBudget,
      idModePrevision,
      idUB,
      codeTypeSel,
      deferredActionAE,
      idGroupeAE,
      idItemBI,
      deferredSearch,
      filtre,
      idGroupeFiltre,
    ],
  );

  const chargerGrille = useCallback(async () => {
    pageCacheRef.current.clear();
    setPage(1);
    await chargerPage(1, false);
  }, [chargerPage]);

  useEffect(() => {
    if (!ready) {
      setLignes([]);
      return;
    }
    setGroupesReplies(new Set());
    void chargerPage(1, false);
  }, [ready, contexteKeyBase, chargerPage]);

  const loadMore = useCallback(() => {
    if (!hasNextPage || loadingMore || loadingGrille) return;
    void chargerPage(page + 1, true);
  }, [hasNextPage, loadingMore, loadingGrille, chargerPage, page]);

  const toggleGroupe = useCallback((idGroupeRB: number) => {
    setGroupesReplies((prev) => {
      const next = new Set(prev);
      if (next.has(idGroupeRB)) next.delete(idGroupeRB);
      else next.add(idGroupeRB);
      return next;
    });
  }, []);

  const setMontantAnnuel = (idRB: number | null, index: number, raw: string) => {
    const n = parseMontantInput(raw);
    if (n === null) return;
    setLignes((prev) => {
      const next = prev.map((l, i) => {
        const match = idRB != null ? l.idRB === idRB : i === index;
        if (!match) return l;
        // Mode ANNUEL : ne pas inventer de répartition ; conserver les mois existants tels quels.
        return { ...l, montantAnnuel: n, dirty: true };
      });
      return codeTypeSel === 'BI' ? next : agregatSections(next, false);
    });
  };

  const setMontantMois = (idRB: number | null, index: number, mois: number, raw: string) => {
    const n = parseMontantInput(raw);
    if (n === null) return;
    setLignes((prev) => {
      const next = prev.map((l, i) => {
        const match = idRB != null ? l.idRB === idRB : i === index;
        if (!match) return l;
        const reps = setMoisMontant(l.repartitions, mois, n);
        const cumul = cumulRepartitions(reps);
        // Cohérence : MontantAnnuel = SUM(mois) dès qu'une répartition est saisie.
        return { ...l, repartitions: reps, cumulMensuel: cumul, montantAnnuel: cumul, dirty: true };
      });
      return codeTypeSel === 'BI' ? next : agregatSections(next, codeMode === 'MENSUEL');
    });
  };

  const appliquerRepartition = (idRB: number, montant: number) => {
    const reps = repartirMontantSur12Mois(montant);
    if (reps === null) return;
    setLignes((prev) => {
      const next = prev.map((l) => {
        if (l.idRB !== idRB) return l;
        const cumul = cumulRepartitions(reps);
        return {
          ...l,
          repartitions: reps,
          cumulMensuel: cumul,
          montantAnnuel: cumul,
          dirty: true,
        };
      });
      return agregatSections(next, true);
    });
    setRepartDialog(null);
    setRepartError(null);
  };

  const ouvrirRepartition = (idRB: number) => {
    const ligne = lignes.find((l) => l.idRB === idRB);
    if (!ligne) return;
    const prefill =
      ligne.cumulMensuel > 0
        ? String(ligne.cumulMensuel)
        : ligne.montantAnnuel > 0
          ? String(ligne.montantAnnuel)
          : '';
    setRepartError(null);
    setRepartDialog({
      idRB,
      codeRB: ligne.codeRB ?? '',
      libelle: ligne.libelleRB ?? '',
      montantRaw: prefill,
    });
  };

  const demanderRepartition = async () => {
    if (!repartDialog) return;
    const n = parseMontantInput(repartDialog.montantRaw);
    if (n === null) {
      setRepartError('Montant invalide (négatif ou non numérique).');
      return;
    }
    const ligne = lignes.find((l) => l.idRB === repartDialog.idRB);
    if (ligne && hasRepartitionNonVide(ligne.repartitions)) {
      const ok = await msgBox.confirm({
        title: 'Remplacer la répartition ?',
        message:
          'Une répartition mensuelle existe déjà pour cette RB. La nouvelle répartition remplacera les montants actuels.',
        confirmLabel: 'Remplacer',
        cancelLabel: 'Annuler',
      });
      if (!ok) return;
    }
    appliquerRepartition(repartDialog.idRB, n);
  };

  const demanderEffacer = async (idRB: number) => {
    const ligne = lignes.find((l) => l.idRB === idRB);
    if (!ligne) return;
    if (!hasRepartitionNonVide(ligne.repartitions)) return;
    const ok = await msgBox.confirm({
      title: 'Effacer la répartition ?',
      message:
        'Les montants mensuels de cette RB seront effacés. Le montant annuel existant est conservé (répartition non définie).',
      confirmLabel: 'Effacer',
      cancelLabel: 'Annuler',
      danger: true,
    });
    if (!ok) return;
    setLignes((prev) => {
      const next = prev.map((l) =>
        l.idRB === idRB
          ? {
              ...l,
              repartitions: [],
              cumulMensuel: 0,
              dirty: true,
            }
          : l,
      );
      return agregatSections(next, true);
    });
  };

  const reinitialiserFiltresAffichage = () => {
    setSearch('');
    setFiltre('toutes');
    setIdGroupeFiltre('');
  };

  const ajouterDetailBI = () => {
    const detail = nouveauDetailBI.trim();
    if (!detail) return;
    if (lignes.some((l) => (l.detailBI ?? '').toLowerCase() === detail.toLowerCase())) {
      void msgBox.warning('Ce détail BI existe déjà dans la grille.');
      return;
    }
    setLignes((prev) => [
      ...prev,
      {
        idPrevision: null,
        idRB: null,
        codeRB: null,
        libelleRB: null,
        parentIdRB: null,
        niveauRB: null,
        estSection: false,
        detailBI: detail,
        montantAnnuel: 0,
        cumulMensuel: 0,
        repartitions: [],
        dirty: true,
        nouveau: true,
      },
    ]);
    setNouveauDetailBI('');
  };

  const creerGroupe = async () => {
    const libelle = nouveauGroupe.trim();
    if (!libelle) return;
    try {
      const created = await createGroupeItemAE(libelle);
      setGroupesAE((prev) => [...prev, created]);
      setIdGroupeAE(String(created.idGroupeItemAE));
      setNouveauGroupe('');
    } catch (err) {
      void msgBox.error(apiErrorMessage(err, 'Impossible de créer le groupe AE.'));
    }
  };

  const enregistrer = async () => {
    if (!ready) {
      void msgBox.warning('Complétez le contexte avant d’enregistrer.');
      return;
    }
    if (!user?.idUtilisateur) {
      void msgBox.error('Session utilisateur absente. Reconnectez-vous pour enregistrer.');
      return;
    }
    setSaving(true);
    try {
      const aSauver = lignes.filter((l) => {
        if (l.estSection) return false;
        if (l.nouveau && codeTypeSel === 'BI') return true;
        if (l.dirty) return true;
        return false;
      });

      if (aSauver.length === 0) {
        void msgBox.info('Aucune modification à enregistrer.');
        setSaving(false);
        return;
      }

      const result = await sauvegarderGrillePrevision({
        idVersion: Number(idVersion),
        idTypeBudget: Number(idTypeBudget),
        idModePrevision: Number(idModePrevision),
        idUB: Number(idUB),
        idItemBI: codeTypeSel === 'BI' ? Number(idItemBI) : null,
        idGroupeItemAE: codeTypeSel === 'AE' && idGroupeAE ? Number(idGroupeAE) : null,
        libelleItemAE: codeTypeSel === 'AE' ? actionAE.trim() : null,
        lignes: aSauver.map((l) => ({
          idPrevision: l.idPrevision,
          idRB: l.idRB,
          detailBI: l.detailBI,
          // Toujours envoyer le montant annuel (ne pas le remplacer par 0 si répartition vide).
          montantAnnuel: l.montantAnnuel,
          supprimer: false,
          // ANNUEL : ne pas envoyer de répartition (backend ne remplace pas).
          // MENSUEL : envoyer l'état local (vide = répartition non définie).
          repartitions: codeMode === 'MENSUEL' ? l.repartitions : undefined,
        })),
      });
      void msgBox.success(
        `Enregistrement terminé — créées : ${result.lignesCreees}, modifiées : ${result.lignesModifiees}.`,
      );
      if (codeTypeSel === 'AE') {
        const action = actionAE.trim();
        if (action) {
          setActionsAEOptions((prev) =>
            prev.includes(action) ? prev : [...prev, action].sort((a, b) => a.localeCompare(b, 'fr')),
          );
        } else if (idExercice) {
          try {
            setActionsAEOptions(await fetchActionsExploitation({ idExercice: Number(idExercice) }));
          } catch {
            /* ignore */
          }
        }
      }
      await chargerGrille();
    } catch (err) {
      void msgBox.error(apiErrorMessage(err, "Échec de l'enregistrement des prévisions."));
    } finally {
      setSaving(false);
    }
  };

  const totalGrille = useMemo(
    () =>
      lignes
        .filter((l) => !l.estSection || codeTypeSel === 'BI')
        .reduce((s, l) => s + (codeMode === 'MENSUEL' ? l.cumulMensuel : l.montantAnnuel), 0),
    [lignes, codeMode, codeTypeSel],
  );

  const dirtyCount = useMemo(() => lignes.filter((l) => l.dirty && !l.estSection).length, [lignes]);

  const statutNorm = normalizeStatut(statutVersion);
  const canSoumettre =
    ready &&
    (statutNorm === 'BROUILLON' || statutNorm === 'REJETEE') &&
    hasPerm(user, 'previsions.soumettre') &&
    !workflowBusy;
  const canAnnulerSoumission =
    ready && statutNorm === 'SOUMISE' && hasPerm(user, 'previsions.soumettre') && !workflowBusy;
  const canControler =
    ready && statutNorm === 'SOUMISE' && hasPerm(user, 'versions.controler') && !workflowBusy;
  const canValider =
    ready && statutNorm === 'CONTROLEE' && hasPerm(user, 'versions.valider') && !workflowBusy;
  const canRejeter =
    ready &&
    (statutNorm === 'SOUMISE' || statutNorm === 'CONTROLEE') &&
    hasPerm(user, 'versions.rejeter') &&
    !workflowBusy;
  const canModifierRejetee =
    ready && statutNorm === 'REJETEE' && hasPerm(user, 'previsions.ecrire') && !workflowBusy;

  const appliquerStatutUb = (wf: WorkflowPrevisionUb) => {
    setStatutVersion(wf.statut);
    setModifiable(['BROUILLON', 'REJETEE'].includes(normalizeStatut(wf.statut)));
  };

  const runWorkflowUb = async (
    action: () => Promise<WorkflowPrevisionUb>,
    successMsg: string,
    docKind?: 'SUB' | 'REJ' | 'CTL' | 'VAL',
  ) => {
    if (!idVersion || !idUB) return;
    setWorkflowBusy(true);
    try {
      const updated = await action();
      appliquerStatutUb(updated);
      void msgBox.success(successMsg);
      if (docKind) {
        const result = buildDocumentActionResult(updated.document, docKind);
        if (result) setDocResult(result);
      }
      await chargerGrille();
    } catch (err) {
      void msgBox.error(apiErrorMessage(err, 'Action workflow impossible.'));
    } finally {
      setWorkflowBusy(false);
    }
  };

  const confirmerRejet = async () => {
    const motif = rejectMotif.trim();
    if (!motif) {
      setRejectError('Le motif du rejet est obligatoire.');
      return;
    }
    setRejectError(null);
    setRejectOpen(false);
    await runWorkflowUb(
      () => rejeterPrevisionUb(Number(idVersion), Number(idUB), motif),
      'Prévision UB rejetée.',
      'REJ',
    );
    setRejectMotif('');
  };

  const lignesAffichees = useMemo(() => {
    if (codeTypeSel === 'BI') return lignes;
    const agregées = agregatSections(lignes, codeMode === 'MENSUEL');
    return filtrerGroupesReplies(agregées, groupesReplies);
  }, [lignes, codeMode, codeTypeSel, groupesReplies]);
  const contexteMeta = useMemo(() => {
    const ex = exercices.find((e) => String(e.idExercice) === idExercice);
    const ver = versionsFiltrees.find((v) => String(v.idVersion) === idVersion);
    const ty = types.find((t) => String(t.idTypeBudget) === idTypeBudget);
    const mo = modes.find((m) => String(m.idModePrevision) === idModePrevision);
    return {
      titre: `PRÉVISION ${ty?.codeType ?? codeTypeSel}`,
      annee: ex ? String(ex.annee) : '—',
      version: ver ? `V${ver.numeroVersion}` : '—',
      type: ty?.codeType ?? '—',
      mode: mo?.codeMode ?? '—',
      ub: ubSelectionnee ? formatUbOption(ubSelectionnee) : 'UB non sélectionnée',
    };
  }, [
    exercices,
    versionsFiltrees,
    types,
    modes,
    ubSelectionnee,
    idExercice,
    idVersion,
    idTypeBudget,
    idModePrevision,
    codeTypeSel,
  ]);

  const showContexteEdit = contexteOuvert || !ready;
  const canSave =
    ready && modifiable && hasPerm(user, 'previsions.ecrire') && dirtyCount > 0 && !saving;

  if (loadingRefs) return <LoadingState label="Chargement des référentiels…" />;

  return (
    <Box
      sx={{
        display: 'flex',
        flexDirection: 'column',
        height: { xs: 'auto', md: 'calc(100vh - 88px)' },
        minHeight: { md: 0 },
        gap: 1,
      }}
    >
      <PageHeader
        dense
        title="Prévisions budgétaires"
        subtitle="Saisie DC / Actions d'exploitation / BI — montants annuels ou mensuels"
        breadcrumbs={[
          { label: BRAND_NAME, to: '/dashboard' },
          { label: 'Budget', to: '/budget' },
          { label: 'Prévisions' },
        ]}
        actions={
          <>
            {codeTypeSel === 'AE' && idVersion && idUB && (
              <SecondaryButton
                size="small"
                startIcon={<ReorderIcon />}
                onClick={() => setClassementAeOpen(true)}
                disabled={!idVersion || !idUB || workflowBusy}
              >
                Ordre AE
              </SecondaryButton>
            )}
            <SecondaryButton size="small" onClick={() => void chargerGrille()} disabled={!ready || loadingGrille || workflowBusy}>
              Actualiser
            </SecondaryButton>
            {(statutNorm === 'BROUILLON' || statutNorm === 'REJETEE') && (
              <PrimaryButton
                size="small"
                startIcon={<SaveOutlinedIcon />}
                onClick={() => void enregistrer()}
                disabled={!canSave}
              >
                Enregistrer{dirtyCount > 0 ? ` (${dirtyCount})` : ''}
              </PrimaryButton>
            )}
            {statutNorm === 'REJETEE' && (
              <SecondaryButton
                size="small"
                startIcon={<EditOutlinedIcon />}
                disabled={!canModifierRejetee}
                onClick={() =>
                  void runWorkflowUb(
                    () => reouvrirPrevisionUb(Number(idVersion), Number(idUB)),
                    'Prévision UB réouverte en BROUILLON — saisie autorisée.',
                  )
                }
              >
                Modifier
              </SecondaryButton>
            )}
            {(statutNorm === 'BROUILLON' || statutNorm === 'REJETEE') && (
              <PrimaryButton
                size="small"
                disabled={!canSoumettre}
                onClick={() =>
                  void runWorkflowUb(
                    () => soumettrePrevisionUb(Number(idVersion), Number(idUB)),
                    'Prévision UB soumise.',
                    'SUB',
                  )
                }
              >
                Soumettre
              </PrimaryButton>
            )}
            {statutNorm === 'SOUMISE' && (
              <>
                {canAnnulerSoumission && (
                  <SecondaryButton
                    size="small"
                    disabled={!canAnnulerSoumission}
                    onClick={() => {
                      if (
                        !window.confirm(
                          'Annuler la soumission et repasser en brouillon pour modifier les prévisions ?',
                        )
                      ) {
                        return;
                      }
                      void runWorkflowUb(
                        () => annulerSoumissionPrevisionUb(Number(idVersion), Number(idUB)),
                        'Soumission annulée — la prévision est de nouveau modifiable.',
                      );
                    }}
                  >
                    Annuler la soumission
                  </SecondaryButton>
                )}
                <PrimaryButton
                  size="small"
                  disabled={!canControler}
                  onClick={() =>
                    void runWorkflowUb(
                      () => controlerPrevisionUb(Number(idVersion), Number(idUB)),
                      'Prévision UB contrôlée.',
                      'CTL',
                    )
                  }
                >
                  Contrôler
                </PrimaryButton>
                <SecondaryButton
                  size="small"
                  disabled={!canRejeter}
                  onClick={() => {
                    setRejectMotif('');
                    setRejectError(null);
                    setRejectOpen(true);
                  }}
                >
                  Rejeter
                </SecondaryButton>
              </>
            )}
            {statutNorm === 'CONTROLEE' && (
              <>
                <PrimaryButton
                  size="small"
                  disabled={!canValider}
                  onClick={() =>
                    void runWorkflowUb(
                      () => validerPrevisionUb(Number(idVersion), Number(idUB)),
                      'Prévision UB validée.',
                      'VAL',
                    )
                  }
                >
                  Valider
                </PrimaryButton>
                <SecondaryButton
                  size="small"
                  disabled={!canRejeter}
                  onClick={() => {
                    setRejectMotif('');
                    setRejectError(null);
                    setRejectOpen(true);
                  }}
                >
                  Rejeter
                </SecondaryButton>
              </>
            )}
          </>
        }
      />

      {versionsFiltrees.length === 0 && idExercice && (
        <Alert severity="info" sx={{ py: 0.5 }}>
          Aucune version pour cet exercice — créez une version BROUILLON avant de saisir.
        </Alert>
      )}
      {error && (
        <Alert severity="error" sx={{ py: 0.5 }} onClose={() => setError(null)}>
          {error}
        </Alert>
      )}

      <Paper
        variant="outlined"
        sx={{
          px: 2,
          py: 1.5,
          flexShrink: 0,
          borderRadius: '10px',
          borderColor: 'var(--ef-border)',
          bgcolor: 'var(--ef-surface)',
        }}
      >
        <Stack
          direction={{ xs: 'column', md: 'row' }}
          spacing={1}
          sx={{ alignItems: { md: 'center' }, justifyContent: 'space-between' }}
        >
          <Stack
            direction="row"
            spacing={0.75}
            useFlexGap
            sx={{ flexWrap: 'wrap', alignItems: 'center', minWidth: 0, flex: 1 }}
          >
            <Typography
              variant="subtitle2"
              sx={{ fontWeight: 800, whiteSpace: 'nowrap', mr: 0.5, color: 'text.primary' }}
            >
              {contexteMeta.titre}
            </Typography>
            <Divider orientation="vertical" flexItem sx={{ display: { xs: 'none', sm: 'block' } }} />
            <Chip size="small" label={contexteMeta.annee} variant="outlined" sx={{ height: 24 }} />
            <Chip size="small" label={contexteMeta.version} variant="outlined" sx={{ height: 24 }} />
            <Chip size="small" label={contexteMeta.type} variant="outlined" sx={{ height: 24 }} />
            <Chip size="small" label={contexteMeta.mode} variant="outlined" sx={{ height: 24 }} />
            <Chip
              size="small"
              label={contexteMeta.ub}
              variant="outlined"
              sx={{ height: 24, maxWidth: { xs: '100%', md: 320 } }}
              title={contexteMeta.ub}
            />
          </Stack>
          <Stack direction="row" spacing={0.75} sx={{ flexShrink: 0, alignItems: 'center' }}>
            <SecondaryButton
              size="small"
              startIcon={<EditOutlinedIcon />}
              onClick={() => setContexteVisible(!(showContexteEdit && ready))}
            >
              {showContexteEdit && ready ? 'Masquer le contexte' : 'Modifier le contexte'}
            </SecondaryButton>
          </Stack>
        </Stack>

        <Stack direction="row" spacing={0.75} useFlexGap sx={{ flexWrap: 'wrap', mt: 0.75, alignItems: 'center' }}>
          {!ready && <Chip size="small" color="warning" label="Contexte incomplet" sx={{ height: 22 }} />}
          {ready && statutVersion && (
            <Chip
              size="small"
              variant="outlined"
              label={`UB · ${statutNorm}${modifiable ? ' — Saisie autorisée' : ' — Lecture seule'}`}
              sx={{
                height: 22,
                fontWeight: 700,
                letterSpacing: 0.3,
                ...statutChipSx(statutNorm),
              }}
            />
          )}
          {ready && (
            <Chip
              size="small"
              variant="outlined"
              label={`${lignesAffichees.length}/${totalCount} lignes`}
              sx={{ height: 22 }}
            />
          )}
          {ready && (
            <Chip
              size="small"
              variant="outlined"
              color={dirtyCount > 0 ? 'warning' : 'default'}
              label={`${dirtyCount} modifiée(s)`}
              sx={{ height: 22 }}
            />
          )}
          {ready && (
            <Chip
              size="small"
              variant="outlined"
              label={`Total : ${formatMontantBudget(totalGrille)}`}
              sx={{ height: 22 }}
            />
          )}
        </Stack>

        <Collapse in={showContexteEdit}>
          <Box sx={{ pt: 1.25, borderTop: 1, borderColor: 'divider', mt: 1 }}>
            <FilterFields>
              <SearchableSelect
                label="Exercice"
                value={idExercice}
                onChange={setIdExercice}
                fullWidth
                options={exercices.map((e) => ({
                  value: String(e.idExercice),
                  label: `${e.annee} (${e.statut})`,
                }))}
                placeholder="Rechercher un exercice…"
                density="sm"
              />
              <SearchableSelect
                label="Version"
                value={idVersion}
                onChange={setIdVersion}
                fullWidth
                options={versionsFiltrees.map((v) => ({
                  value: String(v.idVersion),
                  label: `V${v.numeroVersion} — ${v.libelle} [${v.statut}]`,
                }))}
                placeholder="Rechercher une version…"
                density="md"
              />
              {!navigationParOnglets && (
                <SearchableSelect
                  label="Type"
                  value={idTypeBudget}
                  onChange={(v) => {
                    setIdTypeBudget(v);
                    setActionAE('');
                    setIdItemBI('');
                    setIdGroupeAE('');
                  }}
                  fullWidth
                  options={types.map((t) => ({
                    value: String(t.idTypeBudget),
                    label: `${t.codeType} — ${t.libelle}`,
                  }))}
                  placeholder="Rechercher un type…"
                  density="sm"
                />
              )}
              <SearchableSelect
                label="Mode"
                value={idModePrevision}
                onChange={setIdModePrevision}
                fullWidth
                options={modes.map((m) => ({
                  value: String(m.idModePrevision),
                  label: `${m.codeMode} — ${m.libelle}`,
                }))}
                placeholder="Rechercher un mode…"
                density="md"
              />
              <Autocomplete
                size="small"
                fullWidth
                options={ubs}
                value={ubSelectionnee}
                onChange={(_, value) => setIdUB(value ? String(value.idUB) : '')}
                getOptionLabel={formatUbOption}
                isOptionEqualToValue={(a, b) => a.idUB === b.idUB}
                filterOptions={(options, state) => {
                  const q = state.inputValue.trim().toLowerCase();
                  if (!q) return options;
                  return options.filter(
                    (u) =>
                      u.codeUB.toLowerCase().includes(q) || u.libelle.toLowerCase().includes(q),
                  );
                }}
                renderInput={(params) => (
                  <TextField {...params} label="Unité budgétaire" placeholder="Rechercher une UB…" />
                )}
                noOptionsText="Aucune unité budgétaire"
              />
            </FilterFields>

            {codeTypeSel === 'AE' && (
              <FilterFields sx={{ mt: 2 }}>
                <Autocomplete
                  freeSolo
                  size="small"
                  fullWidth
                  options={actionsAEOptions}
                  value={actionAE}
                  onChange={(_, value) => setActionAE(typeof value === 'string' ? value : value ?? '')}
                  onInputChange={(_, value, reason) => {
                    if (reason === 'input' || reason === 'clear') setActionAE(value);
                  }}
                  filterOptions={(options, state) => {
                    const q = state.inputValue.trim().toLowerCase();
                    if (!q) return options;
                    return options.filter((a) => a.toLowerCase().includes(q));
                  }}
                  renderInput={(params) => (
                    <TextField
                      {...params}
                      label="Action d'exploitation"
                      placeholder="Choisir ou saisir une action…"
                      helperText={
                        actionsAEOptions.length > 0
                          ? 'Sélectionnez une action existante ou saisissez-en une nouvelle'
                          : 'Saisissez une nouvelle action (aucune existante pour cet exercice)'
                      }
                    />
                  )}
                  noOptionsText="Aucune action — saisissez un nouveau libellé"
                />
                <SearchableSelect
                  label="Groupe AE (facultatif)"
                  value={idGroupeAE}
                  onChange={setIdGroupeAE}
                  allowEmpty
                  fullWidth
                  options={[
                    { value: '', label: 'Aucun' },
                    ...groupesAE.map((g) => ({
                      value: String(g.idGroupeItemAE),
                      label: g.libelle,
                    })),
                  ]}
                  placeholder="Rechercher un groupe…"
                  density="lg"
                />
                <Stack direction="row" spacing={1} sx={{ width: '100%' }}>
                  <TextField
                    fullWidth
                    size="small"
                    label="Nouveau groupe AE"
                    value={nouveauGroupe}
                    onChange={(e) => setNouveauGroupe(e.target.value)}
                    placeholder="Libellé du groupe…"
                  />
                  <SecondaryButton size="small" onClick={() => void creerGroupe()} sx={{ flexShrink: 0 }}>
                    Ajouter
                  </SecondaryButton>
                </Stack>
              </FilterFields>
            )}

            {codeTypeSel === 'BI' && (
              <FilterFields sx={{ mt: 2 }}>
                <SearchableSelect
                  label="Item BI"
                  value={idItemBI}
                  onChange={setIdItemBI}
                  allowEmpty
                  fullWidth
                  options={[
                    { value: '', label: 'Sélectionner un item' },
                    ...itemsBI.map((it) => ({
                      value: String(it.idItemBI),
                      label: `${it.codeItem} — ${it.libelle}`,
                    })),
                  ]}
                  placeholder="Rechercher un item BI…"
                  density="xxl"
                />
              </FilterFields>
            )}
          </Box>
        </Collapse>
      </Paper>

      {navigationParOnglets && idVersion && idUB && (
        <Paper sx={{ mb: 2, flexShrink: 0 }}>
          <Tabs
            value={ongletTypeActif}
            onChange={(_, v: OngletTypeBudget) => void switchTypeOnglet(v)}
            sx={{ px: 1, borderBottom: 1, borderColor: 'divider' }}
          >
            <Tab label="DC" value="DC" />
            <Tab label="AE" value="AE" />
            <Tab label="BI" value="BI" />
          </Tabs>
          {codeTypeSel === 'AE' && (
            <Box sx={{ p: 1.5 }}>
              <Autocomplete
                freeSolo
                size="small"
                fullWidth
                options={actionsAEOptions}
                value={actionAE}
                onChange={(_, value) => setActionAE(typeof value === 'string' ? value : value ?? '')}
                onInputChange={(_, value, reason) => {
                  if (reason === 'input' || reason === 'clear') setActionAE(value);
                }}
                filterOptions={(options, state) => {
                  const q = state.inputValue.trim().toLowerCase();
                  if (!q) return options;
                  return options.filter((a) => a.toLowerCase().includes(q));
                }}
                renderInput={(params) => (
                  <TextField
                    {...params}
                    label="Action d'exploitation"
                    placeholder="Choisir ou saisir une action…"
                  />
                )}
                noOptionsText="Aucune action — saisissez un nouveau libellé"
              />
            </Box>
          )}
          {codeTypeSel === 'BI' && (
            <Box sx={{ p: 1.5 }}>
              <SearchableSelect
                label="Item BI"
                value={idItemBI}
                onChange={setIdItemBI}
                allowEmpty
                fullWidth
                options={[
                  { value: '', label: 'Sélectionner un item' },
                  ...itemsBI.map((it) => ({
                    value: String(it.idItemBI),
                    label: `${it.codeItem} — ${it.libelle}`,
                  })),
                ]}
                placeholder="Rechercher un item BI…"
                density="xxl"
              />
            </Box>
          )}
        </Paper>
      )}

      {ready && (codeTypeSel === 'DC' || codeTypeSel === 'AE') && (
        <Paper variant="outlined" sx={{ px: 1.25, py: 0.75, flexShrink: 0 }}>
          <Stack direction={{ xs: 'column', md: 'row' }} spacing={1} sx={{ alignItems: { md: 'center' } }}>
            <TextField
              size="small"
              placeholder="Rechercher une rubrique…"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              sx={{ flex: 1, minWidth: 180 }}
              slotProps={{
                input: {
                  startAdornment: (
                    <InputAdornment position="start">
                      <SearchIcon fontSize="small" color="action" />
                    </InputAdornment>
                  ),
                },
              }}
            />
            <SearchableSelect
              label="Groupe"
              value={idGroupeFiltre}
              onChange={setIdGroupeFiltre}
              allowEmpty
              density="lg"
              options={[
                { value: '', label: 'Tous les groupes' },
                ...groupesRBOrdonnes.map((g) => ({
                  value: String(g.idGroupeRB),
                  label: formatGroupeNiveau1({
                    codeGroupe: g.codeGroupe,
                    libelleGroupe: g.libelle,
                  }),
                })),
              ]}
              placeholder="Rechercher un groupe…"
            />
            <SearchableSelect
              label="État"
              value={filtre}
              onChange={(v) => setFiltre(v as 'toutes' | 'avec' | 'sans')}
              density="md"
              options={[
                { value: 'toutes', label: 'Toutes les RB' },
                { value: 'avec', label: 'RB avec prévision' },
                { value: 'sans', label: 'RB sans prévision' },
              ]}
              placeholder="Filtrer…"
            />
            <SecondaryButton size="small" startIcon={<RestartAltIcon />} onClick={reinitialiserFiltresAffichage}>
              Réinitialiser
            </SecondaryButton>
          </Stack>
        </Paper>
      )}

      <Box sx={{ flex: 1, minHeight: { xs: 360, md: 0 }, display: 'flex', flexDirection: 'column' }}>
        {!ready && (
          <ErrorState
            severity="warning"
            title="Contexte incomplet"
            message={
              codeTypeSel === 'AE'
                ? 'Sélectionnez Exercice, Version, Type, Mode, UB et une Action d’exploitation.'
                : codeTypeSel === 'BI'
                  ? 'Sélectionnez Exercice, Version, Type, Mode, UB et un Item BI.'
                  : 'Sélectionnez Exercice, Version, Type, Mode et UB pour déverrouiller la grille.'
            }
          />
        )}

        {ready && loadingGrille && <LoadingState label="Chargement de la grille…" />}

        {ready && !loadingGrille && codeTypeSel === 'BI' && modifiable && (
          <Stack direction="row" spacing={1} sx={{ mb: 1, flexShrink: 0 }}>
            <TextField
              size="small"
              fullWidth
              label="Nouveau détail d’investissement"
              value={nouveauDetailBI}
              onChange={(e) => setNouveauDetailBI(e.target.value)}
              onKeyDown={(e) => e.key === 'Enter' && ajouterDetailBI()}
            />
            <PrimaryButton size="small" startIcon={<AddIcon />} onClick={ajouterDetailBI}>
              Ajouter
            </PrimaryButton>
          </Stack>
        )}

        {ready && !loadingGrille && (
          <Box sx={{ flex: 1, minHeight: 0, display: 'flex', flexDirection: 'column' }}>
            <PrevisionGrilleVirtual
              lignes={lignesAffichees}
              codeType={codeTypeSel}
              codeMode={codeMode}
              modifiable={modifiable}
              hasNextPage={hasNextPage}
              loadingMore={loadingMore}
              groupesReplies={groupesReplies}
              onToggleGroupe={toggleGroupe}
              onLoadMore={loadMore}
              onMontantAnnuel={setMontantAnnuel}
              onMontantMois={setMontantMois}
              onRepartirAnnuel={
                codeMode === 'MENSUEL' && codeTypeSel !== 'BI' ? ouvrirRepartition : undefined
              }
              onEffacerRepartition={
                codeMode === 'MENSUEL' && codeTypeSel !== 'BI' ? demanderEffacer : undefined
              }
            />
          </Box>
        )}
      </Box>

      <Dialog
        open={!!repartDialog}
        onClose={() => setRepartDialog(null)}
        maxWidth="xs"
        fullWidth
      >
        <DialogTitle>Répartir sur 12 mois</DialogTitle>
        <DialogContent>
          {repartDialog && (
            <Stack spacing={1.5} sx={{ pt: 0.5 }}>
              <Typography variant="body2" color="text.secondary">
                {repartDialog.codeRB} — {repartDialog.libelle}
              </Typography>
              <AmountField
                autoFocus
                label="Montant annuel à répartir"
                value={repartDialog.montantRaw}
                onChange={(value) => {
                  setRepartError(null);
                  setRepartDialog({ ...repartDialog, montantRaw: value });
                }}
                onKeyDown={(e) => e.key === 'Enter' && void demanderRepartition()}
                fullWidth
                currency={null}
                error={!!repartError}
                helperText={repartError ?? 'Répartition égale ; le reste de centimes va aux premiers mois.'}
              />
            </Stack>
          )}
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2 }}>
          <Button onClick={() => setRepartDialog(null)}>Annuler</Button>
          <Button variant="contained" onClick={() => void demanderRepartition()}>
            Répartir sur 12 mois
          </Button>
        </DialogActions>
      </Dialog>

      <Dialog open={rejectOpen} onClose={() => !workflowBusy && setRejectOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>Rejeter la version</DialogTitle>
        <DialogContent>
          <TextField
            autoFocus
            required
            fullWidth
            multiline
            minRows={3}
            margin="dense"
            label="Motif du rejet"
            value={rejectMotif}
            onChange={(e) => {
              setRejectError(null);
              setRejectMotif(e.target.value);
            }}
            error={!!rejectError}
            helperText={rejectError ?? 'Obligatoire — visible dans la traçabilité de la version.'}
            slotProps={{ htmlInput: { maxLength: 1000 } }}
          />
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2 }}>
          <Button onClick={() => setRejectOpen(false)} disabled={workflowBusy}>
            Annuler
          </Button>
          <Button variant="contained" color="error" onClick={() => void confirmerRejet()} disabled={workflowBusy}>
            Confirmer le rejet
          </Button>
        </DialogActions>
      </Dialog>

      <DocumentActionResultDialog
        open={!!docResult}
        state={docResult}
        onClose={() => setDocResult(null)}
      />

      <ClassementAeDialog
        open={classementAeOpen}
        onClose={() => setClassementAeOpen(false)}
        idVersion={Number(idVersion) || 0}
        idUB={Number(idUB) || 0}
        codeUB={ubSelectionnee?.codeUB}
      />
    </Box>
  );
}
