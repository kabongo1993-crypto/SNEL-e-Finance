import type {

  TauxChangeDto,

  TauxChangeListQuery,

} from '../../services/apiClient';



export type TauxChangeRow = TauxChangeDto & { id: string };



export const STATUTS_TAUX = ['ACTIF', 'INACTIF'] as const;



export const DECIMALES_INVERSE = 8;



export function formatPaireLibelle(deviseBase: string, deviseQuote: string): string {

  return `${deviseBase} / ${deviseQuote}`;

}



export function formatDateFr(iso: string | null | undefined): string {

  if (!iso) return '—';

  const d = new Date(iso.length <= 10 ? `${iso}T00:00:00` : iso);

  if (Number.isNaN(d.getTime())) return '—';

  return d.toLocaleDateString('fr-FR', { day: '2-digit', month: '2-digit', year: 'numeric' });

}



export function formatDateTimeFr(iso: string | null | undefined): string {

  if (!iso) return '—';

  const d = new Date(iso);

  if (Number.isNaN(d.getTime())) return '—';

  return d.toLocaleString('fr-FR', {

    day: '2-digit',

    month: '2-digit',

    year: 'numeric',

    hour: '2-digit',

    minute: '2-digit',

  });

}



export function formatTaux(n: number, decimals = 2): string {

  return new Intl.NumberFormat('fr-FR', {

    minimumFractionDigits: Math.min(2, decimals),

    maximumFractionDigits: decimals,

  }).format(n ?? 0);

}



/** Taux inverse calculé (affichage uniquement — 8 décimales). */

export function calculerTauxInverseAffichage(tauxReference: number): number {

  if (!Number.isFinite(tauxReference) || tauxReference <= 0) return 0;

  const inverse = 1 / tauxReference;

  const factor = 10 ** DECIMALES_INVERSE;

  return Math.round(inverse * factor) / factor;

}



export function buildApercuPaire(tauxReference: number, deviseBase: string, deviseQuote: string) {

  const ref = Number(String(tauxReference).replace(',', '.'));

  if (!Number.isFinite(ref) || ref <= 0) {

    return null;

  }

  const inverse = calculerTauxInverseAffichage(ref);

  return {

    direct: `1 ${deviseBase} = ${formatTaux(ref)} ${deviseQuote}`,

    inverse: `1 ${deviseQuote} = ${formatTaux(inverse, DECIMALES_INVERSE)} ${deviseBase}`,

  };

}



export function sortTauxChangeRows(rows: TauxChangeRow[]): TauxChangeRow[] {

  return [...rows].sort((a, b) => {

    const da = a.dateEffet.localeCompare(b.dateEffet);

    if (da !== 0) return -da;

    return b.idTauxChange - a.idTauxChange;

  });

}



export function toTauxChangeRows(data: TauxChangeDto[]): TauxChangeRow[] {

  return sortTauxChangeRows(

    data.map((t) => ({

      ...t,

      estModifiable: t.estModifiable ?? false,

      id: String(t.idTauxChange),

    })),

  );

}



export function buildTauxChangeListQuery(filters: {

  paireKey: string;

  statut: string;

  dateEffetMin: string;

  dateEffetMax: string;

}): TauxChangeListQuery {

  const query: TauxChangeListQuery = {};

  if (filters.paireKey.trim()) {

    const [base, quote] = filters.paireKey.split('/');

    if (base?.trim()) query.deviseBase = base.trim().toUpperCase();

    if (quote?.trim()) query.deviseQuote = quote.trim().toUpperCase();

  }

  if (filters.statut && filters.statut !== 'all') query.statut = filters.statut;

  if (filters.dateEffetMin.trim()) query.dateEffetMin = filters.dateEffetMin.trim();

  if (filters.dateEffetMax.trim()) query.dateEffetMax = filters.dateEffetMax.trim();

  return query;

}



export function paireKeyFromCodes(deviseBase: string, deviseQuote: string): string {

  return `${deviseBase.trim().toUpperCase()}/${deviseQuote.trim().toUpperCase()}`;

}



export type CreateTauxChangeForm = {

  deviseBase: string;

  deviseQuote: string;

  tauxReference: string;

  dateEffet: string;

};



export function validateCreateTauxChangeForm(

  form: CreateTauxChangeForm,

  devisesActives: string[],

): string | null {

  const base = form.deviseBase.trim().toUpperCase();

  const quote = form.deviseQuote.trim().toUpperCase();

  if (!base) return 'La devise de base est obligatoire.';

  if (!quote) return 'La devise cotée est obligatoire.';

  if (base === quote) return 'Les deux devises doivent être différentes.';

  const allowed = new Set(devisesActives.map((c) => c.trim().toUpperCase()));

  if (!allowed.has(base) || !allowed.has(quote)) {

    return 'Les devises sélectionnées doivent être actives dans le référentiel.';

  }

  const taux = Number(String(form.tauxReference).replace(',', '.'));

  if (!Number.isFinite(taux) || taux <= 0) return 'Le taux de référence doit être strictement positif.';

  if (!form.dateEffet.trim()) return "La date d'effet est obligatoire.";

  return null;

}



export function parseCreatePayload(
  form: CreateTauxChangeForm,
  options?: { confirmerRemplacement?: boolean },
) {
  return {
    deviseBase: form.deviseBase.trim().toUpperCase(),
    deviseQuote: form.deviseQuote.trim().toUpperCase(),
    tauxReference: Number(String(form.tauxReference).replace(',', '.')),
    dateEffet: form.dateEffet,
    ...(options?.confirmerRemplacement ? { confirmerRemplacement: true } : {}),
  };
}



export function validateUpdateTauxChangeForm(form: {
  tauxReference: string;
  dateEffet: string;
}): string | null {
  const taux = Number(String(form.tauxReference).replace(',', '.'));
  if (!Number.isFinite(taux) || taux <= 0) return 'Le taux de référence doit être strictement positif.';
  if (!form.dateEffet.trim()) return "La date d'effet est obligatoire.";
  return null;
}

export function parseUpdatePayload(form: { tauxReference: string; dateEffet: string }) {
  return {
    tauxReference: Number(String(form.tauxReference).replace(',', '.')),
    dateEffet: form.dateEffet,
  };
}

/** Vérifie qu'aucune action « désactiver » n'est exposée (règle historisation). */
export function assertNoEditAction(actions: { id: string }[] | undefined): boolean {
  if (!actions?.length) return true;
  return !actions.some((a) => a.id === 'desactiver');
}



export function helperTextTauxReference(deviseBase: string, deviseQuote: string): string {

  if (!deviseBase.trim() || !deviseQuote.trim()) {

    return 'Ex. 1 EUR = 3 450 USD — saisissez le taux tel que vous le connaissez métier.';

  }

  return `Ex. ${deviseBase.trim().toUpperCase()}/${deviseQuote.trim().toUpperCase()} : le taux signifie 1 ${deviseBase.trim().toUpperCase()} = X ${deviseQuote.trim().toUpperCase()}`;

}


