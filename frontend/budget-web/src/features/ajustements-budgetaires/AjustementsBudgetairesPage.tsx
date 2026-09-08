import AddIcon from '@mui/icons-material/Add';
import RemoveIcon from '@mui/icons-material/Remove';
import {
  Alert,
  Box,
  Button,
  Chip,
  Collapse,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
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
import {
  DataTable,
  ErrorState,
  FilterZone,
  LoadingState,
  PrimaryButton,
  SearchableSelect,
  SecondaryButton,
  AmountField,
  useMsgBox,
  type DataTableColumn,
} from '../../components';
import { useAuth } from '../auth';
import { formatMontantUsd, hasPerm } from '../suivi-previsions/suiviUtils';
import {
  annulerAjustementBudgetaire,
  createAjustementBudgetaire,
  fetchAjustementHistoriqueLigne,
  fetchAjustementLignesDisponibles,
  fetchAjustementsBudgetaires,
  fetchExercices,
  validerAjustementBudgetaire,
  type AjustementBudgetaire,
  type AjustementHistoriqueLigne,
  type AjustementLigneCandidate,
  type Exercice,
} from '../../services/apiClient';

type AjustRow = AjustementBudgetaire & { id: string };

type UbGroup = {
  idUB: number;
  codeUB: string;
  libelleUB: string;
  lignes: AjustementLigneCandidate[];
  montantTotal: number;
};

type DeptGroup = {
  key: string;
  idDepartement: number;
  codeDepartement: string;
  libelleDepartement: string;
  idVersion: number;
  numeroVersion: number;
  annee: number;
  estExerciceCourant: boolean;
  ubs: UbGroup[];
  nbLignes: number;
  montantTotal: number;
};

/** Même logique que Soumissions : regroupement Département (puis UB → lignes). */
function groupByDepartement(lignes: AjustementLigneCandidate[]): DeptGroup[] {
  const deptMap = new Map<string, DeptGroup>();

  for (const row of lignes) {
    const deptKey = `${row.idVersion}-${row.idDepartement}`;
    let dept = deptMap.get(deptKey);
    if (!dept) {
      dept = {
        key: deptKey,
        idDepartement: row.idDepartement,
        codeDepartement: row.codeDepartement,
        libelleDepartement: row.libelleDepartement,
        idVersion: row.idVersion,
        numeroVersion: row.numeroVersion,
        annee: row.annee,
        estExerciceCourant: row.estExerciceCourant,
        ubs: [],
        nbLignes: 0,
        montantTotal: 0,
      };
      deptMap.set(deptKey, dept);
    }

    let ub = dept.ubs.find((u) => u.idUB === row.idUB);
    if (!ub) {
      ub = {
        idUB: row.idUB,
        codeUB: row.codeUB,
        libelleUB: row.libelleUB,
        lignes: [],
        montantTotal: 0,
      };
      dept.ubs.push(ub);
    }

    ub.lignes.push(row);
    ub.montantTotal += row.montantActuel;
    dept.nbLignes += 1;
    dept.montantTotal += row.montantActuel;
  }

  for (const dept of deptMap.values()) {
    dept.ubs.sort((a, b) => a.codeUB.localeCompare(b.codeUB, 'fr', { sensitivity: 'base' }));
    for (const ub of dept.ubs) {
      ub.lignes.sort(
        (a, b) =>
          a.typeBudget.localeCompare(b.typeBudget, 'fr') ||
          a.libelleLigne.localeCompare(b.libelleLigne, 'fr', { sensitivity: 'base' }),
      );
    }
  }

  return [...deptMap.values()].sort(
    (a, b) =>
      a.libelleDepartement.localeCompare(b.libelleDepartement, 'fr') ||
      a.numeroVersion - b.numeroVersion,
  );
}

function apiErrorMessage(err: unknown, fallback: string): string {
  const ax = err as { response?: { data?: { message?: string } }; message?: string };
  return ax.response?.data?.message ?? ax.message ?? fallback;
}

function statutColor(statut: string): 'default' | 'info' | 'success' | 'error' {
  switch ((statut ?? '').toUpperCase()) {
    case 'BROUILLON':
      return 'info';
    case 'VALIDE':
      return 'success';
    case 'ANNULE':
      return 'error';
    default:
      return 'default';
  }
}

export function AjustementsBudgetairesPage() {
  const { user } = useAuth();
  const msgBox = useMsgBox();
  const canWrite = hasPerm(user, 'ajustements.ecrire');
  const canValidate = hasPerm(user, 'ajustements.valider');
  const canRead =
    canWrite || canValidate || hasPerm(user, 'ajustements.lire') || hasPerm(user, 'versions.valider');

  const [lignes, setLignes] = useState<AjustementLigneCandidate[]>([]);
  const [brouillons, setBrouillons] = useState<AjustRow[]>([]);
  const [exercices, setExercices] = useState<Exercice[]>([]);
  const [exerciceCourant, setExerciceCourant] = useState<Exercice | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  /** '' = exercice courant (défaut métier). */
  const [exerciceFilter, setExerciceFilter] = useState('');
  const [expandedDept, setExpandedDept] = useState<Record<string, boolean>>({});
  const [expandedUb, setExpandedUb] = useState<Record<string, boolean>>({});

  const [ajustOpen, setAjustOpen] = useState(false);
  const [ligneCible, setLigneCible] = useState<AjustementLigneCandidate | null>(null);
  const [montantNouveau, setMontantNouveau] = useState('');
  const [motif, setMotif] = useState('');
  const [saving, setSaving] = useState(false);

  const [histo, setHisto] = useState<AjustementHistoriqueLigne | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const ex = await fetchExercices();
      const courant =
        [...ex]
          .filter((e) => (e.statut ?? '').toUpperCase() === 'OUVERT')
          .sort((a, b) => b.annee - a.annee)[0] ?? null;
      setExercices(ex);
      setExerciceCourant(courant);

      const idExercice =
        exerciceFilter === ''
          ? courant?.idExercice
          : exerciceFilter === 'all'
            ? undefined
            : Number(exerciceFilter);

      const params = { idExercice };
      const [dispo, aj] = await Promise.all([
        fetchAjustementLignesDisponibles(params),
        fetchAjustementsBudgetaires({
          idExercice: idExercice,
          statut: 'BROUILLON',
        }),
      ]);
      setLignes(dispo);
      setBrouillons(aj.map((a) => ({ ...a, id: String(a.idAjustement) })));
    } catch (err) {
      setError(apiErrorMessage(err, 'Impossible de charger les lignes budgétaires validées.'));
    } finally {
      setLoading(false);
    }
  }, [exerciceFilter]);

  useEffect(() => {
    void load();
  }, [load]);

  const filteredLignes = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return lignes;
    return lignes.filter(
      (r) =>
        r.codeUB.toLowerCase().includes(q) ||
        r.libelleUB.toLowerCase().includes(q) ||
        r.libelleDepartement.toLowerCase().includes(q) ||
        r.codeDepartement.toLowerCase().includes(q) ||
        r.libelleLigne.toLowerCase().includes(q) ||
        r.typeBudget.toLowerCase().includes(q),
    );
  }, [lignes, search]);

  const groupes = useMemo(() => groupByDepartement(filteredLignes), [filteredLignes]);

  const developperTout = () => {
    const nextDept: Record<string, boolean> = {};
    const nextUb: Record<string, boolean> = {};
    for (const g of groupes) {
      nextDept[g.key] = true;
      for (const ub of g.ubs) nextUb[`${g.key}-${ub.idUB}`] = true;
    }
    setExpandedDept(nextDept);
    setExpandedUb(nextUb);
  };

  const reduireTout = () => {
    const nextDept: Record<string, boolean> = {};
    const nextUb: Record<string, boolean> = {};
    for (const g of groupes) {
      nextDept[g.key] = false;
      for (const ub of g.ubs) nextUb[`${g.key}-${ub.idUB}`] = false;
    }
    setExpandedDept(nextDept);
    setExpandedUb(nextUb);
  };

  const variationPreview = useMemo(() => {
    if (!ligneCible) return null;
    const n = Number(montantNouveau.replace(',', '.'));
    if (Number.isNaN(n)) return null;
    return n - ligneCible.montantActuel;
  }, [ligneCible, montantNouveau]);

  const brouillonColumns: DataTableColumn<AjustRow>[] = [
    { id: 'reference', label: 'Référence', mobile: 'title', render: (r) => r.reference },
    { id: 'ub', label: 'UB', mobile: 'meta', render: (r) => r.codeUB },
    { id: 'ligne', label: 'Ligne', mobile: 'subtitle', render: (r) => r.libelleLigne },
    {
      id: 'var',
      label: 'Variation',
      align: 'right',
      mobile: 'meta',
      render: (r) => formatMontantUsd(r.variation),
    },
    {
      id: 'statut',
      label: 'Statut',
      mobile: 'meta',
      render: (r) => <Chip size="small" label={r.statut} color={statutColor(r.statut)} />,
    },
  ];

  function openAjuster(row: AjustementLigneCandidate) {
    if (!row.estExerciceCourant || !canWrite || row.hasBrouillon) return;
    setLigneCible(row);
    setMontantNouveau(String(row.montantActuel));
    setMotif('');
    setAjustOpen(true);
  }

  async function handleCreate() {
    if (!ligneCible || !motif.trim() || montantNouveau === '') return;
    setSaving(true);
    try {
      await createAjustementBudgetaire({
        idPrevision: ligneCible.idPrevision,
        montantNouveau: Number(montantNouveau.replace(',', '.')),
        motif: motif.trim(),
      });
      void msgBox.success('Ajustement créé en brouillon. Validez-le pour appliquer le nouveau budget.');
      setAjustOpen(false);
      setLigneCible(null);
      await load();
    } catch (err) {
      void msgBox.error(apiErrorMessage(err, 'Création impossible.'));
    } finally {
      setSaving(false);
    }
  }

  async function handleValider(row: AjustRow) {
    const ok = await msgBox.confirm({
      title: "Valider l'ajustement",
      message: `Appliquer ${formatMontantUsd(row.montantNouveau)} (variation ${formatMontantUsd(row.variation)}) ?`,
      confirmLabel: 'Valider',
    });
    if (!ok) return;
    try {
      await validerAjustementBudgetaire(row.idAjustement);
      void msgBox.success('Ajustement validé — budget applicable mis à jour.');
      await load();
    } catch (err) {
      void msgBox.error(apiErrorMessage(err, 'Validation impossible.'));
    }
  }

  async function handleAnnulerBrouillon(row: AjustRow) {
    const ok = await msgBox.confirm({
      title: 'Annuler le brouillon',
      message: 'Annuler définitivement cet ajustement en brouillon ?',
      confirmLabel: 'Annuler le brouillon',
      danger: true,
    });
    if (!ok) return;
    try {
      await annulerAjustementBudgetaire(row.idAjustement);
      void msgBox.success('Brouillon annulé.');
      await load();
    } catch (err) {
      void msgBox.error(apiErrorMessage(err, 'Annulation impossible.'));
    }
  }

  if (!canRead) {
    return (
      <Box>
        <Alert severity="warning">Vous n&apos;avez pas l&apos;autorisation de consulter les ajustements.</Alert>
      </Box>
    );
  }

  return (
    <Box>
      {!exerciceCourant && (
        <Alert severity="info" sx={{ mb: 2 }}>
          Aucun exercice ouvert — consultation seule.
        </Alert>
      )}

      <Paper sx={{ p: 2, mb: 2 }}>
        <FilterZone
          columns={{ xs: 1, sm: 2, md: 2, lg: 2, xl: 2 }}
          search={
            <TextField
              size="small"
              fullWidth
              label="Recherche"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Département, UB, ligne…"
            />
          }
          actions={
            <>
              <SecondaryButton size="small" onClick={developperTout}>
                Développer tout
              </SecondaryButton>
              <SecondaryButton size="small" onClick={reduireTout}>
                Réduire tout
              </SecondaryButton>
            </>
          }
        >
          <SearchableSelect
            label="Exercice"
            value={exerciceFilter}
            onChange={setExerciceFilter}
            allowEmpty
            fullWidth
            options={[
              {
                value: '',
                label: `Courant${exerciceCourant ? ` (${exerciceCourant.annee})` : ''}`,
              },
              { value: 'all', label: 'Tous (consultation)' },
              ...exercices.map((ex) => ({
                value: String(ex.idExercice),
                label: `${ex.annee} — ${ex.statut}`,
              })),
            ]}
            helperText={exerciceFilter === '' ? 'Par défaut : exercice courant' : undefined}
          />
        </FilterZone>
      </Paper>

      {loading && <LoadingState label="Chargement des lignes budgétaires validées…" />}
      {!loading && error && lignes.length === 0 && (
        <ErrorState message={error} onRetry={() => void load()} />
      )}

      {!loading && groupes.length === 0 && (
        <Paper sx={{ p: 3 }}>
          <Typography color="text.secondary" sx={{ textAlign: 'center' }}>
            Aucune ligne budgétaire validée pour ces filtres.
          </Typography>
        </Paper>
      )}

      {!loading &&
        groupes.map((g) => {
          const deptOpen = expandedDept[g.key] !== false;
          return (
            <Paper key={g.key} sx={{ mb: 2, overflow: 'hidden' }}>
              <Box
                sx={{
                  px: 2,
                  py: 1.5,
                  bgcolor: 'var(--ef-surface-secondary)',
                  borderBottom: deptOpen ? '1px solid var(--ef-border)' : 'none',
                }}
              >
                <Stack direction="row" spacing={1} sx={{ justifyContent: 'space-between', alignItems: 'flex-start' }}>
                  <Box sx={{ minWidth: 0, flex: 1 }}>
                    <Typography variant="h6" sx={{ fontWeight: 700 }}>
                      {g.libelleDepartement}
                    </Typography>
                    <Typography variant="body2" color="text.secondary">
                      {g.codeDepartement} · V{g.numeroVersion} · Exercice {g.annee} · {g.ubs.length} UB ·{' '}
                      {g.nbLignes} ligne(s)
                    </Typography>
                    <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: 'wrap', mt: 0.75, alignItems: 'center' }}>
                      <Typography variant="body2" sx={{ fontWeight: 700 }}>
                        TOTAL {formatMontantUsd(g.montantTotal)}
                      </Typography>
                      {g.estExerciceCourant ? (
                        <Chip size="small" color="success" label="Exercice courant" />
                      ) : (
                        <Chip size="small" color="warning" label="Ajustement non autorisé" />
                      )}
                    </Stack>
                  </Box>
                  <IconButton
                    size="small"
                    aria-label={deptOpen ? 'Réduire' : 'Développer'}
                    onClick={() => setExpandedDept((p) => ({ ...p, [g.key]: !deptOpen }))}
                  >
                    {deptOpen ? <RemoveIcon /> : <AddIcon />}
                  </IconButton>
                </Stack>
              </Box>

              <Collapse in={deptOpen} unmountOnExit>
                {g.ubs.map((ub) => {
                  const ubKey = `${g.key}-${ub.idUB}`;
                  const ubOpen = expandedUb[ubKey] !== false;
                  return (
                    <Box key={ubKey} sx={{ borderTop: '1px solid', borderColor: 'divider' }}>
                      <Stack
                        direction="row"
                        spacing={1}
                        sx={{
                          px: 2,
                          py: 1,
                          alignItems: 'center',
                          justifyContent: 'space-between',
                          bgcolor: 'action.hover',
                        }}
                      >
                        <Box>
                          <Typography variant="body2" sx={{ fontWeight: 700 }}>
                            {ub.codeUB}
                          </Typography>
                          <Typography variant="caption" color="text.secondary">
                            {ub.libelleUB} · {ub.lignes.length} ligne(s) · {formatMontantUsd(ub.montantTotal)}
                          </Typography>
                        </Box>
                        <IconButton
                          size="small"
                          aria-label={ubOpen ? 'Réduire UB' : 'Développer UB'}
                          onClick={() => setExpandedUb((p) => ({ ...p, [ubKey]: !ubOpen }))}
                        >
                          {ubOpen ? <RemoveIcon fontSize="small" /> : <AddIcon fontSize="small" />}
                        </IconButton>
                      </Stack>
                      <Collapse in={ubOpen} unmountOnExit>
                        <TableContainer sx={{ overflowX: 'auto' }}>
                          <Table size="small">
                            <TableHead>
                              <TableRow>
                                <TableCell sx={{ display: { xs: 'none', md: 'table-cell' } }}>Nature</TableCell>
                                <TableCell>Ligne</TableCell>
                                <TableCell align="right" sx={{ display: { xs: 'none', md: 'table-cell' } }}>
                                  Budget initial
                                </TableCell>
                                <TableCell align="right">Budget actuel</TableCell>
                                <TableCell>Statut</TableCell>
                                <TableCell align="right">Action</TableCell>
                              </TableRow>
                            </TableHead>
                            <TableBody>
                              {ub.lignes.map((row) => {
                                const peutAjuster =
                                  canWrite && row.estExerciceCourant && !row.hasBrouillon;
                                return (
                                  <TableRow key={row.idPrevision} hover>
                                    <TableCell sx={{ display: { xs: 'none', md: 'table-cell' } }}>
                                      {row.typeBudget}
                                    </TableCell>
                                    <TableCell>{row.libelleLigne}</TableCell>
                                    <TableCell align="right" sx={{ display: { xs: 'none', md: 'table-cell' } }}>
                                      {formatMontantUsd(row.montantInitial)}
                                    </TableCell>
                                    <TableCell align="right" sx={{ fontWeight: 700 }}>
                                      {formatMontantUsd(row.montantActuel)}
                                    </TableCell>
                                    <TableCell>
                                      {!row.estExerciceCourant ? (
                                        <Chip size="small" label="Exercice clôturé / non courant" />
                                      ) : row.hasBrouillon ? (
                                        <Chip size="small" color="info" label="Brouillon en cours" />
                                      ) : row.nbAjustementsValides > 0 ? (
                                        <Chip
                                          size="small"
                                          color="info"
                                          label={`${row.nbAjustementsValides} ajust.`}
                                        />
                                      ) : (
                                        '—'
                                      )}
                                    </TableCell>
                                    <TableCell align="right">
                                      <Stack
                                        direction="row"
                                        spacing={0.75}
                                        useFlexGap
                                        sx={{ flexWrap: 'wrap', justifyContent: 'flex-end' }}
                                      >
                                        {peutAjuster && (
                                          <Button size="small" variant="contained" onClick={() => openAjuster(row)}>
                                            Ajuster
                                          </Button>
                                        )}
                                        {!row.estExerciceCourant && canWrite && (
                                          <Typography variant="caption" color="text.secondary">
                                            Ajustement non autorisé
                                          </Typography>
                                        )}
                                        <Button
                                          size="small"
                                          variant="outlined"
                                          onClick={async () => {
                                            try {
                                              setHisto(await fetchAjustementHistoriqueLigne(row.idPrevision));
                                            } catch (err) {
                                              void msgBox.error(apiErrorMessage(err, 'Historique inaccessible.'));
                                            }
                                          }}
                                        >
                                          Historique
                                        </Button>
                                      </Stack>
                                    </TableCell>
                                  </TableRow>
                                );
                              })}
                            </TableBody>
                          </Table>
                        </TableContainer>
                      </Collapse>
                    </Box>
                  );
                })}
              </Collapse>
            </Paper>
          );
        })}

      {!loading && brouillons.length > 0 && (
        <Box sx={{ mt: 3 }}>
          <Typography variant="subtitle2" sx={{ mb: 1 }}>
            Brouillons d&apos;ajustement en attente de validation
          </Typography>
          <DataTable
            columns={brouillonColumns}
            rows={brouillons}
            actions={[
              {
                id: 'valider',
                label: 'Valider',
                hidden: () => !canValidate,
                onClick: (r) => void handleValider(r),
              },
              {
                id: 'annuler',
                label: 'Annuler brouillon',
                hidden: () => !canWrite,
                onClick: (r) => void handleAnnulerBrouillon(r),
              },
            ]}
          />
        </Box>
      )}

      <Dialog open={ajustOpen} onClose={() => setAjustOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>Ajuster la ligne budgétaire</DialogTitle>
        <DialogContent>
          {ligneCible && (
            <Stack spacing={2} sx={{ mt: 1 }}>
              <Typography variant="body2">
                {ligneCible.libelleDepartement} · {ligneCible.codeUB} · [{ligneCible.typeBudget}]{' '}
                {ligneCible.libelleLigne}
              </Typography>
              <Typography variant="body2">
                Budget initial : <strong>{formatMontantUsd(ligneCible.montantInitial)}</strong>
              </Typography>
              <Typography variant="body2">
                Budget actuel : <strong>{formatMontantUsd(ligneCible.montantActuel)}</strong>
              </Typography>
              <AmountField
                label="Nouveau montant (USD)"
                value={montantNouveau}
                onChange={setMontantNouveau}
                currency="USD"
              />
              {variationPreview != null && (
                <Typography variant="body2">
                  Variation :{' '}
                  <strong style={{ color: variationPreview < 0 ? '#c62828' : '#2e7d32' }}>
                    {formatMontantUsd(variationPreview)}
                  </strong>
                </Typography>
              )}
              <TextField
                label="Motif"
                size="small"
                multiline
                minRows={3}
                value={motif}
                onChange={(e) => setMotif(e.target.value)}
                required
              />
            </Stack>
          )}
        </DialogContent>
        <DialogActions>
          <SecondaryButton onClick={() => setAjustOpen(false)}>Fermer</SecondaryButton>
          <PrimaryButton
            disabled={saving || !motif.trim() || montantNouveau === ''}
            onClick={() => void handleCreate()}
          >
            Créer le brouillon
          </PrimaryButton>
        </DialogActions>
      </Dialog>

      <Dialog open={!!histo} onClose={() => setHisto(null)} fullWidth maxWidth="sm">
        <DialogTitle>Historique de la ligne</DialogTitle>
        <DialogContent>
          {histo && (
            <Stack spacing={1.5} sx={{ mt: 1 }}>
              <Typography variant="subtitle2">{histo.libelleLigne}</Typography>
              <Typography>
                Budget initial : <strong>{formatMontantUsd(histo.montantInitial)}</strong>
              </Typography>
              <Divider />
              {histo.ajustements
                .filter((a) => a.statut === 'VALIDE')
                .map((a) => (
                  <Box key={a.idAjustement}>
                    <Typography variant="body2">
                      {a.reference} — {formatMontantUsd(a.variation)} — {a.motif}
                    </Typography>
                  </Box>
                ))}
              {histo.ajustements.filter((a) => a.statut === 'VALIDE').length === 0 && (
                <Typography variant="body2" color="text.secondary">
                  Aucun ajustement validé.
                </Typography>
              )}
              <Divider />
              <Typography>
                Budget actuel : <strong>{formatMontantUsd(histo.montantActuel)}</strong>
              </Typography>
            </Stack>
          )}
        </DialogContent>
        <DialogActions>
          <SecondaryButton onClick={() => setHisto(null)}>Fermer</SecondaryButton>
        </DialogActions>
      </Dialog>

    </Box>
  );
}
