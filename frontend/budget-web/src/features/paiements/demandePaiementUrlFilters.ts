import { todayLocalIsoDate } from './paiementDateUtils';
import {
  normalizeStatutNavValue,
  type StatutNavValue,
} from './paiementStatutNavConfig';
import type { StatutDpm } from './paiementUtils';

const ISO_DATE_RE = /^\d{4}-\d{2}-\d{2}$/;

export type DemandePaiementUrlScope = 'mes-demandes' | 'charge-dpm' | 'budget';

export type BudgetFileMode = 'FILE' | 'TOUTES';

/** Paramètres communs liste + compteurs (sans statut). */
export type DemandePaiementApiFilterParams = {
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

export type MesDemandesUrlFilters = DemandePaiementApiFilterParams & {
  scope: 'mes-demandes';
  statut: StatutNavValue;
  dateDebut: string;
  dateFin: string;
  idExerciceUi: string;
  idUBUi: string;
  idCasDossierUi: string;
};

export type ChargeDpmUrlFilters = DemandePaiementApiFilterParams & {
  scope: 'charge-dpm';
  statut: StatutNavValue;
  idExerciceUi: string;
  idDepartementUi: string;
  idUBUi: string;
  idDemandeurUi: string;
  idCasDossierUi: string;
  referenceUi: string;
};

export type BudgetUrlFilters = DemandePaiementApiFilterParams & {
  scope: 'budget';
  filtreFile: BudgetFileMode;
  statut: StatutNavValue;
  dateDebut: string;
  dateFin: string;
  idExerciceUi: string;
  idDepartementUi: string;
  idUBUi: string;
  idCasDossierUi: string;
  idTypeBudgetUi: string;
  referenceUi: string;
  beneficiaireUi: string;
};

export type DemandePaiementUrlFilters =
  | MesDemandesUrlFilters
  | ChargeDpmUrlFilters
  | BudgetUrlFilters;

/** Parse un ID entier positif strict ; invalide → undefined. */
export function parseIdParam(value: string | null | undefined): number | undefined {
  if (value == null) return undefined;
  const trimmed = value.trim();
  if (!trimmed || !/^\d+$/.test(trimmed)) return undefined;
  const n = Number(trimmed);
  if (!Number.isSafeInteger(n) || n <= 0) return undefined;
  return n;
}

export function idParamToUiValue(id: number | undefined): string {
  return id !== undefined ? String(id) : '';
}

/** Date ISO valide ou undefined (absente/invalide). */
export function parseIsoDateParamStrict(value: string | null | undefined): string | undefined {
  if (!value) return undefined;
  const trimmed = value.trim();
  if (!ISO_DATE_RE.test(trimmed)) return undefined;
  return trimmed;
}

/** Date ISO avec repli métier (ex. aujourd'hui). */
export function parseIsoDateParam(
  value: string | null | undefined,
  fallback: string,
): string {
  return parseIsoDateParamStrict(value) ?? fallback;
}

export function parseBudgetFileParam(value: string | null | undefined): BudgetFileMode {
  const v = (value ?? '').trim().toLowerCase();
  if (v === 'toutes') return 'TOUTES';
  return 'FILE';
}

export function budgetFileToUrlParam(mode: BudgetFileMode): string {
  return mode === 'TOUTES' ? 'toutes' : 'active';
}

export function parseReferenceParam(value: string | null | undefined): string | undefined {
  const trimmed = (value ?? '').trim();
  return trimmed || undefined;
}

export function removeEmptySearchParams(params: URLSearchParams): URLSearchParams {
  const out = new URLSearchParams();
  for (const [key, value] of params.entries()) {
    if (value.trim()) out.set(key, value.trim());
  }
  return out;
}

export function serializeSearchParams(params: URLSearchParams): string {
  return [...params.entries()]
    .sort(([a], [b]) => a.localeCompare(b))
    .map(([k, v]) => `${k}=${v}`)
    .join('&');
}

export function searchParamsEqual(a: URLSearchParams, b: URLSearchParams): boolean {
  return serializeSearchParams(a) === serializeSearchParams(b);
}

function setOrDelete(params: URLSearchParams, key: string, value: string | undefined): void {
  if (value === undefined || value.trim() === '') params.delete(key);
  else params.set(key, value.trim());
}

function shouldPersistMesDemandesDates(input: {
  statut: StatutNavValue;
  dateDebut: string;
  dateFin: string;
  today: string;
  explicitDates?: boolean;
}): boolean {
  if (input.explicitDates) return true;
  if (input.statut) return true;
  return input.dateDebut !== input.today || input.dateFin !== input.today;
}

function shouldPersistBudgetDates(input: {
  dateDebut: string;
  dateFin: string;
  today: string;
  explicitDates?: boolean;
}): boolean {
  if (input.explicitDates) return true;
  return input.dateDebut !== input.today || input.dateFin !== input.today;
}

export type BuildSearchParamsPatch = {
  statut?: StatutNavValue;
  dateDebut?: string;
  dateFin?: string;
  idExercice?: string;
  idUB?: string;
  idDepartement?: string;
  idCasDossier?: string;
  idTypeBudget?: string;
  idDemandeur?: string;
  reference?: string;
  beneficiaire?: string;
  file?: BudgetFileMode;
  /** Force l'écriture des dates (modification utilisateur explicite). */
  explicitDates?: boolean;
};

export function buildMesDemandesSearchParams(
  current: URLSearchParams,
  patch: BuildSearchParamsPatch,
  options?: { today?: string },
): URLSearchParams {
  const today = options?.today ?? todayLocalIsoDate();
  const merged = new URLSearchParams(current);

  const statut =
    patch.statut !== undefined
      ? patch.statut
      : normalizeStatutNavValue(merged.get('statut'));
  const dateDebut =
    patch.dateDebut !== undefined
      ? patch.dateDebut
      : parseIsoDateParam(merged.get('dateDebut'), today);
  const dateFin =
    patch.dateFin !== undefined
      ? patch.dateFin
      : parseIsoDateParam(merged.get('dateFin'), today);

  setOrDelete(merged, 'statut', statut || undefined);

  if (
    shouldPersistMesDemandesDates({
      statut,
      dateDebut,
      dateFin,
      today,
      explicitDates: patch.explicitDates,
    })
  ) {
    setOrDelete(merged, 'dateDebut', dateDebut);
    setOrDelete(merged, 'dateFin', dateFin);
  } else {
    merged.delete('dateDebut');
    merged.delete('dateFin');
  }

  if (patch.idExercice !== undefined) setOrDelete(merged, 'idExercice', patch.idExercice);
  if (patch.idUB !== undefined) setOrDelete(merged, 'idUB', patch.idUB);
  if (patch.idCasDossier !== undefined) setOrDelete(merged, 'idCasDossier', patch.idCasDossier);

  merged.delete('idDepartement');
  merged.delete('idTypeBudget');
  merged.delete('idDemandeur');
  merged.delete('reference');
  merged.delete('beneficiaire');
  merged.delete('file');

  return removeEmptySearchParams(merged);
}

export function buildChargeDpmSearchParams(
  current: URLSearchParams,
  patch: BuildSearchParamsPatch,
): URLSearchParams {
  const merged = new URLSearchParams(current);

  if (patch.statut !== undefined) {
    setOrDelete(merged, 'statut', patch.statut || undefined);
  }

  if (patch.idExercice !== undefined) setOrDelete(merged, 'idExercice', patch.idExercice);
  if (patch.idDepartement !== undefined) setOrDelete(merged, 'idDepartement', patch.idDepartement);
  if (patch.idUB !== undefined) setOrDelete(merged, 'idUB', patch.idUB);
  if (patch.idDemandeur !== undefined) setOrDelete(merged, 'idDemandeur', patch.idDemandeur);
  if (patch.idCasDossier !== undefined) setOrDelete(merged, 'idCasDossier', patch.idCasDossier);
  if (patch.reference !== undefined) setOrDelete(merged, 'reference', patch.reference);

  merged.delete('dateDebut');
  merged.delete('dateFin');
  merged.delete('idTypeBudget');
  merged.delete('beneficiaire');
  merged.delete('file');

  return removeEmptySearchParams(merged);
}

export function buildBudgetSearchParams(
  current: URLSearchParams,
  patch: BuildSearchParamsPatch,
  options?: { today?: string },
): URLSearchParams {
  const today = options?.today ?? todayLocalIsoDate();
  const merged = new URLSearchParams(current);

  if (patch.file !== undefined) {
    setOrDelete(merged, 'file', budgetFileToUrlParam(patch.file));
  }

  if (patch.statut !== undefined) {
    setOrDelete(merged, 'statut', patch.statut || undefined);
  }

  const dateDebut =
    patch.dateDebut !== undefined
      ? patch.dateDebut
      : parseIsoDateParam(merged.get('dateDebut'), today);
  const dateFin =
    patch.dateFin !== undefined
      ? patch.dateFin
      : parseIsoDateParam(merged.get('dateFin'), today);

  if (
    shouldPersistBudgetDates({
      dateDebut,
      dateFin,
      today,
      explicitDates: patch.explicitDates,
    })
  ) {
    setOrDelete(merged, 'dateDebut', dateDebut);
    setOrDelete(merged, 'dateFin', dateFin);
  } else {
    merged.delete('dateDebut');
    merged.delete('dateFin');
  }

  if (patch.idExercice !== undefined) setOrDelete(merged, 'idExercice', patch.idExercice);
  if (patch.idDepartement !== undefined) setOrDelete(merged, 'idDepartement', patch.idDepartement);
  if (patch.idUB !== undefined) setOrDelete(merged, 'idUB', patch.idUB);
  if (patch.idCasDossier !== undefined) setOrDelete(merged, 'idCasDossier', patch.idCasDossier);
  if (patch.idTypeBudget !== undefined) setOrDelete(merged, 'idTypeBudget', patch.idTypeBudget);
  if (patch.reference !== undefined) setOrDelete(merged, 'reference', patch.reference);
  if (patch.beneficiaire !== undefined) setOrDelete(merged, 'beneficiaire', patch.beneficiaire);

  merged.delete('idDemandeur');

  return removeEmptySearchParams(merged);
}

export function buildDemandePaiementSearchParams(
  scope: DemandePaiementUrlScope,
  current: URLSearchParams,
  patch: BuildSearchParamsPatch,
  options?: { today?: string },
): URLSearchParams {
  switch (scope) {
    case 'mes-demandes':
      return buildMesDemandesSearchParams(current, patch, options);
    case 'charge-dpm':
      return buildChargeDpmSearchParams(current, patch);
    case 'budget':
      return buildBudgetSearchParams(current, patch, options);
  }
}

export type NormalizeUrlOptions = {
  today?: string;
  allowedStatuts?: readonly StatutDpm[];
  /** Budget : tous les statuts DPM valides (hors normalisation role-aware). */
  budgetStatut?: boolean;
};

function canonicalStatutFromRaw(
  raw: string | null,
  options: NormalizeUrlOptions,
): StatutNavValue {
  if (options.budgetStatut) {
    return normalizeStatutNavValue(raw);
  }
  return normalizeStatutNavValue(raw, { allowed: options.allowedStatuts });
}

function buildCanonicalMesDemandesParams(
  searchParams: URLSearchParams,
  options: NormalizeUrlOptions,
): URLSearchParams {
  const today = options.today ?? todayLocalIsoDate();
  const statut = canonicalStatutFromRaw(searchParams.get('statut'), options);
  const dateDebutRaw = parseIsoDateParamStrict(searchParams.get('dateDebut'));
  const dateFinRaw = parseIsoDateParamStrict(searchParams.get('dateFin'));
  const dateDebut = dateDebutRaw ?? today;
  const dateFin = dateFinRaw ?? today;

  const params = new URLSearchParams();
  if (statut) params.set('statut', statut);

  if (shouldPersistMesDemandesDates({ statut, dateDebut, dateFin, today })) {
    if (dateDebutRaw) params.set('dateDebut', dateDebutRaw);
    if (dateFinRaw) params.set('dateFin', dateFinRaw);
    if (statut && !dateDebutRaw && !dateFinRaw) {
      params.set('dateDebut', dateDebut);
      params.set('dateFin', dateFin);
    }
  }

  const idExercice = parseIdParam(searchParams.get('idExercice'));
  const idUB = parseIdParam(searchParams.get('idUB'));
  const idCasDossier = parseIdParam(searchParams.get('idCasDossier'));
  if (idExercice !== undefined) params.set('idExercice', String(idExercice));
  if (idUB !== undefined) params.set('idUB', String(idUB));
  if (idCasDossier !== undefined) params.set('idCasDossier', String(idCasDossier));

  return removeEmptySearchParams(params);
}

function buildCanonicalChargeDpmParams(
  searchParams: URLSearchParams,
  options: NormalizeUrlOptions,
): URLSearchParams {
  const statut = canonicalStatutFromRaw(searchParams.get('statut'), options);
  const params = new URLSearchParams();
  if (statut) params.set('statut', statut);

  const idExercice = parseIdParam(searchParams.get('idExercice'));
  const idDepartement = parseIdParam(searchParams.get('idDepartement'));
  const idUB = parseIdParam(searchParams.get('idUB'));
  const idDemandeur = parseIdParam(searchParams.get('idDemandeur'));
  const idCasDossier = parseIdParam(searchParams.get('idCasDossier'));
  const reference = parseReferenceParam(searchParams.get('reference'));

  if (idExercice !== undefined) params.set('idExercice', String(idExercice));
  if (idDepartement !== undefined) params.set('idDepartement', String(idDepartement));
  if (idUB !== undefined) params.set('idUB', String(idUB));
  if (idDemandeur !== undefined) params.set('idDemandeur', String(idDemandeur));
  if (idCasDossier !== undefined) params.set('idCasDossier', String(idCasDossier));
  if (reference) params.set('reference', reference);

  return removeEmptySearchParams(params);
}

function buildCanonicalBudgetParams(
  searchParams: URLSearchParams,
  options: NormalizeUrlOptions,
): URLSearchParams {
  const today = options.today ?? todayLocalIsoDate();
  const filtreFile = parseBudgetFileParam(searchParams.get('file'));
  const statut = canonicalStatutFromRaw(searchParams.get('statut'), {
    ...options,
    budgetStatut: true,
  });

  const dateDebutRaw = parseIsoDateParamStrict(searchParams.get('dateDebut'));
  const dateFinRaw = parseIsoDateParamStrict(searchParams.get('dateFin'));
  const dateDebut = dateDebutRaw ?? today;
  const dateFin = dateFinRaw ?? today;

  const params = new URLSearchParams();
  if (filtreFile === 'TOUTES') params.set('file', 'toutes');

  if (statut) params.set('statut', statut);

  if (shouldPersistBudgetDates({ dateDebut, dateFin, today })) {
    if (dateDebutRaw) params.set('dateDebut', dateDebutRaw);
    if (dateFinRaw) params.set('dateFin', dateFinRaw);
  }

  const idExercice = parseIdParam(searchParams.get('idExercice'));
  const idDepartement = parseIdParam(searchParams.get('idDepartement'));
  const idUB = parseIdParam(searchParams.get('idUB'));
  const idCasDossier = parseIdParam(searchParams.get('idCasDossier'));
  const idTypeBudget = parseIdParam(searchParams.get('idTypeBudget'));
  const reference = parseReferenceParam(searchParams.get('reference'));
  const beneficiaire = parseReferenceParam(searchParams.get('beneficiaire'));

  if (idExercice !== undefined) params.set('idExercice', String(idExercice));
  if (idDepartement !== undefined) params.set('idDepartement', String(idDepartement));
  if (idUB !== undefined) params.set('idUB', String(idUB));
  if (idCasDossier !== undefined) params.set('idCasDossier', String(idCasDossier));
  if (idTypeBudget !== undefined) params.set('idTypeBudget', String(idTypeBudget));
  if (reference) params.set('reference', reference);
  if (beneficiaire) params.set('beneficiaire', beneficiaire);

  return removeEmptySearchParams(params);
}

export function buildCanonicalSearchParams(
  scope: DemandePaiementUrlScope,
  searchParams: URLSearchParams,
  options?: NormalizeUrlOptions,
): URLSearchParams {
  switch (scope) {
    case 'mes-demandes':
      return buildCanonicalMesDemandesParams(searchParams, options ?? {});
    case 'charge-dpm':
      return buildCanonicalChargeDpmParams(searchParams, options ?? {});
    case 'budget':
      return buildCanonicalBudgetParams(searchParams, options ?? {});
  }
}

export function parseMesDemandesUrlFilters(
  searchParams: URLSearchParams,
  options?: NormalizeUrlOptions,
): MesDemandesUrlFilters {
  const today = options?.today ?? todayLocalIsoDate();
  const canonical = buildCanonicalMesDemandesParams(searchParams, options ?? {});
  const statut = canonicalStatutFromRaw(searchParams.get('statut'), options ?? {});
  const dateDebut = parseIsoDateParam(canonical.get('dateDebut') ?? searchParams.get('dateDebut'), today);
  const dateFin = parseIsoDateParam(canonical.get('dateFin') ?? searchParams.get('dateFin'), today);

  const idExercice = parseIdParam(canonical.get('idExercice'));
  const idUB = parseIdParam(canonical.get('idUB'));
  const idCasDossier = parseIdParam(canonical.get('idCasDossier'));

  return {
    scope: 'mes-demandes',
    statut,
    dateDebut,
    dateFin,
    idExercice,
    idUB,
    idCasDossier,
    idExerciceUi: idParamToUiValue(idExercice),
    idUBUi: idParamToUiValue(idUB),
    idCasDossierUi: idParamToUiValue(idCasDossier),
  };
}

export function parseChargeDpmUrlFilters(
  searchParams: URLSearchParams,
  options?: NormalizeUrlOptions,
): ChargeDpmUrlFilters {
  const canonical = buildCanonicalChargeDpmParams(searchParams, options ?? {});
  const statut = canonicalStatutFromRaw(searchParams.get('statut'), options ?? {});

  const idExercice = parseIdParam(canonical.get('idExercice'));
  const idDepartement = parseIdParam(canonical.get('idDepartement'));
  const idUB = parseIdParam(canonical.get('idUB'));
  const idDemandeur = parseIdParam(canonical.get('idDemandeur'));
  const idCasDossier = parseIdParam(canonical.get('idCasDossier'));
  const reference = parseReferenceParam(canonical.get('reference'));

  return {
    scope: 'charge-dpm',
    statut,
    idExercice,
    idDepartement,
    idUB,
    idDemandeur,
    idCasDossier,
    reference,
    idExerciceUi: idParamToUiValue(idExercice),
    idDepartementUi: idParamToUiValue(idDepartement),
    idUBUi: idParamToUiValue(idUB),
    idDemandeurUi: idParamToUiValue(idDemandeur),
    idCasDossierUi: idParamToUiValue(idCasDossier),
    referenceUi: reference ?? '',
  };
}

export function parseBudgetUrlFilters(
  searchParams: URLSearchParams,
  options?: NormalizeUrlOptions,
): BudgetUrlFilters {
  const today = options?.today ?? todayLocalIsoDate();
  const canonical = buildCanonicalBudgetParams(searchParams, options ?? {});
  const filtreFile = parseBudgetFileParam(canonical.get('file') ?? searchParams.get('file'));
  const statut = canonicalStatutFromRaw(searchParams.get('statut'), {
    ...options,
    budgetStatut: true,
  });
  const dateDebut = parseIsoDateParam(canonical.get('dateDebut') ?? searchParams.get('dateDebut'), today);
  const dateFin = parseIsoDateParam(canonical.get('dateFin') ?? searchParams.get('dateFin'), today);

  const idExercice = parseIdParam(canonical.get('idExercice'));
  const idDepartement = parseIdParam(canonical.get('idDepartement'));
  const idUB = parseIdParam(canonical.get('idUB'));
  const idCasDossier = parseIdParam(canonical.get('idCasDossier'));
  const idTypeBudget = parseIdParam(canonical.get('idTypeBudget'));
  const reference = parseReferenceParam(canonical.get('reference'));
  const beneficiaire = parseReferenceParam(canonical.get('beneficiaire'));

  return {
    scope: 'budget',
    filtreFile,
    statut,
    dateDebut,
    dateFin,
    idExercice,
    idDepartement,
    idUB,
    idCasDossier,
    idTypeBudget,
    reference,
    beneficiaire,
    idExerciceUi: idParamToUiValue(idExercice),
    idDepartementUi: idParamToUiValue(idDepartement),
    idUBUi: idParamToUiValue(idUB),
    idCasDossierUi: idParamToUiValue(idCasDossier),
    idTypeBudgetUi: idParamToUiValue(idTypeBudget),
    referenceUi: reference ?? '',
    beneficiaireUi: beneficiaire ?? '',
  };
}

export function parseDemandePaiementUrlFilters(
  scope: DemandePaiementUrlScope,
  searchParams: URLSearchParams,
  options?: NormalizeUrlOptions,
): DemandePaiementUrlFilters {
  switch (scope) {
    case 'mes-demandes':
      return parseMesDemandesUrlFilters(searchParams, options);
    case 'charge-dpm':
      return parseChargeDpmUrlFilters(searchParams, options);
    case 'budget':
      return parseBudgetUrlFilters(searchParams, options);
  }
}

/** Paramètres API liste (inclut statut si défini). */
export function toListApiParams(filters: DemandePaiementUrlFilters): DemandePaiementApiFilterParams & {
  statut?: StatutDpm;
  scope: DemandePaiementUrlScope;
} {
  const base: DemandePaiementApiFilterParams & { statut?: StatutDpm; scope: DemandePaiementUrlScope } = {
    scope: filters.scope,
    idExercice: filters.idExercice,
    idUB: filters.idUB,
    idDepartement: filters.idDepartement,
    idCasDossier: filters.idCasDossier,
    idTypeBudget: filters.idTypeBudget,
    idDemandeur: filters.idDemandeur,
    reference: filters.reference,
    beneficiaire: filters.beneficiaire,
  };

  if (filters.scope === 'mes-demandes' || filters.scope === 'budget') {
    base.dateDebut = filters.dateDebut;
    base.dateFin = filters.dateFin;
  }

  if (filters.statut) {
    base.statut = filters.statut;
  }

  return base;
}

/** Paramètres API compteurs (sans statut). */
export function toCompteursApiParams(
  filters: DemandePaiementUrlFilters,
): DemandePaiementApiFilterParams {
  const { statut: _s, ...rest } = toListApiParams(filters);
  void _s;
  return rest;
}

// --- Compatibilité Étape 8 (re-export helpers utilisés par selftests existants) ---

export function buildStatutOnlySearchParams(statut: StatutNavValue): URLSearchParams {
  return buildChargeDpmSearchParams(new URLSearchParams(), { statut });
}

export function buildDemandePaiementListSearchParams(input: {
  statut: StatutNavValue;
  dateDebut: string;
  dateFin: string;
  today?: string;
}): URLSearchParams {
  const today = input.today ?? todayLocalIsoDate();
  return buildMesDemandesSearchParams(new URLSearchParams(), {
    statut: input.statut,
    dateDebut: input.dateDebut,
    dateFin: input.dateFin,
    explicitDates: input.statut !== '' || input.dateDebut !== today || input.dateFin !== today,
  }, { today });
}
