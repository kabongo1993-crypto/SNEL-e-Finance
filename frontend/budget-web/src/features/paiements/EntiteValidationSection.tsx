import VisibilityOutlinedIcon from '@mui/icons-material/VisibilityOutlined';
import UploadFileOutlinedIcon from '@mui/icons-material/UploadFileOutlined';
import {
  Alert,
  Box,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { useMemo, useRef, useState } from 'react';
import {
  buildDemandePaiementDocumentPdfViewerOptions,
  DocumentActions,
  FormSection,
  useDocumentViewer,
  useMsgBox,
} from '../../components';
import { useAuth } from '../auth';
import {
  addDemandePaiementPiece,
  deleteDemandePaiementPiece,
  declarerValidationPhysiqueN1,
  declarerValidationPhysiqueN2,
  downloadDemandePaiementPiece,
  previewDemandePaiementPiece,
  fetchDemandePaiement,
  envoyerDemandePaiementEnValidation,
  rejeterValidationEntiteDemandePaiement,
  annulerValidationN1DemandePaiement,
  annulerValidationN2DemandePaiement,
  validerDemandePaiementN1,
  validerDemandePaiementN2,
  type DemandePaiementDetail,
  type DemandePaiementRetourDestinataire,
  type ValidationEntiteDto,
} from '../../services/apiClient';
import {
  canDeclarerValidationPhysiquePaiements,
  canEnvoyerValidationPaiements,
  canImprimerDemandePaiement,
  canJoindreDocumentSignePaiements,
  canRejeterValidationEntitePaiements,
  canAnnulerValidationN1Demande,
  canAnnulerValidationN2Demande,
  canValiderN1Paiements,
  canValiderN2Paiements,
  formatDateFr,
  formatDateTimeFr,
  normalizeStatutDpm,
} from './paiementUtils';
import {
  getConflictUserMessage,
  useDemandePaiementMutationLockShared,
} from './useDemandePaiementMutationLock';
import { useNotifyDemandePaiementMutated } from './useDemandePaiementListInvalidation';
import {
  labelRetourAction,
  labelRetourDialogTitle,
  labelRetourSuccess,
  messageAlerteACorriger,
  resolveRetourContextValidation,
} from './demandePaiementRetourLabels';
import { formatRetourDestinataireLabel, findRetourPourStatutACorriger } from './demandePaiementRoutageUtils';
import {
  PIECE_JUSTIFICATIVE_ACCEPT,
  validateDemandePaiementPieceFile,
} from './pieceUploadUtils';

interface EntiteValidationSectionProps {
  demande: DemandePaiementDetail;
  retoursDestinataires?: DemandePaiementRetourDestinataire[] | null;
  onUpdated: (demande: DemandePaiementDetail) => void;
}

function labelMode(mode: string | null | undefined): string {
  if ((mode ?? '').toUpperCase() === 'PHYSIQUE') return 'Physique';
  if ((mode ?? '').toUpperCase() === 'ELECTRONIQUE') return 'Électronique';
  return '—';
}

function labelStatutValidation(statut: string | null | undefined): string {
  const s = (statut ?? '').toUpperCase();
  if (s === 'VALIDEE') return 'Validé';
  if (s === 'REJETEE') return 'Rejeté';
  return 'En attente';
}

function ValidationCard({
  titre,
  validation,
}: {
  titre: string;
  validation: ValidationEntiteDto | undefined;
}) {
  const validee = (validation?.statut ?? '').toUpperCase() === 'VALIDEE';
  const physique = (validation?.modeValidation ?? '').toUpperCase() === 'PHYSIQUE';

  return (
    <Box
      sx={{
        p: 2,
        borderRadius: 1,
        border: '1px solid',
        borderColor: validee ? 'success.main' : 'divider',
        bgcolor: 'var(--ef-surface-secondary)',
      }}
    >
      <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
        {titre}
      </Typography>
      <Typography variant="body2">État : {labelStatutValidation(validation?.statut)}</Typography>
      {validee && (
        <>
          <Typography variant="body2">Mode : {labelMode(validation?.modeValidation)}</Typography>
          {physique ? (
            <>
              <Typography variant="body2">
                Signataire : {validation?.nomSignatairePhysique ?? '—'}
              </Typography>
              {validation?.fonctionSignatairePhysique && (
                <Typography variant="body2">
                  Fonction : {validation.fonctionSignatairePhysique}
                </Typography>
              )}
              {validation?.dateSignaturePhysique && (
                <Typography variant="body2">
                  Date signature : {formatDateFr(validation.dateSignaturePhysique)}
                </Typography>
              )}
              {validation?.nomUtilisateurDeclarant && (
                <Typography variant="body2" color="text.secondary">
                  Déclarée par {validation.nomUtilisateurDeclarant}
                </Typography>
              )}
            </>
          ) : (
            <>
              <Typography variant="body2">
                Validée par {validation?.nomUtilisateurValidateur ?? '—'}
              </Typography>
              {validation?.dateValidation && (
                <Typography variant="body2">
                  Date : {formatDateTimeFr(validation.dateValidation)}
                </Typography>
              )}
            </>
          )}
          {validation?.commentaire && (
            <Typography variant="body2" color="text.secondary">
              Commentaire : {validation.commentaire}
            </Typography>
          )}
        </>
      )}
    </Box>
  );
}

export function EntiteValidationSection({
  demande,
  retoursDestinataires,
  onUpdated,
}: EntiteValidationSectionProps) {
  const { user } = useAuth();
  const msgBox = useMsgBox();
  const { openDocument } = useDocumentViewer();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const { isMutating, runMutation } = useDemandePaiementMutationLockShared();
  const notifyMutated = useNotifyDemandePaiementMutated();
  const [physiqueOpen, setPhysiqueOpen] = useState<1 | 2 | null>(null);
  const [rejectOpen, setRejectOpen] = useState(false);
  const [nomSignataire, setNomSignataire] = useState('');
  const [fonctionSignataire, setFonctionSignataire] = useState('');
  const [dateSignature, setDateSignature] = useState('');
  const [commentairePhysique, setCommentairePhysique] = useState('');
  const [motifRetour, setMotifRetour] = useState('');
  const [commentaireRetour, setCommentaireRetour] = useState('');

  const statut = normalizeStatutDpm(demande.statut);
  const validations = demande.validationsEntite ?? [];
  const n1 = validations.find((v) => v.niveau === 1);
  const n2 = validations.find((v) => v.niveau === 2);
  const hasSignedDoc = demande.pieces.some(
    (p) => (p.codeTypePiece ?? '').toUpperCase() === 'DOCUMENT_DPM_SIGNE',
  );
  const signedPiece = demande.pieces.find(
    (p) => (p.codeTypePiece ?? '').toUpperCase() === 'DOCUMENT_DPM_SIGNE',
  );

  const canEnvoyer = canEnvoyerValidationPaiements(user) && statut === 'BROUILLON';
  const canValiderN1 =
    canValiderN1Paiements(user) && statut === 'EN_VALIDATION_N1';
  const canValiderN2 =
    canValiderN2Paiements(user) && statut === 'EN_VALIDATION_N2';
  const canDeclarerN1 =
    canDeclarerValidationPhysiquePaiements(user) && statut === 'EN_VALIDATION_N1';
  const canDeclarerN2 =
    canDeclarerValidationPhysiquePaiements(user) &&
    statut === 'EN_VALIDATION_N2' &&
    (n1?.statut ?? '').toUpperCase() === 'VALIDEE';
  const canRejeter =
    canRejeterValidationEntitePaiements(user) &&
    (statut === 'EN_VALIDATION_N1' || statut === 'EN_VALIDATION_N2');
  const canAnnulerN2 = canAnnulerValidationN2Demande(user, statut, n2);
  const canAnnulerN1 = canAnnulerValidationN1Demande(user, statut, n1, n2);
  const canPrint = canImprimerDemandePaiement(user) && demande.idDemandePaiement > 0;
  const canUploadSigned =
    canJoindreDocumentSignePaiements(user) &&
    ['EN_VALIDATION_N2', 'VALIDEE_ENTITE', 'A_CORRIGER', 'BROUILLON'].includes(statut);

  const circuitLabel = useMemo(
    () => demande.circuitEntiteStatut ?? labelStatutDpmCircuit(statut),
    [demande.circuitEntiteStatut, statut],
  );

  const reloadDemande = async () => {
    const detail = await fetchDemandePaiement(demande.idDemandePaiement);
    onUpdated(detail.demande);
  };

  const run = async (
    action: () => Promise<DemandePaiementDetail>,
    success: string,
    message: string,
  ) => {
    if (isMutating) return;
    try {
      const updated = await runMutation(
        async () => {
          const result = await action();
          onUpdated(result);
          return result;
        },
        { onConflict: reloadDemande, message },
      );
      if (updated !== undefined) {
        void msgBox.success(success);
        notifyMutated();
      }
    } catch (err) {
      void msgBox.error(getConflictUserMessage(err, 'Opération impossible.'));
    }
  };

  const handleViewDemande = () => {
    openDocument(
      buildDemandePaiementDocumentPdfViewerOptions(
        demande.idDemandePaiement,
        demande.reference,
      ),
    );
  };

  const handleUploadSigned = async (file: File) => {
    if (isMutating) return;

    const localError = validateDemandePaiementPieceFile(file);
    if (localError) {
      void msgBox.error(localError);
      return;
    }

    try {
      const updated = await runMutation(
        async () => {
          // Remplacement : un seul document signé actif à la fois.
          if (signedPiece) {
            await deleteDemandePaiementPiece(demande.idDemandePaiement, signedPiece.idPieceJointe);
          }
          await addDemandePaiementPiece(
            demande.idDemandePaiement,
            {
              idPieceObligatoire: null,
              codeTypePiece: 'DOCUMENT_DPM_SIGNE',
              libelle: 'Document DPM signé',
            },
            file,
          );
          const detail = await fetchDemandePaiement(demande.idDemandePaiement);
          onUpdated(detail.demande);
          return detail.demande;
        },
        { onConflict: reloadDemande, message: 'Ajout du document signé' },
      );
      if (updated !== undefined) {
        void msgBox.success(signedPiece ? 'Document signé remplacé.' : 'Document signé enregistré.');
      }
    } catch (err) {
      void msgBox.error(getConflictUserMessage(err, 'Ajout du document impossible.'));
    }
  };

  const handleDeleteSigned = async () => {
    if (!signedPiece || isMutating) return;
    const ok = await msgBox.confirm({
      title: 'Supprimer le document signé',
      message: 'Voulez-vous supprimer le document DPM signé actuellement joint ?',
      confirmLabel: 'Supprimer',
      cancelLabel: 'Annuler',
    });
    if (!ok) return;

    try {
      const updated = await runMutation(
        async () => {
          await deleteDemandePaiementPiece(demande.idDemandePaiement, signedPiece.idPieceJointe);
          const detail = await fetchDemandePaiement(demande.idDemandePaiement);
          onUpdated(detail.demande);
          return detail.demande;
        },
        { onConflict: reloadDemande, message: 'Suppression du document signé' },
      );
      if (updated !== undefined) {
        void msgBox.success('Document signé supprimé.');
      }
    } catch (err) {
      void msgBox.error(getConflictUserMessage(err, 'Suppression impossible.'));
    }
  };

  const openPhysique = (niveau: 1 | 2) => {
    setNomSignataire('');
    setFonctionSignataire('');
    setDateSignature(new Date().toISOString().slice(0, 10));
    setCommentairePhysique('');
    setPhysiqueOpen(niveau);
  };

  const submitPhysique = async () => {
    if (!physiqueOpen) return;
    if (!nomSignataire.trim()) {
      void msgBox.error('Le nom du signataire est obligatoire.');
      return;
    }
    if (!dateSignature) {
      void msgBox.error('La date de signature est obligatoire.');
      return;
    }
    if (physiqueOpen === 2 && !hasSignedDoc) {
      void msgBox.error('Le document DPM signé doit être joint avant la déclaration N2.');
      return;
    }
    const payload = {
      nomSignataire: nomSignataire.trim(),
      fonctionSignataire: fonctionSignataire.trim() || null,
      dateSignature,
      commentaire: commentairePhysique.trim() || null,
    };
    setPhysiqueOpen(null);
    await run(
      () =>
        physiqueOpen === 1
          ? declarerValidationPhysiqueN1(demande.idDemandePaiement, payload)
          : declarerValidationPhysiqueN2(demande.idDemandePaiement, payload),
      `Validation physique N${physiqueOpen} déclarée.`,
      physiqueOpen === 1 ? 'Déclaration validation physique N1' : 'Déclaration validation physique N2',
    );
  };

  const retourValidationContext = resolveRetourContextValidation(statut);
  const retourValidationLabel =
    retourValidationContext != null
      ? labelRetourAction(retourValidationContext)
      : 'Retour';

  const submitReject = async () => {
    if (!motifRetour.trim()) {
      void msgBox.error('Le motif de retour est obligatoire.');
      return;
    }
    const ctx = retourValidationContext ?? 'validation_n1';
    setRejectOpen(false);
    await run(
      () =>
        rejeterValidationEntiteDemandePaiement(demande.idDemandePaiement, {
          motifRetour: motifRetour.trim(),
          commentaireRetour: commentaireRetour.trim() || null,
        }),
      labelRetourSuccess(ctx),
      retourValidationLabel,
    );
  };

  return (
    <FormSection title="Circuit de validation de l'entité">
      <Stack spacing={2}>
        <Alert severity="info" sx={{ py: 0.5 }}>
          Validation entité : <strong>{circuitLabel}</strong>
        </Alert>

        <Stack direction={{ xs: 'column', md: 'row' }} spacing={2}>
          <Box sx={{ flex: 1 }}>
            <ValidationCard titre="Niveau 1 — Responsable service" validation={n1} />
          </Box>
          <Box sx={{ flex: 1 }}>
            <ValidationCard titre="Niveau 2 — Responsable entité" validation={n2} />
          </Box>
        </Stack>

        {statut === 'A_CORRIGER' && (
          <Alert severity="warning">
            {messageAlerteACorriger()}
            {(() => {
              const retour = findRetourPourStatutACorriger(retoursDestinataires);
              const retourLabel = retour ? formatRetourDestinataireLabel(retour) : null;
              return retourLabel ? (
                <Typography variant="body2" sx={{ mt: 0.5, fontWeight: 650 }}>
                  {retourLabel}
                </Typography>
              ) : null;
            })()}
            {demande.motifRetour && (
              <Typography variant="body2" sx={{ mt: 0.5 }}>
                Motif : {demande.motifRetour}
              </Typography>
            )}
            {demande.commentaireRetour && (
              <Typography variant="body2">Commentaire : {demande.commentaireRetour}</Typography>
            )}
            {demande.dateRetour && (
              <Typography variant="body2">Date : {formatDateFr(demande.dateRetour)}</Typography>
            )}
          </Alert>
        )}

        <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: 'wrap' }}>
          {canPrint && (
            <Button
              variant="outlined"
              startIcon={<VisibilityOutlinedIcon />}
              onClick={handleViewDemande}
            >
              Afficher la demande
            </Button>
          )}
          {canEnvoyer && (
            <Button
              variant="contained"
              disabled={isMutating}
              onClick={() =>
                void run(
                  () => envoyerDemandePaiementEnValidation(demande.idDemandePaiement),
                  'Demande envoyée en validation N1.',
                  'Envoi en validation',
                )
              }
            >
              Envoyer en validation
            </Button>
          )}
          {canValiderN1 && (
            <Button
              variant="contained"
              disabled={isMutating}
              onClick={() =>
                void run(
                  () => validerDemandePaiementN1(demande.idDemandePaiement),
                  'Validation électronique N1 enregistrée.',
                  'Validation électronique N1',
                )
              }
            >
              Valider N1 (électronique)
            </Button>
          )}
          {canValiderN2 && (
            <Button
              variant="contained"
              disabled={isMutating}
              onClick={() =>
                void run(
                  () => validerDemandePaiementN2(demande.idDemandePaiement),
                  'Validation électronique N2 enregistrée.',
                  'Validation électronique N2',
                )
              }
            >
              Valider N2 (électronique)
            </Button>
          )}
          {canDeclarerN1 && (
            <Button variant="outlined" disabled={isMutating} onClick={() => openPhysique(1)}>
              Déclarer validation physique N1
            </Button>
          )}
          {canDeclarerN2 && (
            <Button variant="outlined" disabled={isMutating} onClick={() => openPhysique(2)}>
              Déclarer validation physique N2
            </Button>
          )}
          {canUploadSigned && (
            <>
              <input
                ref={fileInputRef}
                type="file"
                hidden
                accept={PIECE_JUSTIFICATIVE_ACCEPT}
                onChange={(e) => {
                  const file = e.target.files?.[0];
                  e.target.value = '';
                  if (file) void handleUploadSigned(file);
                }}
              />
              <Button
                variant="outlined"
                startIcon={<UploadFileOutlinedIcon />}
                disabled={isMutating}
                onClick={() => fileInputRef.current?.click()}
              >
                {hasSignedDoc ? 'Remplacer document signé' : 'Joindre document signé'}
              </Button>
            </>
          )}
          {canAnnulerN2 && (
            <Button
              variant="outlined"
              color="warning"
              disabled={isMutating}
              onClick={() =>
                void run(
                  () => annulerValidationN2DemandePaiement(demande.idDemandePaiement),
                  'Validation N2 annulée.',
                  'Annulation validation N2',
                )
              }
            >
              Annuler ma validation N2
            </Button>
          )}
          {canAnnulerN1 && (
            <Button
              variant="outlined"
              color="warning"
              disabled={isMutating}
              onClick={() =>
                void run(
                  () => annulerValidationN1DemandePaiement(demande.idDemandePaiement),
                  'Validation N1 annulée.',
                  'Annulation validation N1',
                )
              }
            >
              Annuler ma validation N1
            </Button>
          )}
          {canRejeter && (
            <Button color="warning" disabled={isMutating} onClick={() => setRejectOpen(true)}>
              {retourValidationLabel}
            </Button>
          )}
        </Stack>

        {signedPiece && (
          <Box sx={{ mt: 1 }}>
            <Typography variant="subtitle2" sx={{ mb: 0.5 }}>
              Document signé physique
            </Typography>
            <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: 'wrap', alignItems: 'center' }}>
              <DocumentActions
                title="Document DPM signé"
                fileName={signedPiece.nomFichierOriginal || 'document-signe.pdf'}
                fileSizeBytes={signedPiece.tailleOctets}
                loadPreview={() =>
                  previewDemandePaiementPiece(demande.idDemandePaiement, signedPiece.idPieceJointe)
                }
                loadDownload={() =>
                  downloadDemandePaiementPiece(demande.idDemandePaiement, signedPiece.idPieceJointe)
                }
              />
              {canUploadSigned && (
                <Button
                  color="warning"
                  size="small"
                  disabled={isMutating}
                  onClick={() => void handleDeleteSigned()}
                >
                  Supprimer
                </Button>
              )}
            </Stack>
          </Box>
        )}

        {canDeclarerN2 && !hasSignedDoc && (
          <Typography variant="caption" color="warning.main">
            Le document DPM signé est requis avant de déclarer la validation physique N2.
          </Typography>
        )}
      </Stack>

      <Dialog open={physiqueOpen !== null} onClose={() => !isMutating && setPhysiqueOpen(null)} maxWidth="sm" fullWidth>
        <DialogTitle>
          Déclarer validation physique — Niveau {physiqueOpen}
        </DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <Alert severity="info">
              Vous déclarez une signature physique. Vous serez enregistré comme déclarant, pas comme signataire.
            </Alert>
            <TextField
              required
              label="Nom du signataire"
              value={nomSignataire}
              onChange={(e) => setNomSignataire(e.target.value)}
              fullWidth
            />
            <TextField
              label="Fonction du signataire"
              value={fonctionSignataire}
              onChange={(e) => setFonctionSignataire(e.target.value)}
              fullWidth
            />
            <TextField
              required
              label="Date de signature"
              type="date"
              value={dateSignature}
              onChange={(e) => setDateSignature(e.target.value)}
              slotProps={{ inputLabel: { shrink: true } }}
              fullWidth
            />
            <TextField
              label="Commentaire"
              value={commentairePhysique}
              onChange={(e) => setCommentairePhysique(e.target.value)}
              multiline
              minRows={2}
              fullWidth
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setPhysiqueOpen(null)} disabled={isMutating}>
            Annuler
          </Button>
          <Button variant="contained" onClick={() => void submitPhysique()} disabled={isMutating}>
            Déclarer
          </Button>
        </DialogActions>
      </Dialog>

      <Dialog open={rejectOpen} onClose={() => !isMutating && setRejectOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>
          {retourValidationContext != null
            ? labelRetourDialogTitle(retourValidationContext)
            : 'Retour de la demande'}
        </DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            {retourValidationContext === 'validation_n2' && (
              <Alert severity="info">
                Le validateur N2 renvoie la demande au validateur N1 — jamais directement au demandeur.
              </Alert>
            )}
            {retourValidationContext === 'validation_n1' && (
              <Alert severity="info">
                La demande sera retournée au demandeur pour correction (statut « Retour au demandeur »).
              </Alert>
            )}
            <TextField
              required
              label="Motif de retour"
              value={motifRetour}
              onChange={(e) => setMotifRetour(e.target.value)}
              fullWidth
            />
            <TextField
              label="Commentaire"
              value={commentaireRetour}
              onChange={(e) => setCommentaireRetour(e.target.value)}
              multiline
              minRows={2}
              fullWidth
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setRejectOpen(false)} disabled={isMutating}>
            Annuler
          </Button>
          <Button color="warning" variant="contained" onClick={() => void submitReject()} disabled={isMutating}>
            {retourValidationLabel}
          </Button>
        </DialogActions>
      </Dialog>
    </FormSection>
  );
}

function labelStatutDpmCircuit(statut: string): string {
  switch (statut) {
    case 'BROUILLON':
      return 'Brouillon';
    case 'EN_VALIDATION_N1':
      return 'EN ATTENTE N1';
    case 'EN_VALIDATION_N2':
      return 'EN ATTENTE N2';
    case 'VALIDEE_ENTITE':
      return 'VALIDÉE';
    case 'SOUMISE':
      return 'SOUMISE AU BUDGET';
    case 'A_CORRIGER':
      return 'RETOUR AU DEMANDEUR';
    default:
      return statut;
  }
}
