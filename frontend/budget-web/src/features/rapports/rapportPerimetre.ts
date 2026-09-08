import type { Structure, UniteBudgetaire } from '../../services/apiClient';

export function buildStructuresById(structures: Structure[]): Map<number, Structure> {
  const m = new Map<number, Structure>();
  for (const s of structures) m.set(s.idStructure, s);
  return m;
}

/** Remonte les parents jusqu'à trouver un type donné. */
export function findAncestor(
  structuresById: Map<number, Structure>,
  startId: number,
  type: string,
): Structure | null {
  let current: Structure | undefined = structuresById.get(startId);
  let guard = 0;
  while (current && guard++ < 30) {
    if (current.typeStructure.toUpperCase() === type.toUpperCase()) return current;
    if (current.parentId == null) break;
    current = structuresById.get(current.parentId);
  }
  return null;
}

/** True si `startId` est `nodeId` ou un descendant de `nodeId`. */
export function isUnder(
  structuresById: Map<number, Structure>,
  startId: number,
  nodeId: number,
): boolean {
  let current: Structure | undefined = structuresById.get(startId);
  let guard = 0;
  while (current && guard++ < 30) {
    if (current.idStructure === nodeId) return true;
    if (current.parentId == null) break;
    current = structuresById.get(current.parentId);
  }
  return false;
}

/**
 * Structures d'un type (ENTITE / DEPARTEMENT / DIVISION) atteignables
 * depuis les UB accessibles au périmètre utilisateur.
 * Si `underRootId` est fourni, ne garde que celles sous ce nœud.
 */
export function structuresAccessiblesParType(
  structuresById: Map<number, Structure>,
  ubs: UniteBudgetaire[],
  type: 'ENTITE' | 'DEPARTEMENT' | 'DIVISION',
  underRootId?: number | null,
): Structure[] {
  const seen = new Map<number, Structure>();
  for (const u of ubs) {
    const ancestor = findAncestor(structuresById, u.idStructure, type);
    if (!ancestor) continue;
    if (
      underRootId != null &&
      !isUnder(structuresById, ancestor.idStructure, underRootId)
    ) {
      continue;
    }
    seen.set(ancestor.idStructure, ancestor);
  }
  return [...seen.values()].sort(
    (a, b) => a.code.localeCompare(b.code, 'fr') || a.libelle.localeCompare(b.libelle, 'fr'),
  );
}

/** Filtre les UB accessibles sous une structure (entité / département / division). */
export function filterUbsSousStructure(
  structuresById: Map<number, Structure>,
  ubs: UniteBudgetaire[],
  rootId: number | null | undefined,
): UniteBudgetaire[] {
  if (rootId == null) return ubs;
  return ubs.filter((u) => isUnder(structuresById, u.idStructure, rootId));
}
