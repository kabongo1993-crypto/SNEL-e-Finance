import { useCallback, useRef, useState } from 'react';
import {
  fetchDemandePaiementRetoursDestinataires,
  fetchDemandePaiementRoutage,
  type DemandePaiementRetourDestinataire,
  type DemandePaiementRoutage,
} from '../../services/apiClient';

export type DemandePaiementRoutageLectureState = {
  routages: DemandePaiementRoutage[];
  retoursDestinataires: DemandePaiementRetourDestinataire[];
  loading: boolean;
};

/**
 * Charge en lecture seule l'historique routage + destinataires de retour (Lots 3.6.2 / 3.6.3).
 * En cas d'erreur API, retourne des listes vides (fallback heuristique côté UI).
 */
export function useDemandePaiementRoutageLecture(
  idDemande: number | undefined,
  enabled = true,
): DemandePaiementRoutageLectureState & { reload: () => Promise<void> } {
  const [routages, setRoutages] = useState<DemandePaiementRoutage[]>([]);
  const [retoursDestinataires, setRetoursDestinataires] = useState<
    DemandePaiementRetourDestinataire[]
  >([]);
  const [loading, setLoading] = useState(false);
  const requestIdRef = useRef(0);

  const reload = useCallback(async () => {
    if (!idDemande || !enabled || Number.isNaN(idDemande)) {
      setRoutages([]);
      setRetoursDestinataires([]);
      setLoading(false);
      return;
    }

    const requestId = ++requestIdRef.current;
    setLoading(true);
    try {
      const [routageRows, retourRows] = await Promise.all([
        fetchDemandePaiementRoutage(idDemande),
        fetchDemandePaiementRetoursDestinataires(idDemande),
      ]);
      if (requestId !== requestIdRef.current) return;
      setRoutages(routageRows);
      setRetoursDestinataires(retourRows);
    } catch {
      if (requestId !== requestIdRef.current) return;
      setRoutages([]);
      setRetoursDestinataires([]);
    } finally {
      if (requestId === requestIdRef.current) {
        setLoading(false);
      }
    }
  }, [idDemande, enabled]);

  return { routages, retoursDestinataires, loading, reload };
}
