import { Box, Divider, Paper, Typography } from '@mui/material';
import type { ReactNode } from 'react';

interface FormSectionProps {
  title: string;
  description?: string;
  children: ReactNode;
  actions?: ReactNode;
}

export function FormSection({ title, description, children, actions }: FormSectionProps) {
  return (
    <Paper sx={{ p: { xs: 2, md: 3 }, mb: 2 }}>
      <Box sx={{ mb: 2 }}>
        <Typography variant="h6">{title}</Typography>
        {description && (
          <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
            {description}
          </Typography>
        )}
      </Box>
      <Divider sx={{ mb: 2.5 }} />
      {children}
      {actions && (
        <Box sx={{ mt: 3, display: 'flex', justifyContent: 'flex-end', gap: 1 }}>{actions}</Box>
      )}
    </Paper>
  );
}

interface DetailPanelProps {
  title: string;
  children: ReactNode;
  actions?: ReactNode;
}

export function DetailPanel({ title, children, actions }: DetailPanelProps) {
  return (
    <Paper sx={{ p: { xs: 2, md: 3 }, mb: 2 }}>
      <Box
        sx={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          mb: 2,
          gap: 1,
          flexWrap: 'wrap',
        }}
      >
        <Typography variant="h6">{title}</Typography>
        {actions}
      </Box>
      <Divider sx={{ mb: 2 }} />
      {children}
    </Paper>
  );
}
