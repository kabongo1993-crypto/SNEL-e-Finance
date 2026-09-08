import CloseIcon from '@mui/icons-material/Close';
import { Box, Drawer, Grid, IconButton, Stack, Typography } from '@mui/material';
import type { ReactNode } from 'react';
import { useResponsive } from '../theme/useResponsive';

interface MasterDetailLayoutProps {
  /** Liste / tableau principal. */
  master: ReactNode;
  /** Panneau de détail. */
  detail?: ReactNode;
  /**
   * Vrai quand un élément est sélectionné. Sous le seuil, pilote l'ouverture
   * du tiroir ; au-dessus, le panneau reste visible avec son état vide.
   */
  detailOpen?: boolean;
  /** Titre du tiroir mobile. */
  detailTitle?: string;
  /** Largeur du panneau de détail sur grand écran, en colonnes MUI (sur 12). */
  detailSpan?: number;
  onCloseDetail?: () => void;
  /** Point de bascule du passage en deux colonnes. */
  breakpoint?: 'md' | 'lg';
}

/**
 * Motif liste + détail des écrans de référentiel.
 * Deux colonnes sur grand écran, détail en tiroir sous le seuil.
 */
export function MasterDetailLayout({
  master,
  detail,
  detailOpen,
  detailTitle = 'Détails',
  detailSpan = 4,
  onCloseDetail,
  breakpoint = 'lg',
}: MasterDetailLayoutProps) {
  const { isMobile, isTablet } = useResponsive();
  const stacked = breakpoint === 'lg' ? isMobile || isTablet : isMobile;

  if (stacked) {
    return (
      <>
        {master}
        <Drawer
          anchor="right"
          open={Boolean(detailOpen ?? detail)}
          onClose={onCloseDetail}
          slotProps={{
            paper: {
              sx: {
                bgcolor: 'background.default',
                width: { xs: '100%', sm: 440 },
                maxWidth: '100%',
                borderRadius: 0,
              },
            },
          }}
        >
          <Stack
            direction="row"
            sx={{
              position: 'sticky',
              top: 0,
              zIndex: 1,
              justifyContent: 'space-between',
              alignItems: 'center',
              px: 2,
              height: 56,
              flexShrink: 0,
              bgcolor: 'background.paper',
              borderBottom: '1px solid',
              borderColor: 'divider',
            }}
          >
            <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
              {detailTitle}
            </Typography>
            <IconButton onClick={onCloseDetail} aria-label="Fermer le détail">
              <CloseIcon fontSize="small" />
            </IconButton>
          </Stack>
          <Box sx={{ flex: 1, overflowY: 'auto', p: 1.5 }}>{detail}</Box>
        </Drawer>
      </>
    );
  }

  return (
    <Grid container spacing={2.5} sx={{ alignItems: 'flex-start' }}>
      <Grid size={{ xs: 12, [breakpoint]: 12 - detailSpan }} sx={{ minWidth: 0 }}>
        {master}
      </Grid>
      <Grid size={{ xs: 12, [breakpoint]: detailSpan }} sx={{ minWidth: 0 }}>
        {detail}
      </Grid>
    </Grid>
  );
}
