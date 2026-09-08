import { Grid } from '@mui/material';
import type { ReactNode } from 'react';

interface KpiRowProps {
  children: ReactNode;
  /** Nombre de cartes par ligne sur grand écran (2, 3, 4 ou 5). */
  columns?: 2 | 3 | 4 | 5;
}

const SPANS: Record<2 | 3 | 4, { sm: number; lg: number }> = {
  2: { sm: 6, lg: 6 },
  3: { sm: 6, lg: 4 },
  4: { sm: 6, lg: 3 },
};

/** Rangée de `StatCard` : une carte par ligne au mobile, deux dès `sm`. */
export function KpiRow({ children, columns = 4 }: KpiRowProps) {
  const items = Array.isArray(children) ? children : [children];

  if (columns === 5) {
    return (
      <Grid container spacing={1.5} sx={{ mb: 2, flexWrap: { xs: 'wrap', lg: 'nowrap' } }}>
        {items.flat().map((child, index) => (
          <Grid key={index} size={{ xs: 12, sm: 6, lg: 'grow' }} sx={{ minWidth: 0 }}>
            {child}
          </Grid>
        ))}
      </Grid>
    );
  }

  const span = SPANS[columns];

  return (
    <Grid container spacing={1.5} sx={{ mb: 2 }}>
      {items.flat().map((child, index) => (
        <Grid key={index} size={{ xs: 12, sm: span.sm, lg: span.lg }}>
          {child}
        </Grid>
      ))}
    </Grid>
  );
}
