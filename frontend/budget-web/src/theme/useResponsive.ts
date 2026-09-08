import { useMediaQuery, useTheme } from '@mui/material';

export interface ResponsiveState {
  /** < md (900px) — téléphone, ou tablette en portrait serré. */
  isMobile: boolean;
  /** md → lg — tablette paysage. */
  isTablet: boolean;
  /** >= lg (1200px). */
  isDesktop: boolean;
  /** Pointeur grossier : cibles tactiles à élargir. */
  isTouch: boolean;
}

/**
 * Point d'entrée unique pour les décisions de mise en page responsive.
 * Évite que chaque écran redéfinisse ses propres seuils.
 */
export function useResponsive(): ResponsiveState {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('md'));
  const isDesktop = useMediaQuery(theme.breakpoints.up('lg'));
  const isTouch = useMediaQuery('(pointer: coarse)');

  return {
    isMobile,
    isTablet: !isMobile && !isDesktop,
    isDesktop,
    isTouch,
  };
}
