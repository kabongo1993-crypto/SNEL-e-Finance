import { Box, Paper, Typography } from '@mui/material';
import type { ReactNode } from 'react';
import { useResponsive } from '../theme/useResponsive';

interface ChartCardProps {
  title: string;
  subtitle?: string;
  children: ReactNode;
  /** Hauteur de la zone graphique sur grand écran ; réduite d'office sous `md`. */
  height?: number;
  action?: ReactNode;
}

export function ChartCard({ title, subtitle, children, height = 280, action }: ChartCardProps) {
  const { isMobile } = useResponsive();
  const chartHeight = isMobile ? Math.min(height, 220) : height;

  return (
    <Paper sx={{ p: { xs: 2, sm: 2.5 }, height: '100%', display: 'flex', flexDirection: 'column' }}>
      <Box
        sx={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'flex-start',
          mb: 2,
          gap: 1,
        }}
      >
        <Box sx={{ minWidth: 0 }}>
          <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
            {title}
          </Typography>
          {subtitle && (
            <Typography variant="caption" color="text.secondary">
              {subtitle}
            </Typography>
          )}
        </Box>
        {action && <Box sx={{ flexShrink: 0 }}>{action}</Box>}
      </Box>
      <Box sx={{ flex: 1, minHeight: chartHeight, width: '100%' }}>{children}</Box>
    </Paper>
  );
}
