import type { RubriqueBudgetaire } from '../../services/apiClient';
import {
  buildRubriqueHierarchy,
  collectRubriqueAncestorIds,
  formatRubriqueParent,
  type RubriqueHierarchyNode,
} from './hierarchy';

/** @deprecated Prefer RubriqueHierarchyNode from ./hierarchy */
export type RubriqueTreeNode = RubriqueHierarchyNode;

export function libelleParent(r: RubriqueBudgetaire): string {
  return formatRubriqueParent(r);
}

export function buildRubriqueTree(items: RubriqueBudgetaire[]): RubriqueHierarchyNode[] {
  return buildRubriqueHierarchy(items);
}

export function collectAncestorIds(items: RubriqueBudgetaire[], id: number): number[] {
  return collectRubriqueAncestorIds(items, id);
}

export function extraireDateIso(value: string | null | undefined): string {
  if (!value) return '';
  const iso = value.slice(0, 10);
  return /^\d{4}-\d{2}-\d{2}$/.test(iso) ? iso : '';
}

export function formatDateFr(value: string | null | undefined): string {
  const iso = extraireDateIso(value);
  if (!iso) return '—';
  const [year, month, day] = iso.split('-');
  return `${day}/${month}/${year}`;
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
