import axios from 'axios';

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5257';

export const apiClient = axios.create({
  baseURL: apiBaseUrl,
  headers: {
    'Content-Type': 'application/json',
  },
});

export interface TypeBudget {
  idTypeBudget: number;
  codeType: string;
  libelle: string;
  ordreAffichage: number;
  actif: boolean;
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

export async function fetchTypesBudget(): Promise<TypeBudget[]> {
  const response = await apiClient.get<TypeBudget[]>('/api/v1/referentiels/types-budget');
  return response.data;
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

export async function createDepartement(payload: CreateDepartementPayload): Promise<Departement> {
  const response = await apiClient.post<Departement>('/api/v1/departements', payload);
  return response.data;
}
