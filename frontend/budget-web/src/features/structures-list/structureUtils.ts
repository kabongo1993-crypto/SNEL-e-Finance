import type { Structure } from '../../services/apiClient';

export const STRUCTURE_TYPES = [
  'ENTITE',
  'DEPARTEMENT',
  'DIRECTION',
  'DIVISION',
  'SERVICE',
  'SECTION',
  'AUTRE',
] as const;

export interface StructureTreeNode {
  structure: Structure;
  children: StructureTreeNode[];
}

export function extraireCodeAffichage(code: string): string {
  const parts = code
    .split('.')
    .map((p) => p.trim())
    .filter(Boolean);
  return parts.length === 0 ? code : (parts[parts.length - 1] ?? code);
}

export function libelleParent(structure: Structure): string {
  if (!structure.parentCode && !structure.parentLibelle) return '—';
  const code = extraireCodeAffichage(structure.parentCode ?? '');
  if (code && structure.parentLibelle) return `${code} — ${structure.parentLibelle}`;
  return structure.parentLibelle ?? code;
}

export function buildStructureTree(structures: Structure[]): StructureTreeNode[] {
  const map = new Map<number, StructureTreeNode>();
  for (const structure of structures) {
    map.set(structure.idStructure, { structure, children: [] });
  }

  const roots: StructureTreeNode[] = [];
  for (const node of map.values()) {
    const parentId = node.structure.parentId;
    if (parentId != null && map.has(parentId)) {
      map.get(parentId)!.children.push(node);
    } else {
      roots.push(node);
    }
  }

  const sortNodes = (nodes: StructureTreeNode[]) => {
    nodes.sort((a, b) => extraireCodeAffichage(a.structure.code).localeCompare(extraireCodeAffichage(b.structure.code), 'fr', { sensitivity: 'base' }));
    nodes.forEach((n) => sortNodes(n.children));
  };
  sortNodes(roots);
  return roots;
}

export function collectAncestorIds(structures: Structure[], id: number): number[] {
  const byId = new Map(structures.map((s) => [s.idStructure, s]));
  const ids: number[] = [];
  let current = byId.get(id);
  const seen = new Set<number>();
  while (current?.parentId != null && !seen.has(current.parentId)) {
    seen.add(current.parentId);
    ids.push(current.parentId);
    current = byId.get(current.parentId);
  }
  return ids;
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
