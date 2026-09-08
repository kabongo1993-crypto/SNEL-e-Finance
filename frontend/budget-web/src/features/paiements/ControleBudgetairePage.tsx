import {
  Alert,
  Box,
  Button,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Grid,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from '@mui/material';
import { useCallback, useEffect, useMemo, useState } from 'react';
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
  controleBudgetaireDemandePaiement,
  downloadDemandePaiementPiece,
  previewDemandePaiementPiece,
  previewFicheImputationPdf,
  downloadFicheImputationPdf,
  fetchDemandePaiementComplet,
  fetchGroupesItemAE,
  fetchItemsBI,
  fetchRubriquesBudgetaires,
  fetchTypesBudget,
  fetchUnitesBudgetaires,
  addDemandePaiementImputation,
  retournerDemandePaiement,
  viserDemandePaiement,
  type ControleBudgetaireDto,
  type DemandePaiementDetailComplet,
  type GroupeItemAE,
  type ItemBI,
  type RubriqueBudgetaire,
  type TypeBudget,
  type UniteBudgetaire,
} from '../../services/apiClient';
import { ControleBudgetaireSummary } from './ControleBudgetaireSummary';
import { ControleImputationSection } from './ControleImputationSection';
import { DemandePaiementStatusBadge } from './DemandePaiementStatusBadge';
import { DemandePaiementTimeline } from './DemandePaiementTimeline';
import { ImputationBudgetaireForm, type ImputationDraft } from './ImputationBudgetaireForm';
import { ImputationAeGrille } from './ImputationAeGrille';
import { ImputationBiGrille } from './ImputationBiGrille';
import { ImputationDcGrille } from './ImputationDcGrille';
import { buildControleSummary } from './paiementBudgetUtils';
import {
  apiErrorMessage,
  canAccessPaiementsBudget,
  canControlerBudget,
  canImputerAe,
  canImputerBi,
  canImputerDc,
  canViserBudget,
  formatDateFr,
  formatDateTimeFr,
  formatMontantDevise,
  formatMontantUsd,
  normalizeStatutDpm,
} from './paiementUtils';
import {
  DemandePaiementMutationLockOverlay,
  getConflictUserMessage,
  useDemandePaiementMutationLock,
} from './useDemandePaiementMutationLock';
import {
  labelRetourAction,
  labelRetourDialogTitle,
  labelRetourSuccess,
  resolveRetourContextControle,
  type RetourActionContext,
} from './demandePaiementRetourLabels';
import { buildAssignationViewFromDetail } from './demandePaiementAssignationFromApi';
import { peutAfficherActionsMetierAssignation } from './demandePaiementAssignationUtils';
import { DemandePaiementWorkflowBlock } from './DemandePaiementWorkflowBlock';
import { useDemandePaiementRoutageLecture } from './useDemandePaiementRoutageLecture';
import { useNotifyDemandePaiementMutated } from './useDemandePaiementListInvalidation';

export function ControleBudgetairePage() {
  const { id } = useParams();
  const idDemande = Number(id);
  const navigate = useNavigate();
  const { user } = useAuth();

  const canAccess = canAccessPaiementsBudget(user);
  const canControler = canControlerBudget(user);
  const canViser = canViserBudget(user);
  const msgBox = useMsgBox();
  const mutationLock = useDemandePaiementMutationLock();
  const { isMutating, runMutation } = mutationLock;
  const notifyMutated = useNotifyDemandePaiementMutated();
  const routageLecture = useDemandePaiementRoutageLecture(idDemande, canAccess);

  const [data, setData] = useState<DemandePaiementDetailComplet | null>(null);
  const [controle, setControle] = useState<ControleBudgetaireDto | null>(null);
  const [ubs, setUbs] = useState<UniteBudgetaire[]>([]);
  const [rubriques, setRubriques] = useState<RubriqueBudgetaire[]>([]);
  const [itemsBI, setItemsBI] = useState<ItemBI[]>([]);
  const [groupesAE, setGroupesAE] = useState<GroupeItemAE[]>([]);
  const [typesBudget, setTypesBudget] = useState<TypeBudget[]>([]);
  const [showImputationForm, setShowImputationForm] = useState(false);
  const [loading, setLoading] = useState(true);
  const [controleLoading, setControleLoading] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [retourOpen, setRetourOpen] = useState(false);
  const [retourContext, setRetourContext] = useState<RetourActionContext>('controle_junior');
  const [retourMotif, setRetourMotif] = useState('');
  const [retourCommentaire, setRetourCommentaire] = useState('');
  const [retourError, setRetourError] = useState<string | null>(null);
  const [dcRepartitionOk, setDcRepartitionOk] = useState(false);
  const [aeRepartitionOk, setAeRepartitionOk] = useState(false);
  const [biRepartitionOk, setBiRepartitionOk] = useState(false);

  const load = useCallback(async () => {
    if (!idDemande) return;
    setLoading(true);
    setLoadError(null);
    try {
      const [detail, ubList, rb, bi, gr, tb, _routage] = await Promise.all([
        fetchDemandePaiementComplet(idDemande),
        fetchUnitesBudgetaires(),
        fetchRubriquesBudgetaires(),
        fetchItemsBI(),
        fetchGroupesItemAE(),
        fetchTypesBudget(),
        routageLecture.reload(),
      ]);
      void _routage;
      setData(detail);
      setUbs(ubList);
      setRubriques(rb);
      setItemsBI(bi);
      setGroupesAE(gr);
      setTypesBudget(tb);
      setControle(detail.controleBudgetaire ?? null);
    } catch (err) {
      setLoadError(apiErrorMessage(err, 'Impossible de charger la demande.'));
      setData(null);
    } finally {
      setLoading(false);
    }
  }, [idDemande, routageLecture.reload]);

  const refreshControle = useCallback(async () => {
    if (!idDemande) return;
    setControleLoading(true);
    try {
      const result = await controleBudgetaireDemandePaiement(idDemande);
      setControle(result);
    } catch (err) {
      void msgBox.error(apiErrorMessage(err, 'Prévisualisation du contrôle impossible.'));
    } finally {
      setControleLoading(false);
    }
  }, [idDemande]);

  useEffect(() => {
    if (!canAccess) return;
    void load();
  }, [canAccess, load]);

  const d = data?.demande;
  const statut = normalizeStatutDpm(d?.statut);
  const assignationView = d ? buildAssignationViewFromDetail(d, user?.idUtilisateur) : null;
  const peutAgirMetier = peutAfficherActionsMetierAssignation(assignationView);
  const ub = useMemo(() => ubs.find((u) => u.idUB === d?.idUB), [ubs, d?.idUB]);
  const enControle = statut === 'EN_CONTROLE_BUDGETAIRE';
  const visee = statut === 'VISEE_BUDGETAIREMENT';
  const readOnly = visee || !enControle;

  useEffect(() => {
    if (!canControler || !enControle || !idDemande) return;
    void refreshControle();
  }, [canControler, enControle, idDemande, refreshControle]);

  const summary = useMemo(
    () => buildControleSummary(controle, d?.montantUsd ?? 0),
    [controle, d?.montantUsd],
  );

  const handleRetour = async () => {
    if (!retourMotif.trim()) {
      setRetourError('Le motif est obligatoire.');
      return;
    }
    setRetourError(null);
    const actionLabel = labelRetourAction(retourContext);
    try {
      await runMutation(
        async () => {
          await retournerDemandePaiement(idDemande, {
            motifRetour: retourMotif.trim(),
            commentaireRetour: retourCommentaire.trim() || null,
            etapeConcernee: 'EN_CONTROLE_BUDGETAIRE',
          });
          setRetourOpen(false);
          void msgBox.success(labelRetourSuccess(retourContext));
          notifyMutated();
          navigate('/paiements/budget');
        },
        { message: actionLabel },
      );
    } catch (err) {
      setRetourError(getConflictUserMessage(err, 'Retour impossible.'));
    }
  };

  const openRetourDialog = (context: RetourActionContext) => {
    setRetourContext(context);
    setRetourMotif('');
    setRetourCommentaire('');
    setRetourError(null);
    setRetourOpen(true);
  };

  const codeType = (d?.codeTypeBudget ?? '').toUpperCase();
  const isDc = codeType === 'DC';
  const isAe = codeType === 'AE';
  const isBi = codeType === 'BI';
  const isTableauImputation = isDc || isAe || isBi;
  const canImputer =
    (codeType === 'DC' && canImputerDc(user)) ||
    (codeType === 'AE' && canImputerAe(user)) ||
    (codeType === 'BI' && canImputerBi(user));
  const typesFiliere = typesBudget.filter((t) => t.codeType.toUpperCase() === codeType);

  const viserDisabledReason = (() => {
    if (isMutating) return true;
    if (isDc) return !dcRepartitionOk;
    if (isAe) return !aeRepartitionOk || !controle?.estValide;
    if (isBi) return !biRepartitionOk || !controle?.estValide;
    return !controle?.estValide;
  })();

  const handleAddImputation = async (draft: ImputationDraft) => {
    if (!d) return;
    try {
      await runMutation(
        async () => {
          await addDemandePaiementImputation(d.idDemandePaiement, {
            ordre: d.imputations.length + 1,
            idTypeBudget: draft.idTypeBudget,
            idUB: draft.idUB,
            idExercice: draft.idExercice,
            idRubriqueBudgetaire: draft.idRubriqueBudgetaire,
            mois: draft.mois,
            libelleItemAE: draft.libelleItemAE,
            idGroupeItemAE: draft.idGroupeItemAE,
            idItemBI: draft.idItemBI,
            detailBI: draft.detailBI,
            idBudgetLigne: draft.idBudgetLigne,
            montantBrut: draft.montantUsd,
            devise: draft.devise,
          });
          setShowImputationForm(false);
          void msgBox.success('Imputation enregistrée.');
          await load();
        },
        { message: 'Enregistrement de l\'imputation' },
      );
    } catch (err) {
      void msgBox.error(getConflictUserMessage(err, "Ajout d'imputation impossible."));
      await load();
    }
  };

  const handleViser = async () => {
    try {
      await runMutation(
        async () => {
          await viserDemandePaiement(idDemande);
          void msgBox.success('Demande visée budgétairement.');
          await load();
          notifyMutated();
        },
        { message: 'Visa budgétaire' },
      );
    } catch (err) {
      void msgBox.error(getConflictUserMessage(err, 'Visa impossible.'));
      await load();
    }
  };

  const handleViserClick = async () => {
    const ok = await msgBox.confirm({
      title: 'Viser la demande de paiement',
      message:
        "Vous êtes sur le point de viser cette demande. Le visa figera la situation budgétaire et constituera l'engagement budgétaire de la demande.",
      confirmLabel: 'Viser la demande',
    });
    if (!ok || isMutating) return;
    await handleViser();
  };

  if (!canAccess) {
    return (
      <Alert severity="warning">
        Permission insuffisante pour accéder au contrôle budgétaire DPM.
      </Alert>
    );
  }
  if (loading) return <LoadingState label="Chargement du dossier…" />;
  if (loadError && !d) return <ErrorState message={loadError} onRetry={() => void load()} />;
  if (!d) return <ErrorState message="Demande introuvable." onRetry={() => void load()} />;

  const hasImputations = (d.imputations?.length ?? 0) > 0;
  const canAfficherFicheTravail = enControle && hasImputations && (canImputer || canControler);
  const canAfficherFicheDefinitive = visee;

  const viserDisabled = viserDisabledReason;
  const viserDisabledHint = isDc
    ? 'Répartissez intégralement le montant (reste à répartir = 0) avant de viser.'
    : isAe
      ? !aeRepartitionOk
        ? 'Répartissez intégralement le montant AE avant de viser.'
        : 'Le crédit disponible annuel AE doit être suffisant pour viser.'
      : isBi
        ? !biRepartitionOk
          ? 'Répartissez intégralement le montant BI avant de viser.'
          : 'Le crédit disponible annuel BI doit être suffisant pour viser.'
        : 'Le contrôle budgétaire doit être valide avant de viser.';

  const actionsMetier =
    enControle && peutAgirMetier && (canControler || canViser) ? (
      <Stack
        direction="row"
        spacing={1}
        useFlexGap
        sx={{ alignItems: 'center', flexWrap: 'wrap' }}
      >
        {canControler && (
          <Button
            color="warning"
            variant="outlined"
            onClick={() => openRetourDialog('controle_junior')}
            disabled={isMutating}
          >
            {labelRetourAction('controle_junior')}
          </Button>
        )}
        {canViser &&
          resolveRetourContextControle({ canControler, canViser, mode: 'viseur' }) && (
            <Button
              color="warning"
              variant="outlined"
              onClick={() => openRetourDialog('controle_viseur')}
              disabled={isMutating}
            >
              {labelRetourAction('controle_viseur')}
            </Button>
          )}
        {canViser && (
          <Button
            variant="contained"
            color="success"
            disabled={viserDisabled}
            title={viserDisabled && !isMutating ? viserDisabledHint : undefined}
            onClick={() => void handleViserClick()}
          >
            Viser la demande
          </Button>
        )}
      </Stack>
    ) : null;

  return (
    <>
      <PageHeader
        entityLabel={isTableauImputation ? undefined : d.reference}
        breadcrumbs={[
          { label: 'e-Finance', to: '/dashboard' },
          { label: 'Paiements', to: '/paiements' },
          { label: 'Budget', to: '/paiements/budget' },
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
            {actionsMetier}
            <Button component={RouterLink} to="/paiements/budget" variant="outlined">
              Retour file
            </Button>
          </Stack>
        }
      />

      {visee && (
        <Alert severity="success" sx={{ mb: isDc ? 1 : 2 }}>
          <Typography sx={{ fontWeight: 700 }}>Demande visée budgétairement</Typography>
          <Typography variant="body2">
            Date du visa : {formatDateTimeFr(d.dateVisa)} · Montant :{' '}
            {d.montantUsd != null ? formatMontantUsd(d.montantUsd) : '—'}
          </Typography>
          {canAfficherFicheDefinitive && (
            <Box sx={{ mt: 1 }}>
              <DocumentActions
                title="Fiche d'imputation budgétaire définitive"
                fileName={`fiche-imputation-${idDemande}-definitive.pdf`}
                loadPreview={() => previewFicheImputationPdf(idDemande, 'Definitive')}
                loadDownload={() => downloadFicheImputationPdf(idDemande, 'Definitive')}
              />
            </Box>
          )}
        </Alert>
      )}

      {enControle && !peutAgirMetier && (
        <Alert severity="warning" sx={{ mb: 2 }}>
          Cette demande est assignée à un autre utilisateur : les actions (retour / visa) sont
          masquées pour votre session.
        </Alert>
      )}

      {enControle && peutAgirMetier && canImputer && !canViser && (
        <Alert severity="info" sx={{ mb: 2 }}>
          Vous pouvez enregistrer l&apos;imputation. Le bouton <strong>Viser la demande</strong>{' '}
          apparaît uniquement si votre session a la permission <code>paiements.viser_budget</code>{' '}
          (déconnectez-vous puis reconnectez-vous après l&apos;avoir accordée).
        </Alert>
      )}

      {enControle && !isTableauImputation && (
        <Alert severity="info" sx={{ mb: 2 }}>
          Cette demande est maintenant en cours de contrôle budgétaire.
        </Alert>
      )}

      {!isTableauImputation && (
        <Box sx={{ mb: 2 }}>
          <DemandePaiementWorkflowBlock
            statut={d.statut}
            assignation={assignationView}
            routages={routageLecture.routages}
            retoursDestinataires={routageLecture.retoursDestinataires}
            routageLoading={routageLecture.loading}
          />
        </Box>
      )}

      {canAfficherFicheTravail && (
        <DetailPanel title="Fiche d'imputation budgétaire (travail)">
          <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
            Document de transmission Junior → Senior. Seuls les engagements en cours sont affichés ;
            la situation budgétaire définitive apparaîtra au visa.
          </Typography>
          <DocumentActions
            title="Fiche d'imputation budgétaire — travail"
            fileName={`fiche-imputation-${idDemande}-travail.pdf`}
            loadPreview={() => previewFicheImputationPdf(idDemande, 'Travail')}
            loadDownload={() => downloadFicheImputationPdf(idDemande, 'Travail')}
          />
        </DetailPanel>
      )}

      {isDc && (
        <DetailPanel title="Imputations et situation budgétaire">
          <ImputationDcGrille
            idDemande={d.idDemandePaiement}
            readOnly={readOnly}
            disabled={isMutating}
            canSave={enControle && canImputer}
            onRepartitionChange={setDcRepartitionOk}
            onSaved={() => {
              void load();
              void refreshControle();
            }}
          />
        </DetailPanel>
      )}

      {isAe && (
        <DetailPanel title="Imputations et situation budgétaire">
          <ImputationAeGrille
            idDemande={d.idDemandePaiement}
            readOnly={readOnly}
            disabled={isMutating}
            canSave={enControle && canImputer}
            idExercice={d.idExercice}
            itemSolliciteInitial={d.itemSollicite}
            onRepartitionChange={setAeRepartitionOk}
            onSaved={() => {
              void load();
              void refreshControle();
            }}
          />
        </DetailPanel>
      )}

      {isBi && (
        <DetailPanel title="Imputations et situation budgétaire">
          <ImputationBiGrille
            idDemande={d.idDemandePaiement}
            readOnly={readOnly}
            disabled={isMutating}
            canSave={enControle && canImputer}
            itemSolliciteInitial={d.itemSollicite}
            onRepartitionChange={setBiRepartitionOk}
            onSaved={() => {
              void load();
              void refreshControle();
            }}
          />
        </DetailPanel>
      )}

      {!isTableauImputation && (
      <Grid container spacing={2} sx={{ mb: 2.5 }}>
        {[
          { label: 'Référence', value: d.reference },
          { label: 'Statut', value: d.statut },
          { label: 'Département', value: ub?.departementLibelle ?? '—' },
          { label: 'UB', value: `${d.codeUB} — ${d.libelleUB}` },
          { label: 'Exercice', value: String(d.anneeExercice) },
          { label: 'Cas de dossier', value: d.libelleCasDossier },
        ].map((k) => (
          <Grid key={k.label} size={{ xs: 12, sm: 6, md: 4 }}>
            <Box
              sx={{
                p: 1.5,
                borderRadius: 1,
                border: '1px solid',
                borderColor: 'divider',
                bgcolor: 'var(--ef-surface-secondary)',
                height: '100%',
              }}
            >
              <Typography variant="caption" color="text.secondary">
                {k.label}
              </Typography>
              <Typography variant="body1" sx={{ fontWeight: 700, overflowWrap: 'anywhere' }}>
                {k.value}
              </Typography>
            </Box>
          </Grid>
        ))}
      </Grid>
      )}

      {(controle || visee) && !isTableauImputation && (
        <Box sx={{ mb: 2 }}>
          {controleLoading ? (
            <LoadingState label="Calcul du contrôle budgétaire…" />
          ) : (
            <ControleBudgetaireSummary summary={summary} />
          )}
          {enControle && canControler && (
            <Button size="small" sx={{ mt: 1 }} onClick={() => void refreshControle()} disabled={controleLoading}>
              Actualiser la prévisualisation
            </Button>
          )}
        </Box>
      )}

      <DetailPanel title="Demande">
        <Stack spacing={0.75}>
          <Typography variant="body2">
            <strong>Objet :</strong> {d.objet}
          </Typography>
          <Typography variant="body2">
            <strong>Bénéficiaire(s) :</strong>{' '}
            {d.beneficiaires.map((b) => b.nomComplet).join(' · ') || '—'}
          </Typography>
          <Typography variant="body2">
            <strong>Montant :</strong> {formatMontantDevise(d.montantBrut, d.devise)} · Taux {d.tauxConversion} ·{' '}
            {d.montantUsd != null ? formatMontantUsd(d.montantUsd) : 'Non converti'}
          </Typography>
          <Typography variant="body2">
            <strong>Mode de paiement :</strong> {d.modePaiementSollicite}
          </Typography>
          <Typography variant="body2">
            <strong>Date émission :</strong> {formatDateFr(d.dateEmission)} · Lieu : {d.lieuEmission ?? '—'}
          </Typography>
        </Stack>
      </DetailPanel>

      <DetailPanel title="Pièces justificatives">
        {data.piecesManquantes.length > 0 && (
          <Alert severity="warning" sx={{ mb: 1.5 }}>
            Pièces manquantes : {data.piecesManquantes.map((p) => p.libelle).join(', ')}
          </Alert>
        )}
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell sx={{ display: { xs: 'none', md: 'table-cell' } }}>Type</TableCell>
              <TableCell>Libellé</TableCell>
              <TableCell sx={{ display: { xs: 'none', md: 'table-cell' } }}>Obligatoire</TableCell>
              <TableCell>Statut</TableCell>
              <TableCell>Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {d.pieces.map((p) => (
              <TableRow key={p.idPieceJointe}>
                <TableCell sx={{ display: { xs: 'none', md: 'table-cell' } }}>
                  {p.codeTypePiece}
                </TableCell>
                <TableCell>{p.libelle}</TableCell>
                <TableCell sx={{ display: { xs: 'none', md: 'table-cell' } }}>
                  {p.estObligatoire ? 'Oui' : 'Non'}
                </TableCell>
                <TableCell>
                  <Chip size="small" color="success" label="Présente" />
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
              </TableRow>
            ))}
            {data.piecesManquantes.map((p, idx) => (
              <TableRow key={`missing-${idx}`}>
                <TableCell sx={{ display: { xs: 'none', md: 'table-cell' } }}>
                  {p.codeTypePiece}
                </TableCell>
                <TableCell>{p.libelle}</TableCell>
                <TableCell sx={{ display: { xs: 'none', md: 'table-cell' } }}>Oui</TableCell>
                <TableCell>
                  <Chip size="small" color="error" variant="outlined" label="Manquante" />
                </TableCell>
                <TableCell>—</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </DetailPanel>

      {!isTableauImputation && (
      <DetailPanel title="Imputations et situation budgétaire">
        {enControle && canImputer && !showImputationForm && (
          <Button sx={{ mb: 2 }} variant="outlined" onClick={() => setShowImputationForm(true)} disabled={isMutating}>
            Ajouter une imputation {codeType}
          </Button>
        )}
        {enControle && canImputer && showImputationForm && d.idTypeBudget && (
          <Box sx={{ mb: 2 }}>
            <ImputationBudgetaireForm
              typesBudget={typesFiliere.length ? typesFiliere : typesBudget}
              idUB={d.idUB}
              idExercice={d.idExercice}
              anneeExercice={d.anneeExercice}
              initial={{ idTypeBudget: d.idTypeBudget, montantUsd: d.montantUsd ?? d.montantBrut }}
              disabled={isMutating}
              onSubmit={(draft) => void handleAddImputation(draft)}
              onCancel={() => setShowImputationForm(false)}
            />
          </Box>
        )}
        {d.imputations.map((imp) => {
          const ctrl = controle?.imputations.find((c) => c.idImputation === imp.idImputation);
          const snap = data.snapshots.find((s) => s.idImputation === imp.idImputation);
          return (
            <ControleImputationSection
              key={imp.idImputation}
              imputation={imp}
              controle={ctrl}
              snapshot={snap}
              rubriques={rubriques}
              itemsBI={itemsBI}
              groupesAE={groupesAE}
              anneeExercice={d.anneeExercice}
              readOnly={readOnly}
            />
          );
        })}
      </DetailPanel>
      )}

      {visee && data.snapshots.length > 0 && (
        <DetailPanel title="Snapshots budgétaires (figés au visa)">
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Imputation</TableCell>
                <TableCell align="right" sx={{ display: { xs: 'none', md: 'table-cell' } }}>
                  Budget annuel
                </TableCell>
                <TableCell align="right" sx={{ display: { xs: 'none', md: 'table-cell' } }}>
                  Prévision
                </TableCell>
                <TableCell align="right" sx={{ display: { xs: 'none', md: 'table-cell' } }}>
                  Dispo avant visa
                </TableCell>
                <TableCell align="right">Montant USD</TableCell>
                <TableCell sx={{ display: { xs: 'none', md: 'table-cell' } }}>Date snapshot</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {data.snapshots.map((s) => (
                <TableRow key={s.idSnapshot}>
                  <TableCell>{s.idImputation}</TableCell>
                  <TableCell align="right" sx={{ display: { xs: 'none', md: 'table-cell' } }}>
                    {formatMontantUsd(s.budgetAnnuel)}
                  </TableCell>
                  <TableCell align="right" sx={{ display: { xs: 'none', md: 'table-cell' } }}>
                    {formatMontantUsd(s.montantPrevision)}
                  </TableCell>
                  <TableCell align="right" sx={{ display: { xs: 'none', md: 'table-cell' } }}>
                    {formatMontantUsd(s.creditDisponibleAnnuelAvantVisa)}
                  </TableCell>
                  <TableCell align="right">{formatMontantUsd(s.montantUsd)}</TableCell>
                  <TableCell sx={{ display: { xs: 'none', md: 'table-cell' } }}>
                    {formatDateTimeFr(s.dateSnapshot)}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </DetailPanel>
      )}

      <DetailPanel title="Historique du traitement">
        <Stack spacing={0.5} sx={{ mb: isDc ? 0.75 : 1 }}>
          {d.dateReception && (
            <Typography variant="body2">
              Réception Budgets : {formatDateTimeFr(d.dateReception)}
            </Typography>
          )}
          {d.dateControle && (
            <Typography variant="body2">
              Prise en charge / contrôle : {formatDateTimeFr(d.dateControle)}
            </Typography>
          )}
          {d.dateVisa && (
            <Typography variant="body2">Visa budgétaire : {formatDateTimeFr(d.dateVisa)}</Typography>
          )}
        </Stack>
        <DemandePaiementTimeline
          demande={d}
          historique={data.historique}
          auditCollapsible={isDc}
        />
      </DetailPanel>

      <Dialog open={retourOpen} onClose={() => !isMutating && setRetourOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>{labelRetourDialogTitle(retourContext)}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <Alert severity="info">
              {retourContext === 'controle_junior'
                ? 'La demande sera renvoyée au Chargé DP pour complément (retour inter-étapes).'
                : 'La demande sera renvoyée au contrôle budgétaire (retour depuis le visa).'}
            </Alert>
            <TextField
              required
              label="Motif"
              value={retourMotif}
              onChange={(e) => setRetourMotif(e.target.value)}
              fullWidth
              multiline
              minRows={2}
            />
            <TextField
              label="Commentaire (facultatif)"
              value={retourCommentaire}
              onChange={(e) => setRetourCommentaire(e.target.value)}
              fullWidth
              multiline
              minRows={2}
            />
            {retourError && <Alert severity="error">{retourError}</Alert>}
          </Stack>
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2 }}>
          <Button onClick={() => setRetourOpen(false)} disabled={isMutating}>
            Annuler
          </Button>
          <Button variant="contained" color="warning" onClick={() => void handleRetour()} disabled={isMutating}>
            Confirmer le retour
          </Button>
        </DialogActions>
      </Dialog>
      <DemandePaiementMutationLockOverlay lock={mutationLock} />
    </>
  );
}
