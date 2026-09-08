import ExpandLessIcon from '@mui/icons-material/ExpandLess';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import CloseIcon from '@mui/icons-material/Close';
import {
  Box,
  Collapse,
  Divider,
  IconButton,
  Paper,
  Stack,
  Typography,
} from '@mui/material';
import { useId, useState, type ReactNode } from 'react';
import { formSectionBodyGap, formSectionPad } from './formTokens';

interface FormSectionProps {
  title: string;
  description?: string;
  children: ReactNode;
  actions?: ReactNode;
  /** Sections de formulaire réductibles (défaut : true). */
  collapsible?: boolean;
  /** État initial si collapsible (défaut : true = ouvert). */
  defaultExpanded?: boolean;
  /** Contrôle externe optionnel. */
  expanded?: boolean;
  onExpandedChange?: (expanded: boolean) => void;
}

/**
 * Section de formulaire standard — réductible, réutilisable partout.
 * La réduction ne touche pas aux données ni à la logique métier.
 */
export function FormSection({
  title,
  description,
  children,
  actions,
  collapsible = true,
  defaultExpanded = true,
  expanded: expandedProp,
  onExpandedChange,
}: FormSectionProps) {
  const [internalExpanded, setInternalExpanded] = useState(defaultExpanded);
  const expanded = expandedProp ?? internalExpanded;
  const panelId = useId();

  const setExpanded = (next: boolean) => {
    if (expandedProp === undefined) setInternalExpanded(next);
    onExpandedChange?.(next);
  };

  const toggle = () => setExpanded(!expanded);

  return (
    <Paper
      variant="outlined"
      sx={{
        p: formSectionPad,
        mb: 2,
        // Ne pas clipper les popovers Autocomplete / menus.
        overflow: 'visible',
      }}
    >
      <Box
        sx={{
          display: 'flex',
          alignItems: 'flex-start',
          justifyContent: 'space-between',
          gap: 1,
          mb: expanded ? 1.5 : 0,
        }}
      >
        <Box sx={{ minWidth: 0, flex: 1 }}>
          <Typography variant="subtitle1" sx={{ fontWeight: 700, lineHeight: 1.3 }}>
            {title}
          </Typography>
          {description && expanded && (
            <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
              {description}
            </Typography>
          )}
        </Box>
        {collapsible && (
          <IconButton
            size="small"
            onClick={toggle}
            aria-expanded={expanded}
            aria-controls={panelId}
            aria-label={expanded ? `Réduire « ${title} »` : `Développer « ${title} »`}
            sx={{ flexShrink: 0, mt: -0.25 }}
          >
            {expanded ? <ExpandLessIcon /> : <ExpandMoreIcon />}
          </IconButton>
        )}
      </Box>

      <Collapse
        in={expanded}
        timeout={180}
        unmountOnExit={false}
        sx={{
          // Labels flottants + menus Autocomplete : ne pas clipper au wrap.
          overflow: 'visible',
          '& .MuiCollapse-wrapper, & .MuiCollapse-wrapperInner': {
            overflow: 'visible',
          },
        }}
      >
        <Box
          id={panelId}
          sx={{
            display: 'flex',
            flexDirection: 'column',
            gap: formSectionBodyGap,
          }}
        >
          <Divider sx={{ mb: 0 }} />
          {children}
          {actions && (
            <Box
              sx={{
                display: 'flex',
                flexDirection: { xs: 'column', sm: 'row' },
                justifyContent: 'flex-end',
                gap: 1,
              }}
            >
              {actions}
            </Box>
          )}
        </Box>
      </Collapse>
    </Paper>
  );
}

interface DetailPanelProps {
  title: string;
  children: ReactNode;
  actions?: ReactNode;
  onClose?: () => void;
  disableSurface?: boolean;
}

export function DetailPanel({
  title,
  children,
  actions,
  onClose,
  disableSurface,
}: DetailPanelProps) {
  const body = (
    <>
      <Box
        sx={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          mb: 2,
          gap: 1,
          flexWrap: 'wrap',
        }}
      >
        <Typography variant="h6" sx={{ minWidth: 0 }}>
          {title}
        </Typography>
        <Stack direction="row" spacing={0.5} sx={{ alignItems: 'center', flexShrink: 0 }}>
          {actions}
          {onClose && (
            <IconButton size="small" onClick={onClose} aria-label="Fermer le détail">
              <CloseIcon fontSize="small" />
            </IconButton>
          )}
        </Stack>
      </Box>
      <Divider sx={{ mb: 2 }} />
      {children}
    </>
  );

  if (disableSurface) {
    return <Box sx={{ p: { xs: 2, md: 3 } }}>{body}</Box>;
  }

  return <Paper sx={{ p: { xs: 2, md: 3 }, mb: 2 }}>{body}</Paper>;
}
