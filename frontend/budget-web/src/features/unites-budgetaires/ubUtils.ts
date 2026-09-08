import type { Structure } from '../../services/apiClient';

export function extraireCodeAffichage(code: string): string {
  const parts = code
    .split('.')
    .map((p) => p.trim())
    .filter(Boolean);
  return parts.length === 0 ? code : (parts[parts.length - 1] ?? code);
}

export function libelleDepartement(code: string, libelle: string): string {
  return `${code} — ${libelle}`;
}

export function libelleStructure(code: string, libelle: string): string {
  return `${extraireCodeAffichage(code)} — ${libelle}`;
}

export function apiErrorMessage(err: unknown, fallback: string): string {
  if (typeof err === 'object' && err !== null && 'response' in err) {
    const data = (err as { response?: { data?: { message?: string; Message?: string } } }).response?.data;
    return data?.message ?? data?.Message ?? fallback;
  }
  return fallback;
}

export function libelleStatut(actif: boolean): 'ACTIF' | 'INACTIF' {
  return actif ? 'ACTIF' : 'INACTIF';
}

/** True if the structure (or an ancestor) is the org-type DEPARTEMENT matching the given department code. */
export function structureAppartientAuDepartement(
  structure: Structure,
  structures: Structure[],
  departementCode: string,
): boolean {
  const byId = new Map(structures.map((s) => [s.idStructure, s]));
  let current: Structure | undefined = structure;
  let guard = 0;
  const cible = departementCode.trim().toUpperCase();

  while (current && guard++ < 20) {
    if (current.typeStructure.toUpperCase() === 'DEPARTEMENT') {
      return extraireCodeAffichage(current.code).toUpperCase() === cible;
    }
    if (current.parentId == null) return false;
    current = byId.get(current.parentId);
  }
  return false;
}
