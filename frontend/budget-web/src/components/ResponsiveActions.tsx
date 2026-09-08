import MoreHorizIcon from '@mui/icons-material/MoreHoriz';
import { Box, IconButton, Menu, Stack } from '@mui/material';
import { Children, useState, type ReactNode } from 'react';
import { useResponsive } from '../theme/useResponsive';

interface ResponsiveActionsProps {
  children: ReactNode;
  /**
   * Nombre d'actions laissées visibles sous `md` ; les suivantes passent
   * dans le menu « … ». La première action est censée être la principale.
   */
  keepVisible?: number;
}

/**
 * Aligne les actions en ligne sur grand écran et replie le surplus dans un
 * menu sur petit écran, pour éviter les barres d'actions qui débordent.
 */
export function ResponsiveActions({ children, keepVisible = 1 }: ResponsiveActionsProps) {
  const { isMobile } = useResponsive();
  const [anchor, setAnchor] = useState<null | HTMLElement>(null);
  const items = Children.toArray(children).filter(Boolean);

  const inline = (
    <Stack direction="row" spacing={0.75} useFlexGap sx={{ flexWrap: 'wrap', flexShrink: 0 }}>
      {children}
    </Stack>
  );

  if (!isMobile || items.length <= keepVisible + 1) {
    return inline;
  }

  const visible = items.slice(0, keepVisible);
  const overflow = items.slice(keepVisible);

  return (
    <Stack direction="row" spacing={0.5} sx={{ alignItems: 'center', flexShrink: 0 }}>
      {visible}
      <IconButton
        size="small"
        aria-label="Plus d'actions"
        onClick={(e) => setAnchor(e.currentTarget)}
        sx={{ border: '1px solid', borderColor: 'divider' }}
      >
        <MoreHorizIcon fontSize="small" />
      </IconButton>
      <Menu
        anchorEl={anchor}
        open={Boolean(anchor)}
        onClose={() => setAnchor(null)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
        transformOrigin={{ vertical: 'top', horizontal: 'right' }}
        slotProps={{ paper: { sx: { minWidth: 220, p: 1 } } }}
      >
        <Stack
          spacing={0.75}
          onClick={() => setAnchor(null)}
          sx={{ '& > *': { width: '100%', justifyContent: 'flex-start' } }}
        >
          {overflow.map((item, index) => (
            <Box key={index} sx={{ display: 'flex' }}>
              {item}
            </Box>
          ))}
        </Stack>
      </Menu>
    </Stack>
  );
}
