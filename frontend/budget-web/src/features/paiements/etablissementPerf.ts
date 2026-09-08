/** Instrumentation temporaire — diagnostic perf action « Établir ». */

export type EtablissementPerfTag =
  | 'PIECE_CAISSE'
  | 'BON_PROVISOIRE'
  | 'MINUTE_CHEQUE'
  | 'BILLET_CONVERSION';

export interface EtablissementPerfSession {
  tag: EtablissementPerfTag;
  demandeId: number;
  clickAt: number;
  httpStartAt?: number;
  httpEndAt?: number;
  refreshStartAt?: number;
  refreshEndAt?: number;
}

const sessions = new Map<string, EtablissementPerfSession>();

function sessionKey(tag: EtablissementPerfTag, demandeId: number) {
  return `${tag}:${demandeId}`;
}

export function beginEtablissementPerf(tag: EtablissementPerfTag, demandeId: number): EtablissementPerfSession {
  const session: EtablissementPerfSession = {
    tag,
    demandeId,
    clickAt: performance.now(),
  };
  sessions.set(sessionKey(tag, demandeId), session);
  console.info(`[PERF][ETABLISSEMENT][FE] DemandeId=${demandeId} Tag=${tag} Etape=CLICK Duration=0ms`);
  return session;
}

export function markEtablissementHttpStart(tag: EtablissementPerfTag, demandeId: number) {
  const session = sessions.get(sessionKey(tag, demandeId));
  if (!session) return;
  session.httpStartAt = performance.now();
  const ms = Math.round(session.httpStartAt - session.clickAt);
  console.info(`[PERF][ETABLISSEMENT][FE] DemandeId=${demandeId} Tag=${tag} Etape=HTTP_START Duration=${ms}ms`);
}

export function markEtablissementHttpEnd(tag: EtablissementPerfTag, demandeId: number) {
  const session = sessions.get(sessionKey(tag, demandeId));
  if (!session || session.httpStartAt === undefined) return;
  session.httpEndAt = performance.now();
  const httpMs = Math.round(session.httpEndAt - session.httpStartAt);
  const sinceClick = Math.round(session.httpEndAt - session.clickAt);
  console.info(
    `[PERF][ETABLISSEMENT][FE] DemandeId=${demandeId} Tag=${tag} Etape=HTTP_END Duration=${httpMs}ms SinceClick=${sinceClick}ms`,
  );
}

export function markEtablissementRefreshStart(tag: EtablissementPerfTag, demandeId: number) {
  const session = sessions.get(sessionKey(tag, demandeId));
  if (!session) return;
  session.refreshStartAt = performance.now();
  const ms = Math.round(session.refreshStartAt - session.clickAt);
  console.info(`[PERF][ETABLISSEMENT][FE] DemandeId=${demandeId} Tag=${tag} Etape=REFRESH_START Duration=${ms}ms`);
}

export function markEtablissementRefreshEnd(tag: EtablissementPerfTag, demandeId: number) {
  const session = sessions.get(sessionKey(tag, demandeId));
  if (!session) return;
  session.refreshEndAt = performance.now();
  const refreshMs =
    session.refreshStartAt !== undefined
      ? Math.round(session.refreshEndAt - session.refreshStartAt)
      : 0;
  const totalMs = Math.round(session.refreshEndAt - session.clickAt);
  const httpMs =
    session.httpStartAt !== undefined && session.httpEndAt !== undefined
      ? Math.round(session.httpEndAt - session.httpStartAt)
      : 0;
  console.info(
    `[PERF][ETABLISSEMENT][FE] DemandeId=${demandeId} Tag=${tag} Etape=REFRESH_END RefreshDuration=${refreshMs}ms HttpDuration=${httpMs}ms TOTAL=${totalMs}ms`,
  );
  sessions.delete(sessionKey(tag, demandeId));
}

/** Mise à jour locale de l'état React (sans GET /complet). */
export function markEtablissementLocalUpdateStart(tag: EtablissementPerfTag, demandeId: number) {
  const session = sessions.get(sessionKey(tag, demandeId));
  if (!session) return;
  session.refreshStartAt = performance.now();
  const ms = Math.round(session.refreshStartAt - session.clickAt);
  console.info(
    `[PERF][ETABLISSEMENT][FE] DemandeId=${demandeId} Tag=${tag} Etape=LOCAL_UPDATE_START Duration=${ms}ms`,
  );
}

export function markEtablissementLocalUpdateEnd(tag: EtablissementPerfTag, demandeId: number) {
  const session = sessions.get(sessionKey(tag, demandeId));
  if (!session) return;
  session.refreshEndAt = performance.now();
  const localMs =
    session.refreshStartAt !== undefined
      ? Math.round(session.refreshEndAt - session.refreshStartAt)
      : 0;
  const totalMs = Math.round(session.refreshEndAt - session.clickAt);
  const httpMs =
    session.httpStartAt !== undefined && session.httpEndAt !== undefined
      ? Math.round(session.httpEndAt - session.httpStartAt)
      : 0;
  console.info(
    `[PERF][ETABLISSEMENT][FE] DemandeId=${demandeId} Tag=${tag} Etape=LOCAL_UPDATE_END LocalUpdateDuration=${localMs}ms HttpDuration=${httpMs}ms RefreshDuration=0ms TOTAL=${totalMs}ms HttpRequests=1`,
  );
  sessions.delete(sessionKey(tag, demandeId));
}
