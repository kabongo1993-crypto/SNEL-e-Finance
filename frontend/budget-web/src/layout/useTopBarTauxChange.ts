import { useCallback, useEffect, useState } from 'react';

import { calculerTauxInverseAffichage } from '../features/taux-change/tauxChangeUtils';

import {

  fetchTauxChangeApplicable,

  fetchTauxChangeList,

  type TauxChangeDto,

} from '../services/apiClient';



export type TopBarTauxPaire = 'USD/CDF' | 'EUR/CDF';



export type TopBarTauxEntry = {

  paire: TopBarTauxPaire;

  source: string;

  cible: string;

  taux: number;

  dateEffet: string;

};



function todayIsoLocal(): string {

  const d = new Date();

  const y = d.getFullYear();

  const m = String(d.getMonth() + 1).padStart(2, '0');

  const day = String(d.getDate()).padStart(2, '0');

  return `${y}-${m}-${day}`;

}



function isActif(statut: string): boolean {

  return statut.trim().toUpperCase() === 'ACTIF';

}



function pickActifRow(rows: TauxChangeDto[]): TauxChangeDto | null {

  return rows.find((r) => isActif(r.statut)) ?? null;

}



function entryFromDirect(

  paire: TopBarTauxPaire,

  source: string,

  cible: string,

  row: TauxChangeDto,

): TopBarTauxEntry {

  return {

    paire,

    source,

    cible,

    taux: row.tauxReference,

    dateEffet: row.dateEffet,

  };

}



async function resolveDirectionnelActif(
  paire: TopBarTauxPaire,
  source: string,
  cible: string,
): Promise<TopBarTauxEntry | null> {
  const applicable = await fetchTauxChangeApplicable(source, cible, todayIsoLocal());
  if (applicable && isActif(applicable.statut) && applicable.taux > 0) {
    return {
      paire,
      source,
      cible,
      taux: applicable.taux,
      dateEffet: applicable.dateEffet,
    };
  }

  const direct = pickActifRow(
    await fetchTauxChangeList({ deviseBase: source, deviseQuote: cible, statut: 'ACTIF' }),
  );
  if (direct && direct.tauxReference > 0) {
    return entryFromDirect(paire, source, cible, direct);
  }

  const inverse = pickActifRow(
    await fetchTauxChangeList({ deviseBase: cible, deviseQuote: source, statut: 'ACTIF' }),
  );
  if (inverse && inverse.tauxReference > 0) {
    const taux = calculerTauxInverseAffichage(inverse.tauxReference);
    if (taux > 0) {
      return { paire, source, cible, taux, dateEffet: inverse.dateEffet };
    }
  }

  return null;
}



async function resolveEurVersCdf(usdCdf: TopBarTauxEntry | null): Promise<TopBarTauxEntry | null> {

  const direct = await resolveDirectionnelActif('EUR/CDF', 'EUR', 'CDF');

  if (direct) return direct;



  const eurUsd = pickActifRow(

    await fetchTauxChangeList({ deviseBase: 'EUR', deviseQuote: 'USD', statut: 'ACTIF' }),

  );

  if (!eurUsd && !pickActifRow(await fetchTauxChangeList({ deviseBase: 'USD', deviseQuote: 'EUR', statut: 'ACTIF' }))) {

    const eurUsdApp = await fetchTauxChangeApplicable('EUR', 'USD', todayIsoLocal());

    if (eurUsdApp && isActif(eurUsdApp.statut) && usdCdf) {

      return {

        paire: 'EUR/CDF',

        source: 'EUR',

        cible: 'CDF',

        taux: eurUsdApp.taux * usdCdf.taux,

        dateEffet: eurUsdApp.dateEffet >= usdCdf.dateEffet ? eurUsdApp.dateEffet : usdCdf.dateEffet,

      };

    }

    return null;

  }



  let tauxEurUsd = 0;

  let dateEurUsd = '';

  if (eurUsd && eurUsd.tauxReference > 0) {

    tauxEurUsd = eurUsd.tauxReference;

    dateEurUsd = eurUsd.dateEffet;

  } else {

    const usdEur = pickActifRow(

      await fetchTauxChangeList({ deviseBase: 'USD', deviseQuote: 'EUR', statut: 'ACTIF' }),

    );

    if (!usdEur || usdEur.tauxReference <= 0) return null;

    tauxEurUsd = calculerTauxInverseAffichage(usdEur.tauxReference);

    dateEurUsd = usdEur.dateEffet;

  }



  if (!usdCdf || tauxEurUsd <= 0) return null;



  return {

    paire: 'EUR/CDF',

    source: 'EUR',

    cible: 'CDF',

    taux: tauxEurUsd * usdCdf.taux,

    dateEffet: dateEurUsd >= usdCdf.dateEffet ? dateEurUsd : usdCdf.dateEffet,

  };

}



export function useTopBarTauxChange(enabled: boolean) {

  const [entries, setEntries] = useState<TopBarTauxEntry[]>([]);

  const [loading, setLoading] = useState(false);



  const refresh = useCallback(async () => {

    if (!enabled) {

      setEntries([]);

      return;

    }

    setLoading(true);

    try {

      const usdCdf = await resolveDirectionnelActif('USD/CDF', 'USD', 'CDF');

      const eurCdf = await resolveEurVersCdf(usdCdf);

      const ordered = [usdCdf, eurCdf].filter((e): e is TopBarTauxEntry => e !== null);

      setEntries(ordered);

    } catch {

      setEntries([]);

    } finally {

      setLoading(false);

    }

  }, [enabled]);



  useEffect(() => {

    void refresh();

    const onFocus = () => void refresh();

    window.addEventListener('focus', onFocus);

    return () => window.removeEventListener('focus', onFocus);

  }, [refresh]);



  return { entries, loading };

}


