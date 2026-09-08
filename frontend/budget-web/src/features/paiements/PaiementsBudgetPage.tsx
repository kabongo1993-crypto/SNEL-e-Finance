import {
  Alert,
  Button,
  Chip,
  Paper,
  Stack,
  TextField,
} from '@mui/material';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ErrorState, FilterZone, LoadingState, PageHeader, SearchableSelect, useMsgBox } from '../../components';
import { useAuth } from '../auth';
import {
  fetchCasDossiers,
  fetchDemandesPaiement,
  fetchDepartements,
  fetchExercices,
  fetchTypesBudget,
  fetchUnitesBudgetaires,
  receptionnerDemandePaiement,
  type CasDossier,
  type DemandePaiementListItem,
  type Departement,
  type Exercice,
  type TypeBudget,
  type UniteBudgetaire,
} from '../../services/apiClient';
import { buildBudgetDemandesRowActions } from './demandeRowActions';
import { DemandeListRowActions } from './DemandeListRowActions';
import { DemandesParDepartementList } from './DemandesParDepartementList';
import {
  enrichDemandesWithDepartement,
  filtreFileBudgets,
  groupDemandesByDepartement,
} from './paiementBudgetUtils';
import {
  applyBudgetFileCompteurs,
  formatStatutDpmChipLabel,
  formatStatutNavChipLabel,
} from './demandePaiementCompteursUtils';
import { useDemandePaiementCompteurs } from './useDemandePaiementCompteurs';
import {
  useDemandePaiementListInvalidation,
  useNotifyDemandePaiementMutated,
} from './useDemandePaiementListInvalidation';
import {
  STATUTS_DPM,
  STATUTS_FILE_BUDGETS,
  apiErrorMessage,
  canAccessPaiementsBudget,
  canControlerBudget,
  canReceptionBudget,
  labelStatutDpm,
} from './paiementUtils';
import {
  getConflictUserMessage,
  DemandePaiementRowMutationLockOverlay,
  useDemandePaiementRowMutationLock,
} from './useDemandePaiementMutationLock';
import { useDemandePaiementUrlFilters } from './useDemandePaiementUrlFilters';
import type { BudgetUrlFilters } from './demandePaiementUrlFilters';
import { normalizeStatutNavValue } from './paiementStatutNavConfig';

export function PaiementsBudgetPage() {
  const navigate = useNavigate();
  const { user } = useAuth();
  const canAccess = canAccessPaiementsBudget(user);
  const canReception = canReceptionBudget(user);
  const canControler = canControlerBudget(user);
  const msgBox = useMsgBox();
  const rowLock = useDemandePaiementRowMutationLock();
  const { isRowBusy, runRowMutation } = rowLock;
  const notifyMutated = useNotifyDemandePaiementMutated();

  const {
    filters: budgetFilters,
    listApiParams,
    compteursApiParams,
    setFiltreFile,
    setStatut,
    setDateDebut,
    setDateFin,
    setIdExercice,
    setIdDepartement,
    setIdUB,
    setIdCasDossier,
    setIdTypeBudget,
    setReference,
    setBeneficiaire,
  } = useDemandePaiementUrlFilters('budget');

  const {
    filtreFile,
    statut,
    dateDebut,
    dateFin,
    idExerciceUi,
    idDepartementUi,
    idUBUi,
    idCasDossierUi,
    idTypeBudgetUi,
    referenceUi,
    beneficiaireUi,
  } = budgetFilters as BudgetUrlFilters;

  const [rows, setRows] = useState<DemandePaiementListItem[]>([]);
  const [ubs, setUbs] = useState<UniteBudgetaire[]>([]);
  const [departements, setDepartements] = useState<Departement[]>([]);
  const [exercices, setExercices] = useState<Exercice[]>([]);
  const [typesBudget, setTypesBudget] = useState<TypeBudget[]>([]);
  const [casDossiers, setCasDossiers] = useState<CasDossier[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  const compteurParams = useMemo(
    () => ({
      scope: 'budget' as const,
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

  const displayCompteurs = useMemo(
    () => (compteurs ? applyBudgetFileCompteurs(compteurs, filtreFile) : null),
    [compteurs, filtreFile],
  );

  const fileActiveCount = useMemo(
    () =>
      compteurs ? applyBudgetFileCompteurs(compteurs, 'FILE').total : undefined,
    [compteurs],
  );

  const load = useCallback(async () => {
    setLoading(true);
    setLoadError(null);
    try {
      const [ex, ubList, dept, tb, cas, demandes] = await Promise.all([
        fetchExercices(),
        fetchUnitesBudgetaires(),
        fetchDepartements(),
        fetchTypesBudget(),
        fetchCasDossiers(),
        fetchDemandesPaiement(listApiParams),
      ]);
      setExercices(ex);
      setUbs(ubList.filter((u) => u.actif));
      setDepartements(dept.filter((d) => d.actif));
      setTypesBudget(tb);
      setCasDossiers(cas);
      setRows(demandes);
    } catch (err) {
      setLoadError(apiErrorMessage(err, 'Impossible de charger la file Budgets.'));
      setRows([]);
    } finally {
      setLoading(false);
    }
  }, [listApiParams]);

  useEffect(() => {
    if (!canAccess) return;
    void load();
  }, [canAccess, load]);

  useDemandePaiementListInvalidation(
    useCallback(() => {
      void load();
      void refreshCompteurs();
    }, [load, refreshCompteurs]),
  );

  const enriched = useMemo(() => enrichDemandesWithDepartement(rows, ubs), [rows, ubs]);

  // reference/beneficiaire : API uniquement (Étape 13 URL) — pas de double filtrage client.
  const filtered = useMemo(() => {
    return enriched.filter((d) => filtreFileBudgets(d, filtreFile));
  }, [enriched, filtreFile]);

  const groupes = useMemo(() => groupDemandesByDepartement(filtered), [filtered]);

  const handleReceptionner = async (id: number) => {
    if (isRowBusy(id)) return;
    try {
      await runRowMutation(
        id,
        async () => {
          await receptionnerDemandePaiement(id);
          void msgBox.success('Demande réceptionnée par le Département des Budgets.');
          notifyMutated();
        },
        { message: 'Réception de la demande' },
      );
    } catch (err) {
      void msgBox.error(getConflictUserMessage(err, 'Réception impossible.'));
    }
  };

  const rowActions = useMemo(
    () =>
      buildBudgetDemandesRowActions({
        navigate,
        canReception,
        canControler,
        isRowBusy,
        idUtilisateurCourant: user?.idUtilisateur,
        onReceptionner: (idDemande) => void handleReceptionner(idDemande),
      }),
    [navigate, canReception, canControler, isRowBusy, user?.idUtilisateur],
  );

  if (!canAccess) {
    return (
      <Alert severity="warning">
        Permission insuffisante. Accès réservé aux rôles Budgets (`paiements.reception_budget`,
        `paiements.controler_budget`, `paiements.viser_budget`).
      </Alert>
    );
  }

  return (
    <>
      <PageHeader
        title="Demandes de paiement — Budget"
        subtitle="File Chargé DP — réception, contrôle et visa budgétaire (jusqu'à VISEE_BUDGETAIREMENT)."
        breadcrumbs={[
          { label: 'e-Finance', to: '/dashboard' },
          { label: 'Paiements', to: '/paiements' },
          { label: 'Budget' },
        ]}
      />

      <Paper variant="outlined" sx={{ p: { xs: 1.5, md: 2 }, mb: 2.5 }}>
        <Stack spacing={1.5}>
          <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: 'wrap' }}>
            <Chip
              label={formatStatutNavChipLabel('File active', fileActiveCount, countsLoading)}
              color={filtreFile === 'FILE' ? 'primary' : 'default'}
              onClick={() => setFiltreFile('FILE')}
              variant={filtreFile === 'FILE' ? 'filled' : 'outlined'}
              sx={{ minWidth: countsLoading ? 100 : undefined }}
            />
            <Chip
              label={formatStatutNavChipLabel(
                'Toutes (incl. visées)',
                compteurs?.total,
                countsLoading,
              )}
              color={filtreFile === 'TOUTES' ? 'primary' : 'default'}
              onClick={() => setFiltreFile('TOUTES')}
              variant={filtreFile === 'TOUTES' ? 'filled' : 'outlined'}
              sx={{ minWidth: countsLoading ? 140 : undefined }}
            />
            {STATUTS_FILE_BUDGETS.map((s) => (
              <Chip
                key={s}
                size="small"
                label={formatStatutDpmChipLabel(labelStatutDpm(s), displayCompteurs, s, countsLoading)}
                variant="outlined"
                sx={{ minWidth: countsLoading ? 72 : undefined }}
              />
            ))}
          </Stack>

          <FilterZone
            search={
              <TextField
                size="small"
                fullWidth
                label="Recherche"
                value={referenceUi}
                onChange={(e) => setReference(e.target.value)}
                placeholder="Référence, objet…"
              />
            }
            actions={
              <Button variant="outlined" onClick={() => void load()}>
                Actualiser
              </Button>
            }
          >
            <TextField
              size="small"
              fullWidth
              label="Bénéficiaire"
              value={beneficiaireUi}
              onChange={(e) => setBeneficiaire(e.target.value)}
            />
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
                ...departements.map((d) => ({
                  value: String(d.idDepartement),
                  label: d.libelle,
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
                  label: `${u.codeUB} — ${u.libelle}`,
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
                ...casDossiers.map((c) => ({
                  value: String(c.idCasDossier),
                  label: c.libelle,
                })),
              ]}
            />
            <SearchableSelect
              label="Type budget"
              value={idTypeBudgetUi}
              onChange={setIdTypeBudget}
              allowEmpty
              fullWidth
              options={[
                { value: '', label: 'Tous' },
                ...typesBudget.map((t) => ({
                  value: String(t.idTypeBudget),
                  label: t.codeType,
                })),
              ]}
            />
            <SearchableSelect
              label="Statut"
              value={statut}
              onChange={(value) => setStatut(normalizeStatutNavValue(value))}
              allowEmpty
              fullWidth
              options={[
                { value: '', label: 'Tous' },
                ...STATUTS_DPM.filter((s) => s !== 'BROUILLON').map((s) => ({
                  value: s,
                  label: labelStatutDpm(s),
                })),
              ]}
            />
            <TextField
              size="small"
              fullWidth
              type="date"
              label="Du"
              value={dateDebut}
              onChange={(e) => setDateDebut(e.target.value)}
              slotProps={{ inputLabel: { shrink: true } }}
            />
            <TextField
              size="small"
              fullWidth
              type="date"
              label="Au"
              value={dateFin}
              onChange={(e) => setDateFin(e.target.value)}
              slotProps={{ inputLabel: { shrink: true } }}
            />
          </FilterZone>
        </Stack>
      </Paper>

      {loading && <LoadingState label="Chargement de la file Budgets…" />}
      {loadError && !loading && filtered.length === 0 && (
        <ErrorState message={loadError} onRetry={() => void load()} />
      )}

      {!loading && (
        <DemandesParDepartementList
          groupes={groupes}
          idUtilisateurCourant={user?.idUtilisateur}
          renderActions={(d) => <DemandeListRowActions row={d} actions={rowActions} />}
        />
      )}
      <DemandePaiementRowMutationLockOverlay lock={rowLock} />
    </>
  );
}
