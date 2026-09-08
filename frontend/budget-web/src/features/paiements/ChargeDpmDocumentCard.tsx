import DescriptionOutlinedIcon from '@mui/icons-material/DescriptionOutlined';
import { Box, Chip, Paper, Stack, Typography } from '@mui/material';
import type { ReactNode } from 'react';
import { efRadius } from '../../theme';
import { DetailFieldGrid, type DetailFieldItem } from './DetailFieldGrid';

export type ChargeDpmDocumentStatut = 'ETABLI' | 'A_ETABLIR' | 'EN_ATTENTE';

export type ChargeDpmDocumentCardProps = {
  title: string;
  statut: ChargeDpmDocumentStatut;
  summaryFields?: DetailFieldItem[];
  children: ReactNode;
};

function statutChip(statut: ChargeDpmDocumentStatut) {
  switch (statut) {
    case 'ETABLI':
      return <Chip size="small" label="Établi" color="success" variant="outlined" />;
    case 'A_ETABLIR':
      return <Chip size="small" label="À établir" color="warning" variant="outlined" />;
    default:
      return <Chip size="small" label="En attente de traitement" variant="outlined" />;
  }
}

/** Carte document de dossier — résumé compact + contenu métier (établissement, PDF). */
export function ChargeDpmDocumentCard({
  title,
  statut,
  summaryFields,
  children,
}: ChargeDpmDocumentCardProps) {
  return (
    <Paper
      variant="outlined"
      sx={{
        p: { xs: 1.5, md: 2 },
        borderRadius: `${efRadius.md}px`,
        bgcolor: 'background.paper',
      }}
    >
      <Stack spacing={1.5}>
        <Stack
          direction="row"
          spacing={1}
          sx={{ alignItems: 'flex-start', justifyContent: 'space-between', gap: 1 }}
        >
          <Stack direction="row" spacing={1} sx={{ alignItems: 'center', minWidth: 0 }}>
            <Box
              sx={{
                display: 'grid',
                placeItems: 'center',
                color: 'primary.main',
                flexShrink: 0,
                '& .MuiSvgIcon-root': { fontSize: 20 },
              }}
            >
              <DescriptionOutlinedIcon />
            </Box>
            <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
              {title}
            </Typography>
          </Stack>
          {statutChip(statut)}
        </Stack>

        {summaryFields && summaryFields.length > 0 && (
          <DetailFieldGrid fields={summaryFields} />
        )}

        <Box sx={{ pt: summaryFields?.length ? 0.5 : 0 }}>{children}</Box>
      </Stack>
    </Paper>
  );
}
