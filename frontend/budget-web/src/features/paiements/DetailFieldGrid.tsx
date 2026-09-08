import { Box, Typography } from '@mui/material';

export type DetailFieldItem = {
  label: string;
  value: string;
  /** Occupe toute la largeur de la grille. */
  fullWidth?: boolean;
};

type DetailFieldGridProps = {
  fields: DetailFieldItem[];
};

/** Grille lecture seule label / valeur pour les sections de consultation. */
export function DetailFieldGrid({ fields }: DetailFieldGridProps) {
  return (
    <Box
      sx={{
        display: 'grid',
        gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr' },
        gap: 2,
        columnGap: 3,
      }}
    >
      {fields.map((field) => (
        <Box
          key={field.label}
          sx={{ gridColumn: field.fullWidth ? '1 / -1' : undefined, minWidth: 0 }}
        >
          <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.25 }}>
            {field.label}
          </Typography>
          <Typography
            variant="body2"
            sx={{ fontWeight: 650, overflowWrap: 'anywhere', lineHeight: 1.45 }}
          >
            {field.value || '—'}
          </Typography>
        </Box>
      ))}
    </Box>
  );
}
