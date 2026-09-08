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
  createGroupeItemAE,
  enregistrerImputationsAe,
  fetchActionsExploitation,
  fetchGrilleImputationAe,
  fetchGroupesItemAE,
  type GrilleImputationAe,
  type GroupeItemAE,
  type LigneGrilleImputationAe,
} from '../../services/apiClient';
import {
  apiErrorMessage,
  calculerMontantUsd,
  formatMontantDevise,
  formatMontantUsd,
  MOIS_LABELS,
  totalImputationsOk,
} from './paiementUtils';

interface ImputationAeGrilleProps {
  idDemande: number;
  readOnly?: boolean;
  disabled?: boolean;
  canSave?: boolean;
  idExercice: number;
  itemSolliciteInitial?: string | null;
  onRepartitionChange?: (ok: boolean) => void;
  onSaved?: () => void;
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

function normalizeItem(value: string | null | undefined): string {
  return (value ?? '').trim().toLowerCase();
}

function sameAeScope(
  ligne: { libelleItemAE: string; mois: number | null },
  item: string,
  mois: number | null,
): boolean {
  return normalizeItem(ligne.libelleItemAE) === normalizeItem(item) && ligne.mois === mois;
}

function moisLabel(mois: number | null): string {
  if (mois == null) return 'Sans mois';
  return MOIS_LABELS.find((m) => m.value === mois)?.label ?? `Mois ${mois}`;
}

const CODE_WIDTH = 96;
const LIBELLE_WIDTH = 240;

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

export function ImputationAeGrille({
  idDemande,
  readOnly = false,
  disabled = false,
  canSave = false,
  idExercice,
  itemSolliciteInitial,
  onRepartitionChange,
  onSaved,
}: ImputationAeGrilleProps) {
  const msgBox = useMsgBox();
  const [grille, setGrille] = useState<GrilleImputationAe | null>(null);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [itemAE, setItemAE] = useState(() => (itemSolliciteInitial ?? '').trim());
  const [idGroupe, setIdGroupe] = useState<number | ''>('');
  const [mois, setMois] = useState<number | null>(null);
  const [amounts, setAmounts] = useState<Record<number, string>>({});
  const [actionsAE, setActionsAE] = useState<string[]>([]);
  const [groupesAE, setGroupesAE] = useState<GroupeItemAE[]>([]);
  const dirtyRef = useRef<Set<number>>(new Set());
  const onRepartitionChangeRef = useRef(onRepartitionChange);
  onRepartitionChangeRef.current = onRepartitionChange;
  const initialLoadDoneRef = useRef(false);
  const datalistId = `imputation-ae-actions-${idDemande}`;

  const applyGrille = useCallback((next: GrilleImputationAe, mergeUnsaved: boolean) => {
    setGrille(next);
    setItemAE(next.libelleItemAE);
    setIdGroupe(next.idGroupeItemAE ?? '');
    setMois(next.mois);
    setAmounts((prev) => {
      const merged: Record<number, string> = {};
      for (const ligne of next.lignes) {
        const dirty = mergeUnsaved && dirtyRef.current.has(ligne.idRubriqueBudgetaire);
        if (dirty && prev[ligne.idRubriqueBudgetaire] != null) {
          merged[ligne.idRubriqueBudgetaire] = prev[ligne.idRubriqueBudgetaire];
        } else {
          merged[ligne.idRubriqueBudgetaire] =
            ligne.engagementEnCoursBrut > 0 ? String(ligne.engagementEnCoursBrut) : '';
        }
      }
      if (!mergeUnsaved) dirtyRef.current = new Set();
      return merged;
    });
  }, []);

  const load = useCallback(
    async (
      params?: {
        libelleItemAE?: string;
        idGroupeItemAE?: number | null;
        mois?: number | null;
      },
      mergeUnsaved = false,
    ) => {
      const libelle = (params?.libelleItemAE ?? itemAE).trim();
      if (!libelle) {
        setLoadError(null);
        setGrille(null);
        return;
      }
      const groupe =
        params?.idGroupeItemAE !== undefined
          ? params.idGroupeItemAE
          : idGroupe === ''
            ? null
            : idGroupe;
      const moisCible = params?.mois !== undefined ? params.mois : mois;

      setLoading(true);
      setLoadError(null);
      try {
        const next = await fetchGrilleImputationAe(idDemande, {
          libelleItemAE: libelle,
          idGroupeItemAE: groupe,
          mois: moisCible,
        });
        applyGrille(next, mergeUnsaved);
      } catch (err) {
        setLoadError(apiErrorMessage(err, "Impossible de charger la grille d'imputation AE."));
      } finally {
        setLoading(false);
      }
    },
    [idDemande, itemAE, idGroupe, mois, applyGrille],
  );

  useEffect(() => {
    let cancelled = false;
    void (async () => {
      try {
        const [actions, groupes] = await Promise.all([
          fetchActionsExploitation({ idExercice }),
          fetchGroupesItemAE(),
        ]);
        if (cancelled) return;
        setActionsAE(actions);
        setGroupesAE(groupes.filter((g) => g.actif));
      } catch {
        if (!cancelled) {
          setActionsAE([]);
          setGroupesAE([]);
        }
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [idExercice]);

  useEffect(() => {
    if (initialLoadDoneRef.current) return;
    initialLoadDoneRef.current = true;
    const initial = (itemSolliciteInitial ?? '').trim();
    if (!initial) return;
    void load({ libelleItemAE: initial }, false);
  }, [itemSolliciteInitial, load]);

  const taux = grille?.tauxConversion && grille.tauxConversion > 0 ? grille.tauxConversion : 0;

  const lignesCalculees = useMemo(() => {
    if (!grille) return [];
    return grille.lignes.map((ligne) => {
      const brut = parseMontant(amounts[ligne.idRubriqueBudgetaire] ?? '');
      const usd = taux > 0 ? calculerMontantUsd(brut, taux) : 0;
      const autresMoisUsd = Math.max(
        0,
        (ligne.engagementEnCoursAnnuelUsd ?? 0) - ligne.engagementEnCoursUsd,
      );
      return {
        ...ligne,
        brut,
        usd,
        dispoAnnuel: ligne.budgetAnnuel - ligne.creditEngageAnnuel - autresMoisUsd - usd,
      };
    });
  }, [grille, amounts, taux]);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return lignesCalculees;
    return lignesCalculees.filter(
      (l) => l.codeRubrique.toLowerCase().includes(q) || l.libelle.toLowerCase().includes(q),
    );
  }, [lignesCalculees, search]);

  const draftBrut = useMemo(
    () => lignesCalculees.reduce((s, l) => s + l.brut, 0),
    [lignesCalculees],
  );
  const draftUsd = useMemo(
    () => lignesCalculees.reduce((s, l) => s + l.usd, 0),
    [lignesCalculees],
  );

  const scopeItem = (grille?.libelleItemAE ?? itemAE).trim();
  const scopeMois = grille?.mois ?? mois;

  const autresBrut = useMemo(() => {
    if (!grille) return 0;
    return grille.lignesExistantes
      .filter((l) => !sameAeScope(l, scopeItem, scopeMois))
      .reduce((s, l) => s + l.montantBrut, 0);
  }, [grille, scopeItem, scopeMois]);

  const autresUsd = useMemo(() => {
    if (!grille) return 0;
    return grille.lignesExistantes
      .filter((l) => !sameAeScope(l, scopeItem, scopeMois))
      .reduce((s, l) => s + l.montantUsd, 0);
  }, [grille, scopeItem, scopeMois]);

  const totalBrut = autresBrut + draftBrut;
  const totalUsd = autresUsd + draftUsd;
  const ecartBrut = (grille?.montantBrut ?? 0) - totalBrut;
  const ecartUsd = (grille?.montantUsd ?? 0) - totalUsd;
  const repartitionOk = totalImputationsOk(totalUsd, grille?.montantUsd ?? 0) && totalUsd > 0;

  useEffect(() => {
    onRepartitionChangeRef.current?.(repartitionOk);
  }, [repartitionOk]);

  const setAmount = (idRb: number, value: string) => {
    dirtyRef.current.add(idRb);
    setAmounts((prev) => ({ ...prev, [idRb]: sanitizeNumericInput(value) }));
  };

  const toggleLigne = (ligne: LigneGrilleImputationAe, checked: boolean) => {
    dirtyRef.current.add(ligne.idRubriqueBudgetaire);
    setAmounts((prev) => ({
      ...prev,
      [ligne.idRubriqueBudgetaire]: checked ? prev[ligne.idRubriqueBudgetaire] || '' : '',
    }));
  };

  const confirmDiscardIfDirty = async (title: string, message: string): Promise<boolean> => {
    if (dirtyRef.current.size === 0) return true;
    return msgBox.confirm({
      title,
      message,
      confirmLabel: 'Continuer',
    });
  };

  const handleItemBlur = async () => {
    const next = itemAE.trim();
    if (!next) {
      setGrille(null);
      return;
    }
    if (grille && normalizeItem(grille.libelleItemAE) === normalizeItem(next)) return;
    const ok = await confirmDiscardIfDirty(
      "Changer d'Item AE",
      'Les montants non enregistrés de cet item / mois seront perdus. Continuer ?',
    );
    if (!ok) {
      if (grille) setItemAE(grille.libelleItemAE);
      return;
    }
    await load({ libelleItemAE: next }, false);
  };

  const handleGroupe = async (raw: string) => {
    const next = raw === '' ? null : Number(raw);
    const current = idGroupe === '' ? null : idGroupe;
    if (next === current) return;
    if (itemAE.trim() && grille) {
      const ok = await confirmDiscardIfDirty(
        'Changer de groupe AE',
        'Les montants non enregistrés de cet item / mois seront perdus. Continuer ?',
      );
      if (!ok) return;
    }
    setIdGroupe(next ?? '');
    if (itemAE.trim()) {
      await load({ idGroupeItemAE: next }, false);
    }
  };

  const handleMois = async (raw: string) => {
    const next = raw === '' ? null : Number(raw);
    if (next === mois) return;
    if (itemAE.trim() && grille) {
      const ok = await confirmDiscardIfDirty(
        'Changer de mois',
        'Les montants non enregistrés de cet item / mois seront perdus. Continuer ?',
      );
      if (!ok) return;
    }
    setMois(next);
    if (itemAE.trim()) {
      await load({ mois: next }, false);
    }
  };

  const handleCreateGroupe = async () => {
    const libelle = window.prompt('Libellé du nouveau groupe AE :')?.trim();
    if (!libelle) return;
    try {
      const created = await createGroupeItemAE(libelle);
      setGroupesAE((prev) => [...prev, created].sort((a, b) => a.libelle.localeCompare(b.libelle)));
      setIdGroupe(created.idGroupeItemAE);
      if (itemAE.trim()) {
        await load({ idGroupeItemAE: created.idGroupeItemAE }, true);
      }
      void msgBox.success('Groupe AE créé.');
    } catch (err) {
      void msgBox.error(apiErrorMessage(err, 'Impossible de créer le groupe AE.'));
    }
  };

  const handleSave = async () => {
    const libelle = itemAE.trim();
    if (!grille || !libelle) return;
    setSaving(true);
    try {
      const next = await enregistrerImputationsAe(idDemande, {
        libelleItemAE: libelle,
        idGroupeItemAE: idGroupe === '' ? null : idGroupe,
        mois,
        lignes: lignesCalculees
          .filter((l) => l.brut > 0)
          .map((l) => ({ idRubriqueBudgetaire: l.idRubriqueBudgetaire, montantBrut: l.brut })),
      });
      dirtyRef.current = new Set();
      applyGrille(next, false);
      void msgBox.success('Imputation AE enregistrée.');
      onSaved?.();
    } catch (err) {
      void msgBox.error(apiErrorMessage(err, "Enregistrement de l'imputation AE impossible."));
    } finally {
      setSaving(false);
    }
  };

  const locked = readOnly || disabled || saving;
  const itemReady = itemAE.trim().length > 0;

  if (loading && !grille && itemReady) {
    return <LoadingState label="Chargement de la grille AE…" />;
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
          required
          size="small"
          label="Item AE"
          value={itemAE}
          onChange={(e) => setItemAE(e.target.value)}
          onBlur={() => void handleItemBlur()}
          onKeyDown={(e) => {
            if (e.key === 'Enter') {
              e.preventDefault();
              (e.target as HTMLInputElement).blur();
            }
          }}
          disabled={locked}
          sx={{ minWidth: 220, flex: 1 }}
          slotProps={{ htmlInput: { list: datalistId } }}
        />
        {actionsAE.length > 0 && (
          <datalist id={datalistId}>
            {actionsAE.map((a) => (
              <option key={a} value={a} />
            ))}
          </datalist>
        )}
        <TextField
          select
          size="small"
          label="Groupe AE"
          value={idGroupe === '' ? '' : String(idGroupe)}
          onChange={(e) => void handleGroupe(e.target.value)}
          disabled={locked}
          sx={{ minWidth: 180 }}
        >
          <MenuItem value="">
            <em>Aucun</em>
          </MenuItem>
          {groupesAE.map((g) => (
            <MenuItem key={g.idGroupeItemAE} value={String(g.idGroupeItemAE)}>
              {g.libelle}
            </MenuItem>
          ))}
        </TextField>
        {!readOnly && (
          <Button variant="text" size="small" onClick={() => void handleCreateGroupe()} disabled={locked}>
            Créer un groupe…
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
          label="Rechercher une rubrique"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
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
              label={`${l.libelleItemAE} · ${moisLabel(l.mois)} · ${l.codeRubrique} · ${formatMontantUsd(l.montantUsd)}`}
            />
          ))}
        </Stack>
      )}

      {!itemReady && (
        <Alert severity="info">Saisissez un Item AE pour charger la grille d&apos;imputation.</Alert>
      )}
      {loadError && <Alert severity="warning">{loadError}</Alert>}
      {itemReady && !loading && !grille && !loadError && (
        <Alert severity="warning">Grille AE indisponible.</Alert>
      )}

      {grille && (
        <TableContainer
          sx={{
            height: { xs: '70vh', md: 'calc(100vh - 260px)' },
            minHeight: { xs: 420, md: 640 },
            maxHeight: 'calc(100vh - 200px)',
            border: '1px solid',
            borderColor: 'divider',
            borderRadius: 1,
          }}
        >
          <Table size="small" stickyHeader sx={{ minWidth: 1100, tableLayout: 'fixed' }}>
            <TableHead>
              <TableRow>
                <TableCell
                  sx={{
                    ...stickyCell(0, false, true),
                    minWidth: CODE_WIDTH,
                    width: CODE_WIDTH,
                    fontWeight: 700,
                  }}
                >
                  Code
                </TableCell>
                <TableCell
                  sx={{
                    ...stickyCell(CODE_WIDTH, false, true),
                    minWidth: LIBELLE_WIDTH,
                    width: LIBELLE_WIDTH,
                    fontWeight: 700,
                  }}
                >
                  Libellé
                </TableCell>
                <TableCell align="center" sx={{ fontWeight: 700 }}>
                  État
                </TableCell>
                <TableCell
                  align="right"
                  sx={{ fontWeight: 700, whiteSpace: 'nowrap', bgcolor: 'var(--ef-surface-secondary)' }}
                >
                  {`Engagement en cours (${grille.devise})`}
                </TableCell>
                {['Budget annuel', 'Crédit engagé annuel', 'Crédit disponible annuel'].map(
                  (label, index) => (
                    <TableCell
                      key={label}
                      align="right"
                      sx={{
                        fontWeight: 700,
                        whiteSpace: 'nowrap',
                        bgcolor: 'var(--ef-surface-secondary)',
                        ...(index === 0
                          ? { borderLeft: '2px solid', borderLeftColor: 'divider' }
                          : {}),
                      }}
                    >
                      {label}
                    </TableCell>
                  ),
                )}
              </TableRow>
            </TableHead>
            <TableBody>
              {filtered.map((ligne) => {
                const checked = ligne.brut > 0;
                return (
                  <TableRow key={ligne.idRubriqueBudgetaire} hover selected={checked}>
                    <TableCell
                      sx={{
                        ...stickyCell(0, checked),
                        fontVariantNumeric: 'tabular-nums',
                        whiteSpace: 'nowrap',
                        minWidth: CODE_WIDTH,
                        width: CODE_WIDTH,
                      }}
                    >
                      {ligne.codeRubrique}
                    </TableCell>
                    <TableCell
                      sx={{
                        ...stickyCell(CODE_WIDTH, checked),
                        minWidth: LIBELLE_WIDTH,
                        width: LIBELLE_WIDTH,
                      }}
                    >
                      {ligne.libelle}
                    </TableCell>
                    <TableCell align="center" padding="checkbox">
                      <Checkbox
                        size="small"
                        checked={checked}
                        disabled={locked}
                        onChange={(e) => toggleLigne(ligne, e.target.checked)}
                      />
                    </TableCell>
                    <TableCell align="right" sx={{ minWidth: 150 }}>
                      <TextField
                        size="small"
                        value={amounts[ligne.idRubriqueBudgetaire] ?? ''}
                        onChange={(e) => setAmount(ligne.idRubriqueBudgetaire, e.target.value)}
                        onPaste={(e) => {
                          e.preventDefault();
                          setAmount(
                            ligne.idRubriqueBudgetaire,
                            sanitizeNumericInput(e.clipboardData.getData('text')),
                          );
                        }}
                        disabled={locked}
                        slotProps={{
                          htmlInput: {
                            inputMode: 'decimal',
                            autoComplete: 'off',
                            'aria-label': `Engagement en cours ${ligne.codeRubrique}`,
                          },
                        }}
                        sx={{
                          width: 128,
                          '& input': { textAlign: 'right', fontVariantNumeric: 'tabular-nums' },
                        }}
                      />
                    </TableCell>
                    <TableCell
                      align="right"
                      sx={{
                        fontVariantNumeric: 'tabular-nums',
                        whiteSpace: 'nowrap',
                        borderLeft: '2px solid',
                        borderLeftColor: 'divider',
                      }}
                    >
                      {formatMontantUsd(ligne.budgetAnnuel)}
                    </TableCell>
                    <TableCell
                      align="right"
                      sx={{ fontVariantNumeric: 'tabular-nums', whiteSpace: 'nowrap' }}
                    >
                      {formatMontantUsd(ligne.creditEngageAnnuel)}
                    </TableCell>
                    <TableCell
                      align="right"
                      sx={{
                        fontVariantNumeric: 'tabular-nums',
                        whiteSpace: 'nowrap',
                        color: montantColor(ligne.dispoAnnuel),
                        fontWeight: 600,
                      }}
                    >
                      {formatMontantUsd(ligne.dispoAnnuel)}
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </TableContainer>
      )}
    </Stack>
  );
}
