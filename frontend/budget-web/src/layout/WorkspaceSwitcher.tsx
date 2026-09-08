import type { MouseEvent } from 'react';
import { Box, ToggleButton, ToggleButtonGroup, Typography } from '@mui/material';
import { useLocation, useNavigate } from 'react-router-dom';
import { hasAnyPerm, PERMS_ADMIN_TECH, useAuth } from '../features/auth';
import { getWorkspaceFromPath, WORKSPACE_HOME, WORKSPACE_LABELS, type Workspace } from './workspace';

export function WorkspaceSwitcher() {
  const navigate = useNavigate();
  const location = useLocation();
  const { user } = useAuth();
  const workspace = getWorkspaceFromPath(location.pathname);
  const canAccessTresorerie = hasAnyPerm(user, PERMS_ADMIN_TECH);

  const handleChange = (_: MouseEvent<HTMLElement>, next: Workspace | null) => {
    if (!next || next === workspace) return;
    navigate(WORKSPACE_HOME[next]);
  };

  return (
    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, minWidth: 0 }}>
      <Typography
        variant="caption"
        sx={{
          display: { xs: 'none', lg: 'block' },
          fontWeight: 600,
          color: 'text.secondary',
          letterSpacing: '0.04em',
          textTransform: 'uppercase',
          whiteSpace: 'nowrap',
        }}
      >
        Espace
      </Typography>
      <ToggleButtonGroup
        exclusive
        size="small"
        value={workspace}
        onChange={handleChange}
        aria-label="Espace métier"
        sx={{
          flexShrink: 0,
          bgcolor: 'var(--ef-surface-secondary)',
          borderRadius: 999,
          p: 0.25,
          '& .MuiToggleButtonGroup-grouped': {
            border: 0,
            borderRadius: '999px !important',
            mx: 0,
            px: { xs: 1.25, sm: 1.75 },
            py: 0.5,
            fontSize: '0.8125rem',
            fontWeight: 600,
            textTransform: 'none',
            color: 'text.secondary',
            '&.Mui-selected': {
              bgcolor: 'background.paper',
              color: 'text.primary',
              boxShadow: '0 1px 2px rgba(0,0,0,0.06)',
              '&:hover': { bgcolor: 'background.paper' },
            },
          },
        }}
      >
        <ToggleButton value="budget" aria-label={WORKSPACE_LABELS.budget}>
          {WORKSPACE_LABELS.budget}
        </ToggleButton>
        {canAccessTresorerie ? (
          <ToggleButton value="tresorerie" aria-label={WORKSPACE_LABELS.tresorerie}>
            {WORKSPACE_LABELS.tresorerie}
          </ToggleButton>
        ) : null}
      </ToggleButtonGroup>
    </Box>
  );
}
