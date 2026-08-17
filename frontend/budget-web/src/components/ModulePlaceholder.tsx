import { Box, Button, Chip, FormControlLabel, Paper, Radio, RadioGroup, Stack, Typography } from '@mui/material';
import type { ReactNode } from 'react';
import { Link as RouterLink } from 'react-router-dom';
import { PageHeader } from './PageHeader';
import { BRAND_NAME, useThemeMode, type ThemePreference } from '../theme';

interface ModulePlaceholderProps {
  title: string;
  subtitle: string;
  breadcrumbs: { label: string; to?: string }[];
  moduleLabel: string;
  children?: ReactNode;
  actions?: ReactNode;
}

export function ModulePlaceholder({
  title,
  subtitle,
  breadcrumbs,
  moduleLabel,
  children,
  actions,
}: ModulePlaceholderProps) {
  return (
    <Box>
      <PageHeader title={title} subtitle={subtitle} breadcrumbs={breadcrumbs} actions={actions} />
      <Paper sx={{ p: { xs: 2, md: 2.5 }, mb: 2 }}>
        <Stack direction="row" spacing={1} useFlexGap sx={{ alignItems: 'center', flexWrap: 'wrap', mb: 1.5 }}>
          <Chip size="small" label="Maquette UI" color="secondary" variant="outlined" />
          <Chip size="small" label={moduleLabel} variant="outlined" />
          <Typography variant="body2" color="text.secondary">
            Emplacement réservé — logique métier à brancher ultérieurement.
          </Typography>
        </Stack>
        {children ?? (
          <Typography color="text.secondary">
            Cet écran fait partie de l’architecture {BRAND_NAME}. Les formulaires, tableaux et traitements
            métier seront implémentés dans une étape suivante.
          </Typography>
        )}
      </Paper>
    </Box>
  );
}

export function PlaceholderLinkButton({ to, label }: { to: string; label: string }) {
  return (
    <Button variant="contained" component={RouterLink} to={to}>
      {label}
    </Button>
  );
}

export function ThemeSettingsPanel() {
  const { preference, setPreference } = useThemeMode();
  return (
    <Paper sx={{ p: 2.5, border: 'none', bgcolor: 'transparent' }} elevation={0}>
      <Typography variant="subtitle1" sx={{ mb: 0.5, fontWeight: 700 }}>
        Apparence
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
        Choisissez le thème de {BRAND_NAME}. Le mode Système suit automatiquement les préférences de
        votre appareil. Le choix est mémorisé.
      </Typography>
      <RadioGroup value={preference} onChange={(_, v) => setPreference(v as ThemePreference)}>
        <FormControlLabel value="light" control={<Radio size="small" />} label="Clair" />
        <FormControlLabel value="dark" control={<Radio size="small" />} label="Sombre" />
        <FormControlLabel value="system" control={<Radio size="small" />} label="Système" />
      </RadioGroup>
    </Paper>
  );
}
