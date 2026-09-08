import type { ItemBI } from '../../services/apiClient';

export interface ItemBITreeNode {
  item: ItemBI;
  children: ItemBITreeNode[];
}

export function libelleParent(r: ItemBI): string {
  if (!r.parentCode && !r.parentLibelle) return '—';
  if (r.parentCode && r.parentLibelle) return `${r.parentCode} — ${r.parentLibelle}`;
  return r.parentLibelle ?? r.parentCode ?? '—';
}

export function buildItemBITree(items: ItemBI[]): ItemBITreeNode[] {
  const map = new Map<number, ItemBITreeNode>();
  for (const item of items) {
    map.set(item.idItemBI, { item, children: [] });
  }

  const roots: ItemBITreeNode[] = [];
  for (const node of map.values()) {
    const parentId = node.item.parentId;
    if (parentId != null && map.has(parentId)) {
      map.get(parentId)!.children.push(node);
    } else {
      roots.push(node);
    }
  }

  const sortNodes = (nodes: ItemBITreeNode[]) => {
    nodes.sort((a, b) => a.item.codeItem.localeCompare(b.item.codeItem, 'fr', { sensitivity: 'base' }));
    nodes.forEach((n) => sortNodes(n.children));
  };
  sortNodes(roots);
  return roots;
}

export function collectAncestorIds(items: ItemBI[], id: number): number[] {
  const byId = new Map(items.map((r) => [r.idItemBI, r]));
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
