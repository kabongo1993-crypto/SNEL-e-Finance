import axios from 'axios';
import { AUTH_TOKEN_KEY, AUTH_USER_KEY, clearStoredAuthKeys } from './authStorage';

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5257';

export const apiClient = axios.create({
  baseURL: apiBaseUrl,
});

export interface AuthUser {
  idUtilisateur: number;
  nomUtilisateur: string;
  nom: string;
  prenom: string | null;
  postnom: string | null;
  email: string | null;
  matricule: string | null;
  actif: boolean;
  roles: string[];
  permissions: string[];
}

export interface LoginResponse {
  accessToken: string;
  tokenType: string;
  expiresAtUtc: string;
  utilisateur: AuthUser;
}

export function getStoredAccessToken(): string | null {
  return sessionStorage.getItem(AUTH_TOKEN_KEY);
}

export function getStoredAuthUser(): AuthUser | null {
  const raw = sessionStorage.getItem(AUTH_USER_KEY);
  if (!raw) return null;
  try {
    return JSON.parse(raw) as AuthUser;
  } catch {
    return null;
  }
}

export function setStoredAuth(token: string, user: AuthUser): void {
  sessionStorage.setItem(AUTH_TOKEN_KEY, token);
  sessionStorage.setItem(AUTH_USER_KEY, JSON.stringify(user));
}

export function clearStoredAuth(): void {
  clearStoredAuthKeys();
}

apiClient.interceptors.request.use((config) => {
  const token = getStoredAccessToken();
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

/** 401 métier (permission / accès) — ne doit pas être traité comme une session expirée. */
function isPermissionDeniedError(error: unknown): boolean {
  const status = (error as { response?: { status?: number; data?: { message?: string } } })?.response?.status;
  if (status === 403) return true;
  if (status !== 401) return false;
  const message = String(
    (error as { response?: { data?: { message?: string } } })?.response?.data?.message ?? '',
  ).toLowerCase();
  return (
    message.includes('permission requise') ||
    message.includes("n'avez pas accès") ||
    message.includes('acces refuse') ||
    message.includes('accès refusé')
  );
}

apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    const status = error?.response?.status;
    const url = String(error?.config?.url ?? '');
    const isAuthCall =
      url.includes('/api/v1/auth/login') ||
      url.includes('/api/v1/auth/me') ||
      url.includes('/api/v1/auth/status') ||
      url.includes('/api/v1/auth/bootstrap');
    if (
      status === 401 &&
      !isAuthCall &&
      !isPermissionDeniedError(error) &&
      getStoredAccessToken()
    ) {
      clearStoredAuth();
      if (typeof window !== 'undefined' && !window.location.pathname.startsWith('/login')) {
        window.location.assign('/login?reason=expired');
      }
    }
    return Promise.reject(error);
  },
);

export async function loginRequest(nomUtilisateur: string, motDePasse: string): Promise<LoginResponse> {
  const response = await apiClient.post<LoginResponse>('/api/v1/auth/login', {
    nomUtilisateur,
    motDePasse,
  });
  return response.data;
}

export async function fetchAuthStatus(): Promise<{ needsBootstrap: boolean; utilisateurCount: number }> {
  const response = await apiClient.get<{ needsBootstrap: boolean; utilisateurCount: number }>(
    '/api/v1/auth/status',
  );
  return response.data;
}

export async function bootstrapAdminRequest(payload: {
  nomUtilisateur: string;
  motDePasse: string;
  nom: string;
  prenom?: string;
  postnom?: string;
  email?: string;
}): Promise<LoginResponse> {
  const response = await apiClient.post<LoginResponse>('/api/v1/auth/bootstrap', payload);
  return response.data;
}

export async function fetchAuthMe(): Promise<{ utilisateur: AuthUser }> {
  const response = await apiClient.get<{ utilisateur: AuthUser }>('/api/v1/auth/me');
  return response.data;
}

export async function changePasswordRequest(payload: {
  motDePasseActuel: string;
  nouveauMotDePasse: string;
  confirmationMotDePasse: string;
}): Promise<{ message: string }> {
  const response = await apiClient.post<{ message: string }>('/api/v1/auth/change-password', payload);
  return response.data;
}

export interface TypeBudget {
  idTypeBudget: number;
  codeType: string;
  libelle: string;
  ordreAffichage: number;
  actif: boolean;
  nombrePrevisions?: number;
}

export interface CreateTypeBudgetPayload {
  codeType: string;
  libelle: string;
  ordreAffichage: number;
  actif?: boolean;
}

export async function fetchTypesBudget(): Promise<TypeBudget[]> {
  const response = await apiClient.get<TypeBudget[]>('/api/v1/types-budget');
  return response.data;
}

export async function fetchTypeBudget(idTypeBudget: number): Promise<TypeBudget> {
  const response = await apiClient.get<TypeBudget>(`/api/v1/types-budget/${idTypeBudget}`);
  return response.data;
}

export async function createTypeBudget(payload: CreateTypeBudgetPayload): Promise<TypeBudget> {
  const response = await apiClient.post<TypeBudget>('/api/v1/types-budget', payload);
  return response.data;
}

export async function updateTypeBudget(
  idTypeBudget: number,
  payload: CreateTypeBudgetPayload,
): Promise<TypeBudget> {
  const response = await apiClient.put<TypeBudget>(`/api/v1/types-budget/${idTypeBudget}`, payload);
  return response.data;
}

export async function deleteTypeBudget(idTypeBudget: number): Promise<void> {
  await apiClient.delete(`/api/v1/types-budget/${idTypeBudget}`);
}

export interface ModePrevision {
  idModePrevision: number;
  codeMode: string;
  libelle: string;
  actif: boolean;
}

export interface ReferentielOrganisationnelCompteurs {
  entites: number;
  departements: number;
  structures: number;
  unitesBudgetaires: number;
}

export interface StructureOrganisationnelle {
  idStructure: number;
  parentId: number | null;
  typeStructure: string;
  code: string;
  codeTechnique: string;
  libelle: string;
  parentCode: string | null;
  parentLibelle: string | null;
  departementCode: string | null;
  departementLibelle: string | null;
  nombreEnfants: number;
  nombreUnitesBudgetaires: number;
}

export interface UniteBudgetaireOrganisation {
  idUB: number;
  codeUB: string;
  libelle: string;
  departementCode: string;
  departementLibelle: string;
  structureId: number;
  structureCode: string;
  structureLibelle: string;
  structureType: string;
}

export interface ReferentielOrganisationnelSnapshot {
  compteurs: ReferentielOrganisationnelCompteurs;
  structures: StructureOrganisationnelle[];
  unitesBudgetaires: UniteBudgetaireOrganisation[];
}

export async function fetchModesPrevision(): Promise<ModePrevision[]> {
  const response = await apiClient.get<ModePrevision[]>('/api/v1/referentiels/modes-prevision');
  return response.data;
}

export async function fetchHealth(): Promise<{ status: string; database: boolean }> {
  const response = await apiClient.get<{ status: string; database: boolean }>('/api/v1/health');
  return response.data;
}

export async function fetchReferentielOrganisationnel(): Promise<ReferentielOrganisationnelSnapshot> {
  const response = await apiClient.get<ReferentielOrganisationnelSnapshot>(
    '/api/v1/referentiel-organisationnel',
  );
  return response.data;
}

export interface Departement {
  idDepartement: number;
  code: string;
  libelle: string;
  actif: boolean;
  dateCreation: string;
  nombreUnitesBudgetaires: number;
}

export interface CreateDepartementPayload {
  code: string;
  libelle: string;
  actif?: boolean;
}

export async function fetchDepartements(): Promise<Departement[]> {
  const response = await apiClient.get<Departement[]>('/api/v1/departements');
  return response.data;
}

export async function fetchDepartement(idDepartement: number): Promise<Departement> {
  const response = await apiClient.get<Departement>(`/api/v1/departements/${idDepartement}`);
  return response.data;
}

export async function createDepartement(payload: CreateDepartementPayload): Promise<Departement> {
  const response = await apiClient.post<Departement>('/api/v1/departements', payload);
  return response.data;
}

export async function updateDepartement(
  idDepartement: number,
  payload: CreateDepartementPayload,
): Promise<Departement> {
  const response = await apiClient.put<Departement>(`/api/v1/departements/${idDepartement}`, payload);
  return response.data;
}

export async function deleteDepartement(idDepartement: number): Promise<void> {
  await apiClient.delete(`/api/v1/departements/${idDepartement}`);
}

export interface Structure {
  idStructure: number;
  parentId: number | null;
  typeStructure: string;
  code: string;
  libelle: string;
  actif: boolean;
  dateCreation: string;
  parentCode: string | null;
  parentLibelle: string | null;
  nombreEnfants: number;
  nombreUnitesBudgetaires: number;
}

export interface CreateStructurePayload {
  typeStructure: string;
  code: string;
  libelle: string;
  parentId?: number | null;
  actif?: boolean;
}

export async function fetchStructures(): Promise<Structure[]> {
  const response = await apiClient.get<Structure[]>('/api/v1/structures');
  return response.data;
}

export async function createStructure(payload: CreateStructurePayload): Promise<Structure> {
  const response = await apiClient.post<Structure>('/api/v1/structures', payload);
  return response.data;
}

export async function fetchStructure(idStructure: number): Promise<Structure> {
  const response = await apiClient.get<Structure>(`/api/v1/structures/${idStructure}`);
  return response.data;
}

export async function updateStructure(
  idStructure: number,
  payload: CreateStructurePayload,
): Promise<Structure> {
  const response = await apiClient.put<Structure>(`/api/v1/structures/${idStructure}`, payload);
  return response.data;
}

export async function deleteStructure(idStructure: number): Promise<void> {
  await apiClient.delete(`/api/v1/structures/${idStructure}`);
}

export interface UniteBudgetaire {
  idUB: number;
  codeUB: string;
  libelle: string;
  idDepartement: number;
  departementCode: string;
  departementLibelle: string;
  idStructure: number;
  structureCode: string;
  structureLibelle: string;
  structureType: string;
  actif: boolean;
  dateCreation: string;
  nombrePrevisions: number;
}

export interface CreateUniteBudgetairePayload {
  codeUB: string;
  libelle: string;
  idDepartement: number;
  idStructure: number;
  actif?: boolean;
}

export async function fetchUnitesBudgetaires(options?: {
  accessibles?: boolean;
  /** 'dpm' = filtre périmètre/proxy DPM (création demandeur) ; défaut = règles prévisions */
  contexte?: 'dpm' | 'previsions';
}): Promise<UniteBudgetaire[]> {
  const params: Record<string, string | boolean> = {};
  if (options?.accessibles) params.accessibles = true;
  if (options?.contexte) params.contexte = options.contexte;
  const response = await apiClient.get<UniteBudgetaire[]>('/api/v1/unites-budgetaires', {
    params: Object.keys(params).length ? params : undefined,
  });
  return response.data;
}

export async function createUniteBudgetaire(
  payload: CreateUniteBudgetairePayload,
): Promise<UniteBudgetaire> {
  const response = await apiClient.post<UniteBudgetaire>('/api/v1/unites-budgetaires', payload);
  return response.data;
}

export async function fetchUniteBudgetaire(idUb: number): Promise<UniteBudgetaire> {
  const response = await apiClient.get<UniteBudgetaire>(`/api/v1/unites-budgetaires/${idUb}`);
  return response.data;
}

export async function updateUniteBudgetaire(
  idUb: number,
  payload: CreateUniteBudgetairePayload,
): Promise<UniteBudgetaire> {
  const response = await apiClient.put<UniteBudgetaire>(`/api/v1/unites-budgetaires/${idUb}`, payload);
  return response.data;
}

export async function deleteUniteBudgetaire(idUb: number): Promise<void> {
  await apiClient.delete(`/api/v1/unites-budgetaires/${idUb}`);
}

export interface Exercice {
  idExercice: number;
  annee: number;
  statut: string;
  dateOuverture: string | null;
  dateCloture: string | null;
  nombreVersions: number;
}

export interface CreateExercicePayload {
  annee: number;
  statut: string;
  dateOuverture?: string | null;
  dateCloture?: string | null;
}

export async function fetchExercices(): Promise<Exercice[]> {
  const response = await apiClient.get<Exercice[]>('/api/v1/exercices');
  return response.data;
}

export async function fetchExercice(idExercice: number): Promise<Exercice> {
  const response = await apiClient.get<Exercice>(`/api/v1/exercices/${idExercice}`);
  return response.data;
}

export async function createExercice(payload: CreateExercicePayload): Promise<Exercice> {
  const response = await apiClient.post<Exercice>('/api/v1/exercices', payload);
  return response.data;
}

export async function updateExercice(idExercice: number, payload: CreateExercicePayload): Promise<Exercice> {
  const response = await apiClient.put<Exercice>(`/api/v1/exercices/${idExercice}`, payload);
  return response.data;
}

export async function deleteExercice(idExercice: number): Promise<void> {
  await apiClient.delete(`/api/v1/exercices/${idExercice}`);
}

export interface VersionBudgetaire {
  idVersion: number;
  idExercice: number;
  anneeExercice: number;
  statutExercice: string;
  numeroVersion: number;
  libelle: string;
  idVersionPrecedente: number | null;
  numeroVersionPrecedente: number | null;
  libelleVersionPrecedente: string | null;
  dateCreation: string;
  dateDebutEffet: string;
  dateFinEffet: string | null;
  motif: string | null;
  statut: string;
  idUtilisateurCreation: number;
  nomUtilisateurCreation: string;
  idUtilisateurValidation: number | null;
  nomUtilisateurValidation: string | null;
  dateValidation: string | null;
  idUtilisateurSoumission: number | null;
  nomUtilisateurSoumission: string | null;
  dateSoumission: string | null;
  idUtilisateurControle: number | null;
  nomUtilisateurControle: string | null;
  dateControle: string | null;
  idUtilisateurRejet: number | null;
  nomUtilisateurRejet: string | null;
  dateRejet: string | null;
  motifRejet: string | null;
  nombrePrevisions: number;
  nombreTransferts: number;
  nombreVersionsSuivantes: number;
}

export interface CreateVersionBudgetairePayload {
  idExercice: number;
  numeroVersion: number;
  libelle: string;
  idVersionPrecedente?: number | null;
  dateDebutEffet: string;
  dateFinEffet?: string | null;
  motif?: string | null;
  /** Ignoré côté API — création toujours BROUILLON. */
  statut?: string;
  idUtilisateurCreation: number;
  idUtilisateurValidation?: number | null;
  dateValidation?: string | null;
}

export interface UpdateVersionBudgetairePayload {
  idExercice: number;
  numeroVersion: number;
  libelle: string;
  idVersionPrecedente?: number | null;
  dateDebutEffet: string;
  dateFinEffet?: string | null;
  motif?: string | null;
  idUtilisateurCreation: number;
}

export interface UtilisateurLookup {
  idUtilisateur: number;
  nomUtilisateur: string;
  nom: string;
  prenom: string | null;
  actif: boolean;
}

export async function fetchVersionsBudgetaires(): Promise<VersionBudgetaire[]> {
  const response = await apiClient.get<VersionBudgetaire[]>('/api/v1/versions-budgetaires');
  return response.data;
}

export async function fetchVersionBudgetaire(idVersion: number): Promise<VersionBudgetaire> {
  const response = await apiClient.get<VersionBudgetaire>(`/api/v1/versions-budgetaires/${idVersion}`);
  return response.data;
}

export async function createVersionBudgetaire(
  payload: CreateVersionBudgetairePayload,
): Promise<VersionBudgetaire> {
  const response = await apiClient.post<VersionBudgetaire>('/api/v1/versions-budgetaires', payload);
  return response.data;
}

export async function updateVersionBudgetaire(
  idVersion: number,
  payload: UpdateVersionBudgetairePayload,
): Promise<VersionBudgetaire> {
  const response = await apiClient.put<VersionBudgetaire>(
    `/api/v1/versions-budgetaires/${idVersion}`,
    payload,
  );
  return response.data;
}

export async function deleteVersionBudgetaire(idVersion: number): Promise<void> {
  await apiClient.delete(`/api/v1/versions-budgetaires/${idVersion}`);
}

export async function soumettreVersionBudgetaire(idVersion: number): Promise<VersionBudgetaire> {
  const response = await apiClient.post<VersionBudgetaire>(
    `/api/v1/versions-budgetaires/${idVersion}/soumettre`,
  );
  return response.data;
}

export async function controlerVersionBudgetaire(idVersion: number): Promise<VersionBudgetaire> {
  const response = await apiClient.post<VersionBudgetaire>(
    `/api/v1/versions-budgetaires/${idVersion}/controler`,
  );
  return response.data;
}

export async function validerVersionBudgetaire(idVersion: number): Promise<VersionBudgetaire> {
  const response = await apiClient.post<VersionBudgetaire>(
    `/api/v1/versions-budgetaires/${idVersion}/valider`,
  );
  return response.data;
}

export async function rejeterVersionBudgetaire(
  idVersion: number,
  motif: string,
): Promise<VersionBudgetaire> {
  const response = await apiClient.post<VersionBudgetaire>(
    `/api/v1/versions-budgetaires/${idVersion}/rejeter`,
    { motif },
  );
  return response.data;
}

export async function reouvrirVersionBudgetaire(idVersion: number): Promise<VersionBudgetaire> {
  const response = await apiClient.post<VersionBudgetaire>(
    `/api/v1/versions-budgetaires/${idVersion}/reouvrir`,
  );
  return response.data;
}

/** Workflow opérationnel Version × UB (statut individuel). */
export interface WorkflowPrevisionUb {
  idWorkflowPrevisionUB: number;
  idVersion: number;
  idUB: number;
  codeUB: string;
  libelleUB: string;
  idDepartement: number;
  codeDepartement: string;
  libelleDepartement: string;
  statut: string;
  dateSoumission: string | null;
  nomUtilisateurSoumission: string | null;
  dateControle: string | null;
  nomUtilisateurControle: string | null;
  dateValidation: string | null;
  nomUtilisateurValidation: string | null;
  dateRejet: string | null;
  nomUtilisateurRejet: string | null;
  motifRejet: string | null;
  document?: DocumentPrevision | null;
}

export interface WorkflowUbRejetDetail {
  idUB: number;
  codeUB: string;
  statutAvant: string;
  statutApres: string | null;
  outcome: string;
}

export interface WorkflowDepartementBulkResult {
  traitees: number;
  ignorees: number;
  details: WorkflowUbRejetDetail[];
  document?: DocumentPrevision | null;
}

/** Métadonnées d’un document officiel lié à un événement workflow. */
export interface DocumentPrevision {
  idDocument: number;
  reference: string;
  typeDocument: string;
  titre: string;
  idAudit: number | null;
  idVersion: number;
  anneeExercice: number;
  numeroVersion: number;
  idDepartement: number;
  codeDepartement: string;
  libelleDepartement: string;
  idUB: number | null;
  codeUB: string | null;
  libelleUB: string | null;
  portee: string;
  nbUbConcernees: number;
  idUtilisateurAuteur: number;
  nomUtilisateurAuteur: string;
  dateEvenement: string;
  statutAvant: string | null;
  statutApres: string | null;
  motif: string | null;
  montantDC: number;
  montantAE: number;
  montantBI: number;
  montantTotal: number;
  dateGeneration: string;
  tailleOctets: number;
}

export async function fetchDocumentPrevisionById(idDocument: number): Promise<DocumentPrevision> {
  const response = await apiClient.get<DocumentPrevision>(`/api/v1/documents-previsions/${idDocument}`);
  return response.data;
}

export async function fetchDocumentPrevisionByAudit(idAudit: number): Promise<DocumentPrevision | null> {
  try {
    const response = await apiClient.get<DocumentPrevision>(`/api/v1/documents-previsions/by-audit/${idAudit}`);
    return response.data;
  } catch (err: unknown) {
    const status = (err as { response?: { status?: number } })?.response?.status;
    if (status === 404) return null;
    throw err;
  }
}

export async function downloadDocumentPrevisionPdf(idDocument: number): Promise<Blob> {
  const response = await apiClient.get<Blob>(`/api/v1/documents-previsions/${idDocument}/pdf`, {
    responseType: 'blob',
  });
  return response.data;
}

export async function soumettrePrevisionUb(
  idVersion: number,
  idUB: number,
): Promise<WorkflowPrevisionUb> {
  const response = await apiClient.post<WorkflowPrevisionUb>(
    '/api/v1/workflow-previsions-ub/soumettre',
    { idVersion, idUB },
  );
  return response.data;
}

export async function controlerPrevisionUb(
  idVersion: number,
  idUB: number,
): Promise<WorkflowPrevisionUb> {
  const response = await apiClient.post<WorkflowPrevisionUb>(
    '/api/v1/workflow-previsions-ub/controler',
    { idVersion, idUB },
  );
  return response.data;
}

export async function validerPrevisionUb(
  idVersion: number,
  idUB: number,
): Promise<WorkflowPrevisionUb> {
  const response = await apiClient.post<WorkflowPrevisionUb>(
    '/api/v1/workflow-previsions-ub/valider',
    { idVersion, idUB },
  );
  return response.data;
}

export async function rejeterPrevisionUb(
  idVersion: number,
  idUB: number,
  motif: string,
): Promise<WorkflowPrevisionUb> {
  const response = await apiClient.post<WorkflowPrevisionUb>(
    '/api/v1/workflow-previsions-ub/rejeter',
    { idVersion, idUB, motif },
  );
  return response.data;
}

export async function soumettreDepartementPrevisions(
  idVersion: number,
  idDepartement: number,
): Promise<WorkflowDepartementBulkResult> {
  const response = await apiClient.post<WorkflowDepartementBulkResult>(
    '/api/v1/workflow-previsions-ub/soumettre-departement',
    { idVersion, idDepartement },
  );
  return response.data;
}

export async function controlerDepartementPrevisions(
  idVersion: number,
  idDepartement: number,
): Promise<WorkflowDepartementBulkResult> {
  const response = await apiClient.post<WorkflowDepartementBulkResult>(
    '/api/v1/workflow-previsions-ub/controler-departement',
    { idVersion, idDepartement },
  );
  return response.data;
}

export async function validerDepartementPrevisions(
  idVersion: number,
  idDepartement: number,
): Promise<WorkflowDepartementBulkResult> {
  const response = await apiClient.post<WorkflowDepartementBulkResult>(
    '/api/v1/workflow-previsions-ub/valider-departement',
    { idVersion, idDepartement },
  );
  return response.data;
}

export async function rejeterDepartementPrevisions(
  idVersion: number,
  idDepartement: number,
  motif: string,
): Promise<WorkflowDepartementBulkResult> {
  const response = await apiClient.post<WorkflowDepartementBulkResult>(
    '/api/v1/workflow-previsions-ub/rejeter-departement',
    { idVersion, idDepartement, motif },
  );
  return response.data;
}

export async function reouvrirPrevisionUb(
  idVersion: number,
  idUB: number,
): Promise<WorkflowPrevisionUb> {
  const response = await apiClient.post<WorkflowPrevisionUb>(
    '/api/v1/workflow-previsions-ub/reouvrir',
    { idVersion, idUB },
  );
  return response.data;
}

export async function annulerSoumissionPrevisionUb(
  idVersion: number,
  idUB: number,
): Promise<WorkflowPrevisionUb> {
  const response = await apiClient.post<WorkflowPrevisionUb>(
    '/api/v1/workflow-previsions-ub/annuler-soumission',
    { idVersion, idUB },
  );
  return response.data;
}

export async function fetchUtilisateursLookup(): Promise<UtilisateurLookup[]> {
  const response = await apiClient.get<UtilisateurLookup[]>('/api/v1/utilisateurs');
  return response.data;
}

export interface PerimetreUtilisateur {
  tousDepartements: boolean;
  toutesUnitesBudgetaires: boolean;
  idDepartements: number[];
  idUnitesBudgetaires: number[];
}

export interface PermissionEtat {
  code: string;
  description: string | null;
  heriteeProfil: boolean;
  accordeeIndividuellement: boolean;
  effective: boolean;
}

export interface UtilisateurAdminListItem {
  idUtilisateur: number;
  matricule: string;
  nom: string;
  postnom: string | null;
  prenom: string | null;
  nomComplet: string;
  nomUtilisateur: string;
  email: string | null;
  actif: boolean;
  dateDerniereConnexion: string | null;
  idDepartementPrincipal: number | null;
  codeDepartementPrincipal: string | null;
  libelleDepartementPrincipal: string | null;
  profils: string[];
}

export interface UtilisateurAdminDetail {
  idUtilisateur: number;
  matricule: string;
  nom: string;
  postnom: string | null;
  prenom: string | null;
  nomUtilisateur: string;
  email: string | null;
  actif: boolean;
  dateCreation: string;
  dateDerniereConnexion: string | null;
  idStructureOrganisationnelle: number | null;
  libelleStructure: string | null;
  idDepartementPrincipal: number | null;
  codeDepartementPrincipal: string | null;
  libelleDepartementPrincipal: string | null;
  idStructureService: number | null;
  libelleService: string | null;
  profils: string[];
  permissionsProfils: string[];
  permissionsIndividuelles: string[];
  permissionsEffectives: string[];
  permissionsComplementairesEtat: PermissionEtat[];
  perimetre: PerimetreUtilisateur;
}

export interface CreateUtilisateurAdminPayload {
  matricule: string;
  nom: string;
  postnom?: string | null;
  prenom?: string | null;
  nomUtilisateur: string;
  email?: string | null;
  motDePasseInitial: string;
  actif: boolean;
  idStructureOrganisationnelle?: number | null;
  idDepartementPrincipal?: number | null;
  idStructureService?: number | null;
  profils?: string[];
  permissionsIndividuelles?: string[];
  perimetre?: PerimetreUtilisateur;
}

export interface UpdateUtilisateurAdminPayload {
  matricule: string;
  nom: string;
  postnom?: string | null;
  prenom?: string | null;
  nomUtilisateur: string;
  email?: string | null;
  actif: boolean;
  idStructureOrganisationnelle?: number | null;
  idDepartementPrincipal?: number | null;
  idStructureService?: number | null;
  profils?: string[];
  permissionsIndividuelles?: string[];
  perimetre?: PerimetreUtilisateur;
}

export interface ProfilCatalogueItem {
  code: string;
  libelle: string;
  historique: boolean;
  permissions: string[];
}

export interface PermissionCatalogueItem {
  code: string;
  description: string | null;
  complementaire: boolean;
}

export interface ProfilsCatalogue {
  profils: ProfilCatalogueItem[];
  permissions: PermissionCatalogueItem[];
}

export async function fetchAdminUtilisateurs(): Promise<UtilisateurAdminListItem[]> {
  const response = await apiClient.get<UtilisateurAdminListItem[]>('/api/v1/administration/utilisateurs');
  return response.data;
}

export async function fetchAdminUtilisateur(idUtilisateur: number): Promise<UtilisateurAdminDetail> {
  const response = await apiClient.get<UtilisateurAdminDetail>(
    `/api/v1/administration/utilisateurs/${idUtilisateur}`,
  );
  return response.data;
}

export async function createAdminUtilisateur(
  payload: CreateUtilisateurAdminPayload,
): Promise<UtilisateurAdminDetail> {
  const response = await apiClient.post<UtilisateurAdminDetail>(
    '/api/v1/administration/utilisateurs',
    payload,
  );
  return response.data;
}

export async function updateAdminUtilisateur(
  idUtilisateur: number,
  payload: UpdateUtilisateurAdminPayload,
): Promise<UtilisateurAdminDetail> {
  const response = await apiClient.put<UtilisateurAdminDetail>(
    `/api/v1/administration/utilisateurs/${idUtilisateur}`,
    payload,
  );
  return response.data;
}

export async function setAdminUtilisateurActif(
  idUtilisateur: number,
  actif: boolean,
): Promise<UtilisateurAdminDetail> {
  const response = await apiClient.patch<UtilisateurAdminDetail>(
    `/api/v1/administration/utilisateurs/${idUtilisateur}/actif`,
    { actif },
  );
  return response.data;
}

export async function resetAdminUtilisateurMotDePasse(
  idUtilisateur: number,
  nouveauMotDePasse: string,
): Promise<void> {
  await apiClient.post(`/api/v1/administration/utilisateurs/${idUtilisateur}/reset-mot-de-passe`, {
    nouveauMotDePasse,
  });
}

export async function fetchAdminProfilsCatalogue(): Promise<ProfilsCatalogue> {
  const response = await apiClient.get<ProfilsCatalogue>('/api/v1/administration/profils');
  return response.data;
}

export interface RubriqueBudgetaire {
  idRB: number;
  codeRB: string;
  libelle: string;
  parentId: number | null;
  parentCode: string | null;
  parentLibelle: string | null;
  niveau: number;
  actif: boolean;
  dateCreation: string;
  nombreEnfants: number;
  nombrePrevisions: number;
  idGroupeRB?: number | null;
  codeGroupe?: string | null;
  libelleGroupe?: string | null;
  ordreAffichageGroupe?: number | null;
}

export interface GroupeRubriqueBudgetaire {
  idGroupeRB: number;
  codeGroupe: string;
  libelle: string;
  ordreAffichage: number;
  actif: boolean;
  dateCreation: string;
  nombreRubriques: number;
}

export interface CreateRubriqueBudgetairePayload {
  codeRB: string;
  libelle: string;
  parentId?: number | null;
  actif?: boolean;
}

export interface RubriqueBudgetaireNoeud {
  idRB: number;
  codeRB: string;
  libelle: string;
  niveau: number;
  actif: boolean;
  /** Présent si l’API l’expose ; sinon déduire de enfants.length. */
  estRupture?: boolean;
  enfants: RubriqueBudgetaireNoeud[];
}

export async function fetchRubriquesBudgetaires(): Promise<RubriqueBudgetaire[]> {
  const response = await apiClient.get<RubriqueBudgetaire[]>('/api/v1/rubriques-budgetaires');
  return response.data;
}

export async function fetchGroupesRubriquesBudgetaires(): Promise<GroupeRubriqueBudgetaire[]> {
  const response = await apiClient.get<GroupeRubriqueBudgetaire[]>('/api/v1/groupes-rubriques-budgetaires');
  return response.data;
}

export async function fetchRubriquesBudgetairesArbre(): Promise<RubriqueBudgetaireNoeud[]> {
  const response = await apiClient.get<RubriqueBudgetaireNoeud[]>('/api/v1/rubriques-budgetaires/arbre');
  return response.data;
}

export async function createRubriqueBudgetaire(
  payload: CreateRubriqueBudgetairePayload,
): Promise<RubriqueBudgetaire> {
  const response = await apiClient.post<RubriqueBudgetaire>('/api/v1/rubriques-budgetaires', payload);
  return response.data;
}

export async function updateRubriqueBudgetaire(
  idRB: number,
  payload: CreateRubriqueBudgetairePayload,
): Promise<RubriqueBudgetaire> {
  const response = await apiClient.put<RubriqueBudgetaire>(`/api/v1/rubriques-budgetaires/${idRB}`, payload);
  return response.data;
}

export async function setRubriqueBudgetaireActif(
  idRB: number,
  actif: boolean,
): Promise<RubriqueBudgetaire> {
  const response = await apiClient.put<RubriqueBudgetaire>(`/api/v1/rubriques-budgetaires/${idRB}/actif`, {
    actif,
  });
  return response.data;
}

export async function deleteRubriqueBudgetaire(idRB: number): Promise<void> {
  await apiClient.delete(`/api/v1/rubriques-budgetaires/${idRB}`);
}

export interface ItemBI {
  idItemBI: number;
  codeItem: string;
  libelle: string;
  parentId: number | null;
  parentCode: string | null;
  parentLibelle: string | null;
  niveau: number;
  categorie: string | null;
  actif: boolean;
  dateCreation: string;
  nombreEnfants: number;
  nombrePrevisions: number;
}

export interface CreateItemBIPayload {
  codeItem: string;
  libelle: string;
  parentId?: number | null;
  categorie?: string | null;
  actif?: boolean;
}

export interface ItemBINoeud {
  idItemBI: number;
  codeItem: string;
  libelle: string;
  niveau: number;
  categorie: string | null;
  actif: boolean;
  enfants: ItemBINoeud[];
}

export async function fetchItemsBI(): Promise<ItemBI[]> {
  const response = await apiClient.get<ItemBI[]>('/api/v1/items-bi');
  return response.data;
}

export async function fetchItemsBIArbre(): Promise<ItemBINoeud[]> {
  const response = await apiClient.get<ItemBINoeud[]>('/api/v1/items-bi/arbre');
  return response.data;
}

export async function createItemBI(payload: CreateItemBIPayload): Promise<ItemBI> {
  const response = await apiClient.post<ItemBI>('/api/v1/items-bi', payload);
  return response.data;
}

export async function updateItemBI(idItemBI: number, payload: CreateItemBIPayload): Promise<ItemBI> {
  const response = await apiClient.put<ItemBI>(`/api/v1/items-bi/${idItemBI}`, payload);
  return response.data;
}

export async function setItemBIActif(idItemBI: number, actif: boolean): Promise<ItemBI> {
  const response = await apiClient.put<ItemBI>(`/api/v1/items-bi/${idItemBI}/actif`, { actif });
  return response.data;
}

export async function deleteItemBI(idItemBI: number): Promise<void> {
  await apiClient.delete(`/api/v1/items-bi/${idItemBI}`);
}

export interface RepartitionMensuelle {
  mois: number;
  montant: number;
}

export interface PrevisionGrilleLigne {
  idPrevision: number | null;
  idRB: number | null;
  codeRB: string | null;
  libelleRB: string | null;
  parentIdRB: number | null;
  niveauRB: number | null;
  estSection: boolean;
  detailBI: string | null;
  montantAnnuel: number;
  cumulMensuel: number;
  repartitions: RepartitionMensuelle[];
  /** Groupe métier niveau 1 (DC/AE) — clé de regroupement, pas codeGroupe. */
  idGroupeRB?: number | null;
  codeGroupe?: string | null;
  libelleGroupe?: string | null;
  ordreAffichageGroupe?: number | null;
}

export interface PrevisionGrille {
  idVersion: number;
  statutVersion: string;
  modificationAutorisee: boolean;
  idTypeBudget: number;
  codeType: string;
  idModePrevision: number;
  codeMode: string;
  idUB: number;
  codeUB: string;
  idItemBI: number | null;
  libelleItemAE: string | null;
  idGroupeItemAE: number | null;
  lignes: PrevisionGrilleLigne[];
}

export interface PrevisionGrillePage extends PrevisionGrille {
  page: number;
  pageSize: number;
  totalCount: number;
  hasNextPage: boolean;
}

export interface PrevisionResumeCategorie {
  codeType: string;
  libelleType: string;
  montantTotal: number;
  nombreLignes: number;
}

export interface PrevisionBudgetaire {
  idPrevision: number;
  idVersion: number;
  idTypeBudget: number;
  codeType: string;
  idModePrevision: number;
  codeMode: string;
  idUB: number;
  libelleItemAE: string | null;
  idItemBI: number | null;
  idGroupeItemAE: number | null;
  dateCreation: string;
  dateModification: string | null;
}

export interface GroupeItemAE {
  idGroupeItemAE: number;
  libelle: string;
  actif: boolean;
  dateCreation: string;
  nombrePrevisions: number;
}

export interface PrevisionLigneSauvegarde {
  idPrevision?: number | null;
  idRB?: number | null;
  detailBI?: string | null;
  montantAnnuel?: number | null;
  supprimer?: boolean;
  repartitions?: RepartitionMensuelle[];
}

export interface SauvegarderGrillePrevisionPayload {
  idVersion: number;
  idTypeBudget: number;
  idModePrevision: number;
  idUB: number;
  idItemBI?: number | null;
  idGroupeItemAE?: number | null;
  libelleItemAE?: string | null;
  /** Conservé pour compatibilité API ; écrasé côté serveur par l'utilisateur JWT. */
  idUtilisateur?: number;
  lignes: PrevisionLigneSauvegarde[];
}

export async function fetchPrevisionGrille(params: {
  idVersion: number;
  idTypeBudget: number;
  idModePrevision: number;
  idUB: number;
  libelleItemAE?: string;
  idGroupeItemAE?: number | null;
  idItemBI?: number | null;
}): Promise<PrevisionGrille> {
  const response = await apiClient.get<PrevisionGrille>('/api/v1/previsions-budgetaires/grille', { params });
  return response.data;
}

export async function fetchPrevisionGrillePage(params: {
  idVersion: number;
  idTypeBudget: number;
  idModePrevision: number;
  idUB: number;
  libelleItemAE?: string;
  idGroupeItemAE?: number | null;
  idItemBI?: number | null;
  page?: number;
  pageSize?: number;
  search?: string;
  filtre?: string;
  /** Filtre affichage par Groupe N1 (id, pas code). */
  idGroupeRB?: number | null;
}): Promise<PrevisionGrillePage> {
  const response = await apiClient.get<PrevisionGrillePage>('/api/v1/previsions-budgetaires/grille-page', {
    params,
  });
  return response.data;
}

export async function fetchPrevisionResume(idVersion: number): Promise<PrevisionResumeCategorie[]> {
  const response = await apiClient.get<PrevisionResumeCategorie[]>('/api/v1/previsions-budgetaires/resume', {
    params: { idVersion },
  });
  return response.data;
}

export async function fetchPrevisionsBudgetaires(params: {
  idVersion?: number;
  idTypeBudget?: number;
  idUB?: number;
  idModePrevision?: number;
  libelleItemAE?: string;
  idItemBI?: number;
}): Promise<PrevisionBudgetaire[]> {
  const response = await apiClient.get<PrevisionBudgetaire[]>('/api/v1/previsions-budgetaires', { params });
  return response.data;
}

/** Actions d'exploitation déjà utilisées (distinctes) — combo multi-UB. */
export async function fetchActionsExploitation(params: {
  idExercice?: number;
  idVersion?: number;
}): Promise<string[]> {
  const response = await apiClient.get<string[]>('/api/v1/previsions-budgetaires/actions-ae', { params });
  return response.data;
}

export async function sauvegarderGrillePrevision(
  payload: SauvegarderGrillePrevisionPayload,
): Promise<{ lignesCreees: number; lignesModifiees: number; lignesSupprimees: number }> {
  const response = await apiClient.post('/api/v1/previsions-budgetaires/sauvegarder-grille', payload);
  return response.data;
}

export async function fetchGroupesItemAE(): Promise<GroupeItemAE[]> {
  const response = await apiClient.get<GroupeItemAE[]>('/api/v1/groupes-item-ae');
  return response.data;
}

export async function createGroupeItemAE(libelle: string): Promise<GroupeItemAE> {
  const response = await apiClient.post<GroupeItemAE>('/api/v1/groupes-item-ae', { libelle, actif: true });
  return response.data;
}

/* —— Classement AE (ordre Version × UB) —— */

export interface ClassementAeLigne {
  idClassementAE: number;
  typeLigne: 'GROUPE' | 'ITEM' | string;
  idGroupeItemAE: number | null;
  libelleGroupe: string | null;
  libelleItemAE: string | null;
  idGroupeDeduit: number | null;
  libelleGroupeDeduit: string | null;
  ordreAffichage: number;
}

export interface ClassementAeInitResult {
  couplesTraites: number;
  lignesCreees: number;
  couplesDejaInitialises: number;
}

export async function fetchClassementAe(idVersion: number, idUB: number): Promise<ClassementAeLigne[]> {
  const response = await apiClient.get<ClassementAeLigne[]>('/api/v1/classements-ae', {
    params: { idVersion, idUB },
  });
  return response.data;
}

export async function reorderClassementAe(payload: {
  idVersion: number;
  idUB: number;
  idsClassementOrdonnes: number[];
}): Promise<ClassementAeLigne[]> {
  const response = await apiClient.put<ClassementAeLigne[]>('/api/v1/classements-ae/reorder', payload);
  return response.data;
}

export async function initialiserClassementAe(): Promise<ClassementAeInitResult> {
  const response = await apiClient.post<ClassementAeInitResult>('/api/v1/classements-ae/initialiser');
  return response.data;
}

/* —— Suivi prévisions / soumissions (Version×UB, agrégats dynamiques) —— */

export interface SuiviPrevisionCompteurs {
  brouillons: number;
  soumises: number;
  controlees: number;
  validees: number;
  rejetees: number;
}

export interface SuiviPrevisionUbResume {
  idVersion: number;
  numeroVersion: number;
  libelleVersion: string;
  statut: string;
  idExercice: number;
  annee: number;
  idUB: number;
  codeUB: string;
  libelleUB: string;
  idDepartement: number;
  codeDepartement: string;
  libelleDepartement: string;
  montantDC: number;
  montantAE: number;
  montantBI: number;
  montantTotal: number;
  dateDerniereModification: string;
  dateSoumission: string | null;
  nomUtilisateurSoumission: string | null;
  dateRejet: string | null;
  nomUtilisateurRejet: string | null;
  motifRejet: string | null;
}

export interface SuiviPrevisionListe {
  compteurs: SuiviPrevisionCompteurs;
  lignes: SuiviPrevisionUbResume[];
}

export interface SuiviUbDetail {
  idVersion: number;
  numeroVersion: number;
  libelleVersion: string;
  statut: string;
  idExercice: number;
  annee: number;
  idUB: number;
  codeUB: string;
  libelleUB: string;
  idDepartement: number;
  codeDepartement: string;
  libelleDepartement: string;
  montantDC: number;
  montantAE: number;
  montantBI: number;
  montantTotal: number;
  dateSoumission: string | null;
  nomUtilisateurSoumission: string | null;
  dateControle: string | null;
  nomUtilisateurControle: string | null;
  dateValidation: string | null;
  nomUtilisateurValidation: string | null;
  dateRejet: string | null;
  nomUtilisateurRejet: string | null;
  motifRejet: string | null;
  peutControler: boolean;
  peutValider: boolean;
  peutRejeter: boolean;
}

export interface SuiviUbLigneDc {
  estSection: boolean;
  idGroupeRB: number | null;
  codeGroupe: string | null;
  libelleGroupe: string | null;
  ordreAffichageGroupe: number | null;
  idRB: number | null;
  codeRB: string | null;
  libelle: string;
  montantAnnuel: number;
  repartitionDefinie: boolean;
  montantsMensuels: number[] | null;
}

export interface SuiviUbLigneAe {
  libelleItemAE: string;
  libelleGroupeAE: string | null;
  idRB: number | null;
  codeRB: string | null;
  libelleRB: string | null;
  montantAnnuel: number;
  codeMode: string;
  repartitionDefinie: boolean;
  montantsMensuels: number[] | null;
}

export interface SuiviUbLigneBi {
  idItemBI: number;
  codeItem: string;
  libelleItem: string;
  detailBI: string | null;
  montantAnnuel: number;
  codeMode: string;
  repartitionDefinie: boolean;
  montantsMensuels: number[] | null;
}

export interface SuiviUbDetailLignes {
  codeType: string;
  lignesDC: SuiviUbLigneDc[] | null;
  lignesAE: SuiviUbLigneAe[] | null;
  lignesBI: SuiviUbLigneBi[] | null;
}

export async function fetchMesPrevisions(params: {
  idExercice?: number;
  idVersion?: number;
  idDepartement?: number;
  statut?: string;
  searchUb?: string;
}): Promise<SuiviPrevisionListe> {
  const response = await apiClient.get<SuiviPrevisionListe>('/api/v1/suivi-previsions/mes-previsions', { params });
  return response.data;
}

export async function fetchSoumissionsBudgetaires(params: {
  idExercice?: number;
  idVersion?: number;
  idDepartement?: number;
  statut?: string;
  searchUb?: string;
}): Promise<SuiviPrevisionListe> {
  const response = await apiClient.get<SuiviPrevisionListe>('/api/v1/suivi-previsions/soumissions', { params });
  return response.data;
}

export async function fetchSuiviUbDetail(idVersion: number, idUB: number): Promise<SuiviUbDetail> {
  const response = await apiClient.get<SuiviUbDetail>('/api/v1/suivi-previsions/ub-detail', {
    params: { idVersion, idUB },
  });
  return response.data;
}

export async function fetchSuiviUbLignes(
  idVersion: number,
  idUB: number,
  codeType: 'DC' | 'AE' | 'BI',
): Promise<SuiviUbDetailLignes> {
  const response = await apiClient.get<SuiviUbDetailLignes>('/api/v1/suivi-previsions/ub-detail/lignes', {
    params: { idVersion, idUB, codeType },
  });
  return response.data;
}

export interface HistoriquePrevisionEvenement {
  idAudit: number;
  dateHeure: string;
  idExercice: number;
  anneeExercice: number;
  idVersion: number;
  numeroVersion: number;
  libelleVersion: string | null;
  idDepartement: number;
  codeDepartement: string;
  libelleDepartement: string;
  idUB: number;
  codeUB: string;
  libelleUB: string;
  typePrevision: string | null;
  action: string;
  actionLibelle: string;
  portee: string;
  ancienStatut: string | null;
  nouveauStatut: string | null;
  idUtilisateur: number;
  nomUtilisateur: string;
  motif: string | null;
  montantDC: number;
  montantAE: number;
  montantBI: number;
  montantTotal: number;
  statutActuel: string | null;
}

export interface HistoriquePrevisionPage {
  items: HistoriquePrevisionEvenement[];
  page: number;
  pageSize: number;
  totalCount: number;
  peutVoirToutes: boolean;
}

export interface HistoriquePrevisionTimeline {
  idVersion: number;
  numeroVersion: number;
  libelleVersion: string | null;
  anneeExercice: number;
  idDepartement: number;
  codeDepartement: string;
  libelleDepartement: string;
  idUB: number;
  codeUB: string;
  libelleUB: string;
  statutActuel: string;
  montantDC: number;
  montantAE: number;
  montantBI: number;
  montantTotal: number;
  evenements: HistoriquePrevisionEvenement[];
}

export async function fetchHistoriquePrevisions(params: {
  exerciseId?: number;
  versionId?: number;
  departementId?: number;
  ubId?: number;
  type?: string;
  action?: string;
  statut?: string;
  dateDebut?: string;
  dateFin?: string;
  search?: string;
  monHistorique?: boolean;
  page?: number;
  pageSize?: number;
}): Promise<HistoriquePrevisionPage> {
  const response = await apiClient.get<HistoriquePrevisionPage>('/api/v1/historique-previsions', { params });
  return response.data;
}

export async function fetchHistoriquePrevisionsTimeline(
  idVersion: number,
  idUB: number,
): Promise<HistoriquePrevisionTimeline> {
  const response = await apiClient.get<HistoriquePrevisionTimeline>('/api/v1/historique-previsions/timeline', {
    params: { idVersion, idUB },
  });
  return response.data;
}


export interface SnelComptesCompteurs {
  totalDetecte: number;
  aCreer: number;
  dejaExistant: number;
  enConflit: number;
  anomalies: number;
}

export interface SnelComptesAnomalie {
  severite: string;
  code: string;
  message: string;
  numeroLigne: number | null;
  codeElement: string | null;
}

export interface SnelComptesLigneIgnoree {
  numeroLigne: number;
  code: string;
  libelle: string;
  motif: string;
}

export interface SnelComptesNoeudPreview {
  code: string;
  libelle: string;
  niveau: number;
  categorie: string | null;
  statut: string;
  enfants: SnelComptesNoeudPreview[];
}

export interface SnelComptesPreview {
  fichierSource: string;
  feuille: string;
  rubriques: SnelComptesCompteurs;
  itemsBI: SnelComptesCompteurs;
  rubriquesDetail: unknown[];
  itemsBIDetail: unknown[];
  arbreRubriques: SnelComptesNoeudPreview[];
  arbreItemsBI: SnelComptesNoeudPreview[];
  lignesIgnorees: SnelComptesLigneIgnoree[];
  anomalies: SnelComptesAnomalie[];
  peutImporter: boolean;
  message: string;
}

export interface SnelComptesExecuteResult {
  succes: boolean;
  message: string;
  rubriquesInserees: number;
  itemsBIInserees: number;
  lignesIgnorees: number;
  conflits: number;
  anomalies: number;
  arbreRubriques: SnelComptesNoeudPreview[];
  arbreItemsBI: SnelComptesNoeudPreview[];
}

export async function previewImportSnelComptes(filePath?: string): Promise<SnelComptesPreview> {
  const response = await apiClient.post<SnelComptesPreview>('/api/v1/import/snel-comptes/preview', null, {
    params: filePath ? { filePath } : undefined,
  });
  return response.data;
}

export async function executeImportSnelComptes(
  confirm: boolean,
  filePath?: string,
): Promise<SnelComptesExecuteResult> {
  try {
    const response = await apiClient.post<SnelComptesExecuteResult>('/api/v1/import/snel-comptes/execute', {
      confirm,
      fichierSource: filePath ?? null,
    });
    return response.data;
  } catch (err) {
    if (axios.isAxiosError(err) && err.response?.data && typeof err.response.data === 'object') {
      return err.response.data as SnelComptesExecuteResult;
    }
    throw err;
  }
}

/* ——— Rapports prévisions DC ——— */

export type RapportDcNiveau = 'ENTITE' | 'DEPARTEMENT' | 'UB';
export type RapportDcLayout = 'ANNUEL' | 'MENSUEL' | 'MIXTE';

export interface RapportDcLigneRb {
  codeRB: string;
  libelle: string;
  codeMode: string;
  montantAnnuel: number;
  m01?: number | null;
  m02?: number | null;
  m03?: number | null;
  m04?: number | null;
  m05?: number | null;
  m06?: number | null;
  m07?: number | null;
  m08?: number | null;
  m09?: number | null;
  m10?: number | null;
  m11?: number | null;
  m12?: number | null;
}

export interface RapportDcGroupe {
  idGroupeRB: number | null;
  codeGroupe: string;
  libelle: string;
  ordreAffichage: number;
  sousTotalDc: number;
  lignes: RapportDcLigneRb[];
}

export interface RapportDcUbIdentite {
  idEntite?: number | null;
  codeEntite?: string | null;
  libelleEntite?: string | null;
  idDepartementStructure?: number | null;
  codeDepartementStructure?: string | null;
  libelleDepartementStructure?: string | null;
  codeDepartementTable: string;
  libelleDepartementTable: string;
  idDivision?: number | null;
  codeDivision?: string | null;
  libelleDivision?: string | null;
  idUB: number;
  codeUB: string;
  libelleUB: string;
  typeBudget: string;
  statut: string;
  devise: string;
  annee: number;
  numeroVersion: number;
  libelleVersion?: string | null;
}

export interface RapportDcUbBloc {
  identite: RapportDcUbIdentite;
  totalDc: number;
  groupes: RapportDcGroupe[];
}

export interface RapportDcDto {
  enTete: {
    titre: string;
    annee: number;
    idVersion: number;
    numeroVersion: number;
    libelleVersion?: string | null;
    niveau: string;
    idEntite?: number | null;
    codeEntite?: string | null;
    libelleEntite?: string | null;
    idDepartementStructure?: number | null;
    codeDepartementStructure?: string | null;
    libelleDepartementStructure?: string | null;
    idDivision?: number | null;
    codeDivision?: string | null;
    libelleDivision?: string | null;
    nbUB: number;
    montantTotalDc: number;
    devise: string;
    statutFiltre: string;
    dateImpression: string;
    referenceDocument: string;
  };
  layoutColonnes: RapportDcLayout;
  blocsUb: RapportDcUbBloc[];
  groupesConsolides: RapportDcConsolideGroupe[];
}

export interface RapportDcQueryParams {
  idVersion: number;
  niveau: RapportDcNiveau;
  idEntite: number;
  idDepartementStructure?: number;
  idDivision?: number;
  idUB?: number;
  statutConsultation?: string;
}

export async function fetchRapportPrevisionsDc(params: RapportDcQueryParams): Promise<RapportDcDto> {
  const response = await apiClient.get<RapportDcDto>('/api/v1/rapports/previsions-dc', { params });
  return response.data;
}

export async function downloadRapportPrevisionsDcPdf(params: RapportDcQueryParams): Promise<Blob> {
  const response = await apiClient.get('/api/v1/rapports/previsions-dc/pdf', {
    params,
    responseType: 'blob',
  });
  return response.data as Blob;
}

/* ——— Rapports prévisions DC consolidées ——— */

export interface RapportDcConsolideLigne {
  idRB: number;
  codeRB: string;
  libelle: string;
  codeMode: string;
  montantCumul: number;
  m01?: number | null;
  m02?: number | null;
  m03?: number | null;
  m04?: number | null;
  m05?: number | null;
  m06?: number | null;
  m07?: number | null;
  m08?: number | null;
  m09?: number | null;
  m10?: number | null;
  m11?: number | null;
  m12?: number | null;
}

export interface RapportDcConsolideGroupe {
  idGroupeRB: number | null;
  codeGroupe: string;
  libelle: string;
  ordreAffichage: number;
  sousTotalDc: number;
  lignes: RapportDcConsolideLigne[];
}

export interface RapportDcConsolideDto {
  enTete: {
    titre: string;
    annee: number;
    idVersion: number;
    numeroVersion: number;
    libelleVersion?: string | null;
    idEntite?: number | null;
    codeEntite?: string | null;
    libelleEntite?: string | null;
    idDepartementStructure?: number | null;
    codeDepartementStructure?: string | null;
    libelleDepartementStructure?: string | null;
    idDivision?: number | null;
    codeDivision?: string | null;
    libelleDivision?: string | null;
    nbUB: number;
    montantTotalDc: number;
    devise: string;
    statutFiltre: string;
    dateImpression: string;
    referenceDocument: string;
  };
  groupes: RapportDcConsolideGroupe[];
}

export interface RapportDcConsolideQueryParams {
  idVersion: number;
  idEntite?: number;
  idDepartementStructure?: number;
  idDivision?: number;
  statutConsultation?: string;
}

export async function fetchRapportPrevisionsDcConsolide(
  params: RapportDcConsolideQueryParams,
): Promise<RapportDcConsolideDto> {
  const response = await apiClient.get<RapportDcConsolideDto>('/api/v1/rapports/previsions-dc-consolide', {
    params,
  });
  return response.data;
}

export async function downloadRapportPrevisionsDcConsolidePdf(
  params: RapportDcConsolideQueryParams,
): Promise<Blob> {
  const response = await apiClient.get('/api/v1/rapports/previsions-dc-consolide/pdf', {
    params,
    responseType: 'blob',
  });
  return response.data as Blob;
}

export type RapportAeNiveau = 'ENTITE' | 'DEPARTEMENT' | 'UB';

export interface RapportAeColonneRb {
  idRB: number;
  codeRB: string;
  libelleRB: string;
  idGroupeRB?: number | null;
  ordreAffichage: number;
}

export interface RapportAeLigneDetail {
  typeLigne: 'GROUPE' | 'ITEM';
  ordreAffichage: number;
  idGroupeItemAE?: number | null;
  libelleGroupe?: string | null;
  numero?: string | null;
  libelleItemAE?: string | null;
  montantsParRb: Array<number | null>;
  total?: number | null;
}

export interface RapportAeDetailMensuel {
  mois: number;
  libelleMois: string;
  colonnesRb: RapportAeColonneRb[];
  lignes: RapportAeLigneDetail[];
  totauxParRb: number[];
  totalGeneral: number;
}

export interface RapportAeUbIdentite {
  idEntite?: number | null;
  codeEntite?: string | null;
  libelleEntite?: string | null;
  idDepartementStructure?: number | null;
  codeDepartementStructure?: string | null;
  libelleDepartementStructure?: string | null;
  codeDepartementTable: string;
  libelleDepartementTable: string;
  idDivision?: number | null;
  codeDivision?: string | null;
  libelleDivision?: string | null;
  idUB: number;
  codeUB: string;
  libelleUB: string;
  typeBudget: string;
  statut: string;
  devise: string;
  annee: number;
  numeroVersion: number;
  libelleVersion?: string | null;
}

export interface RapportAeUbBloc {
  identite: RapportAeUbIdentite;
  detailsMensuels: RapportAeDetailMensuel[];
}

export interface RapportAeSyntheseItemLigne {
  typeLigne: 'GROUPE' | 'ITEM';
  numero?: string | null;
  libelleGroupe?: string | null;
  libelleItemAE?: string | null;
  m01?: number | null;
  m02?: number | null;
  m03?: number | null;
  m04?: number | null;
  m05?: number | null;
  m06?: number | null;
  m07?: number | null;
  m08?: number | null;
  m09?: number | null;
  m10?: number | null;
  m11?: number | null;
  m12?: number | null;
  total?: number | null;
}

export interface RapportAeSyntheseRbLigne {
  codeRB: string;
  libelle: string;
  codeMode: string;
  montantCumul: number;
  m01?: number | null;
  m02?: number | null;
  m03?: number | null;
  m04?: number | null;
  m05?: number | null;
  m06?: number | null;
  m07?: number | null;
  m08?: number | null;
  m09?: number | null;
  m10?: number | null;
  m11?: number | null;
  m12?: number | null;
}

export interface RapportAeSyntheseRbGroupe {
  codeGroupe: string;
  libelle: string;
  sousTotal: number;
  lignes: RapportAeSyntheseRbLigne[];
}

export interface RapportAeDto {
  enTete: {
    titre: string;
    annee: number;
    idVersion: number;
    numeroVersion: number;
    libelleVersion?: string | null;
    niveau: string;
    modeMois: string;
    libelleMois?: string | null;
    codeEntite?: string | null;
    libelleEntite?: string | null;
    codeDepartementStructure?: string | null;
    libelleDepartementStructure?: string | null;
    codeDivision?: string | null;
    libelleDivision?: string | null;
    nbUB: number;
    montantTotalAe: number;
    devise: string;
    statutFiltre: string;
    dateImpression: string;
    referenceDocument: string;
  };
  estTousLesMois: boolean;
  blocsUb: RapportAeUbBloc[];
  syntheseItem?: {
    titre: string;
    lignes: RapportAeSyntheseItemLigne[];
    totalGeneral: number;
  } | null;
  syntheseRb?: {
    titre: string;
    groupes: RapportAeSyntheseRbGroupe[];
    totalGeneral: number;
  } | null;
}

export interface RapportAeQueryParams {
  idVersion: number;
  niveau: RapportAeNiveau;
  mois: string;
  idEntite: number;
  idDepartementStructure?: number;
  idDivision?: number;
  idUB?: number;
  statutConsultation?: string;
}

export async function fetchRapportPrevisionsAe(params: RapportAeQueryParams): Promise<RapportAeDto> {
  const response = await apiClient.get<RapportAeDto>('/api/v1/rapports/previsions-ae', { params });
  return response.data;
}

export async function downloadRapportPrevisionsAePdf(params: RapportAeQueryParams): Promise<Blob> {
  const response = await apiClient.get('/api/v1/rapports/previsions-ae/pdf', {
    params,
    responseType: 'blob',
  });
  return response.data as Blob;
}

/* ——— Rapports prévisions BI ——— */

export type RapportBiNiveau = 'ENTITE' | 'DEPARTEMENT' | 'UB';

export interface RapportBiLigne {
  typeLigne: 'CATEGORIE' | 'ITEM' | 'DETAIL' | 'TOTAL_UB';
  niveauHierarchique: number;
  idItemBI?: number | null;
  codeItem?: string | null;
  libelle: string;
  codeAffichage?: string | null;
  detailBI?: string | null;
  codeMode?: string | null;
  estAgrege: boolean;
  montantCumul: number;
  m01?: number | null;
  m02?: number | null;
  m03?: number | null;
  m04?: number | null;
  m05?: number | null;
  m06?: number | null;
  m07?: number | null;
  m08?: number | null;
  m09?: number | null;
  m10?: number | null;
  m11?: number | null;
  m12?: number | null;
  idPrevision?: number | null;
}

export interface RapportBiUbBloc {
  identite: {
    idUB: number;
    codeUB: string;
    libelleUB: string;
    codeDepartementStructure?: string | null;
    libelleDepartementStructure?: string | null;
  };
  layoutColonnes: string;
  lignes: RapportBiLigne[];
  totalUb: number;
}

export interface RapportBiDepartementBloc {
  idDepartementStructure?: number | null;
  codeDepartement: string;
  libelleDepartement: string;
  ordre: number;
  ubs: RapportBiUbBloc[];
  totalDepartement: number;
}

export interface RapportBiSyntheseLigne {
  typeLigne: string;
  codeAffichage?: string | null;
  libelle: string;
  idItemBI?: number | null;
  montantsParDepartement: number[];
  totalLigne: number;
}

export interface RapportBiDto {
  enTete: {
    titre: string;
    titreSynthese?: string | null;
    annee: number;
    idVersion: number;
    numeroVersion: number;
    libelleVersion?: string | null;
    niveau: string;
    codeEntite?: string | null;
    libelleEntite?: string | null;
    nbUB: number;
    nbDepartements: number;
    montantTotalBi: number;
    devise: string;
    statutFiltre: string;
    referenceDocument: string;
  };
  layoutColonnes: string;
  departements: RapportBiDepartementBloc[];
  syntheseEntite?: {
    titre: string;
    colonnesDepartement: { idDepartementStructure?: number | null; codeDepartement: string; libelleDepartement: string }[];
    lignes: RapportBiSyntheseLigne[];
    totauxParDepartement: number[];
    totalGeneral: number;
  } | null;
}

export interface RapportBiQueryParams {
  idVersion: number;
  niveau: RapportBiNiveau;
  idEntite: number;
  idDepartementStructure?: number;
  idDivision?: number;
  idUB?: number;
  statutConsultation?: string;
}

export async function fetchRapportPrevisionsBi(params: RapportBiQueryParams): Promise<RapportBiDto> {
  const response = await apiClient.get<RapportBiDto>('/api/v1/rapports/previsions-bi', { params });
  return response.data;
}

export async function downloadRapportPrevisionsBiPdf(params: RapportBiQueryParams): Promise<Blob> {
  const response = await apiClient.get('/api/v1/rapports/previsions-bi/pdf', {
    params,
    responseType: 'blob',
  });
  return response.data as Blob;
}

/* ─── Ajustements budgétaires DG ─────────────────────────────────────────── */

export interface AjustementBudgetaire {
  idAjustement: number;
  reference: string;
  idPrevision: number;
  idVersion: number;
  numeroVersion: number;
  libelleVersion?: string | null;
  idExercice: number;
  annee: number;
  idUB: number;
  codeUB: string;
  libelleUB: string;
  typeBudget: string;
  libelleLigne: string;
  montantAncien: number;
  montantNouveau: number;
  variation: number;
  motif: string;
  statut: string;
  idUtilisateurCreation: number;
  nomUtilisateurCreation: string;
  dateCreation: string;
  idUtilisateurValidation?: number | null;
  nomUtilisateurValidation?: string | null;
  dateValidation?: string | null;
}

export interface AjustementHistoriqueLigne {
  idPrevision: number;
  libelleLigne: string;
  montantInitial: number;
  montantActuel: number;
  ajustements: AjustementBudgetaire[];
}

export interface AjustementLigneCandidate {
  idPrevision: number;
  idVersion: number;
  numeroVersion: number;
  libelleVersion?: string | null;
  idExercice: number;
  annee: number;
  idDepartement: number;
  codeDepartement: string;
  libelleDepartement: string;
  idUB: number;
  codeUB: string;
  libelleUB: string;
  typeBudget: string;
  libelleLigne: string;
  montantInitial: number;
  montantActuel: number;
  codeMode: string;
  nbAjustementsValides: number;
  hasBrouillon: boolean;
  estExerciceCourant: boolean;
}

export async function fetchAjustementsBudgetaires(params?: {
  idExercice?: number;
  idVersion?: number;
  idUB?: number;
  statut?: string;
}): Promise<AjustementBudgetaire[]> {
  const response = await apiClient.get<AjustementBudgetaire[]>('/api/v1/ajustements-budgetaires', { params });
  return response.data;
}

export async function fetchAjustementBudgetaire(id: number): Promise<AjustementBudgetaire> {
  const response = await apiClient.get<AjustementBudgetaire>(`/api/v1/ajustements-budgetaires/${id}`);
  return response.data;
}

export async function fetchAjustementHistoriqueLigne(idPrevision: number): Promise<AjustementHistoriqueLigne> {
  const response = await apiClient.get<AjustementHistoriqueLigne>(
    '/api/v1/ajustements-budgetaires/historique-ligne',
    { params: { idPrevision } },
  );
  return response.data;
}

export async function fetchAjustementLignesDisponibles(params?: {
  idExercice?: number;
  idVersion?: number;
  idUB?: number;
}): Promise<AjustementLigneCandidate[]> {
  const response = await apiClient.get<AjustementLigneCandidate[]>(
    '/api/v1/ajustements-budgetaires/lignes-disponibles',
    { params },
  );
  return response.data;
}

export async function fetchAjustementLignesValidees(
  idVersion: number,
  idUB: number,
): Promise<AjustementLigneCandidate[]> {
  const response = await apiClient.get<AjustementLigneCandidate[]>(
    '/api/v1/ajustements-budgetaires/lignes-validees',
    { params: { idVersion, idUB } },
  );
  return response.data;
}

export async function createAjustementBudgetaire(body: {
  idPrevision: number;
  montantNouveau: number;
  motif: string;
}): Promise<AjustementBudgetaire> {
  const response = await apiClient.post<AjustementBudgetaire>('/api/v1/ajustements-budgetaires', body);
  return response.data;
}

export async function updateAjustementBudgetaire(
  id: number,
  body: { montantNouveau: number; motif: string },
): Promise<AjustementBudgetaire> {
  const response = await apiClient.put<AjustementBudgetaire>(`/api/v1/ajustements-budgetaires/${id}`, body);
  return response.data;
}

export async function validerAjustementBudgetaire(id: number): Promise<AjustementBudgetaire> {
  const response = await apiClient.post<AjustementBudgetaire>(`/api/v1/ajustements-budgetaires/${id}/valider`);
  return response.data;
}

export async function annulerAjustementBudgetaire(id: number): Promise<AjustementBudgetaire> {
  const response = await apiClient.post<AjustementBudgetaire>(`/api/v1/ajustements-budgetaires/${id}/annuler`);
  return response.data;
}



/* ========== Demande de paiement (DPM) ========== */

export type StatutDemandePaiement =
  | 'BROUILLON'
  | 'EN_VALIDATION_N1'
  | 'EN_VALIDATION_N2'
  | 'VALIDEE_ENTITE'
  | 'SOUMISE'
  | 'EN_TRAITEMENT_DPM'
  | 'RECEPTIONNEE_BUDGETS'
  | 'EN_CONTROLE_BUDGETAIRE'
  | 'A_CORRIGER'
  | 'VISEE_BUDGETAIREMENT'
  | string;

export interface ValidationEntiteDto {
  niveau: number;
  ordre: number;
  statut: string;
  modeValidation: string | null;
  idUtilisateurValidateur: number | null;
  nomUtilisateurValidateur: string | null;
  idUtilisateurDeclarant: number | null;
  nomUtilisateurDeclarant: string | null;
  nomSignatairePhysique: string | null;
  fonctionSignatairePhysique: string | null;
  dateSignaturePhysique: string | null;
  dateValidation: string | null;
  commentaire: string | null;
}

export interface DemandePaiementListItem {
  idDemandePaiement: number;
  reference: string;
  dateEmission: string;
  idExercice: number;
  anneeExercice: number;
  idUB: number;
  codeUB: string;
  libelleUB: string;
  idDemandeur: number | null;
  codeDemandeur: string | null;
  libelleDemandeur: string | null;
  idDepartement: number | null;
  codeDepartement: string | null;
  libelleDepartement: string | null;
  idCasDossier: number;
  codeCasDossier: string;
  libelleCasDossier: string;
  objet: string;
  montantBrut: number;
  devise: string;
  idDevise?: number | null;
  montantUsd: number | null;
  idTypeBudget?: number | null;
  codeTypeBudget?: string | null;
  statut: StatutDemandePaiement;
  dateSoumission: string | null;
  dateCreation: string;
  idUtilisateurAssigne: number | null;
  nomUtilisateurAssigne: string | null;
  prenomUtilisateurAssigne: string | null;
  modePaiementSollicite?: string | null;
  typeInstrumentPaiement?: string | null;
}

/** Compteurs DPM par statut (GET /demandes-paiement/compteurs). Total explicite côté API. */
export interface DemandePaiementCompteursDto {
  total: number;
  brouillon: number;
  enValidationN1: number;
  enValidationN2: number;
  valideeEntite: number;
  soumise: number;
  enTraitementDpm: number;
  enControleBudgetaire: number;
  aCorriger: number;
  viseeBudgetairement: number;
}

export type DemandePaiementCompteursScope =
  | 'mes-demandes'
  | 'charge-dpm'
  | 'budget'
  | 'junior-dc'
  | 'junior-ae'
  | 'junior-bi';

export interface BeneficiaireDto {
  idBeneficiaire: number;
  typeBeneficiaire: string;
  nomComplet: string;
  matricule: string | null;
  fonction: string | null;
  raisonSociale: string | null;
  rccm: string | null;
  adresse: string | null;
  banque: string | null;
  numeroCompte: string | null;
  estPrincipal: boolean;
  ordre: number;
}

export interface DemandePaiementImputation {
  idImputation: number;
  ordre: number;
  idTypeBudget: number;
  codeTypeBudget: string;
  idUB: number;
  idExercice: number;
  idRubriqueBudgetaire: number | null;
  mois: number | null;
  libelleItemAE: string | null;
  idGroupeItemAE: number | null;
  idItemBI: number | null;
  detailBI: string | null;
  idBudgetLigne: number | null;
  montantBrut: number;
  devise: string;
  tauxConversion: number;
  montantUsd: number;
  numeroFicheSuivi: number | null;
}

export interface DemandePaiementPiece {
  idPieceJointe: number;
  idPieceObligatoire: number | null;
  codeTypePiece: string;
  libelle: string;
  estObligatoire: boolean;
  nomFichierOriginal: string;
  cheminRelatif: string;
  hashSha256: string;
  tailleOctets: number;
  dateUpload: string;
}

export interface DemandePaiementDetail {
  idDemandePaiement: number;
  reference: string;
  dateEmission: string;
  lieuEmission: string | null;
  idExercice: number;
  anneeExercice: number;
  idVersion: number | null;
  numeroVersion: number | null;
  idUB: number;
  codeUB: string;
  libelleUB: string;
  idDemandeur: number | null;
  codeDemandeur: string | null;
  libelleDemandeur: string | null;
  idDepartement: number | null;
  codeDepartement: string | null;
  libelleDepartement: string | null;
  idCasDossier: number;
  codeCasDossier: string;
  libelleCasDossier: string;
  idTypeBudget: number | null;
  codeTypeBudget: string | null;
  typeBudgetSollicite: string | null;
  itemSollicite: string | null;
  destinationSolliciteeAffichage: string | null;
  objet: string;
  compteSection: string | null;
  montantBrut: number;
  devise: string;
  idDevise?: number | null;
  tauxConversion: number | null;
  montantUsd: number | null;
  idTauxChange: number | null;
  modePaiementSollicite: string | null;
  modePaiementVerrouille?: boolean;
  motifVerrouillageModePaiement?: string | null;
  typeInstrumentPaiement: string | null;
  devisePaiement: string | null;
  montantPaiement: number | null;
  tauxPaiement: number | null;
  idTauxChangePaiement: number | null;
  statut: StatutDemandePaiement;
  motifRetour: string | null;
  commentaireRetour: string | null;
  dateCreation: string;
  dateSoumission: string | null;
  dateReception: string | null;
  dateControle: string | null;
  dateVisa: string | null;
  dateRetour: string | null;
  idUtilisateurAssigne: number | null;
  nomUtilisateurAssigne: string | null;
  prenomUtilisateurAssigne: string | null;
  beneficiaires: BeneficiaireDto[];
  imputations: DemandePaiementImputation[];
  pieces: DemandePaiementPiece[];
  circuitEntiteStatut?: string | null;
  validationsEntite?: ValidationEntiteDto[];
}

export interface SnapshotDto {
  idSnapshot: number;
  idImputation: number;
  dateSnapshot: string;
  budgetMensuel: number | null;
  creditEngageMensuel: number | null;
  creditDisponibleMensuelAvantVisa: number | null;
  budgetAnnuel: number;
  creditEngageAnnuel: number;
  creditDisponibleAnnuelAvantVisa: number;
  montantPrevision: number;
  ecartPrevisionImputation: number;
  montantBrut: number;
  devise: string;
  tauxConversion: number;
  montantUsd: number;
  idBudgetLigne: number | null;
}

export interface PieceManquanteDto {
  idPieceObligatoire: number | null;
  codeTypePiece: string;
  libelle: string;
}

export interface JournalAuditDemandeDto {
  idAudit: number;
  operation: string;
  dateHeure: string;
  anciennesValeurs: string | null;
  nouvellesValeurs: string | null;
}

export interface DemandePaiementDetailComplet {
  demande: DemandePaiementDetail;
  snapshots: SnapshotDto[];
  controleBudgetaire: ControleBudgetaireDto | null;
  piecesManquantes: PieceManquanteDto[];
  historique: JournalAuditDemandeDto[];
  billetConversion: BilletConversion | null;
  pieceCaisse: PieceCaisse | null;
  bonProvisoire: BonProvisoire | null;
  minuteCheque: MinuteCheque | null;
}

export interface BilletConversion {
  idBilletConversion: number;
  idDemandePaiement: number;
  statut: string;
  dateConversion: string;
  deviseOrigine: string;
  montantDeviseOrigine: number;
  tauxApplique: number;
  montantCdf: number;
  idTauxChange: number | null;
  demandeChequeNumero: string | null;
  coursEchangeBanque: string | null;
  soldeAPayerDevise: number | null;
  idUtilisateurEtabli: number;
  nomUtilisateurEtabli: string | null;
  dateEtabli: string;
  nomUtilisateurApprouve: string | null;
  nomUtilisateurVisa: string | null;
  referenceDemande: string;
  beneficiaireAffichage: string;
}

export interface EtablirBilletConversionPayload {
  dateConversion?: string | null;
  demandeChequeNumero?: string | null;
  coursEchangeBanque?: string | null;
  soldeAPayerDevise?: number | null;
}

export interface PieceCaisse {
  idPieceCaisse: number;
  idDemandePaiement: number;
  statut: string;
  numeroPiece: string;
  datePiece: string;
  montantFc: number;
  montantEnLettres: string;
  referenceDemande: string;
  motif: string;
  pieceJustificative: string | null;
  beneficiaireAffichage: string;
  beneficiaireMatricule: string | null;
  beneficiaireIdentite: string | null;
  recuSnel: string | null;
  sr: string | null;
  comptabiliteGenerale: string | null;
  cp: string | null;
  cpa: string | null;
  numeroAppariement: string | null;
  identifiantVerification: string;
  idUtilisateurEtabli: number;
  nomUtilisateurEtabli: string | null;
  dateEtabli: string;
}

export interface EtablirPieceCaissePayload {
  datePiece?: string | null;
}

export interface BonProvisoire {
  idBonProvisoire: number;
  idDemandePaiement: number;
  statut: string;
  numeroBon: string;
  dateBon: string;
  montantFc: number;
  montantEnLettres: string;
  referenceDemande: string;
  motif: string;
  mentionJustificationRetrait: string | null;
  beneficiaireAffichage: string;
  beneficiaireMatricule: string | null;
  beneficiaireIdentite: string | null;
  directionBeneficiaire: string | null;
  recuCaisseCentrale: string | null;
  compteGeneral: string | null;
  compteParticulier: string | null;
  numeroAppariement: string | null;
  identifiantVerification: string;
  idUtilisateurEtabli: number;
  nomUtilisateurEtabli: string | null;
  dateEtabli: string;
}

export interface EtablirBonProvisoirePayload {
  dateBon?: string | null;
}

export interface MinuteCheque {
  idMinuteCheque: number;
  idDemandePaiement: number;
  statut: string;
  numeroOp: string;
  dateDocument: string;
  montantPaiement: number;
  devisePaiement: string;
  montantEnLettres: string;
  referenceDemande: string;
  motif: string;
  beneficiaireAffichage: string;
  beneficiaireAdresse: string | null;
  beneficiaireBanque: string | null;
  beneficiaireNumeroCompte: string | null;
  compteGeneral: string | null;
  cpCa: string | null;
  ls: string | null;
  suiviExtraComptable: string | null;
  numeroAppariement: string | null;
  montantSuiviExtraComptable: number | null;
  identifiantVerification: string;
  idUtilisateurEtabli: number;
  nomUtilisateurEtabli: string | null;
  dateEtabli: string;
}

export interface EtablirMinuteChequePayload {
  dateDocument?: string | null;
}

export interface ControleImputationDto {
  idImputation: number;
  ordre: number;
  codeTypeBudget: string;
  estValide: boolean;
  motifRejet: string | null;
  budgetAnnuel: number;
  budgetMensuel: number | null;
  creditEngageAnnuel: number;
  creditEngageMensuel: number | null;
  engagementEnCours: number;
  creditDisponibleAnnuel: number;
  creditDisponibleMensuel: number | null;
  montantPrevision: number;
  ecartPrevisionImputation: number;
  montantCourantUsd: number;
  idBudgetLigne: number | null;
  depassementMensuel?: boolean;
  depassementAnnuel?: boolean;
}

export interface ControleBudgetaireDto {
  estValide: boolean;
  motifRejet: string | null;
  imputations: ControleImputationDto[];
}

export interface LigneGrilleImputationDc {
  idRubriqueBudgetaire: number;
  codeRubrique: string;
  libelle: string;
  estCochee: boolean;
  idImputation: number | null;
  idBudgetLigne: number | null;
  previsionExiste: boolean;
  budgetMensuel: number;
  creditEngageMensuel: number;
  engagementEnCoursBrut: number;
  engagementEnCoursUsd: number;
  montantUsd: number;
  creditDisponibleMensuel: number;
  budgetAnnuel: number;
  creditEngageAnnuel: number;
  creditDisponibleAnnuel: number;
  engagementEnCoursAnnuelUsd?: number;
}

export interface GrilleImputationDc {
  idDemandePaiement: number;
  reference: string;
  devise: string;
  montantBrut: number;
  tauxConversion: number | null;
  montantUsd: number | null;
  idUB: number;
  codeUB: string;
  libelleUB: string;
  mois: number;
  budgetAnnuelUb: number;
  creditEngageAnnuelUb: number;
  lignes: LigneGrilleImputationDc[];
  totalRepartiBrut: number;
  ecartBrut: number;
  totalRepartiUsd: number;
  ecartUsd: number;
}

export interface LigneGrilleImputationAe {
  idRubriqueBudgetaire: number;
  codeRubrique: string;
  libelle: string;
  estCochee: boolean;
  idImputation: number | null;
  idBudgetLigne: number | null;
  previsionExiste: boolean;
  engagementEnCoursBrut: number;
  engagementEnCoursUsd: number;
  montantUsd: number;
  budgetAnnuel: number;
  creditEngageAnnuel: number;
  creditDisponibleAnnuel: number;
  engagementEnCoursAnnuelUsd?: number;
}

export interface LigneExistanteImputationAe {
  idImputation: number;
  libelleItemAE: string;
  idGroupeItemAE: number | null;
  mois: number | null;
  idRubriqueBudgetaire: number;
  codeRubrique: string;
  libelleRubrique: string;
  montantBrut: number;
  montantUsd: number;
}

export interface GrilleImputationAe {
  idDemandePaiement: number;
  reference: string;
  devise: string;
  montantBrut: number;
  tauxConversion: number | null;
  montantUsd: number | null;
  idUB: number;
  codeUB: string;
  libelleUB: string;
  libelleItemAE: string;
  idGroupeItemAE: number | null;
  mois: number | null;
  budgetAnnuelItemUb: number;
  creditEngageAnnuelItemUb: number;
  lignes: LigneGrilleImputationAe[];
  lignesExistantes: LigneExistanteImputationAe[];
  totalRepartiBrut: number;
  ecartBrut: number;
  totalRepartiUsd: number;
  ecartUsd: number;
}

export interface EnregistrerImputationsDcPayload {
  mois: number;
  lignes: { idRubriqueBudgetaire: number; montantBrut: number }[];
}

export interface EnregistrerImputationsAePayload {
  libelleItemAE: string;
  idGroupeItemAE?: number | null;
  mois?: number | null;
  lignes: { idRubriqueBudgetaire: number; montantBrut: number }[];
}

export interface LigneGrilleImputationBi {
  detailBI: string;
  code: number;
  libelle: string;
  estCochee: boolean;
  idImputation: number | null;
  idBudgetLigne: number | null;
  previsionExiste: boolean;
  engagementEnCoursBrut: number;
  engagementEnCoursUsd: number;
  montantUsd: number;
  budgetAnnuel: number;
  creditEngageAnnuel: number;
  creditDisponibleAnnuel: number;
  engagementEnCoursAnnuelUsd?: number;
}

export interface LigneExistanteImputationBi {
  idImputation: number;
  idItemBI: number;
  libelleItemBI: string;
  detailBI: string;
  mois: number | null;
  montantBrut: number;
  montantUsd: number;
}

export interface GrilleImputationBi {
  idDemandePaiement: number;
  reference: string;
  devise: string;
  montantBrut: number;
  tauxConversion: number | null;
  montantUsd: number | null;
  idUB: number;
  codeUB: string;
  libelleUB: string;
  idItemBI: number;
  codeItemBI: string;
  libelleItemBI: string;
  mois: number | null;
  budgetAnnuelItemUb: number;
  creditEngageAnnuelItemUb: number;
  lignes: LigneGrilleImputationBi[];
  lignesExistantes: LigneExistanteImputationBi[];
  totalRepartiBrut: number;
  ecartBrut: number;
  totalRepartiUsd: number;
  ecartUsd: number;
}

export interface EnregistrerImputationsBiPayload {
  idItemBI: number;
  mois?: number | null;
  lignes: { detailBI: string; montantBrut: number }[];
}

export interface RetourDemandePaiementPayload {
  motifRetour: string;
  commentaireRetour?: string | null;
  etapeConcernee?: string | null;
}

export interface ValidationEntitePayload {
  commentaire?: string | null;
}

export interface DeclarationValidationPhysiquePayload {
  nomSignataire: string;
  fonctionSignataire?: string | null;
  dateSignature: string;
  commentaire?: string | null;
}

export interface HistoriqueDemandePaiement {
  idDemandePaiement: number;
  reference: string;
  entrees: JournalAuditDemandeDto[];
}

/** Historique structuré DEMANDE_PAIEMENT_ROUTAGE (Lot 3.6.2). */
export interface DemandePaiementRoutage {
  idRoutage: number;
  action: string;
  statutSource: string;
  statutCible: string;
  idUtilisateurSource: number | null;
  nomUtilisateurSource: string | null;
  prenomUtilisateurSource: string | null;
  idUtilisateurCible: number | null;
  nomUtilisateurCible: string | null;
  prenomUtilisateurCible: string | null;
  dateRoutage: string;
  estActif: boolean;
  motif: string | null;
}

/** Destinataire d'un retour métier projeté depuis le routage (Lot 3.6.3). */
export interface DemandePaiementRetourDestinataire {
  idRoutage: number;
  typeRetour: string | null;
  actionRoutage: string;
  statutSource: string;
  statutCible: string;
  idUtilisateurDestinataire: number | null;
  nomUtilisateurDestinataire: string | null;
  prenomUtilisateurDestinataire: string | null;
  dateRoutage: string;
  motif: string | null;
}

export interface CasDossierPieceObligatoire {
  idPieceObligatoire: number;
  codeTypePiece: string;
  libelle: string;
  ordre: number;
  actif: boolean;
  obligatoire: boolean;
}

export interface CasDossier {
  idCasDossier: number;
  code: string;
  libelle: string;
  ordre: number;
  actif: boolean;
  piecesObligatoires: CasDossierPieceObligatoire[];
}

export interface TauxChangeDto {
  idTauxChange: number;
  deviseBase: string;
  deviseQuote: string;
  tauxReference: number;
  dateEffet: string;
  statut: string;
  dateCreation: string;
  idUtilisateurCreation: number;
  libelleUtilisateurCreation: string | null;
  dateModification: string | null;
  idUtilisateurModification: number | null;
  libelleUtilisateurModification: string | null;
  estModifiable: boolean;
}

export interface TauxChangeApplicable {
  idTauxChange: number | null;
  deviseSource: string;
  deviseCible: string;
  taux: number;
  tauxReference: number;
  dateEffet: string;
  statut: string;
  estIdentite: boolean;
  estInverseCalcule: boolean;
}

export interface PaireTauxChangeDto {
  deviseBase: string;
  deviseQuote: string;
  libelle: string;
}

export interface TauxChangeListQuery {
  deviseBase?: string;
  deviseQuote?: string;
  statut?: string;
  dateEffetMin?: string;
  dateEffetMax?: string;
}

export interface CreateTauxChangePayload {
  deviseBase: string;
  deviseQuote: string;
  tauxReference: number;
  dateEffet: string;
  confirmerRemplacement?: boolean;
}

export interface TauxChangeRemplacementPropose {
  code: string;
  message: string;
  idTauxExistant: number;
  deviseBase: string;
  deviseQuote: string;
  tauxReferenceExistant: number;
  dateEffetExistante: string;
  estUtilise: boolean;
  modePropose: 'ECRASER' | 'CLOTURER_ET_CREER' | string;
}

export interface InactivateTauxChangePayload {
  motif?: string | null;
}

export interface UpdateTauxChangePayload {
  tauxReference: number;
  dateEffet: string;
}

export interface ConversionResult {
  montantSource: number;
  deviseSource: string;
  montantCible: number;
  deviseCible: string;
  tauxApplique: number;
  tauxReference: number;
  idTauxChange: number | null;
  estIdentite: boolean;
}

export interface ConvertirTauxChangePayload {
  montantSource: number;
  deviseSource: string;
  deviseCible: string;
  dateReference: string;
}

export interface LigneBudgetaireDisponible {
  idBudgetLigne: number | null;
  codeTypeBudget: string;
  budgetAnnuel: number;
  budgetMensuel: number | null;
  montantPrevision: number;
  creditEngageAnnuel: number;
  creditEngageMensuel: number | null;
  creditDisponibleAnnuel: number;
  creditDisponibleMensuel: number | null;
  previsionExiste: boolean;
}

export interface CreateBeneficiairePayload {
  typeBeneficiaire: string;
  nomComplet: string;
  matricule?: string | null;
  fonction?: string | null;
  raisonSociale?: string | null;
  rccm?: string | null;
  adresse?: string | null;
  banque?: string | null;
  numeroCompte?: string | null;
  estPrincipal: boolean;
  ordre: number;
}

export interface CreateDemandePaiementPayload {
  dateEmission: string;
  lieuEmission?: string | null;
  idExercice: number;
  idDemandeur: number;
  idUB?: number | null;
  idCasDossier: number;
  objet: string;
  compteSection?: string | null;
  montantBrut: number;
  devise: string;
  idDevise?: number | null;
  typeBudgetSollicite: string;
  itemSollicite?: string | null;
  modePaiementSollicite: string;
  beneficiaires?: CreateBeneficiairePayload[] | null;
}

export interface UpdateDemandePaiementPayload {
  dateEmission: string;
  lieuEmission?: string | null;
  objet: string;
  compteSection?: string | null;
  montantBrut: number;
  devise: string;
  idDevise?: number | null;
  typeBudgetSollicite: string;
  itemSollicite?: string | null;
  modePaiementSollicite: string;
  beneficiaires?: CreateBeneficiairePayload[] | null;
}

export interface DeviseDto {
  idDevise: number;
  code: string;
  libelle: string;
  symbole: string | null;
  actif: boolean;
}

export interface CreateDevisePayload {
  code: string;
  libelle: string;
  symbole?: string | null;
  actif?: boolean;
}

export interface UpdateDevisePayload {
  libelle: string;
  symbole?: string | null;
  actif: boolean;
}

export interface DemandeurDto {
  idDemandeur: number;
  code: string;
  libelle: string;
  idUB: number;
  codeUB: string;
  libelleUB: string;
  idDepartement: number;
  codeDepartement: string;
  libelleDepartement: string;
  actif: boolean;
  dateCreation: string;
  dateModification: string | null;
}

export interface CreateDemandeurPayload {
  code: string;
  libelle: string;
  idUB: number;
}

export interface CreateImputationPayload {
  ordre: number;
  idTypeBudget: number;
  idUB: number;
  idExercice: number;
  idRubriqueBudgetaire?: number | null;
  mois?: number | null;
  libelleItemAE?: string | null;
  idGroupeItemAE?: number | null;
  idItemBI?: number | null;
  detailBI?: string | null;
  idBudgetLigne?: number | null;
  montantBrut: number;
  devise: string;
  numeroFicheSuivi?: number | null;
}

export interface UpdateImputationPayload {
  ordre: number;
  idRubriqueBudgetaire?: number | null;
  mois?: number | null;
  libelleItemAE?: string | null;
  idGroupeItemAE?: number | null;
  idItemBI?: number | null;
  detailBI?: string | null;
  idBudgetLigne?: number | null;
  montantBrut: number;
  devise: string;
  numeroFicheSuivi?: number | null;
}

export interface UploadDemandePaiementPieceMeta {
  idPieceObligatoire?: number | null;
  codeTypePiece: string;
  libelle: string;
}

export async function fetchDemandePaiementPieces(id: number): Promise<DemandePaiementPiece[]> {
  const response = await apiClient.get<DemandePaiementPiece[]>(`/api/v1/demandes-paiement/${id}/pieces`);
  return response.data;
}

export async function addDemandePaiementPiece(
  id: number,
  meta: UploadDemandePaiementPieceMeta,
  file: File,
): Promise<DemandePaiementPiece> {
  const form = new FormData();
  form.append('fichier', file);
  form.append('codeTypePiece', meta.codeTypePiece);
  form.append('libelle', meta.libelle);
  if (meta.idPieceObligatoire != null) {
    form.append('idPieceObligatoire', String(meta.idPieceObligatoire));
  }

  const response = await apiClient.post<DemandePaiementPiece>(
    `/api/v1/demandes-paiement/${id}/pieces`,
    form,
  );
  return response.data;
}

export async function downloadDemandePaiementPiece(
  idDemande: number,
  idPiece: number,
): Promise<Blob> {
  const response = await apiClient.get<Blob>(
    `/api/v1/demandes-paiement/${idDemande}/pieces/${idPiece}/contenu`,
    { responseType: 'blob' },
  );
  return response.data;
}

export async function previewDemandePaiementPiece(
  idDemande: number,
  idPiece: number,
): Promise<Blob> {
  const response = await apiClient.get<Blob>(
    `/api/v1/demandes-paiement/${idDemande}/pieces/${idPiece}/apercu`,
    { responseType: 'blob' },
  );
  return response.data;
}

export async function deleteDemandePaiementPiece(id: number, idPiece: number): Promise<void> {
  await apiClient.delete(`/api/v1/demandes-paiement/${id}/pieces/${idPiece}`);
}

export async function deleteDemandePaiement(id: number): Promise<void> {
  await apiClient.delete(`/api/v1/demandes-paiement/${id}`);
}

export async function fetchDemandesPaiement(params?: {
  scope?: DemandePaiementCompteursScope;
  idExercice?: number;
  idUB?: number;
  idDepartement?: number;
  idCasDossier?: number;
  idTypeBudget?: number;
  idDemandeur?: number;
  statut?: string;
  reference?: string;
  beneficiaire?: string;
  dateDebut?: string;
  dateFin?: string;
}): Promise<DemandePaiementListItem[]> {
  const response = await apiClient.get<DemandePaiementListItem[]>('/api/v1/demandes-paiement', { params });
  return response.data;
}

export async function fetchDemandesPaiementCompteurs(params: {
  scope: DemandePaiementCompteursScope;
  idExercice?: number;
  idUB?: number;
  idDepartement?: number;
  idCasDossier?: number;
  idTypeBudget?: number;
  idDemandeur?: number;
  reference?: string;
  beneficiaire?: string;
  dateDebut?: string;
  dateFin?: string;
  signal?: AbortSignal;
}): Promise<DemandePaiementCompteursDto> {
  const { signal, ...query } = params;
  const response = await apiClient.get<DemandePaiementCompteursDto>('/api/v1/demandes-paiement/compteurs', {
    params: query,
    signal,
  });
  return response.data;
}

export async function fetchDemandePaiement(
  id: number,
  options?: { signal?: AbortSignal },
): Promise<DemandePaiementDetailComplet> {
  const response = await apiClient.get<DemandePaiementDetailComplet>(`/api/v1/demandes-paiement/${id}`, {
    signal: options?.signal,
  });
  return response.data;
}

/** Détail complet (snapshots, contrôle, instruments) — Chargé DP, contrôle budgétaire. */
export async function fetchDemandePaiementComplet(
  id: number,
  options?: { signal?: AbortSignal },
): Promise<DemandePaiementDetailComplet> {
  const response = await apiClient.get<DemandePaiementDetailComplet>(
    `/api/v1/demandes-paiement/${id}/complet`,
    { signal: options?.signal },
  );
  return response.data;
}

export async function createDemandePaiement(body: CreateDemandePaiementPayload): Promise<DemandePaiementDetail> {
  const response = await apiClient.post<DemandePaiementDetail>('/api/v1/demandes-paiement', body);
  return response.data;
}

export async function updateDemandePaiement(
  id: number,
  body: UpdateDemandePaiementPayload,
): Promise<DemandePaiementDetail> {
  const response = await apiClient.put<DemandePaiementDetail>(`/api/v1/demandes-paiement/${id}`, body);
  return response.data;
}

export async function soumettreDemandePaiement(id: number): Promise<DemandePaiementDetail> {
  const response = await apiClient.post<DemandePaiementDetail>(`/api/v1/demandes-paiement/${id}/soumettre`);
  return response.data;
}

export async function annulerSoumissionDemandePaiement(id: number): Promise<DemandePaiementDetail> {
  const response = await apiClient.post<DemandePaiementDetail>(
    `/api/v1/demandes-paiement/${id}/annuler-soumission`,
  );
  return response.data;
}

export async function envoyerDemandePaiementEnValidation(id: number): Promise<DemandePaiementDetail> {
  const response = await apiClient.post<DemandePaiementDetail>(
    `/api/v1/demandes-paiement/${id}/envoyer-validation`,
  );
  return response.data;
}

export async function validerDemandePaiementN1(
  id: number,
  body?: ValidationEntitePayload,
): Promise<DemandePaiementDetail> {
  const response = await apiClient.post<DemandePaiementDetail>(
    `/api/v1/demandes-paiement/${id}/validation/n1`,
    body ?? {},
  );
  return response.data;
}

export async function validerDemandePaiementN2(
  id: number,
  body?: ValidationEntitePayload,
): Promise<DemandePaiementDetail> {
  const response = await apiClient.post<DemandePaiementDetail>(
    `/api/v1/demandes-paiement/${id}/validation/n2`,
    body ?? {},
  );
  return response.data;
}

export async function declarerValidationPhysiqueN1(
  id: number,
  body: DeclarationValidationPhysiquePayload,
): Promise<DemandePaiementDetail> {
  const response = await apiClient.post<DemandePaiementDetail>(
    `/api/v1/demandes-paiement/${id}/validation/n1/physique`,
    body,
  );
  return response.data;
}

export async function declarerValidationPhysiqueN2(
  id: number,
  body: DeclarationValidationPhysiquePayload,
): Promise<DemandePaiementDetail> {
  const response = await apiClient.post<DemandePaiementDetail>(
    `/api/v1/demandes-paiement/${id}/validation/n2/physique`,
    body,
  );
  return response.data;
}

export async function rejeterValidationEntiteDemandePaiement(
  id: number,
  body: RetourDemandePaiementPayload,
): Promise<DemandePaiementDetail> {
  const response = await apiClient.post<DemandePaiementDetail>(
    `/api/v1/demandes-paiement/${id}/rejeter-validation-entite`,
    body,
  );
  return response.data;
}

export async function annulerValidationN2DemandePaiement(id: number): Promise<DemandePaiementDetail> {
  const response = await apiClient.post<DemandePaiementDetail>(
    `/api/v1/demandes-paiement/${id}/validation/n2/annuler`,
  );
  return response.data;
}

export async function annulerValidationN1DemandePaiement(id: number): Promise<DemandePaiementDetail> {
  const response = await apiClient.post<DemandePaiementDetail>(
    `/api/v1/demandes-paiement/${id}/validation/n1/annuler`,
  );
  return response.data;
}

export async function downloadDemandePaiementDocumentPdf(id: number): Promise<Blob> {
  const response = await apiClient.get(`/api/v1/demandes-paiement/${id}/document-pdf`, {
    responseType: 'blob',
  });
  return response.data;
}

export async function previewDemandePaiementDocumentPdf(id: number): Promise<Blob> {
  const response = await apiClient.get(`/api/v1/demandes-paiement/${id}/document-pdf`, {
    params: { inline: true },
    responseType: 'blob',
  });
  return response.data;
}

export type FicheImputationMode = 'Travail' | 'Definitive';

export async function fetchFicheImputation(id: number, mode: FicheImputationMode = 'Travail') {
  const response = await apiClient.get(`/api/v1/demandes-paiement/${id}/fiche-imputation`, {
    params: { mode },
  });
  return response.data;
}

export async function previewFicheImputationPdf(
  id: number,
  mode: FicheImputationMode = 'Travail',
): Promise<Blob> {
  const response = await apiClient.get(`/api/v1/demandes-paiement/${id}/fiche-imputation/pdf`, {
    params: { mode, inline: true },
    responseType: 'blob',
  });
  return response.data;
}

export async function downloadFicheImputationPdf(
  id: number,
  mode: FicheImputationMode = 'Travail',
): Promise<Blob> {
  const response = await apiClient.get(`/api/v1/demandes-paiement/${id}/fiche-imputation/pdf`, {
    params: { mode },
    responseType: 'blob',
  });
  return response.data;
}

export async function remettreEnBrouillonDemandePaiement(id: number): Promise<DemandePaiementDetail> {
  const response = await apiClient.post<DemandePaiementDetail>(
    `/api/v1/demandes-paiement/${id}/remettre-en-brouillon`,
  );
  return response.data;
}

export async function fetchDemandePaiementHistorique(id: number): Promise<HistoriqueDemandePaiement> {
  const response = await apiClient.get<HistoriqueDemandePaiement>(`/api/v1/demandes-paiement/${id}/historique`);
  return response.data;
}

export async function fetchDemandePaiementRoutage(id: number): Promise<DemandePaiementRoutage[]> {
  const response = await apiClient.get<DemandePaiementRoutage[]>(
    `/api/v1/demandes-paiement/${id}/routage`,
  );
  return response.data;
}

export async function fetchDemandePaiementRetoursDestinataires(
  id: number,
): Promise<DemandePaiementRetourDestinataire[]> {
  const response = await apiClient.get<DemandePaiementRetourDestinataire[]>(
    `/api/v1/demandes-paiement/${id}/retours-destinataires`,
  );
  return response.data;
}

export async function addDemandePaiementImputation(
  id: number,
  body: CreateImputationPayload,
): Promise<DemandePaiementImputation> {
  const response = await apiClient.post<DemandePaiementImputation>(
    `/api/v1/demandes-paiement/${id}/imputations`,
    body,
  );
  return response.data;
}

export async function updateDemandePaiementImputation(
  id: number,
  idImputation: number,
  body: UpdateImputationPayload,
): Promise<DemandePaiementImputation> {
  const response = await apiClient.put<DemandePaiementImputation>(
    `/api/v1/demandes-paiement/${id}/imputations/${idImputation}`,
    body,
  );
  return response.data;
}

export async function deleteDemandePaiementImputation(id: number, idImputation: number): Promise<void> {
  await apiClient.delete(`/api/v1/demandes-paiement/${id}/imputations/${idImputation}`);
}

export async function fetchGrilleImputationDc(
  id: number,
  mois?: number,
): Promise<GrilleImputationDc> {
  const response = await apiClient.get<GrilleImputationDc>(
    `/api/v1/demandes-paiement/${id}/imputation-dc`,
    { params: mois ? { mois } : undefined },
  );
  return response.data;
}

export async function enregistrerImputationsDc(
  id: number,
  body: EnregistrerImputationsDcPayload,
): Promise<GrilleImputationDc> {
  const response = await apiClient.put<GrilleImputationDc>(
    `/api/v1/demandes-paiement/${id}/imputations-dc`,
    body,
  );
  return response.data;
}

export async function fetchGrilleImputationAe(
  id: number,
  params: { libelleItemAE: string; idGroupeItemAE?: number | null; mois?: number | null },
): Promise<GrilleImputationAe> {
  const response = await apiClient.get<GrilleImputationAe>(
    `/api/v1/demandes-paiement/${id}/imputation-ae`,
    {
      params: {
        libelleItemAE: params.libelleItemAE,
        idGroupeItemAE: params.idGroupeItemAE ?? undefined,
        mois: params.mois ?? undefined,
      },
    },
  );
  return response.data;
}

export async function enregistrerImputationsAe(
  id: number,
  body: EnregistrerImputationsAePayload,
): Promise<GrilleImputationAe> {
  const response = await apiClient.put<GrilleImputationAe>(
    `/api/v1/demandes-paiement/${id}/imputations-ae`,
    body,
  );
  return response.data;
}

export async function fetchGrilleImputationBi(
  id: number,
  params: { idItemBI: number; mois?: number | null },
): Promise<GrilleImputationBi> {
  const response = await apiClient.get<GrilleImputationBi>(
    `/api/v1/demandes-paiement/${id}/imputation-bi`,
    {
      params: {
        idItemBI: params.idItemBI,
        mois: params.mois ?? undefined,
      },
    },
  );
  return response.data;
}

export async function enregistrerImputationsBi(
  id: number,
  body: EnregistrerImputationsBiPayload,
): Promise<GrilleImputationBi> {
  const response = await apiClient.put<GrilleImputationBi>(
    `/api/v1/demandes-paiement/${id}/imputations-bi`,
    body,
  );
  return response.data;
}

export async function fetchLigneBudgetaireDisponible(params: {
  idExercice: number;
  idUB: number;
  idTypeBudget: number;
  idRubriqueBudgetaire?: number;
  mois?: number;
  libelleItemAE?: string;
  idGroupeItemAE?: number;
  idItemBI?: number;
  detailBI?: string;
}): Promise<LigneBudgetaireDisponible> {
  const response = await apiClient.get<LigneBudgetaireDisponible>(
    '/api/v1/demandes-paiement/lignes-budgetaires-disponibles',
    { params },
  );
  return response.data;
}

export async function fetchCasDossiers(actifsSeulement = true): Promise<CasDossier[]> {
  const response = await apiClient.get<CasDossier[]>('/api/v1/cas-dossiers', {
    params: { actifsSeulement },
  });
  return response.data;
}

export interface CreateCasDossierPayload {
  code: string;
  libelle: string;
  ordre: number;
  actif?: boolean;
}

export interface UpdateCasDossierPayload {
  libelle: string;
  ordre: number;
  actif: boolean;
}

export interface CreateCasDossierPiecePayload {
  codeTypePiece: string;
  libelle: string;
  ordre: number;
  actif?: boolean;
  obligatoire?: boolean;
}

export interface UpdateCasDossierPiecePayload {
  libelle: string;
  ordre: number;
  actif: boolean;
  obligatoire: boolean;
}

export async function createCasDossier(body: CreateCasDossierPayload): Promise<CasDossier> {
  const response = await apiClient.post<CasDossier>('/api/v1/cas-dossiers', body);
  return response.data;
}

export async function updateCasDossier(id: number, body: UpdateCasDossierPayload): Promise<CasDossier> {
  const response = await apiClient.put<CasDossier>(`/api/v1/cas-dossiers/${id}`, body);
  return response.data;
}

export async function addCasDossierPiece(
  idCas: number,
  body: CreateCasDossierPiecePayload,
): Promise<CasDossierPieceObligatoire> {
  const response = await apiClient.post<CasDossierPieceObligatoire>(
    `/api/v1/cas-dossiers/${idCas}/pieces`,
    body,
  );
  return response.data;
}

export async function updateCasDossierPiece(
  idCas: number,
  idPiece: number,
  body: UpdateCasDossierPiecePayload,
): Promise<CasDossierPieceObligatoire> {
  const response = await apiClient.put<CasDossierPieceObligatoire>(
    `/api/v1/cas-dossiers/${idCas}/pieces/${idPiece}`,
    body,
  );
  return response.data;
}

export async function fetchDevises(actifsSeulement = true): Promise<DeviseDto[]> {
  const response = await apiClient.get<DeviseDto[]>('/api/v1/devises', {
    params: { actifsSeulement },
  });
  return response.data;
}

export async function createDevise(body: CreateDevisePayload): Promise<DeviseDto> {
  const response = await apiClient.post<DeviseDto>('/api/v1/devises', body);
  return response.data;
}

export async function updateDevise(id: number, body: UpdateDevisePayload): Promise<DeviseDto> {
  const response = await apiClient.put<DeviseDto>(`/api/v1/devises/${id}`, body);
  return response.data;
}

export interface BanqueDto {
  idBanque: string;
  libelleBanque: string;
  pays: string | null;
  actif: boolean;
  dateCreation: string;
  dateModification: string | null;
}

export type CreateBanquePayload = {
  idBanque: string;
  libelleBanque: string;
  pays: string | null;
  actif: boolean;
};

export type UpdateBanquePayload = {
  libelleBanque: string;
  pays: string | null;
  actif: boolean;
};

export type ImportBanqueItemPayload = {
  idBanque: string;
  libelleBanque: string;
  pays: string | null;
};

export interface ImportBanquesResultDto {
  total: number;
  crees: number;
  dejaExistantes: number;
  erreurs: number;
  details: { idBanque: string; motif: string }[];
}

export async function fetchBanques(actifsSeulement = false): Promise<BanqueDto[]> {
  const response = await apiClient.get<BanqueDto[]>('/api/v1/banques', {
    params: { actifsSeulement },
  });
  return response.data;
}

export async function createBanque(body: CreateBanquePayload): Promise<BanqueDto> {
  const response = await apiClient.post<BanqueDto>('/api/v1/banques', body);
  return response.data;
}

export async function updateBanque(idBanque: string, body: UpdateBanquePayload): Promise<BanqueDto> {
  const response = await apiClient.put<BanqueDto>(
    `/api/v1/banques/${encodeURIComponent(idBanque)}`,
    body,
  );
  return response.data;
}

export async function importBanques(items: ImportBanqueItemPayload[]): Promise<ImportBanquesResultDto> {
  const response = await apiClient.post<ImportBanquesResultDto>('/api/v1/banques/import', {
    banques: items,
  });
  return response.data;
}

export interface GroupeTypeCompteDto {
  idGroupeTypeCompte: number;
  libelle: string;
  actif: boolean;
  dateCreation: string;
  dateModification: string | null;
}

export type CreateGroupeTypeComptePayload = {
  libelle: string;
  actif: boolean;
};

export type UpdateGroupeTypeComptePayload = {
  libelle: string;
  actif: boolean;
};

export async function fetchGroupesTypesComptes(actifsSeulement = false): Promise<GroupeTypeCompteDto[]> {
  const response = await apiClient.get<GroupeTypeCompteDto[]>('/api/v1/groupes-types-comptes', {
    params: { actifsSeulement },
  });
  return response.data;
}

export async function createGroupeTypeCompte(
  body: CreateGroupeTypeComptePayload,
): Promise<GroupeTypeCompteDto> {
  const response = await apiClient.post<GroupeTypeCompteDto>('/api/v1/groupes-types-comptes', body);
  return response.data;
}

export async function updateGroupeTypeCompte(
  id: number,
  body: UpdateGroupeTypeComptePayload,
): Promise<GroupeTypeCompteDto> {
  const response = await apiClient.put<GroupeTypeCompteDto>(`/api/v1/groupes-types-comptes/${id}`, body);
  return response.data;
}

export interface CategorieCompteDto {
  idCategorieCompte: number;
  libelle: string;
  orientation: string | null;
  actif: boolean;
  dateCreation: string;
  dateModification: string | null;
}

export type CreateCategorieComptePayload = {
  libelle: string;
  orientation: string | null;
  actif: boolean;
};

export type UpdateCategorieComptePayload = {
  libelle: string;
  orientation: string | null;
  actif: boolean;
};

export async function fetchCategoriesComptes(actifsSeulement = false): Promise<CategorieCompteDto[]> {
  const response = await apiClient.get<CategorieCompteDto[]>('/api/v1/categories-comptes', {
    params: { actifsSeulement },
  });
  return response.data;
}

export async function createCategorieCompte(
  body: CreateCategorieComptePayload,
): Promise<CategorieCompteDto> {
  const response = await apiClient.post<CategorieCompteDto>('/api/v1/categories-comptes', body);
  return response.data;
}

export async function updateCategorieCompte(
  id: number,
  body: UpdateCategorieComptePayload,
): Promise<CategorieCompteDto> {
  const response = await apiClient.put<CategorieCompteDto>(`/api/v1/categories-comptes/${id}`, body);
  return response.data;
}

export interface DirectionDto {
  idDirection: number;
  libelle: string;
  actif: boolean;
  dateCreation: string;
  dateModification: string | null;
}

export type CreateDirectionPayload = {
  libelle: string;
  actif: boolean;
};

export type UpdateDirectionPayload = {
  libelle: string;
  actif: boolean;
};

export async function fetchDirections(actifsSeulement = false): Promise<DirectionDto[]> {
  const response = await apiClient.get<DirectionDto[]>('/api/v1/directions', {
    params: { actifsSeulement },
  });
  return response.data;
}

export async function createDirection(body: CreateDirectionPayload): Promise<DirectionDto> {
  const response = await apiClient.post<DirectionDto>('/api/v1/directions', body);
  return response.data;
}

export async function updateDirection(id: number, body: UpdateDirectionPayload): Promise<DirectionDto> {
  const response = await apiClient.put<DirectionDto>(`/api/v1/directions/${id}`, body);
  return response.data;
}

export interface ProvinceDto {
  idProvince: string;
  libelle: string;
  actif: boolean;
  dateCreation: string;
  dateModification: string | null;
}

export type CreateProvincePayload = {
  idProvince: string;
  libelle: string;
  actif: boolean;
};

export type UpdateProvincePayload = {
  idProvince: string;
  libelle: string;
  actif: boolean;
};

export async function fetchProvinces(actifsSeulement = false): Promise<ProvinceDto[]> {
  const response = await apiClient.get<ProvinceDto[]>('/api/v1/provinces', {
    params: { actifsSeulement },
  });
  return response.data;
}

export async function createProvince(body: CreateProvincePayload): Promise<ProvinceDto> {
  const response = await apiClient.post<ProvinceDto>('/api/v1/provinces', body);
  return response.data;
}

export async function updateProvince(id: string, body: UpdateProvincePayload): Promise<ProvinceDto> {
  const response = await apiClient.put<ProvinceDto>(
    `/api/v1/provinces/${encodeURIComponent(id)}`,
    body,
  );
  return response.data;
}

export interface CompteFinancierDto {
  idCompte: number;
  numeroCompte: string;
  libelleCompte: string;
  idBanque: string;
  banqueLibelle: string;
  idDirection: number;
  directionLibelle: string;
  codeTypeCompte: string;
  typeCompteLibelle: string;
  idDevise: number;
  deviseCode: string;
  deviseLibelle: string;
  idProvince: string | null;
  provinceLibelle: string | null;
  idUtilisateur: number | null;
  utilisateurLibelle: string | null;
  dateCreation: string;
  dateCloture: string | null;
  dateModification: string | null;
  actif: boolean;
}

export type CreateCompteFinancierPayload = {
  numeroCompte: string;
  libelleCompte: string;
  idBanque: string;
  idDirection: number;
  codeTypeCompte: string;
  idDevise: number;
  idProvince: string | null;
  idUtilisateur: number | null;
  actif: boolean;
};

export type UpdateCompteFinancierPayload = CreateCompteFinancierPayload;

export type ImportCompteRawPayload = {
  ligneExcel: number;
  idCompte: string | null;
  numeroCompte: string;
  libelleCompte: string;
  direction: string;
  banque: string;
  typeCompte: string;
  devise: string;
  dateCreation: string;
  dateCloture: string;
  etat: string;
  idtProvince: string | null;
};

export interface ImportCompteLigneDto {
  ligneExcel: number;
  idCompte: number | null;
  numeroCompte: string;
  libelleCompte: string;
  banque: string;
  typeCompte: string;
  devise: string;
  direction: string;
  province: string | null;
  statut: string;
  resultat: string;
  champ: string | null;
  valeurRecue: string | null;
}

export interface ImportComptesPreviewDto {
  nomFichier: string;
  lignesDetectees: number;
  lignes: ImportCompteLigneDto[];
  resume: {
    analysees: number;
    aImporter: number;
    doublons: number;
    dejaExistants: number;
    erreurs: number;
  };
}

export interface ImportComptesResultDto {
  analysees: number;
  importes: number;
  ignores: number;
  erreurs: number;
  details: ImportCompteLigneDto[];
}

export async function fetchComptesFinanciers(actifsSeulement = false): Promise<CompteFinancierDto[]> {
  const response = await apiClient.get<CompteFinancierDto[]>('/api/v1/comptes', {
    params: { actifsSeulement },
  });
  return response.data;
}

export async function createCompteFinancier(
  body: CreateCompteFinancierPayload,
): Promise<CompteFinancierDto> {
  const response = await apiClient.post<CompteFinancierDto>('/api/v1/comptes', body);
  return response.data;
}

export async function updateCompteFinancier(
  id: number,
  body: UpdateCompteFinancierPayload,
): Promise<CompteFinancierDto> {
  const response = await apiClient.put<CompteFinancierDto>(`/api/v1/comptes/${id}`, body);
  return response.data;
}

export async function previewImportComptes(
  nomFichier: string,
  lignes: ImportCompteRawPayload[],
): Promise<ImportComptesPreviewDto> {
  const response = await apiClient.post<ImportComptesPreviewDto>('/api/v1/comptes/import/preview', {
    nomFichier,
    lignes,
  });
  return response.data;
}

export async function importComptes(
  nomFichier: string,
  lignes: ImportCompteRawPayload[],
): Promise<ImportComptesResultDto> {
  const response = await apiClient.post<ImportComptesResultDto>('/api/v1/comptes/import', {
    nomFichier,
    lignes,
  });
  return response.data;
}

export interface CompteCategorieDto {
  idCompteCategorie: number;
  idCompte: number;
  numeroCompte: string;
  libelleCompte: string;
  idCategorieCompte: number;
  categorieLibelle: string;
  categorieActif: boolean;
  dateDebut: string;
  dateFin: string | null;
  active: boolean;
}

export type UpsertCompteCategoriePayload = {
  categorieId: number;
  dateDebut: string;
  dateFin: string | null;
};

export type ImportCompteCategorieRawPayload = {
  ligneExcel: number;
  idCompteCategorie: string | null;
  dateDebut: string;
  dateFin: string;
  idCategorieCompte: string | null;
  idCompte: string | null;
};

export interface ImportCompteCategorieLigneDto {
  ligneExcel: number;
  idCompteCategorie: number | null;
  idCompte: number | null;
  compte: string;
  idCategorieCompte: number | null;
  categorie: string;
  dateDebut: string;
  dateFin: string;
  statut: string;
  resultat: string;
  champ: string | null;
  valeurRecue: string | null;
}

export interface ImportCompteCategoriesResumeDto {
  analysees: number;
  aImporter: number;
  doublons: number;
  dejaExistants: number;
  erreurs: number;
  conflitsPeriode: number;
  datesSentinelleConverties: number;
}

export interface ImportCompteCategoriesPreviewDto {
  nomFichier: string;
  lignesDetectees: number;
  lignes: ImportCompteCategorieLigneDto[];
  resume: ImportCompteCategoriesResumeDto;
}

export interface ImportCompteCategoriesResultDto {
  analysees: number;
  importes: number;
  ignores: number;
  erreurs: number;
  details: ImportCompteCategorieLigneDto[];
  resume: ImportCompteCategoriesResumeDto;
}

export async function fetchCompteCategories(compteId: number): Promise<CompteCategorieDto[]> {
  const response = await apiClient.get<CompteCategorieDto[]>(`/api/v1/comptes/${compteId}/categories`);
  return response.data;
}

export async function createCompteCategorie(
  compteId: number,
  body: UpsertCompteCategoriePayload,
): Promise<CompteCategorieDto> {
  const response = await apiClient.post<CompteCategorieDto>(`/api/v1/comptes/${compteId}/categories`, body);
  return response.data;
}

export async function updateCompteCategorie(
  compteId: number,
  relationId: number,
  body: UpsertCompteCategoriePayload,
): Promise<CompteCategorieDto> {
  const response = await apiClient.put<CompteCategorieDto>(
    `/api/v1/comptes/${compteId}/categories/${relationId}`,
    body,
  );
  return response.data;
}

export async function cloturerCompteCategorie(
  compteId: number,
  relationId: number,
  dateFin: string,
): Promise<CompteCategorieDto> {
  const response = await apiClient.post<CompteCategorieDto>(
    `/api/v1/comptes/${compteId}/categories/${relationId}/cloturer`,
    { dateFin },
  );
  return response.data;
}

export async function previewImportCompteCategories(
  nomFichier: string,
  lignes: ImportCompteCategorieRawPayload[],
): Promise<ImportCompteCategoriesPreviewDto> {
  const response = await apiClient.post<ImportCompteCategoriesPreviewDto>(
    '/api/v1/comptes/categories/import/preview',
    { nomFichier, lignes },
  );
  return response.data;
}

export async function importCompteCategories(
  nomFichier: string,
  lignes: ImportCompteCategorieRawPayload[],
): Promise<ImportCompteCategoriesResultDto> {
  const response = await apiClient.post<ImportCompteCategoriesResultDto>(
    '/api/v1/comptes/categories/import',
    { nomFichier, lignes },
  );
  return response.data;
}

export interface TypeCompteDto {
  code: string;
  libelle: string;
  idGroupeTypeCompte: number;
  groupeLibelle: string;
  actif: boolean;
  dateCreation: string;
  dateModification: string | null;
}

export type CreateTypeComptePayload = {
  code: string;
  libelle: string;
  idGroupeTypeCompte: number;
  actif: boolean;
};

export type UpdateTypeComptePayload = {
  code: string;
  libelle: string;
  idGroupeTypeCompte: number;
  actif: boolean;
};

export async function fetchTypesComptes(actifsSeulement = false): Promise<TypeCompteDto[]> {
  const response = await apiClient.get<TypeCompteDto[]>('/api/v1/types-comptes', {
    params: { actifsSeulement },
  });
  return response.data;
}

export async function createTypeCompte(body: CreateTypeComptePayload): Promise<TypeCompteDto> {
  const response = await apiClient.post<TypeCompteDto>('/api/v1/types-comptes', body);
  return response.data;
}

export async function updateTypeCompte(code: string, body: UpdateTypeComptePayload): Promise<TypeCompteDto> {
  const response = await apiClient.put<TypeCompteDto>(
    `/api/v1/types-comptes/${encodeURIComponent(code)}`,
    body,
  );
  return response.data;
}

export type TypeInstrumentPaiementCode = 'PIECE_CAISSE' | 'BON_PROVISOIRE' | 'MINUTE_CHEQUE';

export interface ParametreInstrumentPaiementDto {
  idParametreInstrumentPaiement: number | null;
  typeInstrument: TypeInstrumentPaiementCode;
  sr: string | null;
  comptabiliteGenerale: string | null;
  cp: string | null;
  cpa: string | null;
  compteGeneral: string | null;
  compteParticulier: string | null;
  cpCa: string | null;
  ls: string | null;
  suiviExtraComptable: string | null;
  montantSuiviExtraComptable: number | null;
  numeroAppariement: string | null;
  recuInstitutionnel: string | null;
  actif: boolean;
  estConfigure: boolean;
}

export type UpsertParametreInstrumentPayload = {
  sr?: string | null;
  comptabiliteGenerale?: string | null;
  cp?: string | null;
  cpa?: string | null;
  compteGeneral?: string | null;
  compteParticulier?: string | null;
  cpCa?: string | null;
  ls?: string | null;
  suiviExtraComptable?: string | null;
  montantSuiviExtraComptable?: number | null;
  numeroAppariement?: string | null;
  recuInstitutionnel?: string | null;
  actif?: boolean;
};

export async function fetchParametresInstrumentPaiement(): Promise<ParametreInstrumentPaiementDto[]> {
  const response = await apiClient.get<ParametreInstrumentPaiementDto[]>(
    '/api/v1/parametres-instrument-paiement',
  );
  return response.data;
}

export async function upsertParametreInstrumentPaiement(
  typeInstrument: TypeInstrumentPaiementCode,
  body: UpsertParametreInstrumentPayload,
): Promise<ParametreInstrumentPaiementDto> {
  const response = await apiClient.put<ParametreInstrumentPaiementDto>(
    `/api/v1/parametres-instrument-paiement/${typeInstrument}`,
    body,
  );
  return response.data;
}

export async function fetchCasDossier(id: number, piecesActivesSeulement = true): Promise<CasDossier> {
  const response = await apiClient.get<CasDossier>(`/api/v1/cas-dossiers/${id}`, {
    params: { piecesActivesSeulement },
  });
  return response.data;
}

export async function fetchTauxChangePaires(): Promise<PaireTauxChangeDto[]> {
  const response = await apiClient.get<PaireTauxChangeDto[]>('/api/v1/taux-change/paires');
  return response.data;
}

export async function fetchTauxChangeList(query?: TauxChangeListQuery): Promise<TauxChangeDto[]> {
  const response = await apiClient.get<TauxChangeDto[]>('/api/v1/taux-change', { params: query });
  return response.data;
}

export async function fetchTauxChangeById(id: number): Promise<TauxChangeDto | null> {
  try {
    const response = await apiClient.get<TauxChangeDto>(`/api/v1/taux-change/${id}`);
    return response.data;
  } catch (err: unknown) {
    const status = (err as { response?: { status?: number } })?.response?.status;
    if (status === 404) return null;
    throw err;
  }
}

export async function fetchTauxChangeApplicable(
  deviseSource: string,
  deviseCible: string,
  dateReference: string,
): Promise<TauxChangeApplicable | null> {
  try {
    const response = await apiClient.get<TauxChangeApplicable>('/api/v1/taux-change/applicable', {
      params: { deviseSource, deviseCible, dateReference },
    });
    return response.data;
  } catch (err: unknown) {
    const status = (err as { response?: { status?: number } })?.response?.status;
    if (status === 404) return null;
    throw err;
  }
}

export async function createTauxChange(body: CreateTauxChangePayload): Promise<TauxChangeDto> {
  const response = await apiClient.post<TauxChangeDto>('/api/v1/taux-change', body);
  return response.data;
}

export async function updateTauxChange(id: number, body: UpdateTauxChangePayload): Promise<TauxChangeDto> {
  const response = await apiClient.put<TauxChangeDto>(`/api/v1/taux-change/${id}`, body);
  return response.data;
}

export async function inactivateTauxChange(
  id: number,
  body: InactivateTauxChangePayload = {},
): Promise<TauxChangeDto> {
  const response = await apiClient.post<TauxChangeDto>(`/api/v1/taux-change/${id}/inactivate`, body);
  return response.data;
}

export async function convertirTauxChange(body: ConvertirTauxChangePayload): Promise<ConversionResult> {
  const response = await apiClient.post<ConversionResult>('/api/v1/taux-change/convertir', body);
  return response.data;
}

export async function receptionnerDemandePaiement(id: number): Promise<DemandePaiementDetail> {
  const response = await apiClient.post<DemandePaiementDetail>(
    `/api/v1/demandes-paiement/${id}/receptionner`,
  );
  return response.data;
}

export async function entrerTraitementDemandePaiement(id: number): Promise<DemandePaiementDetail> {
  const response = await apiClient.post<DemandePaiementDetail>(
    `/api/v1/demandes-paiement/${id}/entrer-traitement`,
  );
  return response.data;
}

export async function retenirSollicitationChargeDemandePaiement(
  id: number,
  body: {
    modePaiementSollicite: string;
    typeBudgetSollicite: string;
    itemSollicite?: string | null;
  },
): Promise<DemandePaiementDetail> {
  const response = await apiClient.post<DemandePaiementDetail>(
    `/api/v1/demandes-paiement/${id}/retenir-sollicitation-charge`,
    body,
  );
  return response.data;
}

export async function traiterChargeDemandePaiement(
  id: number,
  body: {
    typeInstrument: string;
    devisePaiement?: string | null;
    tauxPaiement?: number | null;
    idTauxChangePaiement?: number | null;
    modePaiementSollicite?: string | null;
    typeBudgetSollicite?: string | null;
    itemSollicite?: string | null;
  },
): Promise<DemandePaiementDetail> {
  const response = await apiClient.post<DemandePaiementDetail>(
    `/api/v1/demandes-paiement/${id}/traitement-charge`,
    body,
  );
  return response.data;
}

export async function fetchBilletConversion(idDemande: number): Promise<BilletConversion | null> {
  try {
    const response = await apiClient.get<BilletConversion>(
      `/api/v1/demandes-paiement/${idDemande}/billet-conversion`,
    );
    return response.data;
  } catch (err) {
    if (axios.isAxiosError(err) && err.response?.status === 404) return null;
    throw err;
  }
}

export async function etablirBilletConversion(
  idDemande: number,
  body: EtablirBilletConversionPayload,
): Promise<BilletConversion> {
  const { markEtablissementHttpStart, markEtablissementHttpEnd } = await import(
    '../features/paiements/etablissementPerf'
  );
  markEtablissementHttpStart('BILLET_CONVERSION', idDemande);
  try {
    const response = await apiClient.post<BilletConversion>(
      `/api/v1/demandes-paiement/${idDemande}/billet-conversion`,
      body,
    );
    return response.data;
  } finally {
    markEtablissementHttpEnd('BILLET_CONVERSION', idDemande);
  }
}

export async function downloadBilletConversionPdf(idDemande: number): Promise<Blob> {
  const response = await apiClient.get(`/api/v1/demandes-paiement/${idDemande}/billet-conversion/pdf`, {
    responseType: 'blob',
  });
  return response.data;
}

export async function previewBilletConversionPdf(idDemande: number): Promise<Blob> {
  const response = await apiClient.get(`/api/v1/demandes-paiement/${idDemande}/billet-conversion/pdf`, {
    params: { inline: true },
    responseType: 'blob',
  });
  return response.data;
}

export async function fetchPieceCaisse(idDemande: number): Promise<PieceCaisse | null> {
  try {
    const response = await apiClient.get<PieceCaisse>(
      `/api/v1/demandes-paiement/${idDemande}/piece-caisse`,
    );
    return response.data;
  } catch {
    return null;
  }
}

export async function etablirPieceCaisse(
  idDemande: number,
  body: EtablirPieceCaissePayload,
): Promise<PieceCaisse> {
  const { markEtablissementHttpStart, markEtablissementHttpEnd } = await import(
    '../features/paiements/etablissementPerf'
  );
  markEtablissementHttpStart('PIECE_CAISSE', idDemande);
  try {
    const response = await apiClient.post<PieceCaisse>(
      `/api/v1/demandes-paiement/${idDemande}/piece-caisse`,
      body,
    );
    return response.data;
  } finally {
    markEtablissementHttpEnd('PIECE_CAISSE', idDemande);
  }
}

export async function previewPieceCaissePdf(idDemande: number): Promise<Blob> {
  const response = await apiClient.get(`/api/v1/demandes-paiement/${idDemande}/piece-caisse/pdf`, {
    params: { inline: true },
    responseType: 'blob',
  });
  return response.data;
}

export async function fetchBonProvisoire(idDemande: number): Promise<BonProvisoire | null> {
  try {
    const response = await apiClient.get<BonProvisoire>(
      `/api/v1/demandes-paiement/${idDemande}/bon-provisoire`,
    );
    return response.data;
  } catch {
    return null;
  }
}

export async function etablirBonProvisoire(
  idDemande: number,
  body: EtablirBonProvisoirePayload,
): Promise<BonProvisoire> {
  const { markEtablissementHttpStart, markEtablissementHttpEnd } = await import(
    '../features/paiements/etablissementPerf'
  );
  markEtablissementHttpStart('BON_PROVISOIRE', idDemande);
  try {
    const response = await apiClient.post<BonProvisoire>(
      `/api/v1/demandes-paiement/${idDemande}/bon-provisoire`,
      body,
    );
    return response.data;
  } finally {
    markEtablissementHttpEnd('BON_PROVISOIRE', idDemande);
  }
}

export async function previewBonProvisoirePdf(idDemande: number): Promise<Blob> {
  const response = await apiClient.get(`/api/v1/demandes-paiement/${idDemande}/bon-provisoire/pdf`, {
    params: { inline: true },
    responseType: 'blob',
  });
  return response.data;
}

export async function fetchMinuteCheque(idDemande: number): Promise<MinuteCheque | null> {
  try {
    const response = await apiClient.get<MinuteCheque>(
      `/api/v1/demandes-paiement/${idDemande}/minute-cheque`,
    );
    return response.data;
  } catch {
    return null;
  }
}

export async function etablirMinuteCheque(
  idDemande: number,
  body: EtablirMinuteChequePayload,
): Promise<MinuteCheque> {
  const { markEtablissementHttpStart, markEtablissementHttpEnd } = await import(
    '../features/paiements/etablissementPerf'
  );
  markEtablissementHttpStart('MINUTE_CHEQUE', idDemande);
  try {
    const response = await apiClient.post<MinuteCheque>(
      `/api/v1/demandes-paiement/${idDemande}/minute-cheque`,
      body,
    );
    return response.data;
  } finally {
    markEtablissementHttpEnd('MINUTE_CHEQUE', idDemande);
  }
}

export async function previewMinuteChequePdf(idDemande: number): Promise<Blob> {
  const response = await apiClient.get(`/api/v1/demandes-paiement/${idDemande}/minute-cheque/pdf`, {
    params: { inline: true },
    responseType: 'blob',
  });
  return response.data;
}

export async function orienterDemandePaiement(id: number): Promise<DemandePaiementDetail> {
  const response = await apiClient.post<DemandePaiementDetail>(
    `/api/v1/demandes-paiement/${id}/orienter`,
    {},
  );
  return response.data;
}

export async function prendreEnChargeDemandePaiement(id: number): Promise<DemandePaiementDetail> {
  const response = await apiClient.post<DemandePaiementDetail>(
    `/api/v1/demandes-paiement/${id}/prendre-en-charge`,
  );
  return response.data;
}

export async function controleBudgetaireDemandePaiement(id: number): Promise<ControleBudgetaireDto> {
  const response = await apiClient.post<ControleBudgetaireDto>(
    `/api/v1/demandes-paiement/${id}/controle-budgetaire`,
  );
  return response.data;
}

export async function retournerDemandePaiement(
  id: number,
  body: RetourDemandePaiementPayload,
): Promise<DemandePaiementDetail> {
  const response = await apiClient.post<DemandePaiementDetail>(
    `/api/v1/demandes-paiement/${id}/retourner`,
    body,
  );
  return response.data;
}

export type TypeDocumentEtabli =
  | 'BILLET_CONVERSION'
  | 'PIECE_CAISSE'
  | 'BON_PROVISOIRE'
  | 'MINUTE_CHEQUE';

export interface DocumentsEtablisQueryParams {
  dateDebut: string;
  dateFin: string;
  typeDocument?: string;
  idExercice?: number;
  idUB?: number;
  idDepartement?: number;
  selection?: string;
}

export interface DocumentEtabliListItem {
  idDemandePaiement: number;
  reference: string;
  typeDocument: TypeDocumentEtabli | string;
  libelleTypeDocument: string;
  numeroDocument: string;
  dateEtabli: string;
  dateDocument: string;
  montant: number;
  devise: string;
  beneficiaireAffichage: string;
  nomUtilisateurEtabli: string | null;
  pdfRouteSegment: string;
  idUB: number;
}

export async function fetchDocumentsEtablis(
  params: DocumentsEtablisQueryParams,
): Promise<DocumentEtabliListItem[]> {
  const response = await apiClient.get<DocumentEtabliListItem[]>(
    '/api/v1/demandes-paiement/documents-etablis',
    { params },
  );
  return response.data;
}

export async function downloadDocumentsEtablisListePdf(
  params: DocumentsEtablisQueryParams,
): Promise<Blob> {
  const response = await apiClient.get('/api/v1/demandes-paiement/documents-etablis/liste-pdf', {
    params: { ...params, inline: true },
    responseType: 'blob',
  });
  return response.data as Blob;
}

export async function downloadDocumentsEtablisArchivePdf(
  params: DocumentsEtablisQueryParams,
): Promise<Blob> {
  const response = await apiClient.get('/api/v1/demandes-paiement/documents-etablis/archive-pdf', {
    params,
    responseType: 'blob',
  });
  return response.data as Blob;
}

export async function downloadDocumentsEtablisDocumentsPdf(
  params: DocumentsEtablisQueryParams,
): Promise<Blob> {
  const response = await apiClient.get('/api/v1/demandes-paiement/documents-etablis/documents-pdf', {
    params: { ...params, inline: true },
    responseType: 'blob',
  });
  return response.data as Blob;
}

export async function viserDemandePaiement(id: number): Promise<DemandePaiementDetail> {
  const response = await apiClient.post<DemandePaiementDetail>(`/api/v1/demandes-paiement/${id}/viser`);
  return response.data;
}

export async function fetchDemandeurs(actifsSeulement = true): Promise<DemandeurDto[]> {
  const response = await apiClient.get<DemandeurDto[]>('/api/v1/demandeurs', {
    params: { actifsSeulement },
  });
  return response.data;
}

export async function fetchDemandeur(id: number): Promise<DemandeurDto> {
  const response = await apiClient.get<DemandeurDto>(`/api/v1/demandeurs/${id}`);
  return response.data;
}

export async function createDemandeur(body: CreateDemandeurPayload): Promise<DemandeurDto> {
  const response = await apiClient.post<DemandeurDto>('/api/v1/demandeurs', body);
  return response.data;
}
