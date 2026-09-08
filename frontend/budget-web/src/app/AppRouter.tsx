import { lazy, Suspense } from 'react';
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { Box, CircularProgress } from '@mui/material';
import { AppShell } from '../layout';
import { ThemeModeProvider } from '../theme';
import {
  AuthProvider,
  LoginPage,
  RequireAuth,
  RequirePermission,
  SessionScopedOutlet,
} from '../features/auth';
import { MsgBoxProvider, DocumentViewerProvider } from '../components';
import { DashboardPage } from '../features/dashboard';
import {
  NouveauPaiementPage,
  ModifierDemandePaiementPage,
  PaiementDetailPage,
  PaiementsDashboardPage,
  PaiementsListPage,
  PaiementsBudgetPage,
  ControleBudgetairePage,
  ChargeDpmFilePage,
  ChargeDpmDetailPage,
  DocumentsEtablisPage,
  PaiementsJuniorPage,
} from '../features/paiements';
import { DemandePaiementInvalidationProvider } from '../features/paiements/useDemandePaiementListInvalidation';
import {
  BudgetDashboardPage,
  BudgetLignesPage,
  BudgetModulePage,
  BudgetSuiviPage,
} from '../features/budget';
import {
  BanquesPage,
  CategoriesComptesPage,
  ComptesPage,
  DirectionsPage,
  GroupesTypesComptesPage,
  ProvincesPage,
  TypesComptesPage,
  TresorerieDashboardPage,
  TresorerieModulePage,
  TresorerieMouvementsPage,
} from '../features/tresorerie';
import {
  EngagementsListPage,
  EngagementsModulePage,
  NouvelEngagementPage,
} from '../features/engagements';
import { DepartementsPage } from '../features/departements';
import { ReferentielOrganisationnelPage } from '../features/structures';
import { StructuresPage } from '../features/structures-list';
import { UnitesBudgetairesPage } from '../features/unites-budgetaires';
import { ExercicesPage } from '../features/exercices';
import { ReferentielsPage } from '../features/referentiels';
import { TypesBudgetPage } from '../features/types-budget';
import { VersionsBudgetairesPage } from '../features/versions-budgetaires';
import { RubriquesBudgetairesPage } from '../features/rubriques-budgetaires';
import { ItemsBIPage } from '../features/items-bi';
import { SnelComptesImportPage } from '../features/snel-comptes-import';
import {
  MesPrevisionsPage,
  SoumissionsBudgetairesPage,
  SuiviUbDetailPage,
} from '../features/suivi-previsions';
import { HistoriquePrevisionsPage } from '../features/historique-previsions';
import { AjustementsBudgetairesPage } from '../features/ajustements-budgetaires';
import { RapportPrevisionsDcPage } from '../features/rapports/previsions-dc';
import { RapportPrevisionsDcConsolidePage } from '../features/rapports/previsions-dc-consolide';
import { RapportPrevisionsAePage } from '../features/rapports/previsions-ae';
import { RapportPrevisionsBiPage } from '../features/rapports/previsions-bi';
import {
  AdminAuditPage,
  AdminCircuitsPage,
  AdminModulePage,
  RapportModulePage,
  ReferentielModesPaiementPage,
  ReferentielSourcesPage,
} from '../features/shell/ShellPages';
import { AdminUtilisateursPage, AdminProfilsPage } from '../features/administration';
import { DevisesPage } from '../features/devises';
import { ParametresInstrumentPaiementPage } from '../features/parametres-instrument-paiement';
import { CasDossiersPage } from '../features/cas-dossiers';
import { TauxChangePage } from '../features/taux-change';

/** Lazy : une erreur HMR sur Prévisions ne doit pas blanchir toute l’app. */
const PrevisionsBudgetairesPage = lazy(() =>
  import('../features/previsions-budgetaires/PrevisionsBudgetairesPage').then((m) => ({
    default: m.PrevisionsBudgetairesPage,
  })),
);

function RouteFallback() {
  return (
    <Box sx={{ display: 'grid', placeItems: 'center', minHeight: 240, py: 4 }}>
      <CircularProgress size={28} />
    </Box>
  );
}

export function AppRouter() {
  return (
    <ThemeModeProvider>
      <BrowserRouter>
        <AuthProvider>
          <MsgBoxProvider>
          <DocumentViewerProvider>
          <Routes>
            <Route path="/login" element={<LoginPage />} />
            <Route element={<RequireAuth />}>
              <Route element={<SessionScopedOutlet />}>
              <Route element={<RequirePermission />}>
              <Route element={<DemandePaiementInvalidationProvider><AppShell /></DemandePaiementInvalidationProvider>}>
            <Route index element={<Navigate to="/dashboard" replace />} />
            <Route path="dashboard" element={<DashboardPage />} />

            {/* Paiements — DPM demandeur + Budgets */}
            <Route path="paiements/budget/:id/controle" element={<ControleBudgetairePage />} />
            <Route path="paiements/budget" element={<PaiementsBudgetPage />} />
            <Route path="paiements/documents-etablis" element={<DocumentsEtablisPage />} />
            <Route path="paiements/charge-dpm/:id" element={<ChargeDpmDetailPage />} />
            <Route path="paiements/charge-dpm" element={<ChargeDpmFilePage />} />
            <Route path="paiements/junior-dc" element={<PaiementsJuniorPage codeType="DC" />} />
            <Route path="paiements/junior-ae" element={<PaiementsJuniorPage codeType="AE" />} />
            <Route path="paiements/junior-bi" element={<PaiementsJuniorPage codeType="BI" />} />
            <Route path="paiements" element={<PaiementsDashboardPage />} />
            <Route
              path="paiements/demandes"
              element={<PaiementsListPage title="Mes demandes de paiement" />}
            />
            <Route path="paiements/nouveau" element={<NouveauPaiementPage />} />
            <Route
              path="paiements/a-valider"
              element={<Navigate to="/paiements?statut=EN_VALIDATION_N1" replace />}
            />
            <Route
              path="paiements/valides"
              element={<Navigate to="/paiements?statut=VISEE_BUDGETAIREMENT" replace />}
            />
            {/* Legacy : EXECUTEE n'existe pas — état terminal DPM = VISEE_BUDGETAIREMENT (hors Trésorerie). */}
            <Route
              path="paiements/executes"
              element={<Navigate to="/paiements?statut=VISEE_BUDGETAIREMENT" replace />}
            />
            <Route
              path="paiements/rejetes"
              element={<Navigate to="/paiements?statut=A_CORRIGER" replace />}
            />
            {/* Legacy : pas de statut « historique » — alias liste mes demandes sans bouton Nouveau. */}
            <Route
              path="paiements/historique"
              element={
                <PaiementsListPage
                  title="Historique des demandes"
                  subtitle="Consultation de vos demandes (filtres URL, périmètre backend inchangé)."
                  showNewButton={false}
                />
              }
            />
            <Route path="paiements/:id/modifier" element={<ModifierDemandePaiementPage />} />
            <Route path="paiements/:id" element={<PaiementDetailPage />} />

            {/* Budget */}
            <Route path="budget" element={<BudgetDashboardPage />} />
            <Route path="budget/types-budget" element={<TypesBudgetPage />} />
            <Route path="budget/versions" element={<VersionsBudgetairesPage />} />
            <Route path="budget/rubriques" element={<RubriquesBudgetairesPage />} />
            <Route path="budget/items-bi" element={<ItemsBIPage />} />
            <Route path="budget/import-syscohada" element={<SnelComptesImportPage />} />
            <Route path="budget/lignes" element={<BudgetLignesPage />} />
            <Route
              path="budget/previsions"
              element={
                <Suspense fallback={<RouteFallback />}>
                  <PrevisionsBudgetairesPage />
                </Suspense>
              }
            />
            <Route path="budget/suivi-mes-previsions/:idVersion/:idUB" element={<SuiviUbDetailPage />} />
            <Route path="budget/suivi-mes-previsions" element={<MesPrevisionsPage />} />
            <Route path="budget/soumissions" element={<SoumissionsBudgetairesPage />} />
            <Route path="budget/soumissions/:idVersion/:idUB" element={<SuiviUbDetailPage />} />
            <Route path="budget/ajustements" element={<AjustementsBudgetairesPage />} />
            <Route path="budget/historique-previsions" element={<HistoriquePrevisionsPage />} />
            <Route
              path="budget/repartition"
              element={<BudgetModulePage title="Répartition budgétaire" pathLabel="Répartition" />}
            />
            <Route
              path="budget/engagements"
              element={<BudgetModulePage title="Engagements budgétaires" pathLabel="Engagements" />}
            />
            <Route
              path="budget/execution"
              element={<BudgetModulePage title="Exécution budgétaire" pathLabel="Exécution" />}
            />
            <Route path="budget/suivi" element={<BudgetSuiviPage />} />
            <Route
              path="budget/historique"
              element={<BudgetModulePage title="Historique budgétaire" pathLabel="Historique" />}
            />

            {/* Trésorerie */}
            <Route path="tresorerie" element={<Navigate to="/tresorerie/dashboard" replace />} />
            <Route path="tresorerie/dashboard" element={<TresorerieDashboardPage />} />
            <Route
              path="tresorerie/decaissements"
              element={<TresorerieModulePage title="Décaissements" pathLabel="Décaissements" />}
            />
            <Route
              path="tresorerie/programmation-caisse"
              element={
                <TresorerieModulePage title="Programmation caisse" pathLabel="Programmation caisse" />
              }
            />
            <Route
              path="tresorerie/suivi-paiements"
              element={
                <TresorerieModulePage title="Suivi des paiements" pathLabel="Suivi des paiements" />
              }
            />
            <Route
              path="tresorerie/recherche"
              element={<TresorerieModulePage title="Recherche" pathLabel="Recherche" />}
            />
            <Route
              path="tresorerie/analyses"
              element={<TresorerieModulePage title="Analyses" pathLabel="Analyses" />}
            />
            <Route
              path="tresorerie/comptes-financiers"
              element={
                <TresorerieModulePage title="Comptes financiers" pathLabel="Comptes financiers" />
              }
            />
            <Route
              path="tresorerie/configuration"
              element={<TresorerieModulePage title="Configuration" pathLabel="Configuration" />}
            />
            <Route path="tresorerie/referentiels/banques" element={<BanquesPage />} />
            <Route path="tresorerie/referentiels/comptes" element={<ComptesPage />} />
            <Route path="tresorerie/referentiels/categories-comptes" element={<CategoriesComptesPage />} />
            <Route path="tresorerie/referentiels/directions" element={<DirectionsPage />} />
            <Route
              path="tresorerie/referentiels/groupes-types-comptes"
              element={<GroupesTypesComptesPage />}
            />
            <Route path="tresorerie/referentiels/provinces" element={<ProvincesPage />} />
            <Route path="tresorerie/referentiels/types-comptes" element={<TypesComptesPage />} />
            <Route path="tresorerie/referentiels/devises" element={<DevisesPage />} />
            <Route path="tresorerie/referentiels/taux-change" element={<TauxChangePage />} />
            {/* Legacy — anciennes routes trésorerie (compatibilité) */}
            <Route
              path="tresorerie/situation"
              element={<Navigate to="/tresorerie/dashboard" replace />}
            />
            <Route
              path="tresorerie/encaissements"
              element={<Navigate to="/tresorerie/dashboard" replace />}
            />
            <Route
              path="tresorerie/comptes"
              element={<Navigate to="/tresorerie/comptes-financiers" replace />}
            />
            <Route path="tresorerie/mouvements" element={<TresorerieMouvementsPage />} />
            <Route
              path="tresorerie/rapprochement"
              element={<Navigate to="/tresorerie/dashboard" replace />}
            />

            {/* Engagements */}
            <Route path="engagements" element={<EngagementsListPage title="Liste des engagements" />} />
            <Route path="engagements/nouveau" element={<NouvelEngagementPage />} />
            <Route
              path="engagements/en-attente"
              element={
                <EngagementsListPage
                  title="Engagements en attente"
                  statusFilter={['soumis', 'en_attente']}
                />
              }
            />
            <Route
              path="engagements/valides"
              element={<EngagementsListPage title="Engagements validés" statusFilter="valide" />}
            />
            <Route
              path="engagements/suivi"
              element={<EngagementsModulePage title="Suivi des engagements" pathLabel="Suivi" />}
            />
            <Route
              path="engagements/historique"
              element={
                <EngagementsListPage
                  title="Historique des engagements"
                  statusFilter={['execute', 'rejete', 'annule', 'valide']}
                />
              }
            />

            {/* Référentiels — réel + mocks */}
            <Route path="referentiels/organisationnel" element={<ReferentielOrganisationnelPage />} />
            <Route path="referentiel-organisationnel" element={<ReferentielOrganisationnelPage />} />
            <Route path="referentiels/departements" element={<DepartementsPage />} />
            <Route path="referentiels/structures" element={<StructuresPage />} />
            <Route path="referentiels/unites-budgetaires" element={<UnitesBudgetairesPage />} />
            <Route path="referentiels/exercices" element={<ExercicesPage />} />
            <Route path="referentiels/natures" element={<Navigate to="/referentiels/cas-dossiers" replace />} />
            <Route path="referentiels/sources" element={<ReferentielSourcesPage />} />
            <Route path="referentiels/modes-paiement" element={<ReferentielModesPaiementPage />} />
            <Route path="referentiels/devises" element={<DevisesPage />} />
            <Route path="referentiels/cas-dossiers" element={<CasDossiersPage />} />
            <Route
              path="referentiels/instruments-paiement"
              element={<ParametresInstrumentPaiementPage />}
            />
            <Route path="referentiels/taux-change" element={<TauxChangePage />} />
            <Route path="referentiels/types-modes" element={<ReferentielsPage />} />
            <Route path="referentiels" element={<Navigate to="/referentiels/organisationnel" replace />} />

            {/* Rapports */}
            <Route path="rapports" element={<Navigate to="/rapports/previsions-dc" replace />} />
            <Route path="rapports/previsions-dc" element={<RapportPrevisionsDcPage />} />
            <Route path="rapports/previsions-dc-consolide" element={<RapportPrevisionsDcConsolidePage />} />
            <Route path="rapports/previsions-ae" element={<RapportPrevisionsAePage />} />
            <Route path="rapports/previsions-bi" element={<RapportPrevisionsBiPage />} />
            <Route
              path="rapports/situation-budgetaire"
              element={<RapportModulePage title="Situation budgétaire" />}
            />
            <Route
              path="rapports/situation-tresorerie"
              element={<RapportModulePage title="Situation de trésorerie" />}
            />
            <Route
              path="rapports/etat-paiements"
              element={<RapportModulePage title="État des paiements" />}
            />
            <Route path="rapports/execution" element={<RapportModulePage title="Exécution budgétaire" />} />
            <Route path="rapports/engagements" element={<RapportModulePage title="Engagements" />} />
            <Route
              path="rapports/historique"
              element={<RapportModulePage title="Historique des opérations" />}
            />

            {/* Administration */}
            <Route path="administration" element={<Navigate to="/administration/utilisateurs" replace />} />
            <Route path="administration/utilisateurs" element={<AdminUtilisateursPage />} />
            <Route path="administration/roles" element={<AdminProfilsPage />} />
            <Route path="administration/permissions" element={<AdminProfilsPage />} />
            <Route path="administration/circuits" element={<AdminCircuitsPage />} />
            <Route path="administration/parametres" element={<AdminModulePage title="Paramètres" />} />
            <Route path="administration/audit" element={<AdminAuditPage />} />
            <Route
              path="administration/operations"
              element={<AdminModulePage title="Journal des opérations" />}
            />
            <Route
              path="administration/configuration"
              element={<AdminModulePage title="Configuration" />}
            />

            <Route path="*" element={<Navigate to="/dashboard" replace />} />
              </Route>
              </Route>
              </Route>
            </Route>
          </Routes>
          </DocumentViewerProvider>
          </MsgBoxProvider>
        </AuthProvider>
      </BrowserRouter>
    </ThemeModeProvider>
  );
}
