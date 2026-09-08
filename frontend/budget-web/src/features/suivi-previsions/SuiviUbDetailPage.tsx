import {
  Alert,
  Box,
  Button,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Paper,
  Stack,
  Tab,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Tabs,
  TextField,
  Typography,
} from '@mui/material';
import { useCallback, useEffect, useState } from 'react';
import { useLocation, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import {
  ErrorState,
  KpiRow,
  LoadingState,
  PageHeader,
  PrimaryButton,
  SecondaryButton,
  StatCard,
  useMsgBox,
} from '../../components';
import {
  controlerPrevisionUb,
  fetchSuiviUbDetail,
  fetchSuiviUbLignes,
  rejeterPrevisionUb,
  validerPrevisionUb,
  type SuiviUbDetail,
  type SuiviUbDetailLignes,
} from '../../services/apiClient';
import { BRAND_NAME } from '../../theme';
import { useResponsive } from '../../theme/useResponsive';
import { useAuth } from '../auth';
import {
  DocumentActionResultDialog,
  buildDocumentActionResult,
  type DocumentActionResultState,
} from '../documents-previsions';
import { apiErrorMessage } from '../rubriques-budgetaires/rubriqueUtils';
import {
  buildPrevisionsContextUrl,
  formatDateSuivi,
  formatMontantSuivi,
  MOIS_COURTS,
  normalizeStatut,
  peutControlerUb,
  peutRejeterUb,
  peutValiderUb,
  statutChipSx,
  versionLabel,
} from './suiviUtils';

type TabKey = 'DC' | 'AE' | 'BI';

/**
 * Colonnes de détail masquées sous `md` : sur téléphone on garde l'identifiant,
 * la désignation et le montant. Le détail complet revient dès `md`.
 */
const CELLULE_DETAIL = { display: { xs: 'none', md: 'table-cell' } } as const;

export function SuiviUbDetailPage() {
  const { idVersion: idVersionParam, idUB: idUBParam } = useParams();
  const location = useLocation();
  const [searchParams] = useSearchParams();
  const fromMes =
    location.pathname.startsWith('/budget/suivi-mes-previsions/') ||
    searchParams.get('from') === 'mes';
  const navigate = useNavigate();
  const { user } = useAuth();
  const msgBox = useMsgBox();
  const { isMobile } = useResponsive();

  const idVersion = Number(idVersionParam);
  const idUB = Number(idUBParam);

  const [detail, setDetail] = useState<SuiviUbDetail | null>(null);
  const [tab, setTab] = useState<TabKey>('DC');
  const [lignes, setLignes] = useState<SuiviUbDetailLignes | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadingLignes, setLoadingLignes] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [rejectOpen, setRejectOpen] = useState(false);
  const [rejectMotif, setRejectMotif] = useState('');
  const [rejectError, setRejectError] = useState<string | null>(null);
  const [docResult, setDocResult] = useState<DocumentActionResultState | null>(null);

  const loadDetail = useCallback(async () => {
    if (!idVersion || !idUB) return;
    setLoading(true);
    setError(null);
    try {
      setDetail(await fetchSuiviUbDetail(idVersion, idUB));
    } catch (err) {
      setError(apiErrorMessage(err, 'Impossible de charger le détail UB.'));
      setDetail(null);
    } finally {
      setLoading(false);
    }
  }, [idVersion, idUB]);

  const loadLignes = useCallback(async (codeType: TabKey) => {
    if (!idVersion || !idUB) return;
    setLoadingLignes(true);
    try {
      setLignes(await fetchSuiviUbLignes(idVersion, idUB, codeType));
    } catch (err) {
      void msgBox.error(apiErrorMessage(err, `Impossible de charger le détail ${codeType}.`));
      setLignes(null);
    } finally {
      setLoadingLignes(false);
    }
  }, [idVersion, idUB]);

  useEffect(() => {
    void loadDetail();
  }, [loadDetail]);

  useEffect(() => {
    if (detail) void loadLignes(tab);
  }, [detail, tab, loadLignes]);

  const backTo = fromMes ? '/budget/suivi-mes-previsions' : '/budget/soumissions';

  const runControler = async () => {
    if (!detail) return;
    setBusy(true);
    try {
      const wf = await controlerPrevisionUb(detail.idVersion, detail.idUB);
      const result = buildDocumentActionResult(wf.document, 'CTL');
      if (result) setDocResult(result);
      await loadDetail();
    } catch (err) {
      void msgBox.error(apiErrorMessage(err, 'Échec du contrôle.'));
    } finally {
      setBusy(false);
    }
  };

  const runValider = async () => {
    if (!detail) return;
    setBusy(true);
    try {
      const wf = await validerPrevisionUb(detail.idVersion, detail.idUB);
      const result = buildDocumentActionResult(wf.document, 'VAL');
      if (result) setDocResult(result);
      await loadDetail();
    } catch (err) {
      void msgBox.error(apiErrorMessage(err, 'Échec de la validation.'));
    } finally {
      setBusy(false);
    }
  };

  const runRejeter = async () => {
    if (!detail) return;
    const motif = rejectMotif.trim();
    if (!motif) {
      setRejectError('Le motif du rejet est obligatoire.');
      return;
    }
    setBusy(true);
    setRejectError(null);
    try {
      const wf = await rejeterPrevisionUb(detail.idVersion, detail.idUB, motif);
      setRejectOpen(false);
      setRejectMotif('');
      const result = buildDocumentActionResult(wf.document, 'REJ');
      if (result) setDocResult(result);
      await loadDetail();
    } catch (err) {
      void msgBox.error(apiErrorMessage(err, 'Échec du rejet.'));
    } finally {
      setBusy(false);
    }
  };

  if (loading) return <LoadingState label="Chargement…" />;
  if (error && !detail) {
    return <ErrorState title="Erreur" message={error} onRetry={() => void loadDetail()} />;
  }
  if (!detail) return null;

  const statutUb = normalizeStatut(detail.statut);
  const canControler = detail.peutControler || peutControlerUb(detail.statut, user);
  const canValider = detail.peutValider || peutValiderUb(detail.statut, user);
  const canRejeter = detail.peutRejeter || peutRejeterUb(detail.statut, user);
  const showMonthlyDc = (lignes?.lignesDC ?? []).some((l) => !l.estSection && l.repartitionDefinie);

  return (
    <Box>
      <PageHeader
        compact
        breadcrumbs={[
          { label: BRAND_NAME, to: '/dashboard' },
          { label: 'Budget' },
          { label: fromMes ? 'Suivi de mes prévisions' : 'Soumissions', to: backTo },
          { label: detail.codeUB },
        ]}
        actions={[
          <SecondaryButton key="retour" size="small" onClick={() => navigate(backTo)}>
            Retour
          </SecondaryButton>,
          (statutUb === 'BROUILLON' || statutUb === 'REJETEE') && (
            <PrimaryButton
              key="reprendre"
              size="small"
              onClick={() =>
                navigate(
                  buildPrevisionsContextUrl({
                    idExercice: detail.idExercice,
                    idVersion: detail.idVersion,
                    idUB: detail.idUB,
                  }),
                )
              }
            >
              {statutUb === 'REJETEE' ? 'Corriger' : 'Continuer'}
            </PrimaryButton>
          ),
          canControler && (
            <PrimaryButton key="controler" size="small" disabled={busy} onClick={() => void runControler()}>
              Contrôler
            </PrimaryButton>
          ),
          canValider && (
            <PrimaryButton
              key="valider"
              size="small"
              disabled={busy}
              onClick={() =>
                void (async () => {
                  const ok = await msgBox.confirm({
                    title: 'Valider cette prévision budgétaire ?',
                    message: `${detail.libelleDepartement} — UB ${detail.codeUB}\nDC : ${formatMontantSuivi(detail.montantDC)} · AE : ${formatMontantSuivi(detail.montantAE)} · BI : ${formatMontantSuivi(detail.montantBI)}\nTOTAL : ${formatMontantSuivi(detail.montantTotal)}\n\nSeule cette UB passera en VALIDEE (lecture seule). Les autres UB conservent leur propre statut.`,
                    confirmLabel: 'Valider',
                  });
                  if (ok) void runValider();
                })()
              }
            >
              Valider
            </PrimaryButton>
          ),
          canRejeter && (
            <Button
              key="rejeter"
              size="small"
              color="error"
              variant="outlined"
              disabled={busy}
              onClick={() => setRejectOpen(true)}
            >
              Rejeter
            </Button>
          ),
        ]}
      />

      <Paper sx={{ px: 1.75, py: 1.5, mb: 1.5 }}>
        <Stack direction={{ xs: 'column', md: 'row' }} spacing={1.5} sx={{ justifyContent: 'space-between', alignItems: { md: 'flex-start' } }}>
          <Box>
            <Typography variant="overline" color="text.secondary" sx={{ lineHeight: 1.4 }}>
              {detail.libelleDepartement}
            </Typography>
            <Typography variant="h6" component="h1" sx={{ fontWeight: 700, lineHeight: 1.3, mt: 0.25 }}>
              {detail.codeUB} — {detail.libelleUB}
            </Typography>
            <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
              Exercice {detail.annee} · {versionLabel(detail.numeroVersion, detail.libelleVersion)}
            </Typography>
          </Box>
          <Box sx={{ textAlign: { md: 'right' } }}>
            <Chip
              label={statutUb}
              variant="outlined"
              sx={{ fontWeight: 700, ...statutChipSx(detail.statut) }}
            />
            {statutUb === 'VALIDEE' && (
              <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 0.5 }}>
                Lecture seule — prévision validée
              </Typography>
            )}
            <Typography variant="body2" sx={{ mt: 1 }}>
              Soumis par : {detail.nomUtilisateurSoumission ?? '—'}
            </Typography>
            <Typography variant="caption" color="text.secondary">
              Le : {formatDateSuivi(detail.dateSoumission)}
            </Typography>
          </Box>
        </Stack>

        {statutUb === 'REJETEE' && (
          <Alert severity="warning" sx={{ mt: 2 }}>
            <Typography sx={{ fontWeight: 700 }}>Motif du rejet</Typography>
            <Typography variant="body2">{detail.motifRejet || '—'}</Typography>
            <Typography variant="caption" color="text.secondary">
              {detail.nomUtilisateurRejet ?? '—'} — {formatDateSuivi(detail.dateRejet)}
            </Typography>
          </Alert>
        )}
      </Paper>

      <KpiRow>
        {(
          [
            ['DC', detail.montantDC],
            ['AE', detail.montantAE],
            ['BI', detail.montantBI],
            ['TOTAL', detail.montantTotal],
          ] as const
        ).map(([label, value]) => (
          <StatCard key={label} title={label} value={formatMontantSuivi(value)} />
        ))}
      </KpiRow>

      <Paper sx={{ mb: 2 }}>
        <Tabs value={tab} onChange={(_, v: TabKey) => setTab(v)} sx={{ px: 1, borderBottom: 1, borderColor: 'divider' }}>
          <Tab label="DC" value="DC" />
          <Tab label="AE" value="AE" />
          <Tab label="BI" value="BI" />
        </Tabs>
        <Box sx={{ p: 1.5 }}>
          {loadingLignes && <LoadingState label={`Chargement ${tab}…`} />}
          {!loadingLignes && tab === 'DC' && (
            <TableContainer sx={{ overflowX: 'auto' }}>
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>RB</TableCell>
                    <TableCell>Désignation</TableCell>
                    <TableCell align="right">Montant annuel</TableCell>
                    {showMonthlyDc &&
                      !isMobile &&
                      MOIS_COURTS.map((m) => (
                        <TableCell key={m} align="right">
                          {m}
                        </TableCell>
                      ))}
                    {showMonthlyDc && !isMobile && (
                      <TableCell align="right">Cumul</TableCell>
                    )}
                  </TableRow>
                </TableHead>
                <TableBody>
                  {(lignes?.lignesDC ?? []).map((l, idx) => (
                    <TableRow
                      key={l.estSection ? `g-${l.idGroupeRB}-${idx}` : `r-${l.idRB}`}
                      sx={l.estSection ? { bgcolor: 'var(--ef-surface-secondary)' } : undefined}
                    >
                      <TableCell sx={{ fontWeight: l.estSection ? 700 : 500 }}>
                        {l.estSection ? l.codeGroupe : l.codeRB}
                      </TableCell>
                      <TableCell sx={{ fontWeight: l.estSection ? 700 : 400 }}>{l.libelle}</TableCell>
                      <TableCell align="right" sx={{ fontWeight: l.estSection ? 700 : 400 }}>
                        {formatMontantSuivi(l.montantAnnuel)}
                      </TableCell>
                      {showMonthlyDc &&
                        !isMobile &&
                        (l.estSection || !l.repartitionDefinie
                          ? [
                              ...MOIS_COURTS.map((m) => (
                                <TableCell key={m} align="right">
                                  {l.estSection ? '' : '—'}
                                </TableCell>
                              )),
                              <TableCell key="c" align="right">
                                {!l.estSection && !l.repartitionDefinie ? (
                                  <Typography variant="caption" color="text.secondary">
                                    Non définie
                                  </Typography>
                                ) : (
                                  ''
                                )}
                              </TableCell>,
                            ]
                          : [
                              ...(l.montantsMensuels ?? Array(12).fill(0)).map((m, i) => (
                                <TableCell key={i} align="right">
                                  {formatMontantSuivi(m)}
                                </TableCell>
                              )),
                              <TableCell key="c" align="right" sx={{ fontWeight: 650 }}>
                                {formatMontantSuivi((l.montantsMensuels ?? []).reduce((a, b) => a + b, 0))}
                              </TableCell>,
                            ])}
                    </TableRow>
                  ))}
                  {(lignes?.lignesDC ?? []).length === 0 && (
                    <TableRow>
                      <TableCell colSpan={3}>
                        <Typography color="text.secondary">Aucune ligne DC.</Typography>
                      </TableCell>
                    </TableRow>
                  )}
                </TableBody>
              </Table>
            </TableContainer>
          )}

          {!loadingLignes && tab === 'AE' && (
            <TableContainer sx={{ overflowX: 'auto' }}>
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>Action</TableCell>
                    <TableCell sx={CELLULE_DETAIL}>Groupe</TableCell>
                    <TableCell>RB</TableCell>
                    <TableCell>Désignation</TableCell>
                    <TableCell sx={CELLULE_DETAIL}>Mode</TableCell>
                    <TableCell align="right">Montant</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {(lignes?.lignesAE ?? []).map((l, i) => (
                    <TableRow key={`${l.libelleItemAE}-${l.idRB}-${i}`}>
                      <TableCell sx={{ fontWeight: 650 }}>{l.libelleItemAE}</TableCell>
                      <TableCell sx={CELLULE_DETAIL}>{l.libelleGroupeAE ?? '—'}</TableCell>
                      <TableCell>{l.codeRB ?? '—'}</TableCell>
                      <TableCell>{l.libelleRB ?? '—'}</TableCell>
                      <TableCell sx={CELLULE_DETAIL}>
                        {l.codeMode}
                        {l.codeMode?.toUpperCase() === 'MENSUEL' && !l.repartitionDefinie && (
                          <Typography variant="caption" color="text.secondary" sx={{ display: 'block' }}>
                            Répartition mensuelle non définie
                          </Typography>
                        )}
                      </TableCell>
                      <TableCell align="right">{formatMontantSuivi(l.montantAnnuel)}</TableCell>
                    </TableRow>
                  ))}
                  {(lignes?.lignesAE ?? []).length === 0 && (
                    <TableRow>
                      <TableCell colSpan={6}>
                        <Typography color="text.secondary">Aucune ligne AE.</Typography>
                      </TableCell>
                    </TableRow>
                  )}
                </TableBody>
              </Table>
            </TableContainer>
          )}

          {!loadingLignes && tab === 'BI' && (
            <TableContainer sx={{ overflowX: 'auto' }}>
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>Item</TableCell>
                    <TableCell>Désignation</TableCell>
                    <TableCell>Détail</TableCell>
                    <TableCell sx={CELLULE_DETAIL}>Mode</TableCell>
                    <TableCell align="right">Montant</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {(lignes?.lignesBI ?? []).map((l, i) => (
                    <TableRow key={`${l.idItemBI}-${l.detailBI}-${i}`}>
                      <TableCell sx={{ fontWeight: 650 }}>{l.codeItem}</TableCell>
                      <TableCell>{l.libelleItem}</TableCell>
                      <TableCell>{l.detailBI ?? '—'}</TableCell>
                      <TableCell sx={CELLULE_DETAIL}>
                        {l.codeMode}
                        {l.codeMode?.toUpperCase() === 'MENSUEL' && !l.repartitionDefinie && (
                          <Typography variant="caption" color="text.secondary" sx={{ display: 'block' }}>
                            Répartition mensuelle non définie
                          </Typography>
                        )}
                      </TableCell>
                      <TableCell align="right">{formatMontantSuivi(l.montantAnnuel)}</TableCell>
                    </TableRow>
                  ))}
                  {(lignes?.lignesBI ?? []).length === 0 && (
                    <TableRow>
                      <TableCell colSpan={5}>
                        <Typography color="text.secondary">Aucune ligne BI.</Typography>
                      </TableCell>
                    </TableRow>
                  )}
                </TableBody>
              </Table>
            </TableContainer>
          )}
        </Box>
      </Paper>

      <Dialog open={rejectOpen} onClose={() => !busy && setRejectOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>Rejeter la prévision</DialogTitle>
        <DialogContent>
          <Stack spacing={0.75} sx={{ mb: 1.5, mt: 0.5 }}>
            <Typography variant="body2">
              Département : <strong>{detail.libelleDepartement}</strong>
            </Typography>
            <Typography variant="body2">
              UB : <strong>{detail.codeUB}</strong> — {detail.libelleUB}
            </Typography>
            <Typography variant="body2">DC : {formatMontantSuivi(detail.montantDC)}</Typography>
            <Typography variant="body2">AE : {formatMontantSuivi(detail.montantAE)}</Typography>
            <Typography variant="body2">BI : {formatMontantSuivi(detail.montantBI)}</Typography>
            <Typography variant="body2" sx={{ fontWeight: 700 }}>
              Total : {formatMontantSuivi(detail.montantTotal)}
            </Typography>
          </Stack>
          <TextField
            autoFocus
            fullWidth
            multiline
            minRows={3}
            label="Motif du rejet *"
            value={rejectMotif}
            onChange={(e) => setRejectMotif(e.target.value)}
            error={Boolean(rejectError)}
            helperText={rejectError}
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setRejectOpen(false)} disabled={busy}>
            Annuler
          </Button>
          <Button color="error" variant="contained" disabled={busy} onClick={() => void runRejeter()}>
            Rejeter
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
