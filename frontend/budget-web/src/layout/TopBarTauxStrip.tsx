import TrendingFlatIcon from '@mui/icons-material/TrendingFlat';

import { Box, Skeleton, Typography } from '@mui/material';

import { formatTaux } from '../features/taux-change/tauxChangeUtils';

import { hasAnyPerm, PERMS_PAIEMENTS_ACCESS, useAuth } from '../features/auth';

import { useTopBarTauxChange, type TopBarTauxEntry, type TopBarTauxPaire } from './useTopBarTauxChange';



const AFFICHAGE: { paire: TopBarTauxPaire; source: string; cible: string }[] = [

  { paire: 'USD/CDF', source: 'USD', cible: 'CDF' },

  { paire: 'EUR/CDF', source: 'EUR', cible: 'CDF' },

];



function TauxChip({ source, cible, taux }: { source: string; cible: string; taux: number | null }) {

  return (

    <Box

      sx={{

        display: 'inline-flex',

        alignItems: 'center',

        gap: 0.75,

        px: 1.25,

        py: 0.5,

        borderRadius: 999,

        bgcolor: 'var(--ef-surface-secondary)',

        border: '1px solid var(--ef-border-subtle)',

        minWidth: 0,

        opacity: taux === null ? 0.65 : 1,

      }}

    >

      <Typography

        variant="caption"

        sx={{ fontWeight: 700, letterSpacing: '0.04em', color: 'text.secondary', whiteSpace: 'nowrap' }}

      >

        {source}

      </Typography>

      <TrendingFlatIcon sx={{ fontSize: 14, color: 'var(--ef-primary)', opacity: 0.9 }} />

      <Typography

        variant="caption"

        sx={{ fontWeight: 700, letterSpacing: '0.04em', color: 'text.secondary', whiteSpace: 'nowrap' }}

      >

        {cible}

      </Typography>

      <Box

        sx={{

          width: '1px',

          alignSelf: 'stretch',

          my: 0.25,

          bgcolor: 'var(--ef-border-subtle)',

          display: { xs: 'none', sm: 'block' },

        }}

      />

      <Typography

        variant="body2"

        sx={{

          fontWeight: 700,

          color: taux === null ? 'text.disabled' : 'text.primary',

          whiteSpace: 'nowrap',

          fontVariantNumeric: 'tabular-nums',

        }}

      >

        {taux === null ? '—' : formatTaux(taux)}

      </Typography>

    </Box>

  );

}



const PERMS_TOPBAR_TAUX = [...PERMS_PAIEMENTS_ACCESS, 'referentiels.ecrire'] as const;

export function TopBarTauxStrip() {

  const { isAuthenticated, user } = useAuth();

  const canReadTaux = isAuthenticated && hasAnyPerm(user, PERMS_TOPBAR_TAUX);

  const { entries, loading } = useTopBarTauxChange(canReadTaux);



  if (!canReadTaux) {

    return null;

  }



  const byPaire = new Map<TopBarTauxPaire, TopBarTauxEntry>(entries.map((e) => [e.paire, e]));



  if (loading && entries.length === 0) {

    return (

      <Box sx={{ display: 'flex', gap: 1, flex: 1, minWidth: 0 }}>

        {AFFICHAGE.map((item) => (

          <Skeleton key={item.paire} variant="rounded" width={160} height={32} sx={{ borderRadius: 999 }} />

        ))}

      </Box>

    );

  }



  return (

    <Box

      sx={{

        display: 'flex',

        alignItems: 'center',

        gap: 1,

        flex: 1,

        minWidth: 0,

        overflow: 'auto',

        scrollbarWidth: 'none',

        '&::-webkit-scrollbar': { display: 'none' },

      }}

    >

      {AFFICHAGE.map((item) => {

        const entry = byPaire.get(item.paire);

        return (

          <TauxChip

            key={item.paire}

            source={item.source}

            cible={item.cible}

            taux={entry?.taux ?? null}

          />

        );

      })}

    </Box>

  );

}


