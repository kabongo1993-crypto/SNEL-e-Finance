import type { RubriqueBudgetaire } from '../../../services/apiClient';

/** Groupe métier niveau 1 (rupture affichable) — clé = idGroupeRB. */
export interface RubriqueGroupeNiveau1 {
  idGroupeRB: number;
  codeGroupe: string;
  libelleGroupe: string;
  ordreAffichage: number;
  rubriques: RubriqueBudgetaire[];
}

/**
 * Construit la présentation métier : Groupe N1 → RB feuilles.
 * - Regroupe strictement par idGroupeRB (pas par codeGroupe : 02 A ≠ 02 B).
 * - Ignore les sections techniques sans groupe (0010, 0011…).
 * - N’utilise jamais le préfixe de CodeRB.
 * - Si `groupesReferentiel` est fourni, tous les groupes apparaissent (même sans RB).
 */
export function buildRubriqueGroupesNiveau1(
  items: RubriqueBudgetaire[],
  groupesReferentiel?: Array<{
    idGroupeRB: number;
    codeGroupe: string;
    libelle: string;
    ordreAffichage: number;
  }>,
): RubriqueGroupeNiveau1[] {
  const map = new Map<number, RubriqueGroupeNiveau1>();

  if (groupesReferentiel) {
    for (const g of groupesReferentiel) {
      map.set(g.idGroupeRB, {
        idGroupeRB: g.idGroupeRB,
        codeGroupe: g.codeGroupe,
        libelleGroupe: g.libelle,
        ordreAffichage: g.ordreAffichage,
        rubriques: [],
      });
    }
  }

  for (const rb of items) {
    const idGroupe = rb.idGroupeRB;
    if (idGroupe == null) continue;

    let groupe = map.get(idGroupe);
    if (!groupe) {
      groupe = {
        idGroupeRB: idGroupe,
        codeGroupe: rb.codeGroupe ?? '',
        libelleGroupe: rb.libelleGroupe ?? '',
        ordreAffichage: rb.ordreAffichageGroupe ?? 0,
        rubriques: [],
      };
      map.set(idGroupe, groupe);
    }
    groupe.rubriques.push(rb);
  }

  const groupes = Array.from(map.values());
  for (const g of groupes) {
    g.rubriques.sort((a, b) => a.codeRB.localeCompare(b.codeRB, 'fr', { sensitivity: 'base' }));
  }

  groupes.sort((a, b) => {
    if (a.ordreAffichage !== b.ordreAffichage) return a.ordreAffichage - b.ordreAffichage;
    const byCode = a.codeGroupe.localeCompare(b.codeGroupe, 'fr', { sensitivity: 'base' });
    if (byCode !== 0) return byCode;
    return a.libelleGroupe.localeCompare(b.libelleGroupe, 'fr', { sensitivity: 'base' });
  });

  return groupes;
}

/**
 * Filtre référentiel.
 * - Sans recherche : tous les groupes (y compris vides), RB filtrées par statut
 * - RB trouvée → groupe + RB matchées
 * - Recherche groupe → groupe + RB (statut)
 */
export function filterGroupesNiveau1ForReferentiel(
  groupes: RubriqueGroupeNiveau1[],
  options: {
    search: string;
    matchesStatut: (r: RubriqueBudgetaire) => boolean;
  },
): RubriqueGroupeNiveau1[] {
  const q = options.search.trim().toLowerCase();
  const result: RubriqueGroupeNiveau1[] = [];

  for (const groupe of groupes) {
    const groupMatchesSearch =
      !q ||
      groupe.codeGroupe.toLowerCase().includes(q) ||
      groupe.libelleGroupe.toLowerCase().includes(q);

    if (!q) {
      result.push({
        ...groupe,
        rubriques: groupe.rubriques.filter(options.matchesStatut),
      });
      continue;
    }

    if (groupMatchesSearch) {
      result.push({
        ...groupe,
        rubriques: groupe.rubriques.filter(options.matchesStatut),
      });
      continue;
    }

    const rubriques = groupe.rubriques.filter(
      (r) =>
        options.matchesStatut(r) &&
        (r.codeRB.toLowerCase().includes(q) || r.libelle.toLowerCase().includes(q)),
    );
    if (rubriques.length > 0) {
      result.push({ ...groupe, rubriques });
    }
  }

  return result;
}

export function countRubriquesInGroupes(groupes: RubriqueGroupeNiveau1[]): number {
  return groupes.reduce((n, g) => n + g.rubriques.length, 0);
}

export function formatGroupeNiveau1(g: Pick<RubriqueGroupeNiveau1, 'codeGroupe' | 'libelleGroupe'>): string {
  const c = (g.codeGroupe ?? '').trim();
  const l = (g.libelleGroupe ?? '').trim();
  if (c && l) return `${c} — ${l}`;
  return l || c || '—';
}
