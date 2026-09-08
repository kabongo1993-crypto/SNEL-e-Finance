import { useCallback, useEffect, useMemo } from 'react';
import { useSearchParams } from 'react-router-dom';
import { todayLocalIsoDate } from './paiementDateUtils';
import {
  buildDemandePaiementSearchParams,
  buildCanonicalSearchParams,
  parseDemandePaiementUrlFilters,
  searchParamsEqual,
  toCompteursApiParams,
  toListApiParams,
  type BuildSearchParamsPatch,
  type BudgetFileMode,
  type DemandePaiementUrlScope,
} from './demandePaiementUrlFilters';
import type { StatutNavValue } from './paiementStatutNavConfig';
import type { StatutDpm } from './paiementUtils';

export type UseDemandePaiementUrlFiltersOptions = {
  allowedStatuts?: readonly StatutDpm[];
};

export function useDemandePaiementUrlFilters(
  scope: DemandePaiementUrlScope,
  options?: UseDemandePaiementUrlFiltersOptions,
) {
  const [searchParams, setSearchParams] = useSearchParams();
  const today = todayLocalIsoDate();

  const normalizeOptions = useMemo(
    () => ({ today, allowedStatuts: options?.allowedStatuts }),
    [today, options?.allowedStatuts],
  );

  const filters = useMemo(
    () => parseDemandePaiementUrlFilters(scope, searchParams, normalizeOptions),
    [scope, searchParams, normalizeOptions],
  );

  const canonicalParams = useMemo(
    () => buildCanonicalSearchParams(scope, searchParams, normalizeOptions),
    [scope, searchParams, normalizeOptions],
  );

  useEffect(() => {
    if (!searchParamsEqual(searchParams, canonicalParams)) {
      setSearchParams(canonicalParams, { replace: true });
    }
  }, [searchParams, canonicalParams, setSearchParams]);

  const patchFilters = useCallback(
    (patch: BuildSearchParamsPatch, opts?: { replace?: boolean }) => {
      const next = buildDemandePaiementSearchParams(scope, searchParams, patch, { today });
      setSearchParams(next, { replace: opts?.replace ?? false });
    },
    [scope, searchParams, setSearchParams, today],
  );

  const listApiParams = useMemo(() => toListApiParams(filters), [filters]);
  const compteursApiParams = useMemo(() => toCompteursApiParams(filters), [filters]);

  const setStatut = useCallback(
    (value: StatutNavValue) => patchFilters({ statut: value }),
    [patchFilters],
  );

  const setDateDebut = useCallback(
    (value: string) => patchFilters({ dateDebut: value, explicitDates: true }),
    [patchFilters],
  );

  const setDateFin = useCallback(
    (value: string) => patchFilters({ dateFin: value, explicitDates: true }),
    [patchFilters],
  );

  const setIdExercice = useCallback(
    (value: string) => patchFilters({ idExercice: value }),
    [patchFilters],
  );

  const setIdUB = useCallback(
    (value: string) => patchFilters({ idUB: value }),
    [patchFilters],
  );

  const setIdDepartement = useCallback(
    (value: string) => patchFilters({ idDepartement: value }),
    [patchFilters],
  );

  const setIdCasDossier = useCallback(
    (value: string) => patchFilters({ idCasDossier: value }),
    [patchFilters],
  );

  const setIdTypeBudget = useCallback(
    (value: string) => patchFilters({ idTypeBudget: value }),
    [patchFilters],
  );

  const setIdDemandeur = useCallback(
    (value: string) => patchFilters({ idDemandeur: value }),
    [patchFilters],
  );

  const setReference = useCallback(
    (value: string) => patchFilters({ reference: value }),
    [patchFilters],
  );

  const setBeneficiaire = useCallback(
    (value: string) => patchFilters({ beneficiaire: value }),
    [patchFilters],
  );

  const setFiltreFile = useCallback(
    (mode: BudgetFileMode) => patchFilters({ file: mode }),
    [patchFilters],
  );

  return {
    filters,
    listApiParams,
    compteursApiParams,
    patchFilters,
    setStatut,
    setDateDebut,
    setDateFin,
    setIdExercice,
    setIdUB,
    setIdDepartement,
    setIdCasDossier,
    setIdTypeBudget,
    setIdDemandeur,
    setReference,
    setBeneficiaire,
    setFiltreFile,
  };
}
