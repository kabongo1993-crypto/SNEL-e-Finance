import TrendingDownIcon from '@mui/icons-material/TrendingDown';
import TrendingUpIcon from '@mui/icons-material/TrendingUp';
import { Box, Paper, Stack, Typography } from '@mui/material';
import type { ReactNode } from 'react';

interface StatCardProps {
  title: string;
  value: string;
  subtitle?: string;
  icon?: ReactNode;
  trend?: { value: string; direction: 'up' | 'down' | 'neutral' };
  /** Optional — prefer default primary accent for consistency */
  accent?: string;
}

export function StatCard({ title, value, subtitle, icon, trend, accent }: StatCardProps) {
  const barColor = accent ?? 'var(--ef-primary)';

  return (
    <Paper
      sx={{
        p: 1.75,
        height: '100%',
        position: 'relative',
        overflow: 'hidden',
        bgcolor: 'var(--ef-surface)',
        '&::before': {
          content: '""',
          position: 'absolute',
          left: 0,
          top: 0,
          bottom: 0,
          width: 3,
          bgcolor: barColor,
        },
      }}
    >
      <Stack direction="row" spacing={1} sx={{ justifyContent: 'space-between', alignItems: 'flex-start' }}>
        <Box sx={{ minWidth: 0 }}>
          <Typography variant="overline" color="text.secondary" sx={{ lineHeight: 1.2 }}>
            {title}
          </Typography>
          <Typography
            variant="h5"
            sx={{ mt: 0.5, fontWeight: 700, letterSpacing: '-0.02em', fontSize: '1.15rem' }}
          >
            {value}
          </Typography>
          {subtitle && (
            <Typography variant="caption" color="text.secondary" sx={{ mt: 0.35, display: 'block' }}>
              {subtitle}
            </Typography>
          )}
          {trend && (
            <Stack direction="row" spacing={0.5} sx={{ alignItems: 'center', mt: 0.75 }}>
              {trend.direction === 'up' && (
                <TrendingUpIcon sx={{ fontSize: 14, color: 'success.main' }} />
              )}
              {trend.direction === 'down' && (
                <TrendingDownIcon sx={{ fontSize: 14, color: 'error.main' }} />
              )}
              <Typography
                variant="caption"
                sx={{
                  color:
                    trend.direction === 'up'
                      ? 'success.main'
                      : trend.direction === 'down'
                        ? 'error.main'
                        : 'text.secondary',
                  fontWeight: 600,
                }}
              >
                {trend.value}
              </Typography>
            </Stack>
          )}
        </Box>
        {icon && (
          <Box
            sx={{
              width: 34,
              height: 34,
              borderRadius: 1,
              display: 'grid',
              placeItems: 'center',
              bgcolor: 'var(--ef-primary-soft)',
              color: 'var(--ef-primary)',
              flexShrink: 0,
            }}
          >
            {icon}
          </Box>
        )}
      </Stack>
    </Paper>
  );
}
