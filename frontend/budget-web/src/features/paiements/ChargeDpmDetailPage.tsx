import {
  Alert,
  Box,
  Button,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
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
import { DetailPanel, ErrorState, LoadingState, PageHeader, useMsgBox } from '../../components';
import {
  convertirTauxChange,
  fetchDemandePaiementComplet,
  fetchTauxChangeApplicable,
  retenirSollicitationChargeDemandePaiement,
  traiterChargeDemandePaiement,
  entrerTraitementDemandePaiement,
  orienterDemandePaiement,
  receptionnerDemandePaiement,
  retournerDemandePaiement,
  type DemandePaiementDetailComplet,
} from '../../services/apiClient';
import { formatDateFr } from '../taux-change/tauxChangeUtils';
import { useAuth } from '../auth';
import {
  canSubmitChargeTraitement,
  dateTraitementDpmReference,
  needsBilletConversion,
  needsTauxConversion,
  type ChargeDpmTauxState,
} from './chargeDpmTauxUtils';
import { ChargeDpmContextBandeau } from './ChargeDpmContextBandeau';
import { ChargeDpmDocumentsZone } from './ChargeDpmDocumentsZone';
import { ChargeDpmTraitementPanel } from './ChargeDpmTraitementPanel';
import { DemandePaiementStatusBadge } from './DemandePaiementStatusBadge';
import { DetailFieldGrid } from './DetailFieldGrid';
import { labelOperationAudit } from './paiementBudgetUtils';
import {
  apiErrorMessage,
  canChargeDpm,
  formatMontantDevise,
  normalizeStatutDpm,
} from './paiementUtils';
import {
  DemandePaiementMutationLockOverlay,
  getConflictUserMessage,
  useDemandePaiementMutationLock,
} from './useDemandePaiementMutationLock';
import { buildAssignationViewFromDetail } from './demandePaiementAssignationFromApi';
import { peutAfficherActionsMetierAssignation } from './demandePaiementAssignationUtils';
import { DemandePaiementWorkflowBlock } from './DemandePaiementWorkflowBlock';
import { useDemandePaiementRoutageLecture } from './useDemandePaiementRoutageLecture';
import {
  labelRetourAction,
  labelRetourDialogTitle,
  labelRetourSuccess,
  labelTimelineRetourDemandeur,
} from './demandePaiementRetourLabels';
import { useNotifyDemandePaiementMutated } from './useDemandePaiementListInvalidation';
import {
  applyEtablissementPatch,
  type EtablissementLocalPatch,
} from './etablissementLocalUpdate';

function normaliserTypeBudget(type: string | null | undefined): 'DC' | 'AE' | 'BI' {
  const t = (type ?? 'DC').trim().toUpperCase();
  if (t === 'AE' || t === 'BI') return t;
  return 'DC';
}

export function ChargeDpmDetailPage() {
  const { id } = useParams();
  const idDemande = Number(id);
  const navigate = useNavigate();
  const { user } = useAuth();
  const canAccess = canChargeDpm(user);
  const msgBox = useMsgBox();
  const mutationLock = useDemandePaiementMutationLock();
  const { isMutating, runMutation } = mutationLock;
  const notifyMutated = useNotifyDemandePaiementMutated();
  const routageLecture = useDemandePaiementRoutageLecture(idDemande, canAccess);

  const [data, setData] = useState<DemandePaiementDetailComplet | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [modePaiement, setModePaiement] = useState<'CAISSE' | 'BANQUE'>('CAISSE');
  const [typeBudget, setTypeBudget] = useState<'DC' | 'AE' | 'BI'>('DC');
  const [itemSollicite, setItemSollicite] = useState('');
  const [instrument, setInstrument] = useState('PIECE_CAISSE');
  const [devisePaiement, setDevisePaiement] = useState('CDF');

  const [tauxState, setTauxState] = useState<ChargeDpmTauxState>({ status: 'idle' });
  const [previewMontant, setPreviewMontant] = useState<{ montant: number; devise: string } | null>(
    null,
  );
  const [retourOpen, setRetourOpen] = useState(false);
  const [retourMotif, setRetourMotif] = useState('');
  const [retourCommentaire, setRetourCommentaire] = useState('');
  const [retourError, setRetourError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!idDemande) return;
    setLoading(true);
    setLoadError(null);
    try {
      const [detail] = await Promise.all([
        fetchDemandePaiementComplet(idDemande),
        routageLecture.reload(),
      ]);
      setData(detail);
      const row = detail.demande;
      setModePaiement((row.modePaiementSollicite ?? 'CAISSE').toUpperCase() === 'BANQUE' ? 'BANQUE' : 'CAISSE');
      setTypeBudget(normaliserTypeBudget(row.typeBudgetSollicite));
      setItemSollicite(row.itemSollicite ?? '');
      if (row.typeInstrumentPaiement) setInstrument(row.typeInstrumentPaiement);
      if (row.devisePaiement) setDevisePaiement(row.devisePaiement);
      setTauxState({ status: 'idle' });
      setPreviewMontant(null);
    } catch (err) {
      setLoadError(apiErrorMessage(err, 'Impossible de charger la demande.'));
    } finally {
      setLoading(false);
    }
  }, [idDemande, routageLecture.reload]);

  const applyEtablissementLocal = useCallback((patch: EtablissementLocalPatch) => {
    setData((prev) => (prev ? applyEtablissementPatch(prev, patch) : prev));
  }, []);

  useEffect(() => {
    if (!canAccess) return;
    void load();
  }, [canAccess, load]);

  const d = data?.demande;
  const statut = normalizeStatutDpm(d?.statut);
  const assignationView = d ? buildAssignationViewFromDetail(d, user?.idUtilisateur) : null;
  const peutAgirMetier = peutAfficherActionsMetierAssignation(assignationView);
  const enTraitement = statut === 'EN_TRAITEMENT_DPM';
  const modeVerrouille = d?.modePaiementVerrouille === true;
  const peutModifierSollicitation = enTraitement && !modeVerrouille && !isMutating;

  const persistSollicitation = useCallback(
    async (
      mode: 'CAISSE' | 'BANQUE',
      budget: 'DC' | 'AE' | 'BI',
      item: string,
    ) => {
      if (!d || !enTraitement || modeVerrouille || isMutating) return;

      try {
        await runMutation(
          async () => {
            const updated = await retenirSollicitationChargeDemandePaiement(d.idDemandePaiement, {
              modePaiementSollicite: mode,
              typeBudgetSollicite: budget,
              itemSollicite: budget === 'DC' ? null : item.trim() || null,
            });
            setData((prev) => (prev ? { ...prev, demande: updated } : prev));
          },
          { message: 'Enregistrement de la sollicitation' },
        );
      } catch (err) {
        void msgBox.error(
          getConflictUserMessage(
            err,
            'Impossible de retenir le mode de paiement ou la destination budgétaire.',
          ),
        );
        await load();
      }
    },
    [d, enTraitement, modeVerrouille, isMutating, runMutation, load, msgBox],
  );

  const dateReference = useMemo(
    () => (enTraitement ? dateTraitementDpmReference() : null),
    [enTraitement],
  );
  const conversionNeeded = useMemo(
    () => (d ? needsTauxConversion(d.devise, devisePaiement) : false),
    [d, devisePaiement],
  );

  useEffect(() => {
    if (modePaiement === 'CAISSE') {
      setDevisePaiement('CDF');
      setInstrument((current) => (current === 'MINUTE_CHEQUE' ? 'PIECE_CAISSE' : current));
    } else {
      setInstrument('MINUTE_CHEQUE');
      setDevisePaiement((current) => (current === 'CDF' ? (d?.devise ?? 'USD') : current));
    }
  }, [modePaiement, d?.devise]);

  useEffect(() => {
    if (!d || !enTraitement || !dateReference) {
      setTauxState({ status: 'idle' });
      setPreviewMontant(null);
      return;
    }

    const source = d.devise;
    const cible = devisePaiement;
    const montant = d.montantBrut;

    if (!needsTauxConversion(source, cible)) {
      setTauxState({ status: 'identity', taux: 1 });
      setPreviewMontant({ montant, devise: cible });
      return;
    }

    let cancelled = false;
    setTauxState({ status: 'loading' });
    setPreviewMontant(null);

    void (async () => {
      try {
        const applicable = await fetchTauxChangeApplicable(source, cible, dateReference);
        if (cancelled) return;

        if (!applicable) {
          setTauxState({
            status: 'not_found',
            deviseSource: source,
            deviseCible: cible,
            dateReference,
          });
          return;
        }

        const conversion = await convertirTauxChange({
          montantSource: montant,
          deviseSource: source,
          deviseCible: cible,
          dateReference,
        });
        if (cancelled) return;

        setTauxState({ status: 'found', applicable, conversion });
        setPreviewMontant({ montant: conversion.montantCible, devise: cible });
      } catch (err) {
        if (cancelled) return;
        setTauxState({
          status: 'error',
          message: apiErrorMessage(
            err,
            'Impossible de récupérer le taux de change applicable depuis le référentiel.',
          ),
        });
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [d, enTraitement, devisePaiement, dateReference]);

  const instrumentOptions = useMemo(
    () =>
      modePaiement === 'CAISSE'
        ? [
            { value: 'PIECE_CAISSE', label: 'PIECE DE CAISSE' },
            { value: 'BON_PROVISOIRE', label: 'BON PROVISOIRE' },
          ]
        : [{ value: 'MINUTE_CHEQUE', label: 'MINUTE CHÈQUE' }],
    [modePaiement],
  );

  const submitAllowed = canSubmitChargeTraitement(tauxState, conversionNeeded);
  const billetRequired = d ? needsBilletConversion(modePaiement, d.devise) : false;
  const billetEtabli = data?.billetConversion?.statut?.toUpperCase() === 'ETABLI';
  const instrumentDocumentEtabli = useMemo(() => {
    if (instrument === 'PIECE_CAISSE') {
      return data?.pieceCaisse?.statut?.toUpperCase() === 'ETABLI';
    }
    if (instrument === 'BON_PROVISOIRE') {
      return data?.bonProvisoire?.statut?.toUpperCase() === 'ETABLI';
    }
    if (instrument === 'MINUTE_CHEQUE') {
      return data?.minuteCheque?.statut?.toUpperCase() === 'ETABLI';
    }
    return false;
  }, [instrument, data?.pieceCaisse, data?.bonProvisoire, data?.minuteCheque]);
  const canTransmit =
    peutAgirMetier &&
    submitAllowed &&
    (!billetRequired || billetEtabli) &&
    instrumentDocumentEtabli;

  const run = async (fn: () => Promise<unknown>, message: string) => {
    if (isMutating) return;
    try {
      await runMutation(async () => {
        await fn();
        await load();
      }, { message });
      notifyMutated();
    } catch (err) {
      void msgBox.error(getConflictUserMessage(err, 'Opération impossible.'));
      await load();
    }
  };

  const handleRetourCharge = async () => {
    if (!d || !retourMotif.trim()) {
      setRetourError('Le motif est obligatoire.');
      return;
    }
    setRetourError(null);
    const actionLabel = labelRetourAction('charge_dpm');
    try {
      await runMutation(
        async () => {
          await retournerDemandePaiement(d.idDemandePaiement, {
            motifRetour: retourMotif.trim(),
            commentaireRetour: retourCommentaire.trim() || null,
            etapeConcernee: 'EN_TRAITEMENT_DPM',
          });
          setRetourOpen(false);
          void msgBox.success(labelRetourSuccess('charge_dpm'));
          notifyMutated();
          navigate('/paiements/charge-dpm');
        },
        { message: actionLabel },
      );
    } catch (err) {
      setRetourError(getConflictUserMessage(err, 'Retour impossible.'));
    }
  };

  const handleTraiterPuisOrienter = async () => {
    if (!d || isMutating) return;

    if ((typeBudget === 'AE' || typeBudget === 'BI') && !itemSollicite.trim()) {
      void msgBox.error('L’item N° est obligatoire pour AE et BI.');
      return;
    }

    if (!canTransmit) {
      void msgBox.error(
        billetRequired && !billetEtabli
          ? 'Établissez le billet de conversion avant de transmettre au Gestionnaire Junior.'
          : !instrumentDocumentEtabli
            ? 'Établissez le document instrument de paiement avant de transmettre au Gestionnaire Junior.'
            : 'Aucun taux applicable — enregistrez un taux dans le référentiel avant de transmettre.',
      );
      return;
    }

    try {
      await runMutation(
        async () => {
          await traiterChargeDemandePaiement(d.idDemandePaiement, {
            typeInstrument: instrument,
            devisePaiement,
            tauxPaiement: null,
            idTauxChangePaiement: null,
            modePaiementSollicite: modePaiement,
            typeBudgetSollicite: typeBudget,
            itemSollicite: typeBudget === 'DC' ? null : itemSollicite.trim(),
          });
          await orienterDemandePaiement(d.idDemandePaiement);
          notifyMutated();
          navigate('/paiements/charge-dpm');
        },
        { message: 'Transmission au Gestionnaire Junior' },
      );
    } catch (err) {
      void msgBox.error(getConflictUserMessage(err, 'Transmission au Gestionnaire Junior impossible.'));
      await load();
    }
  };

  if (!canAccess) {
    return (
      <Box sx={{ p: 2 }}>
        <Alert severity="warning">Accès réservé au profil Chargé DP.</Alert>
      </Box>
    );
  }
  if (loading) return <LoadingState />;
  if (loadError && !d) return <ErrorState message={loadError} onRetry={() => void load()} />;
  if (!d) return <ErrorState message="Demande introuvable." />;

  const montantAffiche =
    enTraitement && previewMontant
      ? formatMontantDevise(previewMontant.montant, previewMontant.devise)
      : d.montantPaiement != null
        ? formatMontantDevise(d.montantPaiement, d.devisePaiement ?? devisePaiement)
        : '—';

  const beneficiairePrincipal = (() => {
    const principal = d.beneficiaires?.find((b) => b.estPrincipal) ?? d.beneficiaires?.[0];
    if (!principal) return '—';
    return principal.raisonSociale?.trim() || principal.nomComplet?.trim() || '—';
  })();

  const modePaiementLabel = modePaiement === 'CAISSE' ? 'Caisse' : 'Banque';

  const destinationLabel =
    typeBudget === 'BI'
      ? `BI / IVT${itemSollicite ? ` — Item N° ${itemSollicite}` : ''}`
      : typeBudget === 'AE'
        ? `AE${itemSollicite ? ` — Item N° ${itemSollicite}` : ''}`
        : 'DC';

  const demandeurLabel =
    d.codeDemandeur || d.libelleDemandeur
      ? [d.codeDemandeur, d.libelleDemandeur].filter(Boolean).join(' — ')
      : '—';

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

  const traitementPanel = (
    <ChargeDpmTraitementPanel
      demande={d}
      statut={statut}
      enTraitement={enTraitement}
      modeVerrouille={modeVerrouille}
      peutModifierSollicitation={peutModifierSollicitation}
      isMutating={isMutating}
      modePaiement={modePaiement}
      typeBudget={typeBudget}
      itemSollicite={itemSollicite}
      instrument={instrument}
      devisePaiement={devisePaiement}
      montantAffiche={montantAffiche}
      tauxState={tauxState}
      dateReference={dateReference}
      instrumentOptions={instrumentOptions}
      canTransmit={canTransmit}
      billetRequired={billetRequired}
      billetEtabli={billetEtabli}
      instrumentDocumentEtabli={instrumentDocumentEtabli}
      onModePaiementChange={(v) => {
        setModePaiement(v);
        void persistSollicitation(v, typeBudget, itemSollicite);
      }}
      onTypeBudgetChange={(v) => {
        setTypeBudget(v);
        const nextItem = v === 'DC' ? '' : itemSollicite;
        if (v === 'DC') setItemSollicite('');
        void persistSollicitation(modePaiement, v, nextItem);
      }}
      onItemSolliciteChange={setItemSollicite}
      onItemSolliciteBlur={() => {
        if (peutModifierSollicitation) {
          void persistSollicitation(modePaiement, typeBudget, itemSollicite);
        }
      }}
      onInstrumentChange={setInstrument}
      onDevisePaiementChange={setDevisePaiement}
      onTransmit={() => void handleTraiterPuisOrienter()}
    />
  );

  return (
    <Box sx={{ p: { xs: 1.5, md: 2 }, display: 'flex', flexDirection: 'column', gap: 2, pb: 3 }}>
      <PageHeader
        entityLabel={d.reference}
        entityCaption={d.objet}
        breadcrumbs={[
          { label: 'Paiements', to: '/paiements' },
          { label: 'Chargé DP', to: '/paiements/charge-dpm' },
          { label: d.reference },
        ]}
        actions={<DemandePaiementStatusBadge statut={d.statut} size="medium" />}
      />

      <ChargeDpmContextBandeau
        montantLabel={formatMontantDevise(d.montantBrut, d.devise)}
        beneficiaireLabel={beneficiairePrincipal}
        modePaiementLabel={modePaiementLabel}
        destinationLabel={destinationLabel}
      />

      <Box
        sx={{
          display: 'flex',
          flexDirection: { xs: 'column', md: 'row' },
          alignItems: 'flex-start',
          gap: 2,
        }}
      >
        <Box
          sx={{
            flex: { xs: '1 1 auto', md: '1 1 auto' },
            minWidth: 0,
            width: '100%',
            order: { xs: 2, md: 1 },
          }}
        >
          <Stack spacing={2}>
            <DetailPanel title="Fiche demande">
              <DetailFieldGrid
                fields={[
                  { label: 'Demandeur', value: demandeurLabel },
                  { label: 'Unité budgétaire', value: `${d.codeUB} — ${d.libelleUB ?? '—'}` },
                  { label: 'Département', value: d.libelleDepartement ?? '—' },
                  { label: 'Cas de dossier', value: d.libelleCasDossier },
                  { label: "Date d'émission", value: formatDateFr(d.dateEmission) },
                  { label: 'Montant sollicité', value: formatMontantDevise(d.montantBrut, d.devise) },
                  { label: 'Objet', value: d.objet, fullWidth: true },
                ]}
              />
              <Stack direction="row" spacing={1} sx={{ mt: 2, flexWrap: 'wrap' }} useFlexGap>
                {statut === 'BROUILLON' && peutAgirMetier && (
                  <Button
                    variant="contained"
                    disabled={isMutating}
                    onClick={() =>
                      void run(() => entrerTraitementDemandePaiement(d.idDemandePaiement), 'Prise en traitement')
                    }
                  >
                    Entrer en traitement
                  </Button>
                )}
                {statut === 'SOUMISE' && peutAgirMetier && (
                  <Button
                    variant="contained"
                    disabled={isMutating}
                    onClick={() =>
                      void run(() => receptionnerDemandePaiement(d.idDemandePaiement), 'Réception de la demande')
                    }
                  >
                    Réceptionner
                  </Button>
                )}
                {enTraitement && peutAgirMetier && (
                  <Button
                    color="warning"
                    variant="outlined"
                    disabled={isMutating}
                    onClick={() => {
                      setRetourMotif('');
                      setRetourCommentaire('');
                      setRetourError(null);
                      setRetourOpen(true);
                    }}
                  >
                    {labelRetourAction('charge_dpm')}
                  </Button>
                )}
                <Button component={RouterLink} to="/paiements/charge-dpm" variant="outlined">
                  Retour file
                </Button>
              </Stack>
            </DetailPanel>

            <DetailPanel title="Documents du paiement">
              {data && (
                <ChargeDpmDocumentsZone
                  demande={d}
                  data={data}
                  enTraitement={enTraitement}
                  modePaiement={modePaiement}
                  instrument={instrument}
                  billetRequired={billetRequired}
                  busy={isMutating}
                  guardAction={runMutation}
                  onEtablissementLocal={applyEtablissementLocal}
                  onError={(message) => void msgBox.error(message)}
                />
              )}
            </DetailPanel>

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
              {!data || data.historique.length === 0 ? (
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
          </Stack>
        </Box>

        <Box
          sx={{
            flex: { xs: '1 1 auto', md: '0 0 360px' },
            width: { xs: '100%', md: 360 },
            position: { md: 'sticky' },
            top: { md: 88 },
            alignSelf: { md: 'flex-start' },
            maxHeight: { md: 'calc(100vh - 104px)' },
            overflowY: { md: 'auto' },
            order: { xs: 1, md: 2 },
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
            {traitementPanel}
          </Stack>
        </Box>
      </Box>

      <DemandePaiementMutationLockOverlay lock={mutationLock} />

      <Dialog open={retourOpen} onClose={() => !isMutating && setRetourOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>{labelRetourDialogTitle('charge_dpm')}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <Alert severity="info">
              La demande sera retournée au demandeur pour correction (Y → X). Ce n&apos;est pas un retour
              inter-étapes vers le contrôle budgétaire.
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
          <Button variant="contained" color="warning" onClick={() => void handleRetourCharge()} disabled={isMutating}>
            {labelRetourAction('charge_dpm')}
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
