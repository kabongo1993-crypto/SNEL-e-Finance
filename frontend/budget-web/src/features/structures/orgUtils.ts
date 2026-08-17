import type {
  StructureOrganisationnelle,
  UniteBudgetaireOrganisation,
} from '../../services/apiClient';

export interface TreeNode {
  structure: StructureOrganisationnelle;
  children: TreeNode[];
}

export type StructureTypeKey =
  | 'ENTITE'
  | 'DEPARTEMENT'
  | 'DIRECTION'
  | 'DIVISION'
  | 'SERVICE'
  | 'SECTION'
  | 'AUTRE'
  | 'UB';

export interface TypeVisual {
  key: StructureTypeKey;
  label: string;
  color: string;
  soft: string;
}

/** Subtle type palette for hierarchy (light/dark via CSS vars where possible). */
export const STRUCTURE_TYPE_VISUAL: Record<string, TypeVisual> = {
  ENTITE: { key: 'ENTITE', label: 'Entité', color: 'var(--ef-primary)', soft: 'var(--ef-primary-soft)' },
  DEPARTEMENT: { key: 'DEPARTEMENT', label: 'Département', color: 'var(--ef-info)', soft: 'rgba(27, 111, 165, 0.12)' },
  DIRECTION: { key: 'DIRECTION', label: 'Direction', color: '#6B5B95', soft: 'rgba(107, 91, 149, 0.12)' },
  DIVISION: { key: 'DIVISION', label: 'Division', color: 'var(--ef-success)', soft: 'rgba(30, 122, 70, 0.12)' },
  SERVICE: { key: 'SERVICE', label: 'Service', color: 'var(--ef-warning)', soft: 'rgba(183, 121, 31, 0.12)' },
  SECTION: { key: 'SECTION', label: 'Section', color: 'var(--ef-text-secondary)', soft: 'rgba(90, 107, 124, 0.12)' },
  AUTRE: { key: 'AUTRE', label: 'Autre', color: '#C47A3A', soft: 'rgba(196, 122, 58, 0.12)' },
  UB: { key: 'UB', label: 'Unité budgétaire', color: '#C47A3A', soft: 'rgba(196, 122, 58, 0.12)' },
};

export function getTypeVisual(type: string): TypeVisual {
  const key = type.toUpperCase();
  return STRUCTURE_TYPE_VISUAL[key] ?? STRUCTURE_TYPE_VISUAL.AUTRE;
}

export function buildTree(structures: StructureOrganisationnelle[]): TreeNode[] {
  const map = new Map<number, TreeNode>();
  for (const structure of structures) {
    map.set(structure.idStructure, { structure, children: [] });
  }

  const roots: TreeNode[] = [];
  for (const node of map.values()) {
    const parentId = node.structure.parentId;
    if (parentId != null && map.has(parentId)) {
      map.get(parentId)!.children.push(node);
    } else {
      roots.push(node);
    }
  }

  const sortNodes = (nodes: TreeNode[]) => {
    nodes.sort((a, b) => {
      const typeCmp = a.structure.typeStructure.localeCompare(b.structure.typeStructure);
      if (typeCmp !== 0) return typeCmp;
      return a.structure.code.localeCompare(b.structure.code, 'fr', { sensitivity: 'base' });
    });
    nodes.forEach((n) => sortNodes(n.children));
  };
  sortNodes(roots);
  return roots;
}

export function matchesSearch(
  structure: StructureOrganisationnelle,
  unites: UniteBudgetaireOrganisation[],
  query: string,
): boolean {
  if (!query) return true;
  const q = query.toLowerCase();
  if (
    structure.code.toLowerCase().includes(q) ||
    structure.codeTechnique.toLowerCase().includes(q) ||
    structure.libelle.toLowerCase().includes(q) ||
    structure.typeStructure.toLowerCase().includes(q) ||
    (structure.parentLibelle?.toLowerCase().includes(q) ?? false) ||
    (structure.departementLibelle?.toLowerCase().includes(q) ?? false)
  ) {
    return true;
  }

  return unites.some(
    (ub) =>
      ub.structureId === structure.idStructure &&
      (ub.codeUB.toLowerCase().includes(q) || ub.libelle.toLowerCase().includes(q)),
  );
}

export function filterTree(
  nodes: TreeNode[],
  unites: UniteBudgetaireOrganisation[],
  query: string,
  typeFilter: string,
  departementFilter: string,
): TreeNode[] {
  const walk = (list: TreeNode[]): TreeNode[] => {
    const result: TreeNode[] = [];
    for (const node of list) {
      const filteredChildren = walk(node.children);
      const matchesType =
        typeFilter === 'all' || node.structure.typeStructure.toUpperCase() === typeFilter.toUpperCase();
      const matchesDept =
        departementFilter === 'all' ||
        (node.structure.departementCode ?? '').toUpperCase() === departementFilter.toUpperCase() ||
        (node.structure.typeStructure.toUpperCase() === 'DEPARTEMENT' &&
          node.structure.code.toUpperCase() === departementFilter.toUpperCase());
      const matchesQuery = matchesSearch(node.structure, unites, query);

      // Keep node if it matches filters, or has matching descendants (for navigation)
      const selfMatch = matchesType && matchesDept && matchesQuery;
      if (selfMatch || filteredChildren.length > 0) {
        // When filtering by type/dept, still show ancestors path via children
        if (selfMatch || filteredChildren.length > 0) {
          result.push({ structure: node.structure, children: filteredChildren });
        }
      }
    }
    return result;
  };

  if (!query.trim() && typeFilter === 'all' && departementFilter === 'all') return nodes;
  return walk(nodes);
}

export function collectExpandIds(nodes: TreeNode[]): Set<number> {
  const ids = new Set<number>();
  const walk = (list: TreeNode[]) => {
    for (const node of list) {
      if (node.children.length > 0) {
        ids.add(node.structure.idStructure);
        walk(node.children);
      }
    }
  };
  walk(nodes);
  return ids;
}

export function findNode(nodes: TreeNode[], id: number): TreeNode | null {
  for (const node of nodes) {
    if (node.structure.idStructure === id) return node;
    const found = findNode(node.children, id);
    if (found) return found;
  }
  return null;
}

export function getAncestorChain(
  structures: StructureOrganisationnelle[],
  id: number,
): StructureOrganisationnelle[] {
  const byId = new Map(structures.map((s) => [s.idStructure, s]));
  const chain: StructureOrganisationnelle[] = [];
  let current = byId.get(id);
  while (current) {
    chain.unshift(current);
    current = current.parentId != null ? byId.get(current.parentId) : undefined;
  }
  return chain;
}

export function uniqueDepartements(structures: StructureOrganisationnelle[]) {
  const map = new Map<string, string>();
  for (const s of structures) {
    if (s.typeStructure.toUpperCase() === 'DEPARTEMENT') {
      map.set(s.code, s.libelle);
    } else if (s.departementCode) {
      map.set(s.departementCode, s.departementLibelle ?? s.departementCode);
    }
  }
  return [...map.entries()]
    .map(([code, libelle]) => ({ code, libelle }))
    .sort((a, b) => a.code.localeCompare(b.code, 'fr'));
}

export function uniqueTypes(structures: StructureOrganisationnelle[]) {
  const set = new Set(structures.map((s) => s.typeStructure.toUpperCase()));
  return [...set].sort();
}
