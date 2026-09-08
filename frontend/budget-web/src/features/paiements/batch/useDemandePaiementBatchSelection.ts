import { useCallback, useEffect, useMemo, useState } from 'react';
import type { StatutNavValue } from '../paiementStatutNavConfig';
import {
  isBatchSelectableStatut,
  type DemandePaiementBatchScope,
} from './demandePaiementBatchConfig';

export function useDemandePaiementBatchSelection(
  statutFiltre: StatutNavValue,
  scope: DemandePaiementBatchScope = 'mes-demandes',
) {
  const [selectedIds, setSelectedIds] = useState<Set<number>>(() => new Set());

  const enabled = isBatchSelectableStatut(statutFiltre, scope);

  useEffect(() => {
    setSelectedIds(new Set());
  }, [statutFiltre]);

  const toggle = useCallback(
    (id: number) => {
      if (!enabled) return;
      setSelectedIds((prev) => {
        const next = new Set(prev);
        if (next.has(id)) next.delete(id);
        else next.add(id);
        return next;
      });
    },
    [enabled],
  );

  const setAll = useCallback(
    (ids: number[]) => {
      if (!enabled) {
        setSelectedIds(new Set());
        return;
      }
      setSelectedIds(new Set(ids));
    },
    [enabled],
  );

  const clear = useCallback(() => setSelectedIds(new Set()), []);

  const removeIds = useCallback((ids: Iterable<number>) => {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      for (const id of ids) next.delete(id);
      return next;
    });
  }, []);

  const selectedCount = selectedIds.size;
  const selectedList = useMemo(() => [...selectedIds], [selectedIds]);

  return {
    enabled,
    selectedIds,
    selectedList,
    selectedCount,
    toggle,
    setAll,
    clear,
    removeIds,
    isSelected: (id: number) => selectedIds.has(id),
  };
}

export type DemandePaiementBatchSelection = ReturnType<typeof useDemandePaiementBatchSelection>;