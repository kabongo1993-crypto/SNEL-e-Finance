import AddIcon from '@mui/icons-material/Add';
import {
  Box,
  Button,
  Paper,
  TextField,
} from '@mui/material';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link as RouterLink, useNavigate } from 'react-router-dom';
import { ErrorState, FilterZone, LoadingState, PageHeader, SearchableSelect, useMsgBox } from '../../components';
import { useAuth } from '../auth';
import {
  fetchCasDossiers,
  fetchDemandesPaiement,
  fetchExercices,
  fetchUnitesBudgetaires,
  soumettreDemandePaiement,
  annulerSoumissionDemandePaiement,
  deleteDemandePaiement,
  type CasDossier,
  type DemandePaiementListItem,
  type Exercice,
  type UniteBudgetaire,
} from '../../services/apiClient';
import { buildMesDemandesRowActions } from './demandeRowActions';
import { DemandeListRowActions } from './DemandeListRowActions';
import { DemandePaiementStatutNav } from './DemandePaiementStatutNav';
import { DemandesParDepartementList } from './DemandesParDepartementList';
import {
  getConflictUserMessage,
  DemandePaiementRowMutationLockOverlay,
  useDemandePaiementRowMutationLock,
} from './useDemandePaiementMutationLock';
import {
  enrichDemandesWithDepartement,
  groupDemandesByDepartement,
} from './paiementBudgetUtils';
import {
  getAllowedStatutsFromNavItems,
  getStatutDropdownOptions,
  getStatutNavItems,
  normalizeStatutNavValue,
  STATUT_NAV_LABELS,
} from './paiementStatutNavConfig';
import { useDemandePaiementUrlFilters } from './useDemandePaiementUrlFilters';
import { countForStatutNavValue } from './demandePaiementCompteursUtils';
import { useDemandePaiementCompteurs } from './useDemandePaiementCompteurs';
import {
  useDemandePaiementListInvalidation,
  useNotifyDemandePaiementMutated,
} from './useDemandePaiementListInvalidation';
import {
  apiErrorMessage,
  canChargeDpm,
  canEcrirePaiements,
  formatMontantUsd,
} from './paiementUtils';
import {
  DemandePaiementBatchActionBar,
  DemandePaiementBatchConfirmDialog,
  DemandePaiementBatchPhysiqueDialog,
  DemandePaiementBatchRejetDialog,
  DemandePaiementBatchResultDialog,
  executerDemandePaiementBatch,
  getBatchOpsForStatut,
  operationRequiresDeclaration,
  operationRequiresRetour,
  useDemandePaiementBatchSelection,
  type DeclarationValidationPhysiquePayload,
  type DemandePaiementBatchOpDef,
  type DemandePaiementBatchResult,
  type RetourDemandePaiementPayload,
} from './batch';

export interface PaiementsListPageProps {
  title: string;
  subtitle?: string;
  showNewButton?: boolean;
  hideHeader?: boolean;
}

export function PaiementsListPage({
  title,
  subtitle = 'Suivi des demandes de paiement.',
  showNewButton = true,
  hideHeader = false,
}: PaiementsListPageProps) {
  const navigate = useNavigate();
  const { user } = useAuth();
  const msgBox = useMsgBox();
  const rowLock = useDemandePaiementRowMutationLock();
  const { isRowBusy, runRowMutation } = rowLock;
  const notifyMutated = useNotifyDemandePaiementMutated();
  const canWrite = canEcrirePaiements(user);
  const isChargeDpm = canChargeDpm(user);

  const statutNavItems = useMemo(() => getStatutNavItems(user), [user]);
  const allowedStatuts = useMemo(
    () => getAllowedStatutsFromNavItems(statutNavItems),
    [statutNavItems],
  );
  const statutDropdownOptions = useMemo(
    () => getStatutDropdownOptions(statutNavItems),
    [statutNavItems],
  );

  const {
    filters,
    listApiParams,
    compteursApiParams,
    setStatut,
    setDateDebut,
    setDateFin,
    setIdExercice,
    setIdUB,
    setIdCasDossier,
  } = useDemandePaiementUrlFilters('mes-demandes', { allowedStatuts });

  const { statut, dateDebut, dateFin, idExerciceUi, idUBUi, idCasDossierUi } =
    filters.scope === 'mes-demandes'
      ? filters
      : {
          statut: '' as const,
          dateDebut: '',
          dateFin: '',
          idExerciceUi: '',
          idUBUi: '',
          idCasDossierUi: '',
        };

  const batchSelection = useDemandePaiementBatchSelection(statut);
  const batchOps = useMemo(() => getBatchOpsForStatut(statut, user), [statut, user]);
  const statutBatchLabel = STATUT_NAV_LABELS[statut] ?? statut;
  const [batchBusy, setBatchBusy] = useState(false);
  const [pendingBatchOp, setPendingBatchOp] = useState<DemandePaiementBatchOpDef | null>(null);
  const [batchResult, setBatchResult] = useState<DemandePaiementBatchResult | null>(null);
  const [batchResultOpen, setBatchResultOpen] = useState(false);

  const compteurParams = useMemo(
    () => ({
      scope: 'mes-demandes' as const,
      ...compteursApiParams,
    }),
    [compteursApiParams],
  );

  const [rows, setRows] = useState<DemandePaiementListItem[]>([]);
  const [ubs, setUbs] = useState<UniteBudgetaire[]>([]);
  const [exercices, setExercices] = useState<Exercice[]>([]);
  const [casDossiers, setCasDossiers] = useState<CasDossier[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  /** Recherche locale multi-champs (hors URL, compteurs et API — Étape 14). */
  const [search, setSearch] = useState('');

  const {
    compteurs,
    loading: countsLoading,
    refresh: refreshCompteurs,
  } = useDemandePaiementCompteurs(compteurParams, {
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

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [ex, ubList, cas, list] = await Promise.all([
        fetchExercices(),
        fetchUnitesBudgetaires(),
        fetchCasDossiers(),
        fetchDemandesPaiement(listApiParams),
      ]);
      setExercices(ex);
      setUbs(ubList.filter((u) => u.actif));
      setCasDossiers(cas);
      setRows(list);
    } catch (err) {
      setError(apiErrorMessage(err, 'Impossible de charger les demandes.'));
      setRows([]);
    } finally {
      setLoading(false);
    }
  }, [listApiParams]);

  /** Rafraîchit uniquement la liste filtrée et les compteurs (pas les référentiels). */
  const refreshListAndCompteurs = useCallback(async () => {
    try {
      const list = await fetchDemandesPaiement(listApiParams);
      setRows(list);
    } catch (err) {
      setError(apiErrorMessage(err, 'Impossible de rafraîchir les demandes.'));
    }
    void refreshCompteurs();
  }, [listApiParams, refreshCompteurs]);

  useEffect(() => {
    void load();
  }, [load]);

  useDemandePaiementListInvalidation(
    useCallback(() => {
      void refreshListAndCompteurs();
    }, [refreshListAndCompteurs]),
  );

  const groupes = useMemo(() => {
    const enriched = enrichDemandesWithDepartement(rows, ubs);
    const q = search.trim().toLowerCase();
    const filtered = !q
      ? enriched
      : enriched.filter((r) => {
          const hay =
            `${r.reference} ${r.objet} ${r.codeUB} ${r.libelleUB} ${r.libelleCasDossier} ${r.libelleDepartement} ${r.codeDepartement}`.toLowerCase();
          return hay.includes(q);
        });
    return groupDemandesByDepartement(filtered);
  }, [rows, search, ubs]);

  const handleAnnulerSoumission = useCallback(
    async (idDemande: number) => {
      if (isRowBusy(idDemande)) return;

      const ok = await msgBox.confirm({
        title: 'Annuler la soumission ?',
        message:
          "La demande repassera au statut « Validée entité ». Vous pourrez la resoumettre au Budget tant qu'elle n'a pas été réceptionnée.",
        confirmLabel: 'Annuler la soumission',
      });
      if (!ok) return;

      try {
        await runRowMutation(
          idDemande,
          async () => {
            const detail = await annulerSoumissionDemandePaiement(idDemande);
            void msgBox.success(`Soumission annulée — ${detail.reference} repasse en validée entité.`);
            notifyMutated();
          },
          { message: 'Annulation de soumission' },
        );
      } catch (err) {
        void msgBox.error(getConflictUserMessage(err, 'Annulation impossible.'));
      }
    },
    [msgBox, notifyMutated, isRowBusy, runRowMutation],
  );

  const handleSoumettre = useCallback(
    async (idDemande: number) => {
      if (isRowBusy(idDemande)) return;

      const ok = await msgBox.confirm({
        title: 'Soumettre au Budget ?',
        message:
          "La demande a été validée par l'entité. Elle sera transmise au Département des Budgets.",
        confirmLabel: 'Soumettre au Budget',
      });
      if (!ok) return;

      try {
        await runRowMutation(
          idDemande,
          async () => {
            const soumise = await soumettreDemandePaiement(idDemande);
            void msgBox.success(`Demande ${soumise.reference} soumise au Budget.`);
            notifyMutated();
          },
          { message: 'Soumission au Budget' },
        );
      } catch (err) {
        void msgBox.error(getConflictUserMessage(err, 'Soumission impossible.'));
      }
    },
    [msgBox, notifyMutated, isRowBusy, runRowMutation],
  );

  const handleSupprimer = useCallback(
    async (idDemande: number, reference?: string) => {
      if (isRowBusy(idDemande)) return;

      const ok = await msgBox.confirm({
        title: 'Supprimer définitivement ?',
        message:
          `La demande ${reference ? `« ${reference} » ` : ''}sera supprimée de façon permanente avec toutes ses pièces jointes. Cette action est irréversible.`,
        confirmLabel: 'Supprimer définitivement',
      });
      if (!ok) return;

      try {
        await runRowMutation(
          idDemande,
          async () => {
            await deleteDemandePaiement(idDemande);
            void msgBox.success('Demande supprimée définitivement.');
            notifyMutated();
          },
          { message: 'Suppression de la demande' },
        );
      } catch (err) {
        void msgBox.error(getConflictUserMessage(err, 'Suppression impossible.'));
      }
    },
    [msgBox, notifyMutated, isRowBusy, runRowMutation],
  );

  const rowActions = useMemo(
    () =>
      buildMesDemandesRowActions({
        user,
        navigate,
        isChargeDpm,
        isRowBusy,
        onSoumettre: (idDemande) => void handleSoumettre(idDemande),
        onAnnulerSoumission: (idDemande) => void handleAnnulerSoumission(idDemande),
        onSupprimer: (idDemande) => {
          const row = rows.find((r) => r.idDemandePaiement === idDemande);
          void handleSupprimer(idDemande, row?.reference);
        },
      }),
    [user, navigate, isChargeDpm, handleSoumettre, handleAnnulerSoumission, handleSupprimer, isRowBusy, rows],
  );

  const handleStatutDropdownChange = useCallback(
    (value: string) => {
      setStatut(normalizeStatutNavValue(value, { allowed: allowedStatuts }));
    },
    [setStatut, allowedStatuts],
  );

  const handleBatchConfirm = useCallback(
    async (
      declarations?: Map<number, DeclarationValidationPhysiquePayload>,
      retour?: RetourDemandePaiementPayload,
    ) => {
      if (!pendingBatchOp || !batchSelection.enabled || batchSelection.selectedCount === 0) {
        setPendingBatchOp(null);
        return;
      }
      if (!statut) {
        setPendingBatchOp(null);
        return;
      }

      if (operationRequiresDeclaration(pendingBatchOp.operation) && !declarations) {
        void msgBox.error('La déclaration de validation physique est obligatoire pour chaque demande.');
        return;
      }

      if (operationRequiresRetour(pendingBatchOp.operation) && !retour) {
        void msgBox.error('Le motif de rejet est obligatoire.');
        return;
      }

      setBatchBusy(true);
      try {
        const result = await executerDemandePaiementBatch(pendingBatchOp.operation, {
          statutFiltre: statut,
          items: batchSelection.selectedList.map((idDemandePaiement) => ({
            idDemandePaiement,
            declaration: declarations?.get(idDemandePaiement) ?? null,
            retour: retour ?? null,
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
        void refreshListAndCompteurs();
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

  const pendingPhysique =
    pendingBatchOp != null && operationRequiresDeclaration(pendingBatchOp.operation);
  const pendingRejet =
    pendingBatchOp != null && operationRequiresRetour(pendingBatchOp.operation);
  const pendingConfirm =
    pendingBatchOp != null
    && !operationRequiresDeclaration(pendingBatchOp.operation)
    && !operationRequiresRetour(pendingBatchOp.operation);
  const physiqueNiveau: 1 | 2 =
    pendingBatchOp?.operation === 'declarer-validation-physique-n2' ? 2 : 1;
  const physiqueRows = useMemo(
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

  return (
    <>
      {!hideHeader && (
        <PageHeader
          title={title}
          subtitle={subtitle}
          breadcrumbs={[
            { label: 'e-Finance', to: '/dashboard' },
            { label: 'Paiements', to: '/paiements' },
            { label: title },
          ]}
        />
      )}

      <DemandePaiementStatutNav
        items={statutNavItemsWithCounts}
        activeStatut={statut}
        onChange={setStatut}
        countsLoading={countsLoading}
      />

      <Paper variant="outlined" sx={{ p: { xs: 1.5, md: 2 }, mb: 2.5 }}>
        <FilterZone
          columns={{ xs: 1, sm: 2, md: 3, lg: 3, xl: 4 }}
          search={
            <TextField
              size="small"
              fullWidth
              label="Recherche"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Référence, objet, UB…"
            />
          }
          actions={
            <>
              {isChargeDpm && (
                <Button variant="outlined" component={RouterLink} to="/paiements/charge-dpm">
                  File Chargé DP
                </Button>
              )}
              {showNewButton && canWrite && (
                <Button
                  variant="contained"
                  startIcon={<AddIcon />}
                  component={RouterLink}
                  to={isChargeDpm ? '/paiements/nouveau?source=charge-dpm' : '/paiements/nouveau'}
                >
                  Nouvelle demande
                </Button>
              )}
            </>
          }
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
            placeholder="Exercice…"
          />
          <SearchableSelect
            label="Statut"
            value={statut}
            onChange={handleStatutDropdownChange}
            allowEmpty
            fullWidth
            options={statutDropdownOptions}
            placeholder="Statut…"
          />
          <SearchableSelect
            label="Cas de dossier"
            value={idCasDossierUi}
            onChange={setIdCasDossier}
            allowEmpty
            fullWidth
            options={[
              { value: '', label: 'Tous' },
              ...casDossiers.map((c) => ({
                value: String(c.idCasDossier),
                label: c.libelle,
              })),
            ]}
            placeholder="Cas de dossier…"
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
                label: `${u.codeUB} — ${u.libelle}`,
              })),
            ]}
            placeholder="Unité budgétaire…"
          />
          <TextField
            size="small"
            fullWidth
            type="date"
            label="Du"
            slotProps={{ inputLabel: { shrink: true } }}
            value={dateDebut}
            onChange={(e) => setDateDebut(e.target.value)}
          />
          <TextField
            size="small"
            fullWidth
            type="date"
            label="Au"
            slotProps={{ inputLabel: { shrink: true } }}
            value={dateFin}
            onChange={(e) => setDateFin(e.target.value)}
          />
        </FilterZone>
      </Paper>

      {loading && <LoadingState label="Chargement des demandes…" />}
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
            emptyMessage={
              rows.length === 0
                ? 'Aucune demande pour les filtres sélectionnés.'
                : 'Aucun résultat pour la recherche locale.'
            }
            formatMontant={(d) =>
              d.montantUsd != null ? formatMontantUsd(d.montantUsd) : '—'
            }
            renderActions={(d) => <DemandeListRowActions row={d} actions={rowActions} />}
            idUtilisateurCourant={user?.idUtilisateur ?? null}
            selectable={batchSelection.enabled}
            selectedIds={batchSelection.selectedIds}
            onToggleSelect={batchSelection.toggle}
            onSelectAllVisible={batchSelection.setAll}
          />
        </>
      )}
      {!loading && !error && groupes.length === 0 && rows.length > 0 && <Box sx={{ mt: 1 }} />}
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
      <DemandePaiementBatchPhysiqueDialog
        open={pendingPhysique}
        busy={batchBusy}
        niveau={physiqueNiveau}
        operation={pendingBatchOp}
        rows={physiqueRows}
        onConfirm={(declarations) => void handleBatchConfirm(declarations)}
        onClose={() => {
          if (!batchBusy) setPendingBatchOp(null);
        }}
      />
      <DemandePaiementBatchRejetDialog
        open={pendingRejet}
        busy={batchBusy}
        selectedCount={batchSelection.selectedCount}
        operation={pendingBatchOp}
        onConfirm={(retour) => void handleBatchConfirm(undefined, retour)}
        onClose={() => {
          if (!batchBusy) setPendingBatchOp(null);
        }}
      />
      <DemandePaiementBatchResultDialog
        open={batchResultOpen}
        result={batchResult}
        onClose={() => setBatchResultOpen(false)}
      />
    </>
  );
}
