import { Box, Stack, TextField, Tooltip, type TextFieldProps } from '@mui/material';
import type { ReactNode } from 'react';

type ComputedFieldProps = {
  label: string;
  value: string;
  /** Texte d’aide (défaut : « Déterminée automatiquement »). */
  helperText?: string;
  fullWidth?: boolean;
  minWidth?: number;
  maxWidth?: number;
  sx?: TextFieldProps['sx'];
};

/**
 * Champ calculé / déterminé automatiquement — lecture seule, différencié visuellement.
 */
export function ComputedField({
  label,
  value,
  helperText = 'Déterminée automatiquement',
  fullWidth = false,
  minWidth = 260,
  maxWidth = 420,
  sx,
}: ComputedFieldProps) {
  const truncated = value.length > 48;
  const field = (
    <TextField
      label={label}
      value={value || '—'}
      helperText={helperText}
      size="small"
      fullWidth={fullWidth}
      slotProps={{
        input: { readOnly: true },
        inputLabel: { shrink: true },
      }}
      sx={{
        minWidth: fullWidth ? undefined : minWidth,
        maxWidth: fullWidth ? undefined : maxWidth,
        width: fullWidth ? '100%' : undefined,
        flex: fullWidth ? undefined : '0 0 auto',
        flexShrink: 0,
        '& .MuiInputBase-input': {
          cursor: 'default',
          overflow: 'hidden',
          textOverflow: 'ellipsis',
          whiteSpace: 'nowrap',
        },
        '& .MuiOutlinedInput-root': {
          bgcolor: 'action.hover',
        },
        ...((sx as object) ?? {}),
      }}
    />
  );

  if (!truncated) return field;
  return (
    <Tooltip title={value} enterDelay={400}>
      <Box sx={{ minWidth: fullWidth ? undefined : minWidth, maxWidth, flex: 1 }}>{field}</Box>
    </Tooltip>
  );
}

type FieldWithActionProps = {
  /** Champ principal (Select, TextField…). */
  field: ReactNode;
  /** Bouton associé (Ajouter, Créer…). */
  action?: ReactNode;
  /** Largeur min du bloc champ+action. */
  minWidth?: number;
  flex?: number | string;
};

/**
 * Associe visuellement un champ et son bouton d’action (ex. Demandeur + Ajouter).
 */
export function FieldWithAction({
  field,
  action,
  minWidth = 280,
  flex = '1 1 280px',
}: FieldWithActionProps) {
  return (
    <Stack
      direction={{ xs: 'column', sm: 'row' }}
      spacing={1}
      useFlexGap
      sx={{
        alignItems: { sm: 'flex-start' },
        minWidth: { sm: minWidth },
        flex,
        maxWidth: '100%',
      }}
    >
      <Box sx={{ flex: 1, minWidth: 0, width: { xs: '100%', sm: 'auto' } }}>{field}</Box>
      {action && (
        <Box sx={{ flexShrink: 0, pt: { sm: 0.25 }, alignSelf: { xs: 'stretch', sm: 'center' } }}>
          {action}
        </Box>
      )}
    </Stack>
  );
}
