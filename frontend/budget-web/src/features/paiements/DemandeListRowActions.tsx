import MoreVertIcon from '@mui/icons-material/MoreVert';
import {
  IconButton,
  ListItemIcon,
  ListItemText,
  Menu,
  MenuItem,
  Stack,
} from '@mui/material';
import { useState, type ReactNode } from 'react';
import { defaultActionIcons, type DataTableAction } from '../../components';

interface DemandeListRowActionsProps<T> {
  row: T;
  actions: DataTableAction<T>[];
}

/**
 * Menu ⋮ identique au module référentiels (DataTable) : icônes + libellés.
 */
export function DemandeListRowActions<T>({ row, actions }: DemandeListRowActionsProps<T>) {
  const [anchor, setAnchor] = useState<null | HTMLElement>(null);
  const visible = actions.filter((a) => !(a.hidden?.(row) ?? false));

  if (visible.length === 0) return null;

  const closeMenu = () => setAnchor(null);

  return (
    <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'flex-end' }}>
      <IconButton
        size="small"
        aria-label="Actions"
        onClick={(e) => setAnchor(e.currentTarget)}
      >
        <MoreVertIcon fontSize="small" />
      </IconButton>
      <Menu
        anchorEl={anchor}
        open={Boolean(anchor)}
        onClose={closeMenu}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
        transformOrigin={{ vertical: 'top', horizontal: 'right' }}
        slotProps={{ paper: { sx: { minWidth: 200 } } }}
      >
        {visible.map((action) => (
          <MenuItem
            key={action.id}
            disabled={action.disabled?.(row) ?? false}
            onClick={() => {
              if (action.disabled?.(row)) return;
              action.onClick(row);
              closeMenu();
            }}
            sx={action.color === 'error' ? { color: 'error.main' } : undefined}
          >
            <ListItemIcon sx={action.color === 'error' ? { color: 'error.main' } : undefined}>
              {action.icon ?? defaultActionIcons[action.id] ?? (<MoreVertIcon fontSize="small" /> as ReactNode)}
            </ListItemIcon>
            <ListItemText>{action.label}</ListItemText>
          </MenuItem>
        ))}
      </Menu>
    </Stack>
  );
}
