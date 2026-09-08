export function formatMontantBudget(value: number): string {
  return new Intl.NumberFormat('fr-FR', {
    minimumFractionDigits: 0,
    maximumFractionDigits: 2,
  }).format(value);
}

export { parseMontantInput } from '../../components/numericInput';

export function moisMontant(
  repartitions: { mois: number; montant: number }[],
  mois: number,
): number {
  return repartitions.find((r) => r.mois === mois)?.montant ?? 0;
}

export function setMoisMontant(
  repartitions: { mois: number; montant: number }[],
  mois: number,
  montant: number,
): { mois: number; montant: number }[] {
  const next = repartitions.filter((r) => r.mois !== mois);
  if (montant !== 0) next.push({ mois, montant });
  return next.sort((a, b) => a.mois - b.mois);
}

export function cumulRepartitions(repartitions: { mois: number; montant: number }[]): number {
  return repartitions.reduce((s, r) => s + r.montant, 0);
}

export const MOIS_LABELS = [
  'Jan',
  'Fév',
  'Mar',
  'Avr',
  'Mai',
  'Juin',
  'Juil',
  'Août',
  'Sep',
  'Oct',
  'Nov',
  'Déc',
] as const;

export function contexteComplet(params: {
  idExercice: string;
  idVersion: string;
  idTypeBudget: string;
  idModePrevision: string;
  idUB: string;
  codeType: string;
  actionAE: string;
  idItemBI: string;
}): boolean {
  const base =
    !!params.idExercice &&
    !!params.idVersion &&
    !!params.idTypeBudget &&
    !!params.idModePrevision &&
    !!params.idUB;
  if (!base) return false;
  if (params.codeType === 'AE') return params.actionAE.trim().length > 0;
  if (params.codeType === 'BI') return !!params.idItemBI;
  return true;
}

export type PrevisionBudgetaireResume = {
  idPrevision: number;
  idTypeBudget: number;
  codeType: string;
  idModePrevision: number;
  codeMode: string;
  libelleItemAE?: string | null;
  idItemBI?: number | null;
  idGroupeItemAE?: number | null;
  dateCreation: string;
  dateModification?: string | null;
};

export type ContextePrevisionResolu = {
  idTypeBudget: number;
  idModePrevision: number;
  codeType: string;
  codeMode: string;
  libelleItemAE?: string | null;
  idItemBI?: number | null;
  idGroupeItemAE?: number | null;
};

function contextePrevisionKey(p: PrevisionBudgetaireResume): string {
  return [
    p.idTypeBudget,
    p.idModePrevision,
    (p.libelleItemAE ?? '').trim(),
    p.idItemBI ?? '',
  ].join('|');
}

function previsionTimestamp(p: PrevisionBudgetaireResume): number {
  const raw = p.dateModification ?? p.dateCreation;
  const t = Date.parse(raw);
  return Number.isFinite(t) ? t : 0;
}

/** Restaure type/mode (et contexte AE/BI) à partir des lignes déjà enregistrées. */
export function resolveContexteDepuisPrevisions(
  previsions: readonly PrevisionBudgetaireResume[],
): ContextePrevisionResolu | null {
  if (!previsions.length) return null;

  const groups = new Map<string, PrevisionBudgetaireResume[]>();
  for (const p of previsions) {
    const k = contextePrevisionKey(p);
    const arr = groups.get(k) ?? [];
    arr.push(p);
    groups.set(k, arr);
  }

  let best: PrevisionBudgetaireResume[] | null = null;
  for (const arr of groups.values()) {
    if (!best || arr.length > best.length) {
      best = arr;
      continue;
    }
    if (arr.length < best.length) continue;
    const latestArr = Math.max(...arr.map(previsionTimestamp));
    const latestBest = Math.max(...best.map(previsionTimestamp));
    if (latestArr > latestBest) best = arr;
  }

  const pick = best!.reduce((a, b) => (previsionTimestamp(a) >= previsionTimestamp(b) ? a : b));
  return {
    idTypeBudget: pick.idTypeBudget,
    idModePrevision: pick.idModePrevision,
    codeType: pick.codeType.toUpperCase(),
    codeMode: pick.codeMode.toUpperCase(),
    libelleItemAE: pick.libelleItemAE,
    idItemBI: pick.idItemBI,
    idGroupeItemAE: pick.idGroupeItemAE,
  };
}

/**
 * Recalcule les totaux des lignes Groupe N1 (estSection) à partir des RB feuilles
 * du même idGroupeRB. Ne reconstruit jamais le groupe via parentId / préfixe code.
 *
 * Mode mensuel : montantAnnuel du groupe = somme des montants annuels feuilles
 * (y compris annuel sans répartition) ; cumulMensuel = somme des mois.
 */
export function agregatSections<
  T extends {
    idRB: number | null;
    idGroupeRB?: number | null;
    parentIdRB: number | null;
    estSection: boolean;
    montantAnnuel: number;
    cumulMensuel: number;
    repartitions: { mois: number; montant: number }[];
  },
>(lignes: T[], modeMensuel: boolean): T[] {
  return lignes.map((ligne) => {
    if (!ligne.estSection || ligne.idGroupeRB == null) return ligne;

    const feuilles = lignes.filter(
      (l) => !l.estSection && l.idRB != null && l.idGroupeRB === ligne.idGroupeRB,
    );

    const montantAnnuel = feuilles.reduce((s, d) => s + d.montantAnnuel, 0);

    if (modeMensuel) {
      const reps = Array.from({ length: 12 }, (_, i) => {
        const mois = i + 1;
        const montant = feuilles.reduce((s, d) => s + moisMontant(d.repartitions, mois), 0);
        return { mois, montant };
      }).filter((r) => r.montant !== 0);
      const cumul = cumulRepartitions(reps);
      return { ...ligne, repartitions: reps, cumulMensuel: cumul, montantAnnuel };
    }

    return { ...ligne, montantAnnuel, cumulMensuel: montantAnnuel, repartitions: [] };
  });
}

/** Masque les RB des groupes repliés ; conserve la ligne groupe + son total. */
export function filtrerGroupesReplies<
  T extends { estSection: boolean; idGroupeRB?: number | null },
>(lignes: T[], groupesReplies: ReadonlySet<number>): T[] {
  if (groupesReplies.size === 0) return lignes;
  return lignes.filter((l) => {
    if (l.estSection) return true;
    if (l.idGroupeRB != null && groupesReplies.has(l.idGroupeRB)) return false;
    return true;
  });
}

export function hasRepartitionNonVide(
  repartitions: { mois: number; montant: number }[],
): boolean {
  return repartitions.some((r) => r.montant !== 0);
}

/** Montant annuel cohérent à afficher : SUM(mois) si répartition, sinon MontantAnnuel. */
export function montantAnnuelAffiche(
  montantAnnuel: number,
  repartitions: { mois: number; montant: number }[],
): number {
  return hasRepartitionNonVide(repartitions) ? cumulRepartitions(repartitions) : montantAnnuel;
}

/**
 * Répartit un montant annuel sur 12 mois (centimes entiers).
 *
 * Règle déterministe :
 * 1. Convertir en centimes : C = round(montant × 100)
 * 2. base = floor(C / 12) ; reste = C % 12
 * 3. Les `reste` premiers mois (janvier → …) reçoivent base+1 centimes ;
 *    les autres reçoivent `base` centimes.
 * 4. SUM(12 mois) = montant exact (à 0,01 près, via centimes).
 *
 * Exemple 10 000,00 → 833,34 × 4 + 833,33 × 8 = 10 000,00
 * (reste = 4 → jan–avr +0,01).
 *
 * @returns null si montant invalide (&lt; 0 ou non fini) ; [] si 0.
 */
export function repartirMontantSur12Mois(
  montantAnnuel: number,
): { mois: number; montant: number }[] | null {
  if (!Number.isFinite(montantAnnuel) || montantAnnuel < 0) return null;
  const centsTotal = Math.round(montantAnnuel * 100);
  if (centsTotal === 0) return [];

  const base = Math.floor(centsTotal / 12);
  const reste = centsTotal % 12;

  return Array.from({ length: 12 }, (_, i) => {
    const cents = base + (i < reste ? 1 : 0);
    return { mois: i + 1, montant: cents / 100 };
  }).filter((r) => r.montant !== 0);
}
