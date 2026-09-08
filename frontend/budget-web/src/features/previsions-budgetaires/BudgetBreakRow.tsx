import ExpandLessIcon from '@mui/icons-material/ExpandLess';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import { Box, IconButton, TableCell, TableRow, Typography } from '@mui/material';
import type { ReactNode } from 'react';
import { GROUPE_NIVEAU1_PREVISION } from '../rubriques-budgetaires/hierarchy';

interface BudgetBreakRowProps {
  code: ReactNode;
  label: ReactNode;
  cumul: ReactNode;
  monthCells?: ReactNode;
  height?: number;
  collapsed?: boolean;
  showActionsSpacer?: boolean;
  onToggleCollapse?: () => void;
}

/** Ligne de rupture Groupe N1 (non éditable) — bleu-gris sober. */
export function BudgetBreakRow({
  code,
  label,
  cumul,
  monthCells,
  height = 44,
  collapsed = false,
  showActionsSpacer = false,
  onToggleCollapse,
}: BudgetBreakRowProps) {
  const labelText = typeof label === 'string' ? label : undefined;
  return (
    <TableRow
      sx={{
        bgcolor: GROUPE_NIVEAU1_PREVISION.bgcolor,
        color: GROUPE_NIVEAU1_PREVISION.color,
        height,
        '& td': {
          color: 'inherit',
          fontWeight: 700,
          borderBottomColor: 'rgba(0,0,0,0.06)',
          py: 0.85,
        },
        '& td:first-of-type': {
          borderLeft: GROUPE_NIVEAU1_PREVISION.borderLeft,
        },
      }}
    >
      <TableCell sx={{ fontFamily: 'ui-monospace, monospace', width: 100, whiteSpace: 'nowrap' }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.25 }}>
          {onToggleCollapse && (
            <IconButton
              size="small"
              onClick={(e) => {
                e.stopPropagation();
                onToggleCollapse();
              }}
              aria-label={collapsed ? 'Déplier le groupe' : 'Replier le groupe'}
              sx={{ color: 'inherit', p: 0.25 }}
            >
              {collapsed ? <ExpandMoreIcon fontSize="small" /> : <ExpandLessIcon fontSize="small" />}
            </IconButton>
          )}
          {code}
        </Box>
      </TableCell>
      {showActionsSpacer && <TableCell sx={{ width: 40, p: 0 }} />}
      <TableCell sx={{ width: 300, maxWidth: 300 }}>
        <Typography
          component="span"
          title={labelText}
          sx={{
            display: 'block',
            fontWeight: 700,
            fontSize: '0.875rem',
            letterSpacing: 0.2,
            whiteSpace: 'nowrap',
            overflow: 'hidden',
            textOverflow: 'ellipsis',
          }}
        >
          {label}
        </Typography>
      </TableCell>
      <TableCell align="right" sx={{ width: 104, fontWeight: 700, whiteSpace: 'nowrap' }}>
        {cumul}
      </TableCell>
      {monthCells}
    </TableRow>
  );
}

/** Pastille légende pour la barre d’outils. */
export function BudgetBreakLegend() {
  return (
    <Box sx={{ display: 'flex', gap: 1.5, flexWrap: 'wrap', alignItems: 'center' }}>
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.75 }}>
        <Box
          sx={{
            width: 14,
            height: 14,
            bgcolor: GROUPE_NIVEAU1_PREVISION.bgcolor,
            border: `1px solid ${GROUPE_NIVEAU1_PREVISION.border}`,
            borderRadius: 0.5,
          }}
        />
        <Typography variant="caption" color="text.secondary">
          Groupe niveau 1
        </Typography>
      </Box>
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.75 }}>
        <Box
          sx={{
            width: 14,
            height: 14,
            bgcolor: 'background.paper',
            border: '1px solid',
            borderColor: 'divider',
            borderRadius: 0.5,
          }}
        />
        <Typography variant="caption" color="text.secondary">
          Rubrique budgétaire (RB)
        </Typography>
      </Box>
    </Box>
  );
}
