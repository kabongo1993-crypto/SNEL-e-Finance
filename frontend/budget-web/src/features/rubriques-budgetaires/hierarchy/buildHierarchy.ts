import type { RubriqueBudgetaire } from '../../../services/apiClient';
import type { RubriqueHierarchyNode } from './types';

/** Construit l’arbre Rupture → RB à partir du référentiel plat (FK parent). */
export function buildRubriqueHierarchy(items: RubriqueBudgetaire[]): RubriqueHierarchyNode[] {
  const map = new Map<number, RubriqueHierarchyNode>();
  for (const rubrique of items) {
    map.set(rubrique.idRB, { rubrique, children: [] });
  }

  const roots: RubriqueHierarchyNode[] = [];
  for (const node of map.values()) {
    const parentId = node.rubrique.parentId;
    if (parentId != null && map.has(parentId)) {
      map.get(parentId)!.children.push(node);
    } else {
      roots.push(node);
    }
  }

  const sortNodes = (nodes: RubriqueHierarchyNode[]) => {
    nodes.sort((a, b) => a.rubrique.codeRB.localeCompare(b.rubrique.codeRB, 'fr', { sensitivity: 'base' }));
    nodes.forEach((n) => sortNodes(n.children));
  };
  sortNodes(roots);
  return roots;
}

/** Conserve les ruptures parentes lorsqu’une RB (ou rupture) correspond au filtre. */
export function filterRubriqueHierarchy(
  nodes: RubriqueHierarchyNode[],
  predicate: (r: RubriqueBudgetaire) => boolean,
): RubriqueHierarchyNode[] {
  const walk = (list: RubriqueHierarchyNode[]): RubriqueHierarchyNode[] => {
    const result: RubriqueHierarchyNode[] = [];
    for (const node of list) {
      const children = walk(node.children);
      if (predicate(node.rubrique) || children.length > 0) {
        result.push({ rubrique: node.rubrique, children });
      }
    }
    return result;
  };
  return walk(nodes);
}

export function collectRubriqueAncestorIds(items: RubriqueBudgetaire[], id: number): number[] {
  const byId = new Map(items.map((r) => [r.idRB, r]));
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

/** Ids de ruptures à déplier pour exposer les nœuds filtrés. */
export function collectExpandedIdsForFilter(nodes: RubriqueHierarchyNode[]): Set<number> {
  const ids = new Set<number>();
  const walk = (list: RubriqueHierarchyNode[]) => {
    for (const node of list) {
      if (node.children.length > 0) {
        ids.add(node.rubrique.idRB);
        walk(node.children);
      }
    }
  };
  walk(nodes);
  return ids;
}

export function flattenRubriqueHierarchy(nodes: RubriqueHierarchyNode[]): RubriqueHierarchyNode[] {
  const out: RubriqueHierarchyNode[] = [];
  const walk = (list: RubriqueHierarchyNode[]) => {
    for (const node of list) {
      out.push(node);
      if (node.children.length) walk(node.children);
    }
  };
  walk(nodes);
  return out;
}
