import {
  Alert,
  Box,
  Button,
  Checkbox,
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
  enregistrerImputationsDc,
  fetchGrilleImputationDc,
  type GrilleImputationDc,
  type LigneGrilleImputationDc,
} from '../../services/apiClient';
import {
  apiErrorMessage,
  calculerMontantUsd,
  formatMontantDevise,
  formatMontantUsd,
  MOIS_LABELS,
  totalImputationsOk,
} from './paiementUtils';

interface ImputationDcGrilleProps {
  idDemande: number;
  readOnly?: boolean;
  disabled?: boolean;
  canSave?: boolean;
  onRepartitionChange?: (complete: boolean) => void;
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

export function ImputationDcGrille({
  idDemande,
  readOnly = false,
  disabled = false,
  canSave = false,
  onRepartitionChange,
  onSaved,
}: ImputationDcGrilleProps) {
  const msgBox = useMsgBox();
  const [grille, setGrille] = useState<GrilleImputationDc | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [mois, setMois] = useState<number | null>(null);
  const [amounts, setAmounts] = useState<Record<number, string>>({});
  const dirtyRef = useRef<Set<number>>(new Set());
  const onRepartitionChangeRef = useRef(onRepartitionChange);
  onRepartitionChangeRef.current = onRepartitionChange;

  const applyGrille = useCallback((next: GrilleImputationDc, mergeUnsaved: boolean) => {
    setGrille(next);
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
    async (moisCible?: number, mergeUnsaved = false) => {
      setLoading(true);
      setLoadError(null);
      try {
        const next = await fetchGrilleImputationDc(idDemande, moisCible);
        applyGrille(next, mergeUnsaved);
      } catch (err) {
        setLoadError(apiErrorMessage(err, "Impossible de charger la grille d'imputation DC."));
      } finally {
        setLoading(false);
      }
    },
    [idDemande, applyGrille],
  );

  useEffect(() => {
    void load();
  }, [load]);

  const taux = grille?.tauxConversion && grille.tauxConversion > 0 ? grille.tauxConversion : 0;

  const lignesCalculees = useMemo(() => {
    if (!grille) return [];
    return grille.lignes.map((ligne) => {
      const brut = parseMontant(amounts[ligne.idRubriqueBudgetaire] ?? '');
      const usd = taux > 0 ? calculerMontantUsd(brut, taux) : 0;
      const autresMoisUsd = Math.max(0, (ligne.engagementEnCoursAnnuelUsd ?? 0) - ligne.engagementEnCoursUsd);
      return {
        ...ligne,
        brut,
        usd,
        dispoMensuel: ligne.budgetMensuel - ligne.creditEngageMensuel - usd,
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

  const totalBrut = useMemo(
    () => lignesCalculees.reduce((s, l) => s + l.brut, 0),
    [lignesCalculees],
  );
  const totalUsd = useMemo(
    () => lignesCalculees.reduce((s, l) => s + l.usd, 0),
    [lignesCalculees],
  );
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

  const toggleLigne = (ligne: LigneGrilleImputationDc, checked: boolean) => {
    dirtyRef.current.add(ligne.idRubriqueBudgetaire);
    setAmounts((prev) => ({
      ...prev,
      [ligne.idRubriqueBudgetaire]: checked
        ? prev[ligne.idRubriqueBudgetaire] || ''
        : '',
    }));
  };

  const handleMois = async (nextMois: number) => {
    if (nextMois === mois) return;
    if (dirtyRef.current.size > 0) {
      const ok = await msgBox.confirm({
        title: 'Changer de mois',
        message: 'Les montants non enregistrés de ce mois seront perdus. Continuer ?',
        confirmLabel: 'Changer de mois',
      });
      if (!ok) return;
    }
    await load(nextMois, false);
  };

  const handleSave = async () => {
    if (!grille || mois == null) return;
    setSaving(true);
    try {
      const next = await enregistrerImputationsDc(idDemande, {
        mois,
        lignes: lignesCalculees
          .filter((l) => l.brut > 0)
          .map((l) => ({ idRubriqueBudgetaire: l.idRubriqueBudgetaire, montantBrut: l.brut })),
      });
      dirtyRef.current = new Set();
      applyGrille(next, false);
      void msgBox.success('Imputation DC enregistrée.');
      onSaved?.();
    } catch (err) {
      void msgBox.error(apiErrorMessage(err, "Enregistrement de l'imputation DC impossible."));
    } finally {
      setSaving(false);
    }
  };

  if (loading && !grille) return <LoadingState label="Chargement de la grille DC…" />;
  if (loadError && !grille) return <Alert severity="error">{loadError}</Alert>;
  if (!grille || mois == null) return <Alert severity="warning">Grille DC indisponible.</Alert>;

  const locked = readOnly || disabled || saving;

  return (
    <Stack spacing={2}>
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
          { label: 'Taux utilisé', value: grille.tauxConversion != null ? String(grille.tauxConversion) : '—' },
          {
            label: 'Montant USD',
            value: grille.montantUsd != null ? formatMontantUsd(grille.montantUsd) : '—',
          },
          { label: 'UB', value: `${grille.codeUB} — ${grille.libelleUB}` },
          { label: 'Annuel prévu', value: formatMontantUsd(grille.budgetAnnuelUb) },
          { label: 'Annuel engagé', value: formatMontantUsd(grille.creditEngageAnnuelUb) },
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

      <Stack
        direction={{ xs: 'column', md: 'row' }}
        spacing={1.25}
        useFlexGap
        sx={{ alignItems: { md: 'center' }, flexWrap: 'wrap' }}
      >
        <TextField
          select
          size="small"
          label="Mois"
          value={mois}
          onChange={(e) => void handleMois(Number(e.target.value))}
          disabled={locked}
          sx={{ minWidth: 160 }}
        >
          {MOIS_LABELS.map((m) => (
            <MenuItem key={m.value} value={m.value}>
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
          onClick={() => void load(mois, true)}
          disabled={loading || saving}
        >
          Actualiser
        </Button>
        {canSave && !readOnly && (
          <Button variant="contained" onClick={() => void handleSave()} disabled={locked}>
            Enregistrer la grille
          </Button>
        )}
      </Stack>

      {loadError && <Alert severity="warning">{loadError}</Alert>}

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
        <Table size="small" stickyHeader sx={{ minWidth: 1480, tableLayout: 'fixed' }}>
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
              {[
                'Budget mois',
                'Crédit engagé mois',
                `Engagement en cours (${grille.devise})`,
                'Montant USD',
                'Crédit disponible mois',
              ].map((label) => (
                <TableCell
                  key={label}
                  align="right"
                  sx={{ fontWeight: 700, whiteSpace: 'nowrap', bgcolor: 'var(--ef-surface-secondary)' }}
                >
                  <Typography variant="caption" color="text.secondary" sx={{ display: 'block', fontWeight: 700 }}>
                    Mensuel
                  </Typography>
                  {label}
                </TableCell>
              ))}
              {[
                'Budget annuel',
                'Crédit engagé annuel',
                'Crédit disponible annuel',
              ].map((label, index) => (
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
                  <Typography variant="caption" color="text.secondary" sx={{ display: 'block', fontWeight: 700 }}>
                    Annuel
                  </Typography>
                  {label}
                </TableCell>
              ))}
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
                  <TableCell align="right" sx={{ fontVariantNumeric: 'tabular-nums', whiteSpace: 'nowrap' }}>
                    {formatMontantUsd(ligne.budgetMensuel)}
                  </TableCell>
                  <TableCell align="right" sx={{ fontVariantNumeric: 'tabular-nums', whiteSpace: 'nowrap' }}>
                    {formatMontantUsd(ligne.creditEngageMensuel)}
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
                  <TableCell align="right" sx={{ fontVariantNumeric: 'tabular-nums', whiteSpace: 'nowrap' }}>
                    {formatMontantUsd(ligne.usd)}
                  </TableCell>
                  <TableCell
                    align="right"
                    sx={{
                      fontVariantNumeric: 'tabular-nums',
                      whiteSpace: 'nowrap',
                      color: montantColor(ligne.dispoMensuel),
                      fontWeight: 600,
                    }}
                  >
                    {formatMontantUsd(ligne.dispoMensuel)}
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
                  <TableCell align="right" sx={{ fontVariantNumeric: 'tabular-nums', whiteSpace: 'nowrap' }}>
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
    </Stack>
  );
}
