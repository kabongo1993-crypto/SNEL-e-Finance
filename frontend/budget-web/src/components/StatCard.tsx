import TrendingDownIcon from '@mui/icons-material/TrendingDown';
import TrendingUpIcon from '@mui/icons-material/TrendingUp';
import { Box, Paper, Stack, Typography } from '@mui/material';
import type { ReactNode } from 'react';

interface StatCardProps {
  title: string;
  value: string;
  subtitle?: string;
  icon?: ReactNode;
  trend?: {
    value: string;
    direction: 'up' | 'down' | 'neutral';
    /** Ligne de référence sous le delta, ex. « vs. exercice précédent ». */
    comparison?: string;
  };
  /** Optional — prefer default primary accent for consistency */
  accent?: string;
}

export function StatCard({ title, value, subtitle, icon, trend, accent }: StatCardProps) {
  const trendColor =
    trend?.direction === 'up'
      ? 'var(--ef-kpi-positive)'
      : trend?.direction === 'down'
        ? 'var(--ef-kpi-negative)'
        : 'text.secondary';

  return (
    <Paper
      sx={{
        p: { xs: 1.25, sm: 1.5 },
        height: '100%',
        display: 'flex',
        flexDirection: 'column',
        bgcolor: 'var(--ef-surface)',
        transition: 'border-color 150ms ease, box-shadow 150ms ease',
        '&:hover': { borderColor: 'var(--ef-border)' },
      }}
    >
      <Stack direction="row" spacing={1} sx={{ justifyContent: 'space-between', alignItems: 'center' }}>
        <Typography
          variant="caption"
          sx={{ fontWeight: 600, color: 'text.secondary', minWidth: 0 }}
          noWrap
        >
          {title}
        </Typography>
        {icon && (
          <Box
            sx={{
              display: 'grid',
              placeItems: 'center',
              color: accent ?? 'var(--ef-primary)',
              flexShrink: 0,
              '& .MuiSvgIcon-root': { fontSize: 16 },
            }}
          >
            {icon}
          </Box>
        )}
      </Stack>

      <Stack direction="row" spacing={1} sx={{ alignItems: 'baseline', mt: 0.75, flexWrap: 'wrap' }}>
        <Typography variant="h4" sx={{ fontSize: { xs: '1.125rem', sm: '1.25rem' } }}>
          {value}
        </Typography>
        {trend && (
          <Stack direction="row" spacing={0.25} sx={{ alignItems: 'center' }}>
            {trend.direction === 'up' && <TrendingUpIcon sx={{ fontSize: 14, color: trendColor }} />}
            {trend.direction === 'down' && (
              <TrendingDownIcon sx={{ fontSize: 14, color: trendColor }} />
            )}
            <Typography variant="caption" sx={{ color: trendColor, fontWeight: 700 }}>
              {trend.value}
            </Typography>
          </Stack>
        )}
      </Stack>

      {(trend?.comparison || subtitle) && (
        <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 0.5 }}>
          {trend?.comparison ?? subtitle}
        </Typography>
      )}
    </Paper>
  );
}
