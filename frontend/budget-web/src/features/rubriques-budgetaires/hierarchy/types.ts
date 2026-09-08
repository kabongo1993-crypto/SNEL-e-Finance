import type { RubriqueBudgetaire } from '../../../services/apiClient';

/** Nœud d’arbre RB (référentiel réel via FK parent). */
export interface RubriqueHierarchyNode {
  rubrique: RubriqueBudgetaire;
  children: RubriqueHierarchyNode[];
}

/**
 * Une rupture = rubrique qui a des enfants (regroupement parent).
 * Une RB saisie = feuille (pas d’enfants).
 */
export function estRuptureRubrique(r: Pick<RubriqueBudgetaire, 'nombreEnfants'> | { children?: unknown[] }): boolean {
  if ('nombreEnfants' in r && typeof r.nombreEnfants === 'number') {
    return r.nombreEnfants > 0;
  }
  if ('children' in r && Array.isArray(r.children)) {
    return r.children.length > 0;
  }
  return false;
}

export function estRuptureNoeud(node: RubriqueHierarchyNode): boolean {
  return node.children.length > 0 || node.rubrique.nombreEnfants > 0;
}
