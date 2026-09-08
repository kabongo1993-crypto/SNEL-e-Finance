import { Box } from '@mui/material';
import type { ReactNode } from 'react';
import { efRadius } from '../../theme';

export type NumberedSectionBlockProps = {
  step: number;
  children: ReactNode;
  /** Ligne verticale décorative sous le badge (consultation). */
  showConnector?: boolean;
  /** Dernière section — masque le connecteur. */
  isLast?: boolean;
};

/**
 * Badge numéroté de parcours — n’altère pas le contenu enfant (FormSection, DetailPanel…).
 */
export function NumberedSectionBlock({
  step,
  children,
  showConnector = false,
  isLast = false,
}: NumberedSectionBlockProps) {
  return (
    <Box sx={{ display: 'flex', gap: 1.5, alignItems: 'stretch' }}>
      <Box
        sx={{
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          flexShrink: 0,
        }}
      >
        <Box
          aria-hidden
          sx={{
            width: 32,
            height: 32,
            mt: 1.25,
            borderRadius: `${efRadius.xs}px`,
            bgcolor: 'primary.main',
            color: 'primary.contrastText',
            display: 'grid',
            placeItems: 'center',
            typography: 'subtitle2',
            fontWeight: 700,
          }}
        >
          {step}
        </Box>
        {showConnector && !isLast && (
          <Box
            sx={{
              width: 2,
              flex: 1,
              minHeight: 24,
              mt: 1,
              bgcolor: 'divider',
              borderRadius: 1,
            }}
          />
        )}
      </Box>
      <Box sx={{ flex: 1, minWidth: 0, pb: showConnector && !isLast ? 1 : 0 }}>{children}</Box>
    </Box>
  );
}
