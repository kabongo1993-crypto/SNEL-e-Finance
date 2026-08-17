import ChevronLeftIcon from '@mui/icons-material/ChevronLeft';
import ChevronRightIcon from '@mui/icons-material/ChevronRight';
import ExpandLess from '@mui/icons-material/ExpandLess';
import ExpandMore from '@mui/icons-material/ExpandMore';
import {
  Box,
  Collapse,
  Drawer,
  IconButton,
  List,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Tooltip,
  Typography,
  useMediaQuery,
  useTheme,
} from '@mui/material';
import { useEffect, useState } from 'react';
import { NavLink, useLocation } from 'react-router-dom';
import { BrandLogo } from '../components/BrandLogo';
import { navGroups } from './navConfig';

export const SIDEBAR_WIDTH = 248;
export const SIDEBAR_COLLAPSED_WIDTH = 72;

interface SidebarProps {
  mobileOpen: boolean;
  onMobileClose: () => void;
  collapsed: boolean;
  onToggleCollapsed: () => void;
}

export function Sidebar({ mobileOpen, onMobileClose, collapsed, onToggleCollapsed }: SidebarProps) {
  const theme = useTheme();
  const isDesktop = useMediaQuery(theme.breakpoints.up('md'));
  const location = useLocation();
  const [openGroups, setOpenGroups] = useState<Record<string, boolean>>({});

  useEffect(() => {
    const next: Record<string, boolean> = {};
    for (const group of navGroups) {
      if (group.children?.some((c) => location.pathname === c.path || location.pathname.startsWith(`${c.path}/`))) {
        next[group.id] = true;
      }
    }
    setOpenGroups((prev) => ({ ...prev, ...next }));
  }, [location.pathname]);

  const toggleGroup = (id: string) => {
    setOpenGroups((prev) => ({ ...prev, [id]: !prev[id] }));
  };

  const isActivePath = (path: string) => {
    if (path === '/dashboard') return location.pathname === '/dashboard' || location.pathname === '/';
    if (path === '/paiements') {
      return location.pathname === '/paiements' || location.pathname.startsWith('/paiements/');
    }
    if (path === '/engagements') {
      return location.pathname === '/engagements' || location.pathname.startsWith('/engagements/');
    }
    if (path === '/tresorerie') {
      return location.pathname === '/tresorerie' || location.pathname.startsWith('/tresorerie/');
    }
    if (path === '/rapports') return location.pathname === '/rapports';
    return location.pathname === path || location.pathname.startsWith(`${path}/`);
  };

  const width = collapsed && isDesktop ? SIDEBAR_COLLAPSED_WIDTH : SIDEBAR_WIDTH;

  const content = (
    <Box
      sx={{
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        bgcolor: 'var(--ef-sidebar)',
        color: 'var(--ef-sidebar-text)',
      }}
    >
      <Box
        sx={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: collapsed ? 'center' : 'space-between',
          px: collapsed ? 1 : 2,
          py: 1.75,
          borderBottom: '1px solid rgba(255,255,255,0.08)',
          minHeight: 56,
        }}
      >
        <BrandLogo compact={collapsed && isDesktop} inverted />
        {isDesktop && !collapsed && (
          <IconButton
            size="small"
            onClick={onToggleCollapsed}
            aria-label="Réduire le menu"
            sx={{ color: 'var(--ef-sidebar-text-muted)' }}
          >
            <ChevronLeftIcon fontSize="small" />
          </IconButton>
        )}
      </Box>

      {isDesktop && collapsed && (
        <Box sx={{ display: 'flex', justifyContent: 'center', py: 1 }}>
          <IconButton
            size="small"
            onClick={onToggleCollapsed}
            aria-label="Étendre le menu"
            sx={{ color: 'var(--ef-sidebar-text-muted)' }}
          >
            <ChevronRightIcon fontSize="small" />
          </IconButton>
        </Box>
      )}

      <List dense sx={{ flex: 1, overflow: 'auto', px: 1, py: 1.25 }}>
        {navGroups.map((group) => {
          const Icon = group.icon;
          const hasChildren = Boolean(group.children?.length);
          const open = openGroups[group.id] ?? false;
          const groupActive =
            (group.path && isActivePath(group.path)) ||
            group.children?.some((c) => isActivePath(c.path));

          if (!hasChildren && group.path) {
            const button = (
              <ListItemButton
                key={group.id}
                component={NavLink}
                to={group.path}
                onClick={onMobileClose}
                selected={isActivePath(group.path)}
                sx={{
                  borderRadius: 1,
                  mb: 0.25,
                  justifyContent: collapsed ? 'center' : 'flex-start',
                  px: collapsed ? 1 : 1.25,
                  color: 'var(--ef-sidebar-text)',
                  '&.Mui-selected': {
                    bgcolor: 'var(--ef-sidebar-active)',
                    color: '#fff',
                    '& .MuiListItemIcon-root': { color: '#fff' },
                  },
                  '&:hover': { bgcolor: 'var(--ef-sidebar-hover)' },
                }}
              >
                <ListItemIcon sx={{ minWidth: collapsed ? 0 : 34, color: 'inherit', justifyContent: 'center' }}>
                  <Icon fontSize="small" />
                </ListItemIcon>
                {!collapsed && (
                  <ListItemText
                    primary={group.label}
                    slotProps={{
                      primary: {
                        sx: { fontSize: '0.8125rem', fontWeight: groupActive ? 700 : 500 },
                      },
                    }}
                  />
                )}
              </ListItemButton>
            );
            return collapsed ? (
              <Tooltip key={group.id} title={group.label} placement="right">
                {button}
              </Tooltip>
            ) : (
              button
            );
          }

          if (collapsed) {
            const firstChild = group.children?.[0];
            return (
              <Tooltip key={group.id} title={group.label} placement="right">
                <ListItemButton
                  component={NavLink}
                  to={firstChild?.path ?? '#'}
                  onClick={onMobileClose}
                  selected={Boolean(groupActive)}
                  sx={{
                    borderRadius: 1,
                    mb: 0.25,
                    justifyContent: 'center',
                    px: 1,
                    color: 'var(--ef-sidebar-text)',
                    '&.Mui-selected': { bgcolor: 'var(--ef-sidebar-active)', color: '#fff' },
                    '&:hover': { bgcolor: 'var(--ef-sidebar-hover)' },
                  }}
                >
                  <ListItemIcon sx={{ minWidth: 0, color: 'inherit', justifyContent: 'center' }}>
                    <Icon fontSize="small" />
                  </ListItemIcon>
                </ListItemButton>
              </Tooltip>
            );
          }

          return (
            <Box key={group.id} sx={{ mb: 0.35 }}>
              <ListItemButton
                onClick={() => toggleGroup(group.id)}
                sx={{
                  borderRadius: 1,
                  color: groupActive ? '#fff' : 'var(--ef-sidebar-text)',
                  bgcolor: groupActive ? 'rgba(255,255,255,0.04)' : 'transparent',
                  '&:hover': { bgcolor: 'var(--ef-sidebar-hover)' },
                }}
              >
                <ListItemIcon sx={{ minWidth: 34, color: 'inherit' }}>
                  <Icon fontSize="small" />
                </ListItemIcon>
                <ListItemText
                  primary={group.label}
                  slotProps={{
                    primary: {
                      sx: {
                        fontSize: '0.75rem',
                        fontWeight: 700,
                        letterSpacing: '0.04em',
                        textTransform: 'uppercase',
                        color: 'var(--ef-sidebar-text-muted)',
                      },
                    },
                  }}
                />
                {open ? <ExpandLess fontSize="small" /> : <ExpandMore fontSize="small" />}
              </ListItemButton>
              <Collapse in={open} timeout="auto" unmountOnExit>
                <List dense disablePadding>
                  {group.children!.map((child) => (
                    <ListItemButton
                      key={child.path}
                      component={NavLink}
                      to={child.path}
                      onClick={onMobileClose}
                      selected={isActivePath(child.path)}
                      sx={{
                        pl: 5,
                        borderRadius: 1,
                        mb: 0.1,
                        color: 'var(--ef-sidebar-text)',
                        '&.Mui-selected': {
                          bgcolor: 'var(--ef-sidebar-active)',
                          color: '#fff',
                        },
                        '&:hover': { bgcolor: 'var(--ef-sidebar-hover)' },
                      }}
                    >
                      <ListItemText
                        primary={child.label}
                        slotProps={{
                          primary: { sx: { fontSize: '0.8125rem', fontWeight: 500 } },
                        }}
                      />
                    </ListItemButton>
                  ))}
                </List>
              </Collapse>
            </Box>
          );
        })}
      </List>

      {!collapsed && (
        <Box sx={{ px: 2, py: 1.5, borderTop: '1px solid rgba(255,255,255,0.08)' }}>
          <Typography sx={{ fontSize: '0.65rem', color: 'var(--ef-sidebar-text-muted)' }}>
            Plateforme de gestion financière
          </Typography>
        </Box>
      )}
    </Box>
  );

  if (isDesktop) {
    return (
      <Drawer
        variant="permanent"
        sx={{
          width,
          flexShrink: 0,
          transition: 'width 180ms ease',
          [`& .MuiDrawer-paper`]: {
            width,
            boxSizing: 'border-box',
            borderRight: 'none',
            transition: 'width 180ms ease',
            overflowX: 'hidden',
          },
        }}
        open
      >
        {content}
      </Drawer>
    );
  }

  return (
    <Drawer
      variant="temporary"
      open={mobileOpen}
      onClose={onMobileClose}
      ModalProps={{ keepMounted: true }}
      sx={{
        [`& .MuiDrawer-paper`]: { width: SIDEBAR_WIDTH, boxSizing: 'border-box', borderRight: 'none' },
      }}
    >
      {content}
    </Drawer>
  );
}
