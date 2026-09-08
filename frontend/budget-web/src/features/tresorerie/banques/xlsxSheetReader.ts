/** Lecture minimale d'un .xlsx (OOXML) sans bibliothèque tierce. */

function u16(view: DataView, offset: number): number {
  return view.getUint16(offset, true);
}

function u32(view: DataView, offset: number): number {
  return view.getUint32(offset, true);
}

function findEocd(bytes: Uint8Array): number {
  for (let i = bytes.length - 22; i >= 0; i--) {
    if (
      bytes[i] === 0x50 &&
      bytes[i + 1] === 0x4b &&
      bytes[i + 2] === 0x05 &&
      bytes[i + 3] === 0x06
    ) {
      return i;
    }
  }
  throw new Error('Fichier Excel invalide (archive ZIP).');
}

async function inflateRaw(data: Uint8Array): Promise<Uint8Array> {
  if (typeof DecompressionStream === 'undefined') {
    throw new Error('Décompression ZIP non disponible dans ce navigateur.');
  }
  const copy = new Uint8Array(data.byteLength);
  copy.set(data);
  const stream = new Blob([copy.buffer]).stream().pipeThrough(new DecompressionStream('deflate-raw'));
  return new Uint8Array(await new Response(stream).arrayBuffer());
}

async function readZipEntries(buffer: ArrayBuffer): Promise<Map<string, Uint8Array>> {
  const bytes = new Uint8Array(buffer);
  const view = new DataView(buffer);
  const eocd = findEocd(bytes);
  const total = u16(view, eocd + 10);
  let cdOff = u32(view, eocd + 16);
  const out = new Map<string, Uint8Array>();
  const decoder = new TextDecoder('utf-8');

  for (let i = 0; i < total; i++) {
    if (u32(view, cdOff) !== 0x02014b50) {
      throw new Error('Fichier Excel invalide (répertoire ZIP).');
    }
    const method = u16(view, cdOff + 10);
    const compSize = u32(view, cdOff + 20);
    const nameLen = u16(view, cdOff + 28);
    const extraLen = u16(view, cdOff + 30);
    const commentLen = u16(view, cdOff + 32);
    const localOff = u32(view, cdOff + 42);
    const name = decoder.decode(bytes.subarray(cdOff + 46, cdOff + 46 + nameLen)).replace(/\\/g, '/');
    const localNameLen = u16(view, localOff + 26);
    const localExtraLen = u16(view, localOff + 28);
    const dataStart = localOff + 30 + localNameLen + localExtraLen;
    const compressed = bytes.subarray(dataStart, dataStart + compSize);
    let data: Uint8Array;
    if (method === 0) data = compressed.slice();
    else if (method === 8) data = await inflateRaw(compressed);
    else throw new Error(`Compression Excel non supportée (${method}).`);
    out.set(name, data);
    cdOff += 46 + nameLen + extraLen + commentLen;
  }
  return out;
}

function decodeUtf8(data: Uint8Array): string {
  return new TextDecoder('utf-8').decode(data);
}

function xmlDecode(value: string): string {
  return value
    .replace(/&amp;/g, '&')
    .replace(/&lt;/g, '<')
    .replace(/&gt;/g, '>')
    .replace(/&quot;/g, '"')
    .replace(/&apos;/g, "'");
}

function attr(tag: string, name: string): string | undefined {
  const m = tag.match(new RegExp(`\\b${name}="([^"]*)"`, 'i'))
    ?? tag.match(new RegExp(`\\b${name}='([^']*)'`, 'i'));
  return m?.[1];
}

function colLettersToIndex(col: string): number {
  let n = 0;
  for (const ch of col.toUpperCase()) {
    n = n * 26 + (ch.charCodeAt(0) - 64);
  }
  return n - 1;
}

function cellRef(r: string): { col: number; row: number } {
  const m = r.match(/^([A-Za-z]+)(\d+)$/);
  if (!m) return { col: 0, row: 1 };
  return { col: colLettersToIndex(m[1]), row: Number(m[2]) };
}

function innerTagsT(xml: string): string {
  return [...xml.matchAll(/<(?:[\w]+:)?t\b[^>]*>([\s\S]*?)<\/(?:[\w]+:)?t>/gi)].map((m) => xmlDecode(m[1])).join('');
}

function innerV(xml: string): string {
  const m = xml.match(/<(?:[\w]+:)?v\b[^>]*>([\s\S]*?)<\/(?:[\w]+:)?v>/i);
  return m ? xmlDecode(m[1]) : '';
}

function readSharedStrings(xml: string | undefined): string[] {
  if (!xml) return [];
  return [...xml.matchAll(/<(?:[\w]+:)?si\b[^>]*>([\s\S]*?)<\/(?:[\w]+:)?si>/gi)].map((m) => innerTagsT(m[1]));
}

function formatNumeric(raw: string): string {
  const n = Number(raw);
  if (!Number.isFinite(n)) return raw;
  return Number.isInteger(n) ? String(n) : String(n);
}

function readSheetRows(xml: string, shared: string[]): { headers: string[]; rows: { rowNumber: number; values: string[] }[] } {
  const byRow = new Map<number, Map<number, string>>();
  let maxCol = -1;

  for (const match of xml.matchAll(/<(?:[\w]+:)?c\b([^>]*)>([\s\S]*?)<\/(?:[\w]+:)?c>/gi)) {
    const openAttrs = match[1];
    const inner = match[2];
    const ref = attr(openAttrs, 'r') ?? '';
    const { col, row } = cellRef(ref);
    const type = attr(openAttrs, 't');
    let value = '';
    if (type === 's') {
      value = shared[Number(innerV(inner))] ?? '';
    } else if (type === 'inlineStr') {
      value = innerTagsT(inner);
    } else if (type === 'b') {
      value = innerV(inner) === '1' ? 'TRUE' : 'FALSE';
    } else {
      const v = innerV(inner);
      value = v ? formatNumeric(v) : '';
    }
    if (!byRow.has(row)) byRow.set(row, new Map());
    byRow.get(row)!.set(col, value);
    if (col > maxCol) maxCol = col;
  }

  const rowNumbers = [...byRow.keys()].sort((a, b) => a - b);
  if (rowNumbers.length === 0) {
    throw new Error('La feuille Excel est vide.');
  }

  const headerRowNum = rowNumbers[0];
  const headerMap = byRow.get(headerRowNum)!;
  const colCount = Math.max(maxCol + 1, 1);
  const headers = Array.from({ length: colCount }, (_, i) => (headerMap.get(i) ?? '').trim());

  const rows: { rowNumber: number; values: string[] }[] = [];
  for (const rowNumber of rowNumbers.slice(1)) {
    const map = byRow.get(rowNumber)!;
    const values = Array.from({ length: colCount }, (_, i) => (map.get(i) ?? '').trim());
    if (values.every((v) => !v)) continue;
    rows.push({ rowNumber, values });
  }

  return { headers, rows };
}

function firstSheetPath(entries: Map<string, Uint8Array>): string {
  const workbookPath = [...entries.keys()].find((k) => k.toLowerCase().endsWith('xl/workbook.xml'));
  const relsPath = [...entries.keys()].find((k) => k.toLowerCase().endsWith('xl/_rels/workbook.xml.rels'));
  if (workbookPath && relsPath) {
    const wb = decodeUtf8(entries.get(workbookPath)!);
    const rels = decodeUtf8(entries.get(relsPath)!);
    const sheet = wb.match(/<(?:[\w]+:)?sheet\b[^>]*>/i)?.[0];
    const rid = sheet ? attr(sheet, 'r:id') ?? attr(sheet, 'id') : undefined;
    if (rid) {
      const rel = [...rels.matchAll(/<Relationship\b[^>]*>/gi)]
        .map((m) => m[0])
        .find((tag) => attr(tag, 'Id') === rid);
      const target = rel ? attr(rel, 'Target') : undefined;
      if (target) {
        const normalized = target.replace(/\\/g, '/').replace(/^\//, '');
        const resolved = normalized.startsWith('xl/') ? normalized : `xl/${normalized}`;
        const match = [...entries.keys()].find((k) =>
          k.replace(/\\/g, '/').toLowerCase().endsWith(resolved.toLowerCase()),
        );
        if (match) return match;
      }
    }
  }
  const fallback = [...entries.keys()].find((k) =>
    /xl\/worksheets\/sheet1\.xml$/i.test(k.replace(/\\/g, '/')),
  );
  if (!fallback) throw new Error('Aucune feuille Excel n’a été trouvée.');
  return fallback;
}

export interface XlsxSheetData {
  headers: string[];
  rows: { rowNumber: number; values: string[] }[];
}

export async function readFirstXlsxSheet(file: File): Promise<XlsxSheetData> {
  const name = file.name.toLowerCase();
  if (!name.endsWith('.xlsx')) {
    throw new Error('Le fichier doit être un classeur Excel (.xlsx).');
  }
  const buffer = await file.arrayBuffer();
  const entries = await readZipEntries(buffer);
  const sheetPath = firstSheetPath(entries);
  const sstPath = [...entries.keys()].find((k) => k.replace(/\\/g, '/').toLowerCase().endsWith('xl/sharedstrings.xml'));
  const shared = readSharedStrings(sstPath ? decodeUtf8(entries.get(sstPath)!) : undefined);
  return readSheetRows(decodeUtf8(entries.get(sheetPath)!), shared);
}
