import EditIcon from '@mui/icons-material/Edit';
import DeleteOutlinedIcon from '@mui/icons-material/DeleteOutlined';
import {
  Alert,
  Box,
  Button,
  Chip,
  Divider,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Typography,
} from '@mui/material';
import axios from 'axios';
import { useEffect, useState } from 'react';
import { Link as RouterLink, useNavigate, useParams } from 'react-router-dom';
import {
  DetailPanel,
  DocumentActions,
  ErrorState,
  LoadingState,
  PageHeader,
  useMsgBox,
} from '../../components';
import { useAuth } from '../auth';
import {
  deleteDemandePaiement,
  fetchDemandePaiement,
  previewDemandePaiementPiece,
  downloadDemandePaiementPiece,
  type DemandePaiementDetailComplet,
} from '../../services/apiClient';
import { DemandeDetailSummaryRail } from './DemandeDetailSummaryRail';
import { buildAssignationViewFromDetail } from './demandePaiementAssignationFromApi';
import { DemandePaiementACorrigerAlert } from './DemandePaiementACorrigerAlert';
import { labelTimelineRetourDemandeur } from './demandePaiementRetourLabels';
import { DemandePaiementStatusBadge } from './DemandePaiementStatusBadge';
import { DemandePaiementWorkflowBlock } from './DemandePaiementWorkflowBlock';
import { DetailFieldGrid } from './DetailFieldGrid';
import { EntiteValidationSection } from './EntiteValidationSection';
import { NumberedSectionBlock } from './NumberedSectionBlock';
import { useDemandePaiementRoutageLecture } from './useDemandePaiementRoutageLecture';
import { DemandePaiementMutationLockProvider } from './useDemandePaiementMutationLock';
import { useNotifyDemandePaiementMutated } from './useDemandePaiementListInvalidation';
import { labelOperationAudit } from './paiementBudgetUtils';
import {
  apiErrorMessage,
  canChargeDpm,
  canEcrirePaiements,
  canLirePaiements,
  canSupprimerBrouillon,
  estModifiableDemandeur,
  formatDateFr,
  formatMontantDevise,
  formatMontantUsd,
  normalizeStatutDpm,
} from './paiementUtils';

function isRequestAborted(err: unknown): boolean {
  return (
    axios.isCancel(err) ||
    (axios.isAxiosError(err) &&
      (err.code === 'ERR_CANCELED' || err.message === 'canceled'))
  );
}

export function PaiementDetailPage() {
  const { id } = useParams();
  const idDemande = Number(id);
  const navigate = useNavigate();
  const msgBox = useMsgBox();
  const notifyMutated = useNotifyDemandePaiementMutated();
  const { user } = useAuth();
  const canRead = canLirePaiements(user);
  const canWrite = canEcrirePaiements(user);
  const isChargeDpm = canChargeDpm(user);

  const [data, setData] = useState<DemandePaiementDetailComplet | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [deleting, setDeleting] = useState(false);
  const routageLecture = useDemandePaiementRoutageLecture(idDemande, canRead);

  useEffect(() => {
    if (!canRead || !idDemande) return;

    const controller = new AbortController();
    setLoading(true);
    setError(null);

    void (async () => {
      try {
        const [detail] = await Promise.all([
          fetchDemandePaiement(idDemande, { signal: controller.signal }),
          routageLecture.reload(),
        ]);
        if (controller.signal.aborted) return;
        setData(detail);
      } catch (err) {
        if (isRequestAborted(err) || controller.signal.aborted) return;
        setError(apiErrorMessage(err, 'Impossible de charger la demande.'));
        setData(null);
      } finally {
        if (!controller.signal.aborted) setLoading(false);
      }
    })();

    return () => controller.abort();
  }, [canRead, idDemande, routageLecture.reload]);

  const d = data?.demande;
  const editable = estModifiableDemandeur(d?.statut);

  const reload = () => {
    if (!canRead || !idDemande) return;
    setLoading(true);
    setError(null);
    void Promise.all([fetchDemandePaiement(idDemande), routageLecture.reload()])
      .then(([detail]) => setData(detail))
      .catch((err) => {
        setError(apiErrorMessage(err, 'Impossible de charger la demande.'));
        setData(null);
      })
      .finally(() => setLoading(false));
  };

  const handleDelete = async () => {
    const demande = data?.demande;
    if (!demande || !canSupprimerBrouillon(user, demande.statut) || deleting) return;

    const ok = await msgBox.confirm({
      title: 'Supprimer définitivement ?',
      message: `La demande « ${demande.reference} » sera supprimée de façon permanente avec toutes ses pièces jointes. Cette action est irréversible.`,
      confirmLabel: 'Supprimer définitivement',
    });
    if (!ok) return;

    setDeleting(true);
    try {
      await deleteDemandePaiement(demande.idDemandePaiement);
      notifyMutated();
      void msgBox.success('Demande supprimée définitivement.');
      navigate('/paiements', { replace: true });
    } catch (err) {
      void msgBox.error(apiErrorMessage(err, 'Suppression impossible.'));
    } finally {
      setDeleting(false);
    }
  };

  if (!canRead) {
    return <Alert severity="warning">Permission insuffisante (`paiements.lire`).</Alert>;
  }
  if (loading) return <LoadingState label="Chargement de la demande…" />;
  if (error || !d) {
    return <ErrorState message={error ?? 'Demande introuvable.'} onRetry={reload} />;
  }

  const timelineStops = [
    { key: 'creation', label: 'Création', date: d.dateCreation, done: true },
    { key: 'soumission', label: 'Soumission', date: d.dateSoumission, done: !!d.dateSoumission },
    { key: 'reception', label: 'Réception Budgets', date: d.dateReception, done: !!d.dateReception },
    { key: 'controle', label: 'Contrôle budgétaire', date: d.dateControle, done: !!d.dateControle },
    {
      key: 'retour',
      label: labelTimelineRetourDemandeur(),
      date: d.dateRetour,
      done: !!d.dateRetour && (d.statut ?? '').toUpperCase() === 'A_CORRIGER',
    },
    { key: 'visa', label: 'Visa budgétaire', date: d.dateVisa, done: !!d.dateVisa },
  ];

  const destinationLabel =
    d.destinationSolliciteeAffichage ??
    (d.typeBudgetSollicite === 'BI'
      ? `BI / IVT${d.itemSollicite ? ` — Item N° ${d.itemSollicite}` : ''}`
      : d.itemSollicite
        ? `${d.typeBudgetSollicite} — Item N° ${d.itemSollicite}`
        : d.typeBudgetSollicite) ??
    '—';

  const modePaiementLabel =
    d.modePaiementSollicite === 'CAISSE'
      ? 'Caisse'
      : d.modePaiementSollicite === 'BANQUE'
        ? 'Banque'
        : d.modePaiementSollicite ?? '—';

  const demandeurLabel =
    d.codeDemandeur || d.libelleDemandeur
      ? [d.codeDemandeur, d.libelleDemandeur].filter(Boolean).join(' — ')
      : '—';

  const assignationView = buildAssignationViewFromDetail(d, user?.idUtilisateur);

  return (
    <>
      <PageHeader
        entityLabel={d.reference}
        entityCaption={d.objet}
        breadcrumbs={[
          { label: 'e-Finance', to: '/dashboard' },
          { label: 'Paiements', to: '/paiements' },
          { label: d.reference },
        ]}
        actions={
          <Stack
            direction="row"
            spacing={1}
            useFlexGap
            sx={{ alignItems: 'center', flexWrap: 'wrap' }}
          >
            <DemandePaiementStatusBadge statut={d.statut} size="medium" />
            {isChargeDpm &&
              ['SOUMISE', 'EN_TRAITEMENT_DPM', 'BROUILLON'].includes(normalizeStatutDpm(d.statut)) && (
                <Button
                  variant="contained"
                  component={RouterLink}
                  to={`/paiements/charge-dpm/${d.idDemandePaiement}`}
                >
                  Traiter (Chargé DP)
                </Button>
              )}
            {canWrite && editable && (
              <Button
                variant="contained"
                startIcon={<EditIcon />}
                component={RouterLink}
                to={`/paiements/${d.idDemandePaiement}/modifier`}
              >
                Modifier la demande
              </Button>
            )}
            {canSupprimerBrouillon(user, d.statut) && (
              <Button
                variant="outlined"
                color="error"
                startIcon={<DeleteOutlinedIcon />}
                disabled={deleting}
                onClick={() => void handleDelete()}
              >
                Supprimer définitivement
              </Button>
            )}
          </Stack>
        }
      />

      {(d.statut ?? '').toUpperCase() === 'A_CORRIGER' && (
        <DemandePaiementACorrigerAlert
          demande={d}
          retoursDestinataires={routageLecture.retoursDestinataires}
          description="En tant que demandeur, vous pouvez modifier cette demande puis la resoumettre selon le circuit habituel."
        />
      )}

      <Box
        sx={{
          display: 'flex',
          flexDirection: { xs: 'column', md: 'row' },
          alignItems: 'flex-start',
          gap: 2,
          pb: 3,
        }}
      >
        <Box sx={{ flex: '1 1 auto', minWidth: 0, width: '100%' }}>
          <DemandePaiementMutationLockProvider>
            <EntiteValidationSection
              demande={d}
              retoursDestinataires={routageLecture.retoursDestinataires}
              onUpdated={(updated) => setData((prev) => (prev ? { ...prev, demande: updated } : prev))}
            />
          </DemandePaiementMutationLockProvider>

          <Stack spacing={2} sx={{ mt: 2 }}>
            <NumberedSectionBlock step={1} showConnector>
              <DetailPanel title="Informations générales">
                <DetailFieldGrid
                  fields={[
                    { label: "Date d'émission", value: formatDateFr(d.dateEmission) },
                    { label: "Lieu d'émission", value: d.lieuEmission ?? '—' },
                    { label: 'Exercice', value: String(d.anneeExercice) },
                    { label: 'Demandeur', value: demandeurLabel },
                    { label: 'Objet', value: d.objet, fullWidth: true },
                    { label: 'Compte de section', value: d.compteSection ?? '—', fullWidth: true },
                  ]}
                />
              </DetailPanel>
            </NumberedSectionBlock>

            <NumberedSectionBlock step={2} showConnector>
              <DetailPanel title="Bénéficiaires">
                {d.beneficiaires.length === 0 ? (
                  <Typography variant="body2" color="text.secondary">
                    Aucun bénéficiaire.
                  </Typography>
                ) : (
                  <Table size="small">
                    <TableHead>
                      <TableRow>
                        <TableCell sx={{ display: { xs: 'none', md: 'table-cell' } }}>Type</TableCell>
                        <TableCell>Nom</TableCell>
                        <TableCell sx={{ display: { xs: 'none', md: 'table-cell' } }}>Coordonnées</TableCell>
                        <TableCell>Principal</TableCell>
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {d.beneficiaires.map((b) => (
                        <TableRow key={b.idBeneficiaire}>
                          <TableCell sx={{ display: { xs: 'none', md: 'table-cell' } }}>
                            {b.typeBeneficiaire}
                          </TableCell>
                          <TableCell sx={{ fontWeight: b.estPrincipal ? 650 : 400 }}>
                            {b.nomComplet}
                          </TableCell>
                          <TableCell sx={{ display: { xs: 'none', md: 'table-cell' } }}>
                            {[b.matricule, b.fonction, b.raisonSociale, b.banque, b.numeroCompte]
                              .filter(Boolean)
                              .join(' · ') || '—'}
                          </TableCell>
                          <TableCell>
                            {b.estPrincipal ? (
                              <Chip size="small" label="Principal" color="primary" variant="outlined" />
                            ) : (
                              '—'
                            )}
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                )}
              </DetailPanel>
            </NumberedSectionBlock>

            <NumberedSectionBlock step={3} showConnector>
              <DetailPanel title="Imputations">
                {d.imputations.length === 0 ? (
                  <Typography variant="body2" color="text.secondary">
                    Aucune imputation enregistrée.
                  </Typography>
                ) : (
                  <Table size="small">
                    <TableHead>
                      <TableRow>
                        <TableCell sx={{ display: { xs: 'none', md: 'table-cell' } }}>#</TableCell>
                        <TableCell>Type</TableCell>
                        <TableCell>Détail</TableCell>
                        <TableCell align="right">Montant USD</TableCell>
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {d.imputations.map((i) => (
                        <TableRow key={i.idImputation}>
                          <TableCell sx={{ display: { xs: 'none', md: 'table-cell' } }}>{i.ordre}</TableCell>
                          <TableCell>{i.codeTypeBudget}</TableCell>
                          <TableCell>
                            {i.libelleItemAE || i.detailBI || (i.mois ? `Mois ${i.mois}` : '—')}
                            {!i.idBudgetLigne && (
                              <Typography variant="caption" sx={{ display: 'block' }} color="text.secondary">
                                Prévision : 0 USD
                              </Typography>
                            )}
                          </TableCell>
                          <TableCell align="right">{formatMontantUsd(i.montantUsd)}</TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                )}
              </DetailPanel>
            </NumberedSectionBlock>

            <NumberedSectionBlock step={4} showConnector>
              <DetailPanel title="Pièces justificatives">
                {data.piecesManquantes.length > 0 && (
                  <Alert severity="warning" sx={{ mb: 1.5 }}>
                    Pièces manquantes : {data.piecesManquantes.map((p) => p.libelle).join(', ')}
                  </Alert>
                )}
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell>Libellé</TableCell>
                      <TableCell sx={{ display: { xs: 'none', md: 'table-cell' } }}>Type</TableCell>
                      <TableCell>Actions</TableCell>
                      <TableCell>Obligatoire</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {d.pieces.map((p) => (
                      <TableRow key={p.idPieceJointe}>
                        <TableCell>{p.libelle}</TableCell>
                        <TableCell sx={{ display: { xs: 'none', md: 'table-cell' } }}>
                          {p.codeTypePiece}
                        </TableCell>
                        <TableCell>
                          <DocumentActions
                            title={p.libelle}
                            fileName={p.nomFichierOriginal || 'piece'}
                            fileSizeBytes={p.tailleOctets}
                            showFileName={false}
                            loadPreview={() => previewDemandePaiementPiece(idDemande, p.idPieceJointe)}
                            loadDownload={() => downloadDemandePaiementPiece(idDemande, p.idPieceJointe)}
                          />
                        </TableCell>
                        <TableCell>
                          <Chip
                            size="small"
                            label={p.estObligatoire ? 'Obligatoire' : 'Facultative'}
                            color={p.estObligatoire ? 'warning' : 'default'}
                            variant="outlined"
                          />
                        </TableCell>
                      </TableRow>
                    ))}
                    {d.pieces.length === 0 && (
                      <TableRow>
                        <TableCell colSpan={4}>
                          <Typography variant="body2" color="text.secondary">
                            Aucune pièce jointe.
                          </Typography>
                        </TableCell>
                      </TableRow>
                    )}
                  </TableBody>
                </Table>
              </DetailPanel>
            </NumberedSectionBlock>

            <NumberedSectionBlock step={5} showConnector isLast>
              <DetailPanel title="Historique">
                <Stack spacing={1} sx={{ mb: 2 }}>
                  <Typography variant="subtitle2">Parcours (jusqu’au visa budgétaire)</Typography>
                  <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: 'wrap' }}>
                    {timelineStops.map((t) => (
                      <Chip
                        key={t.key}
                        size="small"
                        variant={t.done ? 'filled' : 'outlined'}
                        color={t.done ? 'primary' : 'default'}
                        label={`${t.label}${t.date ? ` · ${formatDateFr(t.date)}` : ''}`}
                      />
                    ))}
                  </Stack>
                </Stack>
                <Divider sx={{ mb: 1.5 }} />
                {data.historique.length === 0 ? (
                  <Typography variant="body2" color="text.secondary">
                    Aucune entrée d’audit.
                  </Typography>
                ) : (
                  <Table size="small">
                    <TableHead>
                      <TableRow>
                        <TableCell>Date</TableCell>
                        <TableCell>Opération</TableCell>
                        <TableCell sx={{ display: { xs: 'none', md: 'table-cell' } }}>Détail</TableCell>
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {data.historique.map((h) => (
                        <TableRow key={h.idAudit}>
                          <TableCell>{formatDateFr(h.dateHeure)}</TableCell>
                          <TableCell>{labelOperationAudit(h.operation)}</TableCell>
                          <TableCell sx={{ display: { xs: 'none', md: 'table-cell' } }}>
                            <Typography variant="caption" sx={{ wordBreak: 'break-all' }}>
                              {h.nouvellesValeurs ?? h.anciennesValeurs ?? '—'}
                            </Typography>
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                )}
              </DetailPanel>
            </NumberedSectionBlock>
          </Stack>
        </Box>

        <Box
          sx={{
            flex: { xs: '1 1 auto', md: '0 0 320px' },
            width: { xs: '100%', md: 320 },
            position: { md: 'sticky' },
            top: { md: 88 },
            alignSelf: { md: 'flex-start' },
            maxHeight: { md: 'calc(100vh - 104px)' },
            overflowY: { md: 'auto' },
          }}
        >
          <Stack spacing={2}>
            <DemandePaiementWorkflowBlock
              statut={d.statut}
              assignation={assignationView}
              routages={routageLecture.routages}
              retoursDestinataires={routageLecture.retoursDestinataires}
              routageLoading={routageLecture.loading}
            />
            <DemandeDetailSummaryRail
              reference={d.reference}
              montantLabel={formatMontantDevise(d.montantBrut, d.devise)}
              montantUsdLabel={d.montantUsd != null ? formatMontantUsd(d.montantUsd) : 'Non converti'}
              exerciceLabel={String(d.anneeExercice)}
              destinationLabel={destinationLabel}
              modePaiementLabel={modePaiementLabel}
              beneficiairesCount={d.beneficiaires.length}
              demandeurLabel={demandeurLabel}
              ubLabel={`${d.codeUB} — ${d.libelleUB}`}
              departementLabel={d.libelleDepartement ?? '—'}
              casDossierLabel={d.libelleCasDossier}
            />
          </Stack>
        </Box>
      </Box>
    </>
  );
}
