import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  fetchDemandesPaiementCompteurs,
  type DemandePaiementCompteursDto,
  type DemandePaiementCompteursScope,
} from '../../services/apiClient';
import { apiErrorMessage } from './paiementUtils';

export type DemandePaiementCompteursParams = {
  scope: DemandePaiementCompteursScope;
  idExercice?: number;
  idUB?: number;
  idDepartement?: number;
  idCasDossier?: number;
  idTypeBudget?: number;
  idDemandeur?: number;
  reference?: string;
  beneficiaire?: string;
  dateDebut?: string;
  dateFin?: string;
};

export type UseDemandePaiementCompteursResult = {
  compteurs: DemandePaiementCompteursDto | null;
  loading: boolean;
  error: string | null;
  refresh: () => Promise<void>;
};

/**
 * Charge les compteurs DPM (filtres API + scope, sans statut).
 * Conserve les compteurs précédents pendant un changement de filtre.
 * Un changement de statut seul ne doit pas appeler ce hook — exclure statut des deps.
 */
export function useDemandePaiementCompteurs(
  params: DemandePaiementCompteursParams,
  options?: { enabled?: boolean; onError?: (message: string) => void },
): UseDemandePaiementCompteursResult {
  const enabled = options?.enabled ?? true;
  const onErrorRef = useRef(options?.onError);
  onErrorRef.current = options?.onError;
  const paramsKey = useMemo(() => JSON.stringify(params), [params]);
  const paramsRef = useRef(params);
  paramsRef.current = params;
  const [compteurs, setCompteurs] = useState<DemandePaiementCompteursDto | null>(null);
  const [loading, setLoading] = useState(enabled);
  const [error, setError] = useState<string | null>(null);
  const abortRef = useRef<AbortController | null>(null);
  const compteursRef = useRef(compteurs);
  compteursRef.current = compteurs;

  const refresh = useCallback(async () => {
    if (!enabled) return;
    abortRef.current?.abort();
    const controller = new AbortController();
    abortRef.current = controller;
    // Conserver les compteurs affichés pendant un rafraîchissement (évite le clignotement).
    if (!compteursRef.current) {
      setLoading(true);
    }
    setError(null);
    try {
      const data = await fetchDemandesPaiementCompteurs({
        ...paramsRef.current,
        signal: controller.signal,
      });
      if (!controller.signal.aborted) {
        setCompteurs(data);
      }
    } catch (err) {
      if (controller.signal.aborted) return;
      const message = apiErrorMessage(err, 'Impossible de charger les compteurs.');
      setError(message);
      onErrorRef.current?.(message);
      if (!compteursRef.current) {
        setCompteurs(null);
      }
    } finally {
      if (!controller.signal.aborted) {
        setLoading(false);
      }
    }
  }, [enabled, paramsKey]);

  useEffect(() => {
    if (!enabled) {
      setLoading(false);
      return;
    }
    void refresh();
    return () => abortRef.current?.abort();
  }, [enabled, paramsKey, refresh]);

  return { compteurs, loading, error, refresh };
}
