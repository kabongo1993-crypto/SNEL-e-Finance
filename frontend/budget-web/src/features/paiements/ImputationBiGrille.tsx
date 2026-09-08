import {
  Alert,
  Box,
  Button,
  Checkbox,
  Chip,
  MenuItem,
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
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { LoadingState, useMsgBox, sanitizeNumericInput } from '../../components';
import {
  enregistrerImputationsBi,
  fetchGrilleImputationBi,
  fetchItemsBI,
  type GrilleImputationBi,
  type ItemBI,
  type LigneGrilleImputationBi,
} from '../../services/apiClient';
import {
  apiErrorMessage,
  formatMontantDevise,
  formatMontantUsd,
  MOIS_LABELS,
  totalImputationsOk,
} from './paiementUtils';

interface ImputationBiGrilleProps {
  idDemande: number;
  readOnly?: boolean;
  disabled?: boolean;
  canSave?: boolean;
  itemSolliciteInitial?: string | null;
  onRepartitionChange?: (ok: boolean) => void;
  onSaved?: () => void;
}

function cleDetail(detail: string): string {
  return detail.trim().replace(/\s+/g, ' ').toLowerCase();
}

function parseMontant(raw: string): number {
  const n = Number.parseFloat((raw ?? '').replace(/\s/g, '').replace(',', '.').trim());
  return Number.isFinite(n) && n > 0 ? n : 0;
}

function montantColor(value: number): string | undefined {
  return value < 0 ? 'var(--ef-danger)' : undefined;
}

function formatResumeRepartition(brut: number, usd: number, devise: string): string {
  const usdLabel = formatMontantUsd(usd);
  const code = (devise || 'USD').toUpperCase();
  if (code === 'USD' || Math.abs(brut - usd) < 0.01) return usdLabel;
  return `${formatMontantDevise(brut, code)} (${usdLabel})`;
}

function moisLabel(mois: number | null): string {
  if (mois == null) return 'Sans mois';
  return MOIS_LABELS.find((m) => m.value === mois)?.label ?? `Mois ${mois}`;
}

const CODE_WIDTH = 72;
const LIBELLE_WIDTH = 260;

function stickyCell(left: number, selected: boolean, header = false) {
  return {
    position: 'sticky' as const,
    left,
    zIndex: header ? 5 : 3,
    bgcolor: selected ? 'action.selected' : 'var(--ef-surface)',
    boxShadow: '1px 0 0 var(--ef-border)',
    ...(header ? { top: 0 } : {}),
  };
}

function ligneLocale(detail: string, code: number): LigneGrilleImputationBi {
  return {
    detailBI: detail,
    code,
    libelle: detail,
    estCochee: false,
    idImputation: null,
    idBudgetLigne: null,
    previsionExiste: false,
    engagementEnCoursBrut: 0,
    engagementEnCoursUsd: 0,
    montantUsd: 0,
    budgetAnnuel: 0,
    creditEngageAnnuel: 0,
    creditDisponibleAnnuel: 0,
    engagementEnCoursAnnuelUsd: 0,
  };
}

export function ImputationBiGrille({
  idDemande,
  readOnly = false,
  disabled = false,
  canSave = false,
  itemSolliciteInitial,
  onRepartitionChange,
  onSaved,
}: ImputationBiGrilleProps) {
  const msgBox = useMsgBox();
  const [grille, setGrille] = useState<GrilleImputationBi | null>(null);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [idItemBI, setIdItemBI] = useState<number | ''>('');
  const [mois, setMois] = useState<number | null>(null);
  const [amounts, setAmounts] = useState<Record<string, string>>({});
  const [detailsLocaux, setDetailsLocaux] = useState<string[]>([]);
  const [itemsBI, setItemsBI] = useState<ItemBI[]>([]);
  const dirtyRef = useRef<Set<string>>(new Set());
  const onRepartitionChangeRef = useRef(onRepartitionChange);
  onRepartitionChangeRef.current = onRepartitionChange;
  const initialLoadDoneRef = useRef(false);

  const applyGrille = useCallback((next: GrilleImputationBi, mergeUnsaved: boolean) => {
    setGrille(next);
    setIdItemBI(next.idItemBI);
    setMois(next.mois);
    setDetailsLocaux((prev) =>
      prev.filter((d) => !next.lignes.some((l) => cleDetail(l.detailBI) === cleDetail(d))),
    );
    setAmounts((prev) => {
      const merged: Record<string, string> = {};
      for (const ligne of next.lignes) {
        const key = cleDetail(ligne.detailBI);
        const dirty = mergeUnsaved && dirtyRef.current.has(key);
        if (dirty && prev[key] != null) {
          merged[key] = prev[key];
        } else {
          merged[key] =
            ligne.engagementEnCoursBrut > 0 ? String(ligne.engagementEnCoursBrut) : '';
        }
      }
      if (!mergeUnsaved) dirtyRef.current = new Set();
      return merged;
    });
  }, []);

  const load = useCallback(
    async (
      params?: { idItemBI?: number; mois?: number | null },
      mergeUnsaved = false,
    ) => {
      const itemId = params?.idItemBI ?? (idItemBI === '' ? null : idItemBI);
      if (itemId == null || itemId <= 0) {
        setLoadError(null);
        setGrille(null);
        return;
      }
      const moisCible = params?.mois !== undefined ? params.mois : mois;

      setLoading(true);
      setLoadError(null);
      try {
        const next = await fetchGrilleImputationBi(idDemande, {
          idItemBI: itemId,
          mois: moisCible,
        });
        applyGrille(next, mergeUnsaved);
      } catch (err) {
        setLoadError(apiErrorMessage(err, "Impossible de charger la grille d'imputation BI."));
      } finally {
        setLoading(false);
      }
    },
    [idDemande, idItemBI, mois, applyGrille],
  );

  useEffect(() => {
    let cancelled = false;
    void (async () => {
      try {
        const items = await fetchItemsBI();
        if (cancelled) return;
        setItemsBI(items.filter((i) => i.actif !== false));
      } catch {
        if (!cancelled) setItemsBI([]);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    if (initialLoadDoneRef.current || itemsBI.length === 0) return;
    const sollicite = (itemSolliciteInitial ?? '').trim().toLowerCase();
    if (!sollicite) return;
    const match = itemsBI.find(
      (i) =>
        i.libelle.toLowerCase() === sollicite ||
        i.codeItem.toLowerCase() === sollicite ||
        `${i.codeItem} — ${i.libelle}`.toLowerCase().includes(sollicite),
    );
    if (match) {
      initialLoadDoneRef.current = true;
      setIdItemBI(match.idItemBI);
      void load({ idItemBI: match.idItemBI, mois: null }, false);
    }
  }, [itemsBI, itemSolliciteInitial, load]);

  const lignesAffichees = useMemo(() => {
    if (!grille) return [];
    const map = new Map<string, LigneGrilleImputationBi>();
    for (const ligne of grille.lignes) {
      map.set(cleDetail(ligne.detailBI), ligne);
    }
    let code = grille.lignes.length;
    for (const detail of detailsLocaux) {
      const key = cleDetail(detail);
      if (!map.has(key)) {
        code += 1;
        map.set(key, ligneLocale(detail, code));
      }
    }
    return [...map.values()].sort((a, b) => a.libelle.localeCompare(b.libelle, 'fr'));
  }, [grille, detailsLocaux]);

  const lignesFiltrees = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return lignesAffichees;
    return lignesAffichees.filter((l) => l.libelle.toLowerCase().includes(q));
  }, [lignesAffichees, search]);

  const lignesCalculees = useMemo(
    () =>
      lignesAffichees.map((l) => ({
        detailBI: l.detailBI,
        brut: parseMontant(amounts[cleDetail(l.detailBI)] ?? ''),
      })),
    [lignesAffichees, amounts],
  );

  const totalBrut = grille?.totalRepartiBrut ?? 0;
  const totalUsd = grille?.totalRepartiUsd ?? 0;
  const ecartBrut = grille?.ecartBrut ?? 0;
  const ecartUsd = grille?.ecartUsd ?? 0;
  const repartitionOk = grille
    ? totalImputationsOk(grille.montantUsd ?? 0, totalUsd)
    : false;

  useEffect(() => {
    onRepartitionChangeRef.current?.(repartitionOk);
  }, [repartitionOk]);

  const handleItemChange = async (value: string) => {
    if (value === '') {
      setIdItemBI('');
      setGrille(null);
      setDetailsLocaux([]);
      return;
    }
    const nextId = Number.parseInt(value, 10);
    if (!Number.isFinite(nextId)) return;
    setIdItemBI(nextId);
    setDetailsLocaux([]);
    dirtyRef.current = new Set();
    await load({ idItemBI: nextId, mois }, false);
  };

  const handleMois = async (value: string) => {
    const next = value === '' ? null : Number.parseInt(value, 10);
    setMois(next);
    if (idItemBI !== '') {
      await load({ idItemBI, mois: next }, true);
    }
  };

  const handleAmountChange = (detailBI: string, raw: string) => {
    const key = cleDetail(detailBI);
    dirtyRef.current.add(key);
    setAmounts((prev) => ({ ...prev, [key]: sanitizeNumericInput(raw) }));
  };

  const toggleLigne = (ligne: LigneGrilleImputationBi, checked: boolean) => {
    if (checked) return;
    const key = cleDetail(ligne.detailBI);
    dirtyRef.current.add(key);
    setAmounts((prev) => ({ ...prev, [key]: '' }));
  };

  const handleCreateDetail = () => {
    const libelle = window.prompt('Libellé du nouveau détail BI :')?.trim();
    if (!libelle) return;
    const key = cleDetail(libelle);
    if (grille?.lignes.some((l) => cleDetail(l.detailBI) === key)) {
      void msgBox.info('Ce détail existe déjà pour cet Item.');
      return;
    }
    if (detailsLocaux.some((d) => cleDetail(d) === key)) {
      void msgBox.info('Ce détail est déjà dans la liste.');
      return;
    }
    setDetailsLocaux((prev) => [...prev, libelle]);
    void msgBox.success('Détail ajouté à la grille.');
  };

  const handleSave = async () => {
    if (!grille || idItemBI === '') return;
    setSaving(true);
    try {
      const next = await enregistrerImputationsBi(idDemande, {
        idItemBI,
        mois,
        lignes: lignesCalculees
          .filter((l) => l.brut > 0)
          .map((l) => ({ detailBI: l.detailBI, montantBrut: l.brut })),
      });
      dirtyRef.current = new Set();
      applyGrille(next, false);
      void msgBox.success('Imputation BI enregistrée.');
      onSaved?.();
    } catch (err) {
      void msgBox.error(apiErrorMessage(err, "Enregistrement de l'imputation BI impossible."));
    } finally {
      setSaving(false);
    }
  };

  const locked = readOnly || disabled || saving;
  const itemReady = idItemBI !== '';

  if (loading && !grille && itemReady) {
    return <LoadingState label="Chargement de la grille BI…" />;
  }

  return (
    <Stack spacing={2}>
      {grille && (
        <Box
          sx={{
            display: 'grid',
            gap: 1.25,
            gridTemplateColumns: { xs: '1fr 1fr', md: 'repeat(5, minmax(0, 1fr))' },
          }}
        >
          <Box
            sx={{
              p: 1.25,
              borderRadius: 1,
              border: '1px solid',
              borderColor: repartitionOk ? 'var(--ef-success)' : 'var(--ef-warning)',
              bgcolor: repartitionOk ? 'var(--ef-success-soft)' : 'var(--ef-warning-soft)',
            }}
          >
            <Typography variant="caption" color="text.secondary">
              Total réparti
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 800, fontVariantNumeric: 'tabular-nums' }}>
              {formatResumeRepartition(totalBrut, totalUsd, grille.devise)}
            </Typography>
          </Box>
          <Box
            sx={{
              p: 1.25,
              borderRadius: 1,
              border: '1px solid',
              borderColor: repartitionOk ? 'var(--ef-success)' : 'var(--ef-warning)',
              bgcolor: repartitionOk ? 'var(--ef-success-soft)' : 'var(--ef-warning-soft)',
            }}
          >
            <Typography variant="caption" color="text.secondary">
              Reste à répartir
            </Typography>
            <Typography
              variant="body2"
              sx={{
                fontWeight: 800,
                fontVariantNumeric: 'tabular-nums',
                color: montantColor(ecartBrut) ?? montantColor(ecartUsd),
              }}
            >
              {formatResumeRepartition(ecartBrut, ecartUsd, grille.devise)}
            </Typography>
          </Box>
          {[
            { label: 'N° dossier', value: grille.reference },
            { label: 'Montant', value: formatMontantDevise(grille.montantBrut, grille.devise) },
            { label: 'Devise', value: grille.devise },
            {
              label: 'Taux USD',
              value: grille.tauxConversion != null ? String(grille.tauxConversion) : '—',
            },
            {
              label: 'Montant USD',
              value: grille.montantUsd != null ? formatMontantUsd(grille.montantUsd) : '—',
            },
            { label: 'UB', value: `${grille.codeUB} — ${grille.libelleUB}` },
            { label: 'Item BI', value: `${grille.codeItemBI} — ${grille.libelleItemBI}` },
            { label: 'Annuel prévu', value: formatMontantUsd(grille.budgetAnnuelItemUb) },
            { label: 'Annuel engagé', value: formatMontantUsd(grille.creditEngageAnnuelItemUb) },
          ].map((item) => (
            <Box
              key={item.label}
              sx={{
                p: 1.25,
                borderRadius: 1,
                border: '1px solid',
                borderColor: 'divider',
                bgcolor: 'var(--ef-surface-secondary)',
              }}
            >
              <Typography variant="caption" color="text.secondary">
                {item.label}
              </Typography>
              <Typography variant="body2" sx={{ fontWeight: 700, overflowWrap: 'anywhere' }}>
                {item.value}
              </Typography>
            </Box>
          ))}
        </Box>
      )}

      <Stack
        direction={{ xs: 'column', md: 'row' }}
        spacing={1.25}
        useFlexGap
        sx={{ alignItems: { md: 'center' }, flexWrap: 'wrap' }}
      >
        <TextField
          select
          required
          size="small"
          label="Item BI"
          value={idItemBI === '' ? '' : String(idItemBI)}
          onChange={(e) => void handleItemChange(e.target.value)}
          disabled={locked}
          sx={{ minWidth: 260, flex: 1 }}
        >
          <MenuItem value="">
            <em>Sélectionner…</em>
          </MenuItem>
          {itemsBI.map((i) => (
            <MenuItem key={i.idItemBI} value={String(i.idItemBI)}>
              {i.codeItem} — {i.libelle}
            </MenuItem>
          ))}
        </TextField>
        {!readOnly && (
          <Button variant="text" size="small" onClick={handleCreateDetail} disabled={locked || !itemReady}>
            + Créer un détail
          </Button>
        )}
        <TextField
          select
          size="small"
          label="Mois"
          value={mois == null ? '' : String(mois)}
          onChange={(e) => void handleMois(e.target.value)}
          disabled={locked || !itemReady}
          sx={{ minWidth: 160 }}
        >
          <MenuItem value="">
            <em>Sans mois</em>
          </MenuItem>
          {MOIS_LABELS.map((m) => (
            <MenuItem key={m.value} value={String(m.value)}>
              {m.label}
            </MenuItem>
          ))}
        </TextField>
        <TextField
          size="small"
          label="Rechercher un détail"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          disabled={!itemReady}
          sx={{ flex: 1, minWidth: 220 }}
        />
        <Button
          variant="outlined"
          onClick={() => void load(undefined, true)}
          disabled={loading || saving || !itemReady}
        >
          Actualiser
        </Button>
        {canSave && !readOnly && (
          <Button
            variant="contained"
            onClick={() => void handleSave()}
            disabled={locked || !grille || !itemReady}
          >
            Enregistrer la grille
          </Button>
        )}
      </Stack>

      {grille && grille.lignesExistantes.length > 0 && (
        <Stack direction="row" spacing={0.75} useFlexGap sx={{ flexWrap: 'wrap' }}>
          {grille.lignesExistantes.map((l) => (
            <Chip
              key={l.idImputation}
              size="small"
              variant="outlined"
              label={`${l.libelleItemBI} · ${l.detailBI} · ${moisLabel(l.mois)} · ${formatMontantUsd(l.montantUsd)}`}
            />
          ))}
        </Stack>
      )}

      {!itemReady && (
        <Alert severity="info">Sélectionnez un Item BI pour charger les détails et imputer.</Alert>
      )}

      {loadError && <Alert severity="error">{loadError}</Alert>}

      {grille && itemReady && (
        <TableContainer sx={{ maxHeight: 480, border: '1px solid var(--ef-border)', borderRadius: 1 }}>
          <Table stickyHeader size="small">
            <TableHead>
              <TableRow>
                <TableCell sx={{ ...stickyCell(0, false, true), minWidth: CODE_WIDTH, width: CODE_WIDTH }}>
                  Code
                </TableCell>
                <TableCell sx={{ ...stickyCell(CODE_WIDTH, false, true), minWidth: LIBELLE_WIDTH }}>
                  Détail BI
                </TableCell>
                <TableCell align="center">État</TableCell>
                <TableCell align="right">Engt en cours</TableCell>
                <TableCell align="right">Budget annuel</TableCell>
                <TableCell align="right">Crédit engagé</TableCell>
                <TableCell align="right">Crédit dispo.</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {lignesFiltrees.map((ligne) => {
                const key = cleDetail(ligne.detailBI);
                const selected = parseMontant(amounts[key] ?? '') > 0;
                return (
                  <TableRow key={key} selected={selected} hover>
                    <TableCell sx={stickyCell(0, selected)}>{ligne.code}</TableCell>
                    <TableCell sx={stickyCell(CODE_WIDTH, selected)}>{ligne.libelle}</TableCell>
                    <TableCell align="center">
                      <Checkbox
                        size="small"
                        checked={selected}
                        onChange={(e) => toggleLigne(ligne, e.target.checked)}
                        disabled={locked}
                      />
                    </TableCell>
                    <TableCell align="right">
                      <TextField
                        size="small"
                        value={amounts[key] ?? ''}
                        onChange={(e) => handleAmountChange(ligne.detailBI, e.target.value)}
                        disabled={locked}
                        slotProps={{ htmlInput: { style: { textAlign: 'right' } } }}
                        sx={{ width: 120 }}
                      />
                    </TableCell>
                    <TableCell align="right">{formatMontantUsd(ligne.budgetAnnuel)}</TableCell>
                    <TableCell align="right">{formatMontantUsd(ligne.creditEngageAnnuel)}</TableCell>
                    <TableCell align="right" sx={{ color: montantColor(ligne.creditDisponibleAnnuel) }}>
                      {formatMontantUsd(ligne.creditDisponibleAnnuel)}
                    </TableCell>
                  </TableRow>
                );
              })}
              {lignesFiltrees.length === 0 && (
                <TableRow>
                  <TableCell colSpan={7}>
                    <Typography variant="body2" color="text.secondary" sx={{ py: 2, textAlign: 'center' }}>
                      Aucun détail BI disponible pour cet Item. Créez un détail pour commencer l&apos;imputation.
                    </Typography>
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </TableContainer>
      )}
    </Stack>
  );
}
