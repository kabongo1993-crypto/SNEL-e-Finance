import { Box, Typography } from '@mui/material';
import { BRAND_NAME, SNEL_LOGO_SRC } from '../theme';

interface BrandLogoProps {
  compact?: boolean;
  inverted?: boolean;
}

/**
 * Identité sidebar : logo officiel SNEL + nom plateforme e-finance.
 * Le titre d’onglet navigateur reste « SNEL Online — e-Finance » (DOCUMENT_TITLE).
 */
export function BrandLogo({ compact = false, inverted = true }: BrandLogoProps) {
  /** Charte : nom « e-finance » blanc en sidebar ; sous-titre atténué. */
  const textColor = inverted ? '#FFFFFF' : 'var(--ef-text)';
  const mutedColor = inverted ? 'var(--ef-sidebar-text-muted)' : 'var(--ef-text-secondary)';
  const size = compact ? 28 : 36;

  return (
    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.25, minWidth: 0 }}>
      <Box
        component="img"
        src={SNEL_LOGO_SRC}
        alt="SNEL"
        width={size}
        height={size}
        sx={{
          width: size,
          height: size,
          borderRadius: '6px',
          flexShrink: 0,
          objectFit: 'cover',
          display: 'block',
        }}
      />
      {!compact && (
        <Box sx={{ minWidth: 0 }}>
          <Typography
            component="span"
            sx={{
              display: 'block',
              fontWeight: 700,
              fontSize: '1.05rem',
              letterSpacing: '-0.02em',
              lineHeight: 1.15,
              color: textColor,
            }}
          >
            {BRAND_NAME}
          </Typography>
          <Typography
            component="span"
            sx={{
              display: 'block',
              fontSize: '0.65rem',
              lineHeight: 1.2,
              color: mutedColor,
              letterSpacing: '0.02em',
            }}
          >
            Gestion financière intégrée
          </Typography>
        </Box>
      )}
    </Box>
  );
}
