import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { AppShell } from '../layout';
import { ThemeModeProvider } from '../theme';
import { DashboardPage } from '../features/dashboard';
import {
  NouveauPaiementPage,
  PaiementDetailPage,
  PaiementsDashboardPage,
  PaiementsListPage,
} from '../features/paiements';
import {
  BudgetDashboardPage,
  BudgetLignesPage,
  BudgetModulePage,
  BudgetSuiviPage,
} from '../features/budget';
import {
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
import { ReferentielsPage } from '../features/referentiels';
import {
  AdminAuditPage,
  AdminCircuitsPage,
  AdminModulePage,
  AdminRolesPage,
  AdminUtilisateursPage,
  RapportModulePage,
  RapportsPage,
  ReferentielDevisesPage,
  ReferentielExercicesPage,
  ReferentielModesPaiementPage,
  ReferentielNaturesPage,
  ReferentielSourcesPage,
  ReferentielStructuresPlaceholderPage,
  ReferentielUbPlaceholderPage,
} from '../features/shell/ShellPages';

export function AppRouter() {
  return (
    <ThemeModeProvider>
      <BrowserRouter>
        <Routes>
          <Route element={<AppShell />}>
            <Route index element={<Navigate to="/dashboard" replace />} />
            <Route path="dashboard" element={<DashboardPage />} />

            {/* Paiements */}
            <Route path="paiements" element={<PaiementsDashboardPage />} />
            <Route
              path="paiements/demandes"
              element={<PaiementsListPage title="Demandes de paiement" />}
            />
            <Route path="paiements/nouveau" element={<NouveauPaiementPage />} />
            <Route
              path="paiements/a-valider"
              element={
                <PaiementsListPage
                  title="Paiements à valider"
                  statusFilter={['soumis', 'en_validation']}
                />
              }
            />
            <Route
              path="paiements/valides"
              element={<PaiementsListPage title="Paiements validés" statusFilter="valide" />}
            />
            <Route
              path="paiements/executes"
              element={<PaiementsListPage title="Paiements exécutés" statusFilter="execute" />}
            />
            <Route
              path="paiements/rejetes"
              element={<PaiementsListPage title="Paiements rejetés" statusFilter="rejete" />}
            />
            <Route
              path="paiements/historique"
              element={
                <PaiementsListPage
                  title="Historique des paiements"
                  statusFilter={['execute', 'annule', 'rejete', 'valide']}
                  showNewButton={false}
                />
              }
            />
            <Route path="paiements/:id" element={<PaiementDetailPage />} />

            {/* Budget */}
            <Route path="budget" element={<BudgetDashboardPage />} />
            <Route path="budget/lignes" element={<BudgetLignesPage />} />
            <Route
              path="budget/previsions"
              element={<BudgetModulePage title="Prévisions budgétaires" pathLabel="Prévisions" />}
            />
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
            <Route path="tresorerie" element={<TresorerieDashboardPage />} />
            <Route
              path="tresorerie/situation"
              element={<TresorerieModulePage title="Situation de trésorerie" pathLabel="Situation" />}
            />
            <Route
              path="tresorerie/encaissements"
              element={<TresorerieModulePage title="Encaissements" pathLabel="Encaissements" />}
            />
            <Route
              path="tresorerie/decaissements"
              element={<TresorerieModulePage title="Décaissements" pathLabel="Décaissements" />}
            />
            <Route
              path="tresorerie/comptes"
              element={<TresorerieModulePage title="Comptes de trésorerie" pathLabel="Comptes" />}
            />
            <Route path="tresorerie/mouvements" element={<TresorerieMouvementsPage />} />
            <Route
              path="tresorerie/rapprochement"
              element={<TresorerieModulePage title="Rapprochement bancaire" pathLabel="Rapprochement" />}
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
            <Route path="referentiels/structures" element={<ReferentielStructuresPlaceholderPage />} />
            <Route path="referentiels/unites-budgetaires" element={<ReferentielUbPlaceholderPage />} />
            <Route path="referentiels/exercices" element={<ReferentielExercicesPage />} />
            <Route path="referentiels/natures" element={<ReferentielNaturesPage />} />
            <Route path="referentiels/sources" element={<ReferentielSourcesPage />} />
            <Route path="referentiels/modes-paiement" element={<ReferentielModesPaiementPage />} />
            <Route path="referentiels/devises" element={<ReferentielDevisesPage />} />
            <Route path="referentiels/types-modes" element={<ReferentielsPage />} />
            <Route path="referentiels" element={<Navigate to="/referentiels/organisationnel" replace />} />

            {/* Rapports */}
            <Route path="rapports" element={<RapportsPage />} />
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
            <Route path="administration/roles" element={<AdminRolesPage />} />
            <Route path="administration/permissions" element={<AdminModulePage title="Permissions" />} />
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
        </Routes>
      </BrowserRouter>
    </ThemeModeProvider>
  );
}
