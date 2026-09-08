import { Box, Chip, Stack } from '@mui/material';
import type { StatutNavItem, StatutNavValue } from './paiementStatutNavConfig';
import { formatStatutNavChipLabel } from './demandePaiementCompteursUtils';

export interface DemandePaiementStatutNavProps {
  items: StatutNavItem[];
  activeStatut: StatutNavValue;
  onChange: (statut: StatutNavValue) => void;
  /** Affiche (—) sur les chips pendant le chargement des compteurs. */
  countsLoading?: boolean;
  /** Affiche les compteurs sur les chips (false = labels seuls). */
  showCounts?: boolean;
}

/** Segments de navigation par statut DPM (consultation uniquement). */
export function DemandePaiementStatutNav({
  items,
  activeStatut,
  onChange,
  countsLoading = false,
  showCounts = true,
}: DemandePaiementStatutNavProps) {
  if (items.length <= 1) {
    return null;
  }

  const displayCounts = showCounts && (countsLoading || items.some((i) => i.count !== undefined));

  return (
    <Box
      sx={{
        mb: 1.5,
        mx: { xs: -0.5, md: 0 },
        overflowX: 'auto',
        overflowY: 'hidden',
        WebkitOverflowScrolling: 'touch',
        maxWidth: '100%',
      }}
    >
      <Stack
        direction="row"
        spacing={1}
        useFlexGap
        sx={{
          flexWrap: 'nowrap',
          width: 'max-content',
          minWidth: 'min(100%, max-content)',
          pb: 0.25,
          px: { xs: 0.5, md: 0 },
        }}
      >
        {items.map((item) => {
          const isActive = activeStatut === item.value;
          const label = displayCounts
            ? formatStatutNavChipLabel(item.label, item.count, countsLoading)
            : item.label;
          return (
            <Chip
              key={item.value || 'TOUS'}
              label={label}
              size="small"
              clickable
              onClick={() => onChange(item.value)}
              color={isActive ? 'primary' : 'default'}
              variant={isActive ? 'filled' : 'outlined'}
              sx={{
                flexShrink: 0,
                minWidth: displayCounts ? 72 : undefined,
                '& .MuiChip-label': displayCounts ? { minWidth: 48 } : undefined,
              }}
            />
          );
        })}
      </Stack>
    </Box>
  );
}