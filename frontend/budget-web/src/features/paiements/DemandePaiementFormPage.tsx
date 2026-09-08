import DeleteOutlinedIcon from '@mui/icons-material/DeleteOutlined';
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
  MenuItem,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TextField,
  Typography,
  ToggleButton,
  ToggleButtonGroup,
} from '@mui/material';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link as RouterLink, useNavigate, useParams } from 'react-router-dom';
import {
  ComputedField,
  DocumentActions,
  ErrorState,
  FormGrid,
  FormSection,
  LoadingState,
  PageHeader,
  SearchableSelect,
  AmountField,
  useMsgBox,
} from '../../components';
import { formFieldSize } from '../../components/formTokens';
import { useAuth } from '../auth';
import {
  addDemandePaiementPiece,
  createDemandePaiement,
  createDemandeur,
  deleteDemandePaiement,
  deleteDemandePaiementPiece,
  downloadDemandePaiementPiece,
  previewDemandePaiementPiece,
  fetchCasDossier,
  fetchCasDossiers,
  fetchDemandePaiement,
  fetchDemandeurs,
  fetchDevises,
  fetchExercices,
  fetchUnitesBudgetaires,
  remettreEnBrouillonDemandePaiement,
  updateDemandePaiement,
  type CasDossier,
  type CasDossierPieceObligatoire,
  type CreateBeneficiairePayload,
  type DemandePaiementDetail,
  type DemandePaiementPiece,
  type DemandeurDto,
  type DeviseDto,
  type Exercice,
  type UniteBudgetaire,
} from '../../services/apiClient';
import { DemandePaiementStatusBadge } from './DemandePaiementStatusBadge';
import { DemandePaiementACorrigerAlert } from './DemandePaiementACorrigerAlert';
import { useDemandePaiementRoutageLecture } from './useDemandePaiementRoutageLecture';
import { DemandeSummaryRail } from './DemandeSummaryRail';
import { EntiteValidationSection } from './EntiteValidationSection';
import { NumberedSectionBlock } from './NumberedSectionBlock';
import {
  apiErrorMessage,
  canEcrirePaiements,
  canLirePaiements,
  canSupprimerBrouillon,
  estModifiableDemandeur,
  hasPerm,
} from './paiementUtils';
import {
  DemandePaiementMutationLockProvider,
  getConflictUserMessage,
  useDemandePaiementMutationLockContext,
} from './useDemandePaiementMutationLock';
import { useNotifyDemandePaiementMutated } from './useDemandePaiementListInvalidation';
import {
  PIECE_JUSTIFICATIVE_ACCEPT,
  validateDemandePaiementPieceFile,
} from './pieceUploadUtils';

interface BeneficiaireForm extends CreateBeneficiairePayload {
  key: string;
}

function emptyBeneficiaire(ordre: number): BeneficiaireForm {
  return {
    key: `b-${Date.now()}-${ordre}`,
    typeBeneficiaire: 'AGENT',
    nomComplet: '',
    matricule: '',
    fonction: '',
    raisonSociale: '',
    rccm: '',
    adresse: '',
    banque: '',
    numeroCompte: '',
    estPrincipal: ordre === 1,
    ordre,
  };
}

interface DemandePaiementFormPageProps {
  mode: 'create' | 'edit';
}

export function DemandePaiementFormPage({ mode }: DemandePaiementFormPageProps) {
  return (
    <DemandePaiementMutationLockProvider>
      <DemandePaiementFormPageInner mode={mode} />
    </DemandePaiementMutationLockProvider>
  );
}

function DemandePaiementFormPageInner({ mode }: DemandePaiementFormPageProps) {
  const { id } = useParams();
  const idDemande = mode === 'edit' && id ? Number(id) : null;
  const navigate = useNavigate();
  const { user } = useAuth();
  const canRead = canLirePaiements(user);
  const routageLecture = useDemandePaiementRoutageLecture(
    idDemande ?? undefined,
    canRead && mode === 'edit',
  );
  const canWrite = canEcrirePaiements(user);
  const canCreateDemandeur =
    hasPerm(user, 'demandeurs.ecrire') ||
    hasPerm(user, 'referentiels.ecrire') ||
    hasPerm(user, 'admin.all');
  const msgBox = useMsgBox();
  const { isMutating, runMutation } = useDemandePaiementMutationLockContext();
  const notifyMutated = useNotifyDemandePaiementMutated();

  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [exercices, setExercices] = useState<Exercice[]>([]);
  const [ubs, setUbs] = useState<UniteBudgetaire[]>([]);
  const [demandeurs, setDemandeurs] = useState<DemandeurDto[]>([]);
  const [casList, setCasList] = useState<CasDossier[]>([]);
  const [devises, setDevises] = useState<DeviseDto[]>([]);
  const [piecesObligatoires, setPiecesObligatoires] = useState<CasDossierPieceObligatoire[]>([]);

  const [demande, setDemande] = useState<DemandePaiementDetail | null>(null);
  const [pieces, setPieces] = useState<DemandePaiementPiece[]>([]);

  const [dateEmission, setDateEmission] = useState(() => new Date().toISOString().slice(0, 10));
  const [lieuEmission, setLieuEmission] = useState('');
  const [idExercice, setIdExercice] = useState<number | ''>('');
  const [idDemandeur, setIdDemandeur] = useState<number | ''>('');
  const [idCasDossier, setIdCasDossier] = useState<number | ''>('');
  const [objet, setObjet] = useState('');
  const [compteSection, setCompteSection] = useState('');
  const [idDevise, setIdDevise] = useState<number | ''>('');
  const [montantBrut, setMontantBrut] = useState('');
  const [typeBudgetSollicite, setTypeBudgetSollicite] = useState<'DC' | 'AE' | 'BI' | ''>('DC');
  const [itemSollicite, setItemSollicite] = useState('');
  const [modePaiementSollicite, setModePaiementSollicite] = useState<'CAISSE' | 'BANQUE' | ''>('CAISSE');
  const [beneficiaires, setBeneficiaires] = useState<BeneficiaireForm[]>([emptyBeneficiaire(1)]);

  const [newDemandeurOpen, setNewDemandeurOpen] = useState(false);
  const [newDemandeurCode, setNewDemandeurCode] = useState('');
  const [newDemandeurLibelle, setNewDemandeurLibelle] = useState('');
  const [newDemandeurUb, setNewDemandeurUb] = useState<number | ''>('');
  const [creatingDemandeur, setCreatingDemandeur] = useState(false);

  const editable = mode === 'create' || estModifiableDemandeur(demande?.statut);
  const readOnly = !editable || !canWrite;

  const demandeurSelected = useMemo(
    () => demandeurs.find((d) => d.idDemandeur === idDemandeur) ?? null,
    [demandeurs, idDemandeur],
  );

  const ubAuto = useMemo(() => {
    if (demandeurSelected) {
      return {
        codeUB: demandeurSelected.codeUB,
        libelleUB: demandeurSelected.libelleUB,
        libelleDepartement: demandeurSelected.libelleDepartement,
      };
    }
    if (demande && idDemandeur) {
      return {
        codeUB: demande.codeUB,
        libelleUB: demande.libelleUB,
        libelleDepartement: demande.libelleDepartement ?? '—',
      };
    }
    return null;
  }, [demandeurSelected, demande, idDemandeur]);

  const hydrateFromDetail = useCallback((d: DemandePaiementDetail) => {
    setDemande(d);
    setDateEmission(d.dateEmission?.slice(0, 10) ?? '');
    setLieuEmission(d.lieuEmission ?? '');
    setIdExercice(d.idExercice);
    setIdDemandeur(d.idDemandeur ?? '');
    setIdCasDossier(d.idCasDossier);
    setObjet(d.objet);
    setCompteSection(d.compteSection ?? '');
    setIdDevise(d.idDevise ?? '');
    setMontantBrut(String(d.montantBrut));
    setTypeBudgetSollicite((d.typeBudgetSollicite as 'DC' | 'AE' | 'BI') ?? 'DC');
    setItemSollicite(d.itemSollicite ?? '');
    setModePaiementSollicite((d.modePaiementSollicite as 'CAISSE' | 'BANQUE') ?? 'CAISSE');
    setPieces(d.pieces ?? []);
    setBeneficiaires(
      (d.beneficiaires ?? []).length
        ? d.beneficiaires.map((b, idx) => ({
            key: `b-${b.idBeneficiaire}`,
            typeBeneficiaire: b.typeBeneficiaire,
            nomComplet: b.nomComplet,
            matricule: b.matricule ?? '',
            fonction: b.fonction ?? '',
            raisonSociale: b.raisonSociale ?? '',
            rccm: b.rccm ?? '',
            adresse: b.adresse ?? '',
            banque: b.banque ?? '',
            numeroCompte: b.numeroCompte ?? '',
            estPrincipal: b.estPrincipal,
            ordre: b.ordre || idx + 1,
          }))
        : [emptyBeneficiaire(1)],
    );
  }, []);

  const loadRefs = useCallback(async () => {
    const [ex, ub, dems, cas, devisesList] = await Promise.all([
      fetchExercices(),
      fetchUnitesBudgetaires({ accessibles: true, contexte: 'dpm' }),
      fetchDemandeurs(true),
      fetchCasDossiers(),
      fetchDevises(mode === 'edit' ? false : true),
    ]);
    setExercices(ex);
    setUbs(ub.filter((u) => u.actif));
    setDemandeurs(dems);
    setCasList(cas);
    setDevises(devisesList);

    if (mode === 'create') {
      const courant =
        [...ex]
          .filter((e) => (e.statut ?? '').toUpperCase() === 'OUVERT')
          .sort((a, b) => b.annee - a.annee)[0] ?? ex[0];
      if (courant) setIdExercice(courant.idExercice);
      const cdf = devisesList.find((d) => d.code === 'CDF' && d.actif);
      if (cdf) setIdDevise(cdf.idDevise);
      else {
        const firstActif = devisesList.find((d) => d.actif);
        if (firstActif) setIdDevise(firstActif.idDevise);
      }
    }
  }, [mode]);

  useEffect(() => {
    if (!canRead) return;
    void (async () => {
      setLoading(true);
      setLoadError(null);
      try {
        await loadRefs();
        if (mode === 'edit' && idDemande) {
          const [detail] = await Promise.all([
            fetchDemandePaiement(idDemande),
            routageLecture.reload(),
          ]);
          hydrateFromDetail(detail.demande);
          if (detail.demande.idCasDossier) {
            const cas = await fetchCasDossier(detail.demande.idCasDossier);
            setPiecesObligatoires(cas.piecesObligatoires ?? []);
          }
        }
      } catch (err) {
        setLoadError(apiErrorMessage(err, 'Impossible de charger le formulaire.'));
      } finally {
        setLoading(false);
      }
    })();
  }, [canRead, mode, idDemande, loadRefs, hydrateFromDetail, routageLecture.reload]);

  useEffect(() => {
    if (!idCasDossier) {
      setPiecesObligatoires([]);
      return;
    }
    void (async () => {
      try {
        const cas = await fetchCasDossier(Number(idCasDossier));
        setPiecesObligatoires(cas.piecesObligatoires ?? []);
      } catch {
        setPiecesObligatoires([]);
      }
    })();
  }, [idCasDossier]);

  const buildBeneficiairesPayload = (): CreateBeneficiairePayload[] =>
    beneficiaires
      .filter((b) => b.nomComplet.trim())
      .map((b, idx) => ({
        typeBeneficiaire: b.typeBeneficiaire,
        nomComplet: b.nomComplet.trim(),
        matricule: b.matricule || null,
        fonction: b.fonction || null,
        raisonSociale: b.raisonSociale || null,
        rccm: b.rccm || null,
        adresse: b.adresse || null,
        banque: b.banque || null,
        numeroCompte: b.numeroCompte || null,
        estPrincipal: b.estPrincipal || idx === 0,
        ordre: idx + 1,
      }));

  const validateHeader = (): string | null => {
    if (!idExercice) return 'L’exercice est obligatoire.';
    if (!idDemandeur) return 'Le demandeur est obligatoire.';
    if (!idCasDossier) return 'Le cas de dossier est obligatoire.';
    if (!objet.trim()) return 'L’objet est obligatoire.';
    const brut = Number(String(montantBrut).replace(',', '.'));
    if (!Number.isFinite(brut) || brut < 0) return 'Le montant sollicité ne peut pas être négatif.';
    if (!idDevise) return 'La devise sollicitée est obligatoire.';
    if (!typeBudgetSollicite) return 'Le type de budget sollicité est obligatoire.';
    if ((typeBudgetSollicite === 'AE' || typeBudgetSollicite === 'BI') && !itemSollicite.trim()) {
      return 'L’item sollicité est obligatoire pour AE et BI / IVT.';
    }
    if (typeBudgetSollicite === 'DC' && itemSollicite.trim()) {
      return 'Aucun item ne doit être renseigné pour une destination DC.';
    }
    if (!modePaiementSollicite) return 'Le mode de paiement sollicité est obligatoire.';
    return null;
  };

  const saveHeader = async (): Promise<number> => {
    const errMsg = validateHeader();
    if (errMsg) throw new Error(errMsg);

    const deviseSelected = devises.find((d) => d.idDevise === idDevise);
    const payloadBase = {
      dateEmission,
      lieuEmission: lieuEmission || null,
      objet: objet.trim(),
      compteSection: compteSection || null,
      montantBrut: Number(String(montantBrut).replace(',', '.')),
      devise: deviseSelected?.code ?? 'USD',
      idDevise: Number(idDevise),
      typeBudgetSollicite,
      itemSollicite: typeBudgetSollicite === 'DC' ? null : itemSollicite.trim() || null,
      modePaiementSollicite,
      beneficiaires: buildBeneficiairesPayload(),
    };

    if (mode === 'create' || !idDemande) {
      const created = await createDemandePaiement({
        ...payloadBase,
        idExercice: Number(idExercice),
        idDemandeur: Number(idDemandeur),
        idCasDossier: Number(idCasDossier),
      });
      setDemande(created);
      return created.idDemandePaiement;
    }

    if ((demande?.statut ?? '').toUpperCase() === 'A_CORRIGER') {
      await remettreEnBrouillonDemandePaiement(idDemande);
    }
    const updated = await updateDemandePaiement(idDemande, payloadBase);
    setDemande(updated);
    return updated.idDemandePaiement;
  };

  const reloadFromServer = useCallback(async () => {
    const demId = demande?.idDemandePaiement ?? idDemande;
    if (!demId) return;
    const detail = await fetchDemandePaiement(demId);
    hydrateFromDetail(detail.demande);
  }, [demande?.idDemandePaiement, idDemande, hydrateFromDetail]);

  const handleSave = async (andContinue: boolean) => {
    if (!canWrite || readOnly || isMutating) return;
    const wasACorriger = (demande?.statut ?? '').toUpperCase() === 'A_CORRIGER';
    const isCreate = mode === 'create' || !idDemande;
    try {
      await runMutation(
        async () => {
          const newId = await saveHeader();
          if (isCreate || wasACorriger) {
            notifyMutated();
          }
          void msgBox.success('Brouillon enregistré.');
          if (!andContinue) {
            // Retour à la page précédente (liste / historique) ; repli liste DPM.
            if (window.history.length > 1) {
              navigate(-1);
            } else {
              navigate('/paiements', { replace: true });
            }
          } else if (mode === 'create') {
            navigate(`/paiements/${newId}/modifier`, { replace: true });
          } else {
            const detail = await fetchDemandePaiement(newId);
            hydrateFromDetail(detail.demande);
          }
        },
        { onConflict: reloadFromServer, message: 'Enregistrement du brouillon' },
      );
    } catch (err) {
      void msgBox.error(getConflictUserMessage(err, 'Enregistrement impossible.'));
    }
  };

  const handleDelete = async () => {
    const demId = demande?.idDemandePaiement ?? idDemande;
    if (!demId || isMutating || !canSupprimerBrouillon(user, demande?.statut)) return;

    const ok = await msgBox.confirm({
      title: 'Supprimer définitivement ?',
      message: `La demande « ${demande?.reference ?? demId} » sera supprimée de façon permanente avec toutes ses pièces jointes. Cette action est irréversible.`,
      confirmLabel: 'Supprimer définitivement',
    });
    if (!ok) return;

    try {
      await runMutation(
        async () => {
          await deleteDemandePaiement(demId);
          notifyMutated();
          void msgBox.success('Demande supprimée définitivement.');
          navigate('/paiements', { replace: true });
        },
        { message: 'Suppression de la demande' },
      );
    } catch (err) {
      void msgBox.error(getConflictUserMessage(err, 'Suppression impossible.'));
    }
  };

  const handleCreateDemandeur = async () => {
    if (!newDemandeurCode.trim() || !newDemandeurLibelle.trim() || !newDemandeurUb) {
      void msgBox.error('Code, libellé et unité budgétaire sont obligatoires pour créer un demandeur.');
      return;
    }
    setCreatingDemandeur(true);
    try {
      const created = await createDemandeur({
        code: newDemandeurCode.trim(),
        libelle: newDemandeurLibelle.trim(),
        idUB: Number(newDemandeurUb),
      });
      const refreshed = await fetchDemandeurs(true);
      setDemandeurs(refreshed);
      setIdDemandeur(created.idDemandeur);
      setNewDemandeurOpen(false);
      setNewDemandeurCode('');
      setNewDemandeurLibelle('');
      setNewDemandeurUb('');
      void msgBox.success(`Demandeur « ${created.code} » créé et sélectionné.`);
    } catch (err) {
      void msgBox.error(apiErrorMessage(err, 'Création du demandeur impossible.'));
    } finally {
      setCreatingDemandeur(false);
    }
  };

  const handleAddPiece = async (
    obligatoire: CasDossierPieceObligatoire | null,
    file: File,
  ): Promise<boolean> => {
    const localError = validateDemandePaiementPieceFile(file);
    if (localError) {
      void msgBox.error(localError);
      return false;
    }

    try {
      const created = await runMutation(
        async () => {
          let demId = demande?.idDemandePaiement ?? idDemande;
          if (!demId) {
            demId = await saveHeader();
            navigate(`/paiements/${demId}/modifier`, { replace: true });
          }
          return addDemandePaiementPiece(
            demId,
            {
              idPieceObligatoire: obligatoire?.idPieceObligatoire ?? null,
              codeTypePiece: obligatoire?.codeTypePiece ?? 'AUTRE',
              libelle: obligatoire?.libelle ?? file.name,
            },
            file,
          );
        },
        { onConflict: reloadFromServer, message: 'Ajout du fichier' },
      );

      if (!created) return false;

      setPieces((prev) => [...prev, created]);
      void msgBox.success('Pièce enregistrée.');
      return true;
    } catch (err) {
      void msgBox.error(getConflictUserMessage(err, 'Ajout de pièce impossible.'));
      return false;
    }
  };

  const handleDeletePiece = async (idPiece: number) => {
    await runMutation(
      async () => {
        const demId = demande?.idDemandePaiement ?? idDemande;
        if (!demId) return;
        await deleteDemandePaiementPiece(demId, idPiece);
        setPieces((prev) => prev.filter((p) => p.idPieceJointe !== idPiece));
      },
      { onConflict: reloadFromServer, message: 'Suppression du fichier' },
    );
  };

  const resolveDemId = (): number => {
    const demId = demande?.idDemandePaiement ?? idDemande;
    if (!demId) throw new Error('Demande introuvable.');
    return demId;
  };

  /** Props de présentation du rail — miroir de l’état existant, sans nouvelle règle métier. */
  const summaryRail = useMemo(() => {
    const exercice = exercices.find((e) => e.idExercice === idExercice);
    const devise = devises.find((d) => d.idDevise === idDevise);
    const brut = Number(String(montantBrut).replace(',', '.'));
    const montantOk = Number.isFinite(brut) && brut >= 0 && Boolean(idDevise);
    const montantLabel =
      String(montantBrut).trim() === ''
        ? '—'
        : `${String(montantBrut).trim()}${devise?.code ? ` ${devise.code}` : ''}`;

    const destinationLabel =
      typeBudgetSollicite === 'BI'
        ? 'BI / IVT'
        : typeBudgetSollicite || '—';

    const modePaiementLabel =
      modePaiementSollicite === 'CAISSE'
        ? 'Caisse'
        : modePaiementSollicite === 'BANQUE'
          ? 'Banque'
          : '—';

    // Même matching que le tableau « Pièces justificatives » ; liste vide ⇒ every = true
    // (cas sans pièce obligatoire, ou pas encore de lignes attendues).
    const piecesAttenduesCompletes = piecesObligatoires
      .filter((po) => po.obligatoire)
      .every((po) =>
        pieces.some(
          (p) =>
            p.idPieceObligatoire === po.idPieceObligatoire ||
            p.codeTypePiece.toUpperCase() === po.codeTypePiece.toUpperCase(),
        ),
      );

    return {
      reference: demande?.reference ?? null,
      montantLabel,
      exerciceLabel: exercice ? String(exercice.annee) : '—',
      destinationLabel,
      modePaiementLabel,
      beneficiairesCount: beneficiaires.length,
      checklist: [
        {
          id: 'infos',
          label: 'Informations générales',
          done: Boolean(idExercice && idDemandeur && idCasDossier),
        },
        {
          id: 'objet',
          label: 'Objet renseigné',
          done: Boolean(objet.trim()),
        },
        {
          id: 'montant',
          label: 'Montant saisi',
          done: montantOk,
        },
        {
          id: 'beneficiaire',
          label: 'Bénéficiaire complet',
          done: beneficiaires.some((b) => b.nomComplet.trim()),
        },
        {
          id: 'pieces',
          label: 'Pièces jointes',
          done: piecesAttenduesCompletes,
        },
      ],
    };
  }, [
    beneficiaires,
    demande?.reference,
    devises,
    exercices,
    idCasDossier,
    idDemandeur,
    idDevise,
    idExercice,
    modePaiementSollicite,
    montantBrut,
    objet,
    pieces,
    piecesObligatoires,
    typeBudgetSollicite,
  ]);

  if (!canRead) {
    return <Alert severity="warning">Permission insuffisante (`paiements.lire`).</Alert>;
  }

  if (loading) return <LoadingState label="Chargement du formulaire…" />;
  if (loadError && mode === 'edit' && !demande) {
    return <ErrorState message={loadError} onRetry={() => window.location.reload()} />;
  }

  return (
    <>
      <PageHeader
        entityLabel={mode === 'edit' ? demande?.reference : undefined}
        breadcrumbs={[
          { label: 'e-Finance', to: '/dashboard' },
          { label: 'Paiements', to: '/paiements' },
          { label: mode === 'create' ? 'Nouvelle' : 'Modifier' },
        ]}
        actions={demande ? <DemandePaiementStatusBadge statut={demande.statut} size="medium" /> : undefined}
      />

      {demande && (demande.statut ?? '').toUpperCase() === 'A_CORRIGER' && (
        <DemandePaiementACorrigerAlert
          demande={demande}
          retoursDestinataires={routageLecture.retoursDestinataires}
          description="Corrigez les éléments signalés ci-dessous puis enregistrez. Ce statut correspond à un retour au créateur/demandeur, pas à un retour inter-étapes Budget."
        />
      )}

      {!editable && demande && (
        <Alert severity="info" sx={{ mb: 2 }}>
          Cette demande est en statut « {demande.statut} » et n’est plus modifiable par le demandeur.
        </Alert>
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
          <Stack spacing={2}>
            <NumberedSectionBlock step={1}>
        <FormSection title="Informations générales">
          {/* Proportions uniquement — mêmes SearchableSelect / mêmes données. */}
          <FormGrid>
            <SearchableSelect
              required
              label="Exercice"
              value={idExercice === '' ? '' : String(idExercice)}
              onChange={(v) => setIdExercice(v ? Number(v) : '')}
              disabled={readOnly || mode === 'edit'}
              density="sm"
              options={exercices.map((e) => ({
                value: String(e.idExercice),
                label: String(e.annee),
              }))}
              placeholder="Sélectionner un exercice…"
            />

            <SearchableSelect
              required
              label="Demandeur"
              value={idDemandeur === '' ? '' : String(idDemandeur)}
              onChange={(v) => setIdDemandeur(v ? Number(v) : '')}
              disabled={readOnly || mode === 'edit'}
              density="xl"
              options={demandeurs.map((d) => ({
                value: String(d.idDemandeur),
                label: `${d.code} — ${d.libelle}`,
              }))}
              placeholder="Sélectionner un demandeur…"
            />

            {canCreateDemandeur && !readOnly && mode === 'create' && (
              <Button
                variant="outlined"
                onClick={() => setNewDemandeurOpen(true)}
                sx={{
                  height: 40,
                  minWidth: 118,
                  px: 1.75,
                  whiteSpace: 'nowrap',
                  flexShrink: 0,
                  alignSelf: { xs: 'stretch', sm: 'center' },
                }}
              >
                + Ajouter
              </Button>
            )}

            <SearchableSelect
              required
              label="Cas de dossier"
              value={idCasDossier === '' ? '' : String(idCasDossier)}
              onChange={(v) => setIdCasDossier(v ? Number(v) : '')}
              disabled={readOnly || mode === 'edit'}
              density="xl"
              options={casList.map((c) => ({
                value: String(c.idCasDossier),
                label: `${c.code} — ${c.libelle}`,
              }))}
              placeholder="Sélectionner un cas…"
            />
          </FormGrid>

          <FormGrid>
            {ubAuto && (
              <>
                <ComputedField
                  label="Unité budgétaire"
                  value={`${ubAuto.codeUB} — ${ubAuto.libelleUB}`}
                  minWidth={formFieldSize.lg}
                  maxWidth={formFieldSize.xxl}
                />
                <ComputedField
                  label="Département"
                  value={ubAuto.libelleDepartement}
                  helperText="Déterminé automatiquement"
                  minWidth={formFieldSize.lg}
                  maxWidth={formFieldSize.xl}
                />
              </>
            )}

            <TextField
              required
              label="Objet"
              value={objet}
              onChange={(e) => setObjet(e.target.value)}
              disabled={readOnly}
              fullWidth
              placeholder="Décrire l'objet de la demande…"
              slotProps={{ inputLabel: { shrink: true } }}
              sx={{ flex: '1 1 100%', minWidth: '100%' }}
            />
            <TextField
              type="date"
              required
              label="Date d'émission"
              slotProps={{ inputLabel: { shrink: true } }}
              value={dateEmission}
              onChange={(e) => setDateEmission(e.target.value)}
              disabled={readOnly}
              sx={{
                width: formFieldSize.md,
                minWidth: formFieldSize.md,
                flex: '0 0 auto',
              }}
            />
            <TextField
              label="Lieu d'émission"
              value={lieuEmission}
              onChange={(e) => setLieuEmission(e.target.value)}
              disabled={readOnly}
              placeholder="Ex. Kinshasa…"
              helperText="Optionnel"
              slotProps={{ inputLabel: { shrink: true } }}
              sx={{
                width: formFieldSize.lg,
                minWidth: formFieldSize.md,
                maxWidth: formFieldSize.xl,
                flex: '0 0 auto',
              }}
            />
            <TextField
              label="Compte de section"
              value={compteSection}
              onChange={(e) => setCompteSection(e.target.value)}
              disabled={readOnly}
              placeholder="Saisir un compte…"
              helperText="Optionnel"
              slotProps={{ inputLabel: { shrink: true } }}
              sx={{
                width: formFieldSize.lg,
                minWidth: formFieldSize.md,
                maxWidth: formFieldSize.xl,
                flex: '0 0 auto',
              }}
            />
          </FormGrid>
        </FormSection>
            </NumberedSectionBlock>

            <NumberedSectionBlock step={2}>
        <FormSection title="Destination budgétaire sollicitée">
          <Stack spacing={2}>
            <Typography variant="body2" color="text.secondary">
              Type de budget * — choix indicatif, modifiable par le Chargé de paiement au traitement
            </Typography>
            <ToggleButtonGroup
              exclusive
              value={typeBudgetSollicite}
              onChange={(_, v: 'DC' | 'AE' | 'BI' | null) => {
                if (!v || readOnly) return;
                setTypeBudgetSollicite(v);
                if (v === 'DC') setItemSollicite('');
              }}
              disabled={readOnly}
              size="small"
              sx={{ flexWrap: 'wrap' }}
            >
              <ToggleButton value="DC">DC</ToggleButton>
              <ToggleButton value="AE">AE</ToggleButton>
              <ToggleButton value="BI">BI / IVT</ToggleButton>
            </ToggleButtonGroup>
            {(typeBudgetSollicite === 'AE' || typeBudgetSollicite === 'BI') && (
              <TextField
                required
                label="Item N°"
                value={itemSollicite}
                onChange={(e) => setItemSollicite(e.target.value)}
                disabled={readOnly}
                placeholder="Ex. 025"
                slotProps={{ inputLabel: { shrink: true } }}
                sx={{ maxWidth: formFieldSize.lg }}
              />
            )}
          </Stack>
        </FormSection>
            </NumberedSectionBlock>

            <NumberedSectionBlock step={3}>
        <FormSection title="Montant sollicité">
          <FormGrid>
            <TextField
              select
              required
              label="Devise"
              value={idDevise}
              onChange={(e) => setIdDevise(e.target.value ? Number(e.target.value) : '')}
              disabled={readOnly}
              slotProps={{ inputLabel: { shrink: true } }}
              sx={{
                width: formFieldSize.lg,
                minWidth: formFieldSize.md,
                maxWidth: formFieldSize.xl,
                flex: '0 0 auto',
              }}
            >
              {devises
                .filter((d) => d.actif || d.idDevise === idDevise)
                .map((d) => (
                  <MenuItem key={d.idDevise} value={d.idDevise}>
                    {d.code} — {d.libelle}
                    {!d.actif ? ' (inactive)' : ''}
                  </MenuItem>
                ))}
            </TextField>
            <AmountField
              required
              label="Montant sollicité"
              value={montantBrut}
              onChange={setMontantBrut}
              disabled={readOnly}
              placeholder="0,00"
              currency={null}
              sx={{
                width: formFieldSize.lg,
                minWidth: formFieldSize.md,
                maxWidth: formFieldSize.xl,
                flex: '0 0 auto',
              }}
            />
          </FormGrid>
        </FormSection>
            </NumberedSectionBlock>

            <NumberedSectionBlock step={4}>
        <FormSection title="Mode de paiement sollicité">
          <Stack spacing={1}>
            <Typography variant="body2" color="text.secondary">
              Mode de paiement sollicité * — choix indicatif, modifiable par le Chargé de paiement au traitement
            </Typography>
            <ToggleButtonGroup
              exclusive
              value={modePaiementSollicite}
              onChange={(_, v: 'CAISSE' | 'BANQUE' | null) => {
                if (!v || readOnly) return;
                setModePaiementSollicite(v);
              }}
              disabled={readOnly}
              size="small"
            >
              <ToggleButton value="CAISSE">CAISSE</ToggleButton>
              <ToggleButton value="BANQUE">BANQUE</ToggleButton>
            </ToggleButtonGroup>
          </Stack>
        </FormSection>
            </NumberedSectionBlock>

            <NumberedSectionBlock step={5}>
        <FormSection title="Bénéficiaire(s)">
          <Stack spacing={2}>
            {beneficiaires.map((b, idx) => (
              <Paper key={b.key} variant="outlined" sx={{ p: { xs: 1.5, md: 2 } }}>
                <FormGrid>
                  <SearchableSelect
                    label="Type"
                    value={b.typeBeneficiaire}
                    onChange={(v) => {
                      const next = [...beneficiaires];
                      next[idx] = { ...b, typeBeneficiaire: v };
                      setBeneficiaires(next);
                    }}
                    disabled={readOnly}
                    density="md"
                    options={[
                      { value: 'AGENT', label: 'Agent' },
                      { value: 'TIERS', label: 'Tiers' },
                      { value: 'DIVERS', label: 'Divers' },
                    ]}
                    placeholder="Sélectionner un type…"
                  />
                  <TextField
                    required
                    label="Nom complet"
                    value={b.nomComplet}
                    onChange={(e) => {
                      const next = [...beneficiaires];
                      next[idx] = { ...b, nomComplet: e.target.value };
                      setBeneficiaires(next);
                    }}
                    disabled={readOnly}
                    sx={{ minWidth: { md: 220 }, flex: 1 }}
                  />
                  {b.typeBeneficiaire === 'AGENT' && (
                    <>
                      <TextField
                        label="Matricule"
                        value={b.matricule ?? ''}
                        onChange={(e) => {
                          const next = [...beneficiaires];
                          next[idx] = { ...b, matricule: e.target.value };
                          setBeneficiaires(next);
                        }}
                        disabled={readOnly}
                      />
                      <TextField
                        label="Fonction"
                        value={b.fonction ?? ''}
                        onChange={(e) => {
                          const next = [...beneficiaires];
                          next[idx] = { ...b, fonction: e.target.value };
                          setBeneficiaires(next);
                        }}
                        disabled={readOnly}
                      />
                    </>
                  )}
                  {b.typeBeneficiaire === 'TIERS' && (
                    <>
                      <TextField
                        label="Raison sociale"
                        value={b.raisonSociale ?? ''}
                        onChange={(e) => {
                          const next = [...beneficiaires];
                          next[idx] = { ...b, raisonSociale: e.target.value };
                          setBeneficiaires(next);
                        }}
                        disabled={readOnly}
                      />
                      <TextField
                        label="RCCM"
                        value={b.rccm ?? ''}
                        onChange={(e) => {
                          const next = [...beneficiaires];
                          next[idx] = { ...b, rccm: e.target.value };
                          setBeneficiaires(next);
                        }}
                        disabled={readOnly}
                      />
                    </>
                  )}
                  <TextField
                    label="Banque"
                    value={b.banque ?? ''}
                    onChange={(e) => {
                      const next = [...beneficiaires];
                      next[idx] = { ...b, banque: e.target.value };
                      setBeneficiaires(next);
                    }}
                    disabled={readOnly}
                  />
                  <TextField
                    label="N° compte"
                    value={b.numeroCompte ?? ''}
                    onChange={(e) => {
                      const next = [...beneficiaires];
                      next[idx] = { ...b, numeroCompte: e.target.value };
                      setBeneficiaires(next);
                    }}
                    disabled={readOnly}
                  />
                  {!readOnly && beneficiaires.length > 1 && (
                    <Button
                      color="error"
                      startIcon={<DeleteOutlinedIcon />}
                      onClick={() => setBeneficiaires(beneficiaires.filter((_, i) => i !== idx))}
                    >
                      Retirer
                    </Button>
                  )}
                </FormGrid>
              </Paper>
            ))}
            {!readOnly && (
              <Button onClick={() => setBeneficiaires([...beneficiaires, emptyBeneficiaire(beneficiaires.length + 1)])}>
                Ajouter un bénéficiaire
              </Button>
            )}
          </Stack>
        </FormSection>
            </NumberedSectionBlock>

            <NumberedSectionBlock step={6}>
        <FormSection title="Pièces justificatives">
          {piecesObligatoires.length === 0 ? (
            <Typography variant="body2" color="text.secondary">
              Sélectionnez un cas de dossier pour afficher les pièces justificatives attendues.
            </Typography>
          ) : (
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Type</TableCell>
                  <TableCell sx={{ display: { xs: 'none', md: 'table-cell' } }}>Caractère</TableCell>
                  <TableCell>Statut</TableCell>
                  <TableCell sx={{ display: { xs: 'none', md: 'table-cell' } }}>Document</TableCell>
                  <TableCell align="right">Action</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {piecesObligatoires.map((po) => {
                  const fournie = pieces.find(
                    (p) =>
                      p.idPieceObligatoire === po.idPieceObligatoire ||
                      p.codeTypePiece.toUpperCase() === po.codeTypePiece.toUpperCase(),
                  );
                  return (
                    <TableRow key={po.idPieceObligatoire}>
                      <TableCell>
                        {po.libelle}
                        <Typography variant="caption" sx={{ display: 'block' }} color="text.secondary">
                          {po.codeTypePiece}
                        </Typography>
                      </TableCell>
                      <TableCell sx={{ display: { xs: 'none', md: 'table-cell' } }}>
                        <Chip
                          size="small"
                          label={po.obligatoire ? 'Obligatoire' : 'Facultative'}
                          color={po.obligatoire ? 'warning' : 'default'}
                          variant="outlined"
                        />
                      </TableCell>
                      <TableCell>
                        {fournie ? (
                          <Chip size="small" label="Fournie" color="success" />
                        ) : po.obligatoire ? (
                          <Chip size="small" label="Manquante" color="error" variant="outlined" />
                        ) : (
                          <Chip size="small" label="Non fournie" color="default" variant="outlined" />
                        )}
                      </TableCell>
                      <TableCell sx={{ display: { xs: 'none', md: 'table-cell' } }}>
                        {fournie ? (
                          <DocumentActions
                            title={fournie.libelle}
                            fileName={fournie.nomFichierOriginal || 'piece'}
                            fileSizeBytes={fournie.tailleOctets}
                            showFileName
                            loadPreview={() =>
                              previewDemandePaiementPiece(resolveDemId(), fournie.idPieceJointe)
                            }
                            loadDownload={() =>
                              downloadDemandePaiementPiece(resolveDemId(), fournie.idPieceJointe)
                            }
                          />
                        ) : (
                          '—'
                        )}
                      </TableCell>
                      <TableCell align="right">
                        {!readOnly && (
                          <Stack
                            direction={{ xs: 'column', md: 'row' }}
                            spacing={1}
                            sx={{ justifyContent: { md: 'flex-end' } }}
                          >
                            <Button component="label" size="small" disabled={isMutating}>
                              {fournie ? 'Remplacer' : 'Ajouter'}
                              <input
                                hidden
                                type="file"
                                disabled={isMutating}
                                accept={PIECE_JUSTIFICATIVE_ACCEPT}
                                onChange={(e) => {
                                  const file = e.target.files?.[0];
                                  e.target.value = '';
                                  if (!file) return;
                                  void (async () => {
                                    try {
                                      if (fournie) await handleDeletePiece(fournie.idPieceJointe);
                                      await handleAddPiece(po, file);
                                    } catch (err) {
                                      void msgBox.error(
                                        getConflictUserMessage(err, 'Ajout de pièce impossible.'),
                                      );
                                    }
                                  })();
                                }}
                              />
                            </Button>
                            {fournie && (
                              <Button
                                size="small"
                                color="error"
                                disabled={isMutating}
                                onClick={() =>
                                  void handleDeletePiece(fournie.idPieceJointe).catch((err) =>
                                    msgBox.error(getConflictUserMessage(err, 'Suppression impossible.')),
                                  )
                                }
                              >
                                Supprimer
                              </Button>
                            )}
                          </Stack>
                        )}
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          )}
          <Divider sx={{ my: 2 }} />
          {!readOnly && (
            <Button component="label" size="small" variant="outlined" disabled={isMutating}>
              Ajouter une pièce complémentaire
              <input
                hidden
                type="file"
                disabled={isMutating}
                accept={PIECE_JUSTIFICATIVE_ACCEPT}
                onChange={(e) => {
                  const file = e.target.files?.[0];
                  e.target.value = '';
                  if (!file) return;
                  void handleAddPiece(null, file);
                }}
              />
            </Button>
          )}
          {readOnly && pieces.length > 0 && (
            <Stack spacing={1} sx={{ mt: 1 }}>
              {pieces.map((p) => (
                <DocumentActions
                  key={p.idPieceJointe}
                  title={p.libelle}
                  fileName={p.nomFichierOriginal || 'piece'}
                  fileSizeBytes={p.tailleOctets}
                  loadPreview={() => previewDemandePaiementPiece(resolveDemId(), p.idPieceJointe)}
                  loadDownload={() => downloadDemandePaiementPiece(resolveDemId(), p.idPieceJointe)}
                />
              ))}
            </Stack>
          )}
        </FormSection>
            </NumberedSectionBlock>

        {demande && (
          <EntiteValidationSection
            demande={demande}
            onUpdated={(updated) => setDemande(updated)}
          />
        )}
          </Stack>
        </Box>

        <Box
          sx={{
            flex: { xs: '1 1 auto', md: '0 0 300px' },
            width: { xs: '100%', md: 300 },
            position: { md: 'sticky' },
            top: { md: 88 },
            alignSelf: { md: 'flex-start' },
            maxHeight: { md: 'calc(100vh - 104px)' },
            overflowY: { md: 'auto' },
          }}
        >
          <DemandeSummaryRail
            reference={summaryRail.reference}
            montantLabel={summaryRail.montantLabel}
            exerciceLabel={summaryRail.exerciceLabel}
            destinationLabel={summaryRail.destinationLabel}
            modePaiementLabel={summaryRail.modePaiementLabel}
            beneficiairesCount={summaryRail.beneficiairesCount}
            checklist={summaryRail.checklist}
          >
            <Button component={RouterLink} to="/paiements" fullWidth>
              Annuler
            </Button>
            {canSupprimerBrouillon(user, demande?.statut) && mode === 'edit' && (
              <Button
                variant="outlined"
                color="error"
                disabled={isMutating}
                fullWidth
                onClick={() => void handleDelete()}
              >
                Supprimer définitivement
              </Button>
            )}
            {canWrite && editable && (
              <>
                <Button
                  variant="outlined"
                  disabled={isMutating}
                  fullWidth
                  onClick={() => void handleSave(false)}
                >
                  Enregistrer
                </Button>
                <Button
                  variant="outlined"
                  disabled={isMutating}
                  fullWidth
                  onClick={() => void handleSave(true)}
                >
                  Enregistrer et continuer
                </Button>
              </>
            )}
          </DemandeSummaryRail>
        </Box>
      </Box>

      <Dialog open={newDemandeurOpen} onClose={() => !creatingDemandeur && setNewDemandeurOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Ajouter un demandeur</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <TextField
              required
              label="Code"
              value={newDemandeurCode}
              onChange={(e) => setNewDemandeurCode(e.target.value)}
              disabled={creatingDemandeur}
              fullWidth
            />
            <TextField
              required
              label="Libellé"
              value={newDemandeurLibelle}
              onChange={(e) => setNewDemandeurLibelle(e.target.value)}
              disabled={creatingDemandeur}
              fullWidth
            />
            <SearchableSelect
              required
              label="Unité budgétaire"
              value={newDemandeurUb === '' ? '' : String(newDemandeurUb)}
              onChange={(v) => setNewDemandeurUb(v ? Number(v) : '')}
              disabled={creatingDemandeur}
              fullWidth
              options={ubs.map((u) => ({
                value: String(u.idUB),
                label: `${u.codeUB} — ${u.libelle} (${u.departementLibelle})`,
              }))}
              placeholder="Rechercher une UB…"
            />
          </Stack>
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2 }}>
          <Button onClick={() => setNewDemandeurOpen(false)} disabled={creatingDemandeur}>
            Annuler
          </Button>
          <Button variant="contained" onClick={() => void handleCreateDemandeur()} disabled={creatingDemandeur}>
            {creatingDemandeur ? 'Création…' : 'Créer'}
          </Button>
        </DialogActions>
      </Dialog>

    </>
  );
}
