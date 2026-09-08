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
  SwipeableDrawer,
  Tooltip,
  Typography,
  useMediaQuery,
  useTheme,
} from '@mui/material';
import { useEffect, useMemo, useState } from 'react';
import { NavLink, useLocation } from 'react-router-dom';
import { BrandLogo } from '../components/BrandLogo';
import { useAuth } from '../features/auth';
import { getVisibleNavGroupsForPath } from './navAccess';
import {
  getFirstNavLeafPath,
  navItemMatchesPath,
  type NavGroup,
  type NavItem,
} from './navConfig';
import { getWorkspaceFromPath, WORKSPACE_LABELS } from './workspace';

export const SIDEBAR_WIDTH = 264;
export const SIDEBAR_COLLAPSED_WIDTH = 76;

interface SidebarProps {
  mobileOpen: boolean;
  onMobileClose: () => void;
  collapsed: boolean;
  onToggleCollapsed: () => void;
}

function sectionKey(groupId: string, item: NavItem, index: number): string {
  return `${groupId}:${item.id ?? item.path ?? item.label}:${index}`;
}

function NavBadge({ count }: { count: number }) {
  return (
    <Box
      component="span"
      sx={{
        ml: 1,
        px: 0.75,
        minWidth: 20,
        height: 20,
        display: 'inline-flex',
        alignItems: 'center',
        justifyContent: 'center',
        borderRadius: 999,
        bgcolor: 'var(--ef-accent)',
        color: 'var(--ef-primary-dark)',
        fontSize: '0.6875rem',
        fontWeight: 750,
        lineHeight: 1,
      }}
    >
      {count > 99 ? '99+' : count}
    </Box>
  );
}

export function Sidebar({ mobileOpen, onMobileClose, collapsed, onToggleCollapsed }: SidebarProps) {
  const theme = useTheme();
  const isDesktop = useMediaQuery(theme.breakpoints.up('md'));
  const location = useLocation();
  const { user } = useAuth();
  const workspace = getWorkspaceFromPath(location.pathname);
  const visibleGroups = useMemo(
    () => getVisibleNavGroupsForPath(user, location.pathname),
    [user, location.pathname],
  );
  const [openGroups, setOpenGroups] = useState<Record<string, boolean>>({});
  const [openSections, setOpenSections] = useState<Record<string, boolean>>({});

  useEffect(() => {
    const nextGroups: Record<string, boolean> = {};
    const nextSections: Record<string, boolean> = {};
    for (const group of visibleGroups) {
      if (group.children?.some((c) => navItemMatchesPath(c, location.pathname))) {
        nextGroups[group.id] = true;
      }
      group.children?.forEach((child, index) => {
        if (child.children?.length && navItemMatchesPath(child, location.pathname)) {
          nextSections[sectionKey(group.id, child, index)] = true;
        }
      });
    }
    setOpenGroups((prev) => ({ ...prev, ...nextGroups }));
    setOpenSections((prev) => ({ ...prev, ...nextSections }));
  }, [location.pathname, visibleGroups]);

  const toggleGroup = (id: string) => {
    setOpenGroups((prev) => ({ ...prev, [id]: !prev[id] }));
  };

  const toggleSection = (key: string) => {
    setOpenSections((prev) => ({ ...prev, [key]: !prev[key] }));
  };

  const isActivePath = (path: string) => {
    if (path === '/dashboard') return location.pathname === '/dashboard' || location.pathname === '/';
    if (path === '/tresorerie/dashboard') {
      const p = location.pathname;
      return p === '/tresorerie' || p === '/tresorerie/' || p === '/tresorerie/dashboard';
    }
    if (path === '/paiements') {
      const p = location.pathname;
      if (p === '/paiements' || p === '/paiements/') return true;
      if (
        p.startsWith('/paiements/charge-dpm') ||
        p.startsWith('/paiements/junior-') ||
        p.startsWith('/paiements/budget')
      ) {
        return false;
      }
      return p.startsWith('/paiements/');
    }
    return location.pathname === path || location.pathname.startsWith(`${path}/`);
  };

  const width = collapsed && isDesktop ? SIDEBAR_COLLAPSED_WIDTH : SIDEBAR_WIDTH;

  /** Pilule pleine sur l'entrée active, liseré jaune inséré dans la pilule. */
  const navItemSx = {
    borderRadius: '10px',
    minHeight: 40,
    mb: 0.25,
    color: 'var(--ef-sidebar-text)',
    position: 'relative' as const,
    '&:hover': { bgcolor: 'var(--ef-sidebar-hover)' },
    '&.Mui-selected': {
      bgcolor: 'var(--ef-sidebar-active)',
      color: '#fff',
      fontWeight: 700,
      '&:hover': { bgcolor: 'var(--ef-sidebar-active)' },
      '& .MuiListItemIcon-root': { color: '#fff' },
      '&::before': {
        content: '""',
        position: 'absolute',
        left: 4,
        top: 9,
        bottom: 9,
        width: 3,
        borderRadius: 2,
        bgcolor: 'var(--ef-accent)',
      },
    },
  };

  const renderNavChild = (groupId: string, child: NavItem, index: number, depth: number) => {
    const hasNested = Boolean(child.children?.length);
    const key = sectionKey(groupId, child, index);

    if (hasNested) {
      const open = openSections[key] ?? false;
      const sectionActive = navItemMatchesPath(child, location.pathname);
      return (
        <Box key={key}>
          <ListItemButton
            onClick={() => toggleSection(key)}
            sx={{
              ...navItemSx,
              pl: depth === 0 ? 2.25 : 3.5,
              minHeight: 36,
              color: sectionActive ? '#fff' : 'var(--ef-sidebar-text)',
            }}
          >
            <ListItemText
              primary={child.label}
              slotProps={{
                primary: { sx: { fontSize: '0.8125rem', fontWeight: sectionActive ? 700 : 500 } },
              }}
            />
            {open ? <ExpandLess fontSize="small" /> : <ExpandMore fontSize="small" />}
          </ListItemButton>
          <Collapse in={open} timeout="auto" unmountOnExit>
            <List dense disablePadding>
              {child.children!.map((nested, nestedIndex) =>
                renderNavChild(groupId, nested, nestedIndex, depth + 1),
              )}
            </List>
          </Collapse>
        </Box>
      );
    }

    if (!child.path) return null;

    return (
      <ListItemButton
        key={key}
        component={NavLink}
        to={child.path}
        onClick={onMobileClose}
        selected={isActivePath(child.path)}
        sx={{
          ...navItemSx,
          pl: depth === 0 ? 2.25 : 3.5,
          minHeight: 36,
        }}
      >
        <ListItemText
          primary={child.label}
          slotProps={{
            primary: { sx: { fontSize: '0.8125rem', fontWeight: 500 } },
          }}
        />
        {child.badge ? <NavBadge count={child.badge} /> : null}
      </ListItemButton>
    );
  };

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
          height: 64,
          flexShrink: 0,
          borderBottom: '2px solid var(--ef-accent)',
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

      <List
        dense
        sx={{
          flex: 1,
          overflowY: 'auto',
          overflowX: 'hidden',
          px: 1.25,
          py: 1.5,
          '&::-webkit-scrollbar-thumb': {
            backgroundColor: 'rgba(255,255,255,0.18)',
          },
        }}
      >
        {visibleGroups.map((group: NavGroup) => {
          const Icon = group.icon;
          const hasChildren = Boolean(group.children?.length);
          const open = openGroups[group.id] ?? false;
          const groupActive =
            (group.path && isActivePath(group.path)) ||
            group.children?.some((c) => navItemMatchesPath(c, location.pathname));

          if (!hasChildren && group.path) {
            const button = (
              <ListItemButton
                key={group.id}
                component={NavLink}
                to={group.path}
                onClick={onMobileClose}
                selected={isActivePath(group.path)}
                sx={{
                  ...navItemSx,
                  justifyContent: collapsed ? 'center' : 'flex-start',
                  px: collapsed ? 1 : 1.5,
                }}
              >
                <ListItemIcon
                  sx={{
                    minWidth: collapsed ? 0 : 34,
                    color: 'var(--ef-sidebar-text-muted)',
                    justifyContent: 'center',
                  }}
                >
                  <Icon fontSize="small" />
                </ListItemIcon>
                {!collapsed && (
                  <ListItemText
                    primary={group.label}
                    slotProps={{
                      primary: {
                        sx: { fontSize: '0.8438rem', fontWeight: groupActive ? 700 : 550 },
                      },
                    }}
                  />
                )}
                {!collapsed && group.badge ? <NavBadge count={group.badge} /> : null}
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
            const leafPath = getFirstNavLeafPath(group) ?? '#';
            return (
              <Tooltip key={group.id} title={group.label} placement="right">
                <ListItemButton
                  component={NavLink}
                  to={leafPath}
                  onClick={onMobileClose}
                  selected={Boolean(groupActive)}
                  sx={{
                    ...navItemSx,
                    justifyContent: 'center',
                    px: 1,
                  }}
                >
                  <ListItemIcon sx={{ minWidth: 0, color: 'var(--ef-sidebar-text-muted)', justifyContent: 'center' }}>
                    <Icon fontSize="small" />
                  </ListItemIcon>
                </ListItemButton>
              </Tooltip>
            );
          }

          return (
            <Box key={group.id} sx={{ mb: 1 }}>
              {/* En-tête de section : repère typographique, pas une cible principale. */}
              <ListItemButton
                onClick={() => toggleGroup(group.id)}
                sx={{
                  borderRadius: '10px',
                  minHeight: 34,
                  px: 1.5,
                  '&:hover': { bgcolor: 'var(--ef-sidebar-hover)' },
                }}
              >
                <ListItemIcon
                  sx={{
                    minWidth: 30,
                    color: groupActive ? 'var(--ef-accent)' : 'var(--ef-sidebar-text-muted)',
                  }}
                >
                  <Icon sx={{ fontSize: 18 }} />
                </ListItemIcon>
                <ListItemText
                  primary={group.label}
                  slotProps={{
                    primary: {
                      sx: {
                        fontSize: '0.6875rem',
                        fontWeight: 700,
                        letterSpacing: '0.08em',
                        textTransform: 'uppercase',
                        color: groupActive ? 'var(--ef-sidebar-text)' : 'var(--ef-sidebar-text-muted)',
                      },
                    },
                  }}
                />
                {open ? (
                  <ExpandLess sx={{ fontSize: 16, color: 'var(--ef-sidebar-text-muted)' }} />
                ) : (
                  <ExpandMore sx={{ fontSize: 16, color: 'var(--ef-sidebar-text-muted)' }} />
                )}
              </ListItemButton>
              <Collapse in={open} timeout="auto" unmountOnExit>
                <List dense disablePadding sx={{ mt: 0.25 }}>
                  {group.children!.map((child, index) => renderNavChild(group.id, child, index, 0))}
                </List>
              </Collapse>
            </Box>
          );
        })}
      </List>

      {!collapsed && (
        <Box sx={{ px: 2, py: 1.5, borderTop: '1px solid rgba(255,255,255,0.08)', flexShrink: 0 }}>
          <Typography
            sx={{
              fontSize: '0.6875rem',
              fontWeight: 700,
              letterSpacing: '0.06em',
              textTransform: 'uppercase',
              color: 'var(--ef-accent)',
              mb: 0.25,
            }}
          >
            {WORKSPACE_LABELS[workspace]}
          </Typography>
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
    <SwipeableDrawer
      open={mobileOpen}
      onClose={onMobileClose}
      onOpen={() => {}}
      disableSwipeToOpen
      swipeAreaWidth={0}
      ModalProps={{ keepMounted: true }}
      sx={{
        [`& .MuiDrawer-paper`]: { width: SIDEBAR_WIDTH, boxSizing: 'border-box' },
      }}
    >
      {content}
    </SwipeableDrawer>
  );
}
