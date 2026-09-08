/**
 * Contrôles de saisie numérique / monétaire partagés (frontend).
 * Le backend reste la source d'autorité — ces helpers n'autorisent que la forme attendue.
 */

export type NumericInputOptions = {
  /** Autorise un signe « - » en tête (défaut : false). */
  allowNegative?: boolean;
  /** Nombre max de décimales après le séparateur (défaut : 8). */
  maxDecimals?: number;
};

/**
 * Filtre une saisie en cours : refuse lettres et caractères arbitraires.
 * Conserve chiffres, espaces (milliers), un séparateur décimal (, ou .)
 * et éventuellement un signe négatif en tête.
 */
export function sanitizeNumericInput(
  raw: string,
  options: NumericInputOptions = {},
): string {
  const { allowNegative = false, maxDecimals = 8 } = options;
  if (!raw) return '';

  let s = raw.replace(/[^\d\s,.\-]/g, '');

  if (allowNegative) {
    const neg = s.startsWith('-');
    s = (neg ? '-' : '') + s.slice(neg ? 1 : 0).replace(/-/g, '');
  } else {
    s = s.replace(/-/g, '');
  }

  const sign = s.startsWith('-') ? '-' : '';
  const body = sign ? s.slice(1) : s;

  let sepIndex = -1;
  let sepChar = '';
  for (let i = 0; i < body.length; i++) {
    const ch = body[i];
    if (ch === ',' || ch === '.') {
      sepIndex = i;
      sepChar = ch;
      break;
    }
  }

  if (sepIndex < 0) {
    return sign + body.replace(/[^\d\s]/g, '');
  }

  const intPart = body.slice(0, sepIndex).replace(/[^\d\s]/g, '');
  const fracRaw = body
    .slice(sepIndex + 1)
    .replace(/[^\d]/g, '')
    .slice(0, Math.max(0, maxDecimals));
  return `${sign}${intPart}${sepChar}${fracRaw}`;
}

/**
 * Parse une saisie FR/EN vers number.
 * Chaîne vide / « - » seul → null.
 * Valeur non finie ou négative (si non autorisé) → null.
 */
export function parseNumericInput(
  raw: string,
  options: NumericInputOptions = {},
): number | null {
  const { allowNegative = false } = options;
  const cleaned = (raw ?? '').replace(/\s/g, '').replace(',', '.').trim();
  if (cleaned === '' || cleaned === '-' || cleaned === '.' || cleaned === '-.') {
    return null;
  }
  const n = Number(cleaned);
  if (!Number.isFinite(n)) return null;
  if (!allowNegative && n < 0) return null;
  return n;
}

/**
 * Compat prévisions / montants ≥ 0 : vide → 0, invalide → null.
 */
export function parseMontantInput(raw: string): number | null {
  const cleaned = (raw ?? '').replace(/\s/g, '').replace(',', '.').trim();
  if (cleaned === '' || cleaned === '-') return 0;
  const n = parseNumericInput(raw, { allowNegative: false });
  return n;
}
