import { Box, Breadcrumbs, Button, Link, Stack, Typography } from '@mui/material';
import { Link as RouterLink } from 'react-router-dom';
import type { ReactNode } from 'react';
import { ResponsiveActions } from './ResponsiveActions';

export interface BreadcrumbItem {
  label: string;
  to?: string;
}

interface PageHeaderProps {
  /**
   * Titre de module — ignoré. Le contexte vient du menu / fil d’Ariane.
   * Conservé pour ne pas casser les appels existants.
   */
  title?: string;
  /**
   * Sous-titre introductif — ignoré.
   * Conservé pour ne pas casser les appels existants.
   */
  subtitle?: string;
  breadcrumbs?: BreadcrumbItem[];
  actions?: ReactNode;
  /** Ancien mode compact — toutes les pages sont désormais compactes. */
  dense?: boolean;
  /** Identité d’un enregistrement (référence DPM, code UB) — pas un titre de module. */
  entityLabel?: string;
  entityCaption?: string;
  /** Marge inférieure réduite (écrans de détail compacts). */
  compact?: boolean;
}

/**
 * Barre d’en-tête compacte : fil d’Ariane + actions.
 * Pas de grand H1 ni de texte de présentation (redondant avec la navigation).
 */
export function PageHeader({ breadcrumbs, actions, entityLabel, entityCaption, compact }: PageHeaderProps) {
  const hasCrumbs = Boolean(breadcrumbs && breadcrumbs.length > 0);
  const hasActions = Boolean(actions);
  const hasEntity = Boolean(entityLabel);

  if (!hasCrumbs && !hasActions && !hasEntity) {
    return null;
  }

  return (
    <Box sx={{ mb: compact ? 1 : 2, flexShrink: 0 }}>
      {(hasCrumbs || hasActions) && (
        <Stack
          direction="row"
          spacing={1}
          sx={{
            alignItems: 'center',
            justifyContent: 'space-between',
            gap: 1,
            flexWrap: 'wrap',
          }}
        >
          {hasCrumbs ? (
            <Breadcrumbs sx={{ minWidth: 0 }} separator="›">
              {breadcrumbs!.map((item) =>
                item.to ? (
                  <Link
                    key={item.label}
                    component={RouterLink}
                    to={item.to}
                    underline="hover"
                    color="text.secondary"
                    variant="caption"
                  >
                    {item.label}
                  </Link>
                ) : (
                  <Typography key={item.label} variant="caption" color="text.secondary" sx={{ fontWeight: 600 }}>
                    {item.label}
                  </Typography>
                ),
              )}
            </Breadcrumbs>
          ) : (
            <Box />
          )}
          {hasActions && <ResponsiveActions>{actions}</ResponsiveActions>}
        </Stack>
      )}
      {hasEntity && (
        <Typography
          variant="subtitle1"
          component="h1"
          sx={{
            mt: hasCrumbs || hasActions ? 0.5 : 0,
            fontWeight: 700,
            lineHeight: 1.3,
            fontSize: { xs: '1.05rem', sm: '1.15rem' },
          }}
        >
          {entityLabel}
        </Typography>
      )}
      {entityCaption && (
        <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 0.15, lineHeight: 1.35 }}>
          {entityCaption}
        </Typography>
      )}
    </Box>
  );
}

export function HeaderAction(props: React.ComponentProps<typeof Button>) {
  return <Button variant="contained" {...props} />;
}
