import AddIcon from '@mui/icons-material/Add';
import {
  Alert,
  Box,
  Button,
  Paper,
  TextField,
} from '@mui/material';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link as RouterLink, useNavigate } from 'react-router-dom';
import {
  ErrorState,
  FilterZone,
  LoadingState,
  PageHeader,
  SecondaryButton,
  SearchableSelect,
  useMsgBox,
} from '../../components';
import { useAuth } from '../auth';
import {
  fetchCasDossiers,
  fetchDemandesPaiement,
  fetchDemandeurs,
  fetchDepartements,
  fetchExercices,
  fetchUnitesBudgetaires,
  fetchUtilisateursLookup,
  receptionnerDemandePaiement,
  type CasDossier,
  type DemandePaiementListItem,
  type DemandeurDto,
  type Departement,
  type Exercice,
  type UniteBudgetaire,
  type UtilisateurLookup,
} from '../../services/apiClient';
import { buildChargeDpmFileRowActions } from './demandeRowActions';
import { DemandeListRowActions } from './DemandeListRowActions';
import { DemandePaiementStatutNav } from './DemandePaiementStatutNav';
import { DemandesParDepartementList } from './DemandesParDepartementList';
import {
  enrichDemandesWithDepartement,
  groupDemandesByDepartement,
} from './paiementBudgetUtils';
import {
  apiErrorMessage,
  canChargeDpm,
  formatMontantDevise,
} from './paiementUtils';
import {
  getAllowedStatutsFromNavItems,
  getStatutDropdownOptions,
  getStatutNavItems,
  normalizeStatutNavValue,
  STATUT_NAV_LABELS,
} from './paiementStatutNavConfig';
import { useDemandePaiementUrlFilters } from './useDemandePaiementUrlFilters';
import type { ChargeDpmUrlFilters } from './demandePaiementUrlFilters';
import { countForStatutNavValue } from './demandePaiementCompteursUtils';
import { useDemandePaiementCompteurs } from './useDemandePaiementCompteurs';
import {
  useDemandePaiementListInvalidation,
  useNotifyDemandePaiementMutated,
} from './useDemandePaiementListInvalidation';
import {
  getConflictUserMessage,
  DemandePaiementRowMutationLockOverlay,
  useDemandePaiementRowMutationLock,
} from './useDemandePaiementMutationLock';
import {
  DEMANDE_PAIEMENT_BATCH_MAX_ITEMS,
  DemandePaiementBatchActionBar,
  DemandePaiementBatchConfirmDialog,
  DemandePaiementBatchDocumentsDialog,
  DemandePaiementBatchOrientationDialog,
  DemandePaiementBatchResultDialog,
  DemandePaiementBatchTraitementDialog,
  executerDemandePaiementBatch,
  getBatchOpsForStatut,
  operationRequiresDocuments,
  operationRequiresOrientation,
  operationRequiresTraitement,
  useDemandePaiementBatchSelection,
  type DemandePaiementBatchDocumentsPayload,
  type DemandePaiementBatchOpDef,
  type DemandePaiementBatchResult,
  type OrienterDemandePaiementPayload,
  type TraitementChargeDpmPayload,
} from './batch';
export function ChargeDpmFilePage() {
  const navigate = useNavigate();
  const { user } = useAuth();
  const canAccess = canChargeDpm(user);
  const msgBox = useMsgBox();
  const rowLock = useDemandePaiementRowMutationLock();
  const { isRowBusy, runRowMutation } = rowLock;
  const notifyMutated = useNotifyDemandePaiementMutated();

  const statutNavItems = useMemo(() => getStatutNavItems(user, 'charge-dpm'), [user]);
  const allowedStatuts = useMemo(
    () => getAllowedStatutsFromNavItems(statutNavItems),
    [statutNavItems],
  );
  const statutDropdownOptions = useMemo(
    () => getStatutDropdownOptions(statutNavItems, 'Tous les statuts'),
    [statutNavItems],
  );

  const {
    filters: chargeFilters,
    listApiParams,
    compteursApiParams,
    setStatut,
    setIdExercice,
    setIdDepartement,
    setIdUB,
    setIdDemandeur,
    setIdCasDossier,
    setReference,
  } = useDemandePaiementUrlFilters('charge-dpm', { allowedStatuts });

  const {
    statut,
    idExerciceUi,
    idDepartementUi,
    idUBUi,
    idDemandeurUi,
    idCasDossierUi,
    referenceUi,
  } = chargeFilters as ChargeDpmUrlFilters;

  const batchSelection = useDemandePaiementBatchSelection(statut, 'charge-dpm');
  const batchOps = useMemo(
    () => getBatchOpsForStatut(statut, user, 'charge-dpm'),
    [statut, user],
  );
  const statutBatchLabel = STATUT_NAV_LABELS[statut] ?? statut;
  const [batchBusy, setBatchBusy] = useState(false);
  const [pendingBatchOp, setPendingBatchOp] = useState<DemandePaiementBatchOpDef | null>(null);
  const [batchResult, setBatchResult] = useState<DemandePaiementBatchResult | null>(null);
  const [batchResultOpen, setBatchResultOpen] = useState(false);

  const [rows, setRows] = useState<DemandePaiementListItem[]>([]);
  const [ubs, setUbs] = useState<UniteBudgetaire[]>([]);
  const [depts, setDepts] = useState<Departement[]>([]);
  const [cas, setCas] = useState<CasDossier[]>([]);
  const [demandeurs, setDemandeurs] = useState<DemandeurDto[]>([]);
  const [exercices, setExercices] = useState<Exercice[]>([]);
  const [utilisateursLookup, setUtilisateursLookup] = useState<UtilisateurLookup[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const compteurParams = useMemo(
    () => ({
      scope: 'charge-dpm' as const,
      ...compteursApiParams,
    }),
    [compteursApiParams],
  );

  const {
    compteurs,
    loading: countsLoading,
    refresh: refreshCompteurs,
  } = useDemandePaiementCompteurs(compteurParams, {
    enabled: canAccess,
    onError: (message) => void msgBox.warning(message),
  });

  const statutNavItemsWithCounts = useMemo(
    () =>
      statutNavItems.map((item) => ({
        ...item,
        count: countForStatutNavValue(compteurs, item.value),
      })),
    [statutNavItems, compteurs],
  );

  const handleStatutDropdownChange = useCallback(
    (value: string) => {
      setStatut(normalizeStatutNavValue(value, { allowed: allowedStatuts }));
    },
    [setStatut, allowedStatuts],
  );

  const load = useCallback(async () => {
    if (!canAccess) return;
    setLoading(true);
    setError(null);
    try {
      const [list, ubList, deptList, casList, demList, exList, users] = await Promise.all([
        fetchDemandesPaiement(listApiParams),
        fetchUnitesBudgetaires({ accessibles: true, contexte: 'dpm' }),
        fetchDepartements(),
        fetchCasDossiers(),
        fetchDemandeurs(true),
        fetchExercices(),
        fetchUtilisateursLookup().catch(() => [] as UtilisateurLookup[]),
      ]);
      setRows(list);
      setUbs(ubList);
      const ubDeptIds = new Set(
        ubList.map((u) => u.idDepartement).filter((id): id is number => id != null),
      );
      setDepts(
        ubDeptIds.size === 0
          ? deptList
          : deptList.filter((d) => ubDeptIds.has(d.idDepartement)),
      );
      setCas(casList);
      setDemandeurs(demList);
      setExercices(exList);
      setUtilisateursLookup(users.filter((u) => u.actif));
    } catch (err) {
      setError(apiErrorMessage(err, 'Impossible de charger la file Chargé DP.'));
    } finally {
      setLoading(false);
    }
  }, [canAccess, listApiParams]);

  useEffect(() => {
    void load();
  }, [load]);

  const refreshListAndCompteurs = useCallback(() => {
    void load();
    void refreshCompteurs();
  }, [load, refreshCompteurs]);

  useDemandePaiementListInvalidation(
    useCallback(() => {
      refreshListAndCompteurs();
    }, [refreshListAndCompteurs]),
  );

  // reference : filtrée côté API via URL (Étape 13) — pas de second filtre client.
  const groupes = useMemo(() => {
    const enriched = enrichDemandesWithDepartement(rows, ubs);
    return groupDemandesByDepartement(enriched);
  }, [rows, ubs]);

  const onReceptionner = async (id: number) => {
    if (isRowBusy(id)) return;
    try {
      await runRowMutation(
        id,
        async () => {
          await receptionnerDemandePaiement(id);
          notifyMutated();
          navigate(`/paiements/charge-dpm/${id}`);
        },
        { message: 'Réception de la demande' },
      );
    } catch (err) {
      void msgBox.error(getConflictUserMessage(err, 'Réception impossible.'));
    }
  };

  const rowActions = useMemo(
    () =>
      buildChargeDpmFileRowActions({
        navigate,
        isRowBusy,
        idUtilisateurCourant: user?.idUtilisateur,
        onReceptionner: (idDemande) => void onReceptionner(idDemande),
      }),
    [navigate, isRowBusy, user?.idUtilisateur],
  );

  const handleBatchConfirm = useCallback(
    async (
      traitements?: Map<number, TraitementChargeDpmPayload>,
      documents?: Map<number, DemandePaiementBatchDocumentsPayload>,
      orientations?: Map<number, OrienterDemandePaiementPayload>,
    ) => {
      if (!pendingBatchOp || !batchSelection.enabled || batchSelection.selectedCount === 0) {
        setPendingBatchOp(null);
        return;
      }
      if (!statut) {
        setPendingBatchOp(null);
        return;
      }
      if (batchSelection.selectedCount > DEMANDE_PAIEMENT_BATCH_MAX_ITEMS) {
        void msgBox.error(
          `La sélection ne peut pas dépasser ${DEMANDE_PAIEMENT_BATCH_MAX_ITEMS} demandes de paiement.`,
        );
        setPendingBatchOp(null);
        return;
      }

      if (operationRequiresTraitement(pendingBatchOp.operation) && !traitements) {
        void msgBox.error('Le traitement de charge est obligatoire pour chaque demande.');
        return;
      }

      if (operationRequiresDocuments(pendingBatchOp.operation) && !documents) {
        void msgBox.error('Le payload documents est obligatoire pour chaque demande.');
        return;
      }

      if (operationRequiresOrientation(pendingBatchOp.operation) && !orientations) {
        void msgBox.error("L'orientation est obligatoire pour chaque demande.");
        return;
      }

      setBatchBusy(true);
      try {
        const result = await executerDemandePaiementBatch(pendingBatchOp.operation, {
          statutFiltre: statut,
          items: batchSelection.selectedList.map((idDemandePaiement) => ({
            idDemandePaiement,
            traitement: traitements?.get(idDemandePaiement) ?? null,
            documents: documents?.get(idDemandePaiement) ?? null,
            orientation: orientations?.get(idDemandePaiement) ?? null,
          })),
        });
        setBatchResult(result);
        setBatchResultOpen(true);

        const successIds = result.details
          .filter((d) => d.outcome === 'SUCCESS')
          .map((d) => d.idDemandePaiement);
        const ignoredIds = result.details
          .filter((d) => d.outcome === 'IGNORED')
          .map((d) => d.idDemandePaiement);
        batchSelection.removeIds([...successIds, ...ignoredIds]);

        notifyMutated();
        refreshListAndCompteurs();
      } catch (err) {
        void msgBox.error(getConflictUserMessage(err, 'Traitement par lot impossible.'));
      } finally {
        setBatchBusy(false);
        setPendingBatchOp(null);
      }
    },
    [
      pendingBatchOp,
      batchSelection,
      statut,
      msgBox,
      notifyMutated,
      refreshListAndCompteurs,
    ],
  );

  const pendingDocuments =
    pendingBatchOp != null && operationRequiresDocuments(pendingBatchOp.operation);
  const pendingTraitement =
    pendingBatchOp != null && operationRequiresTraitement(pendingBatchOp.operation);
  const pendingOrientation =
    pendingBatchOp != null && operationRequiresOrientation(pendingBatchOp.operation);
  const pendingConfirm =
    pendingBatchOp != null
    && !operationRequiresTraitement(pendingBatchOp.operation)
    && !operationRequiresDocuments(pendingBatchOp.operation)
    && !operationRequiresOrientation(pendingBatchOp.operation);
  const traitementRows = useMemo(
    () =>
      batchSelection.selectedList.map((id) => {
        const row = rows.find((r) => r.idDemandePaiement === id) as
          | (DemandePaiementListItem & {
              typeBudgetSollicite?: string | null;
              itemSollicite?: string | null;
              modePaiementSollicite?: string | null;
              typeInstrumentPaiement?: string | null;
              devisePaiement?: string | null;
            })
          | undefined;
        return {
          idDemandePaiement: id,
          reference: row?.reference ?? `#${id}`,
          deviseSollicitee: row?.devise ?? 'USD',
          modePaiementSollicite: row?.modePaiementSollicite ?? null,
          typeBudgetSollicite: row?.typeBudgetSollicite ?? null,
          itemSollicite: row?.itemSollicite ?? null,
          typeInstrumentPaiement: row?.typeInstrumentPaiement ?? null,
          devisePaiement: row?.devisePaiement ?? null,
        };
      }),
    [batchSelection.selectedList, rows],
  );
  const documentsRows = useMemo(
    () =>
      batchSelection.selectedList.map((id) => {
        const row = rows.find((r) => r.idDemandePaiement === id);
        return {
          idDemandePaiement: id,
          reference: row?.reference ?? `#${id}`,
          deviseSollicitee: row?.devise ?? 'USD',
          modePaiementSollicite: row?.modePaiementSollicite ?? null,
          typeInstrumentPaiement: row?.typeInstrumentPaiement ?? null,
        };
      }),
    [batchSelection.selectedList, rows],
  );
  const orientationRows = useMemo(
    () =>
      batchSelection.selectedList.map((id) => {
        const row = rows.find((r) => r.idDemandePaiement === id);
        return {
          idDemandePaiement: id,
          reference: row?.reference ?? `#${id}`,
        };
      }),
    [batchSelection.selectedList, rows],
  );
  const orientationUsers = useMemo(
    () =>
      utilisateursLookup.map((u) => ({
        idUtilisateur: u.idUtilisateur,
        label: `${u.prenom ? `${u.prenom} ` : ''}${u.nom} (${u.nomUtilisateur})`,
      })),
    [utilisateursLookup],
  );

  if (!canAccess) {
    return (
      <Box sx={{ p: 2 }}>
        <Alert severity="warning">Accès réservé au profil Chargé DP.</Alert>
      </Box>
    );
  }

  return (
    <Box sx={{ p: { xs: 1.5, md: 2 }, display: 'flex', flexDirection: 'column', gap: 2 }}>
      <PageHeader
        entityLabel="Demandes de paiement — Chargé DP"
        breadcrumbs={[
          { label: 'Paiements', to: '/paiements' },
          { label: 'Chargé DP' },
        ]}
        actions={
          <>
            <Button
              variant="outlined"
              component={RouterLink}
              to="/paiements/documents-etablis"
            >
              Documents établis
            </Button>
            <Button
              variant="contained"
              startIcon={<AddIcon />}
              component={RouterLink}
              to="/paiements/nouveau?source=charge-dpm"
            >
              Nouvelle demande
            </Button>
          </>
        }
      />

      <DemandePaiementStatutNav
        items={statutNavItemsWithCounts}
        activeStatut={statut}
        onChange={setStatut}
        countsLoading={countsLoading}
      />

      <Paper variant="outlined" sx={{ p: 2 }}>
        <FilterZone
          search={
            <TextField
              size="small"
              fullWidth
              label="Recherche"
              value={referenceUi}
              onChange={(e) => setReference(e.target.value)}
            />
          }
          actions={<SecondaryButton onClick={() => void load()}>Actualiser</SecondaryButton>}
        >
          <SearchableSelect
            label="Exercice"
            value={idExerciceUi}
            onChange={setIdExercice}
            allowEmpty
            fullWidth
            options={[
              { value: '', label: 'Tous' },
              ...exercices.map((e) => ({
                value: String(e.idExercice),
                label: String(e.annee),
              })),
            ]}
          />
          <SearchableSelect
            label="Département"
            value={idDepartementUi}
            onChange={setIdDepartement}
            allowEmpty
            fullWidth
            options={[
              { value: '', label: 'Tous' },
              ...depts.map((d) => ({
                value: String(d.idDepartement),
                label: `${d.code} — ${d.libelle}`,
              })),
            ]}
          />
          <SearchableSelect
            label="UB"
            value={idUBUi}
            onChange={setIdUB}
            allowEmpty
            fullWidth
            options={[
              { value: '', label: 'Toutes' },
              ...ubs.map((u) => ({
                value: String(u.idUB),
                label: u.codeUB,
              })),
            ]}
          />
          <SearchableSelect
            label="Demandeur"
            value={idDemandeurUi}
            onChange={setIdDemandeur}
            allowEmpty
            fullWidth
            options={[
              { value: '', label: 'Tous' },
              ...demandeurs.map((d) => ({
                value: String(d.idDemandeur),
                label: d.code,
              })),
            ]}
          />
          <SearchableSelect
            label="Cas de dossier"
            value={idCasDossierUi}
            onChange={setIdCasDossier}
            allowEmpty
            fullWidth
            options={[
              { value: '', label: 'Tous' },
              ...cas.map((c) => ({
                value: String(c.idCasDossier),
                label: c.code,
              })),
            ]}
          />
          <SearchableSelect
            label="Statut"
            value={statut}
            onChange={handleStatutDropdownChange}
            allowEmpty
            fullWidth
            options={statutDropdownOptions}
          />
        </FilterZone>
      </Paper>

      {loading && <LoadingState />}
      {error && !loading && <ErrorState message={error} onRetry={() => void load()} />}

      {!loading && !error && (
        <>
          {batchSelection.enabled && batchSelection.selectedCount > 0 && (
            <DemandePaiementBatchActionBar
              selectedCount={batchSelection.selectedCount}
              statutLabel={statutBatchLabel}
              operations={batchOps}
              busy={batchBusy}
              onOperation={(op) => setPendingBatchOp(op)}
              onClear={batchSelection.clear}
            />
          )}
          <DemandesParDepartementList
            groupes={groupes}
            showDemandeur
            showObjet={false}
            montantLabel="Montant"
            emptyMessage="Aucune demande dans la file."
            formatMontant={(d) => formatMontantDevise(d.montantBrut, d.devise)}
            idUtilisateurCourant={user?.idUtilisateur}
            renderActions={(d) => <DemandeListRowActions row={d} actions={rowActions} />}
            selectable={batchSelection.enabled}
            selectedIds={batchSelection.selectedIds}
            onToggleSelect={batchSelection.toggle}
            onSelectAllVisible={batchSelection.setAll}
          />
        </>
      )}
      <DemandePaiementRowMutationLockOverlay lock={rowLock} />
      <DemandePaiementBatchConfirmDialog
        open={pendingConfirm}
        busy={batchBusy}
        selectedCount={batchSelection.selectedCount}
        statutLabel={statutBatchLabel}
        operation={pendingBatchOp}
        onConfirm={() => void handleBatchConfirm()}
        onClose={() => {
          if (!batchBusy) setPendingBatchOp(null);
        }}
      />
      <DemandePaiementBatchTraitementDialog
        open={pendingTraitement}
        busy={batchBusy}
        operation={pendingBatchOp}
        rows={traitementRows}
        onConfirm={(traitements) => void handleBatchConfirm(traitements)}
        onClose={() => {
          if (!batchBusy) setPendingBatchOp(null);
        }}
      />
      <DemandePaiementBatchDocumentsDialog
        open={pendingDocuments}
        busy={batchBusy}
        operation={pendingBatchOp}
        rows={documentsRows}
        onConfirm={(documents) => void handleBatchConfirm(undefined, documents)}
        onClose={() => {
          if (!batchBusy) setPendingBatchOp(null);
        }}
      />
      <DemandePaiementBatchOrientationDialog
        open={pendingOrientation}
        busy={batchBusy}
        operation={pendingBatchOp}
        rows={orientationRows}
        utilisateurs={orientationUsers}
        onConfirm={(orientations) => void handleBatchConfirm(undefined, undefined, orientations)}
        onClose={() => {
          if (!batchBusy) setPendingBatchOp(null);
        }}
      />
      <DemandePaiementBatchResultDialog
        open={batchResultOpen}
        result={batchResult}
        onClose={() => setBatchResultOpen(false)}
      />
    </Box>
  );
}
