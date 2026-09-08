import type { RubriqueBudgetaire } from '../../../services/apiClient';

export function formatRubriqueCodeLibelle(
  code: string | null | undefined,
  libelle: string | null | undefined,
): string {
  const c = (code ?? '').trim();
  const l = (libelle ?? '').trim();
  if (c && l) return `${c} — ${l}`;
  return l || c || '—';
}

export function formatRubrique(r: Pick<RubriqueBudgetaire, 'codeRB' | 'libelle'>): string {
  return formatRubriqueCodeLibelle(r.codeRB, r.libelle);
}

/** Libellé parent / rupture pour contexte (sélection, détail). */
export function formatRubriqueParent(r: Pick<RubriqueBudgetaire, 'parentCode' | 'parentLibelle'>): string {
  if (!r.parentCode && !r.parentLibelle) return '—';
  return formatRubriqueCodeLibelle(r.parentCode, r.parentLibelle);
}

/**
 * Contexte Rupture → RB pour affichages plats (autocomplete, chips…).
 * Ex. « 0011 — Matières consommées › 00113 — Huiles… »
 */
export function formatRubriqueAvecRupture(
  r: Pick<RubriqueBudgetaire, 'codeRB' | 'libelle' | 'parentCode' | 'parentLibelle' | 'nombreEnfants'>,
): string {
  const self = formatRubrique(r);
  if (r.nombreEnfants > 0 || (!r.parentCode && !r.parentLibelle)) return self;
  const rupture = formatRubriqueParent(r);
  if (rupture === '—') return self;
  return `${rupture} › ${self}`;
}
