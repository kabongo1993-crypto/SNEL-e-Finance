import DarkModeOutlinedIcon from '@mui/icons-material/DarkModeOutlined';
import HelpOutlineOutlinedIcon from '@mui/icons-material/HelpOutlineOutlined';
import LightModeOutlinedIcon from '@mui/icons-material/LightModeOutlined';
import LockOutlinedIcon from '@mui/icons-material/LockOutlined';
import LogoutIcon from '@mui/icons-material/Logout';
import MenuIcon from '@mui/icons-material/Menu';
import NotificationsNoneIcon from '@mui/icons-material/NotificationsNone';
import PersonOutlinedIcon from '@mui/icons-material/PersonOutlined';
import SettingsOutlinedIcon from '@mui/icons-material/SettingsOutlined';
import SettingsBrightnessOutlinedIcon from '@mui/icons-material/SettingsBrightnessOutlined';
import {
  AppBar,
  Avatar,
  Badge,
  Box,
  Divider,
  IconButton,
  ListItemIcon,
  ListItemText,
  Menu,
  MenuItem,
  Radio,
  Toolbar,
  Typography,
} from '@mui/material';
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { BRAND_NAME, useThemeMode, type ThemePreference } from '../theme';
import { ChangePasswordDialog, redirectToLoginAfterLogout, useAuth } from '../features/auth';
import { SIDEBAR_COLLAPSED_WIDTH, SIDEBAR_WIDTH } from './Sidebar';
import { TopBarTauxStrip } from './TopBarTauxStrip';
import { WorkspaceSwitcher } from './WorkspaceSwitcher';

interface TopBarProps {
  onMenuClick: () => void;
  sidebarCollapsed: boolean;
}

/** Hauteur de la barre fixe — partagée avec le spacer de `AppShell`. */
export const TOPBAR_HEIGHT = 64;

const themeOptions: { value: ThemePreference; label: string; icon: typeof LightModeOutlinedIcon }[] = [
  { value: 'light', label: 'Clair', icon: LightModeOutlinedIcon },
  { value: 'dark', label: 'Sombre', icon: DarkModeOutlinedIcon },
  { value: 'system', label: 'Système', icon: SettingsBrightnessOutlinedIcon },
];

export function TopBar({ onMenuClick, sidebarCollapsed }: TopBarProps) {
  const navigate = useNavigate();
  const { preference, setPreference } = useThemeMode();
  const { user, displayName, primaryRole, logout } = useAuth();
  const [notifAnchor, setNotifAnchor] = useState<null | HTMLElement>(null);
  const [profileAnchor, setProfileAnchor] = useState<null | HTMLElement>(null);
  const [themeAnchor, setThemeAnchor] = useState<null | HTMLElement>(null);
  const [changePasswordOpen, setChangePasswordOpen] = useState(false);

  const sidebarWidth = sidebarCollapsed ? SIDEBAR_COLLAPSED_WIDTH : SIDEBAR_WIDTH;
  const initials = (displayName || user?.nomUtilisateur || '?')
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((p) => p[0]?.toUpperCase() ?? '')
    .join('');
  const subtitle = primaryRole || user?.email || user?.nomUtilisateur || BRAND_NAME;

  return (
    <AppBar
      position="fixed"
      color="inherit"
      elevation={0}
      sx={{
        width: { md: `calc(100% - ${sidebarWidth}px)` },
        ml: { md: `${sidebarWidth}px` },
        bgcolor: 'var(--ef-topbar)',
        borderBottom: '1px solid',
        borderColor: 'divider',
        color: 'text.primary',
        transition: 'width 180ms ease, margin 180ms ease',
      }}
    >
      <Toolbar sx={{ gap: { xs: 0.5, sm: 1 }, minHeight: `${TOPBAR_HEIGHT}px !important`, px: { xs: 1, sm: 2, md: 2.5 } }}>
        <IconButton
          edge="start"
          onClick={onMenuClick}
          sx={{ display: { md: 'none' } }}
          aria-label="Ouvrir le menu"
        >
          <MenuIcon />
        </IconButton>

        <Box sx={{ display: { xs: 'none', md: 'flex' }, alignItems: 'center', gap: 2, minWidth: 0 }}>
          <WorkspaceSwitcher />
          <TopBarTauxStrip />
        </Box>

        <Box sx={{ display: { xs: 'flex', md: 'none' }, alignItems: 'center', minWidth: 0, flex: 1 }}>
          <WorkspaceSwitcher />
        </Box>

        <Box sx={{ flex: { xs: 0, md: 1 }, minWidth: 8 }} />

        {/* Icônes regroupées dans un même bloc pour les lire comme un ensemble. */}
        <Box
          sx={{
            display: 'flex',
            alignItems: 'center',
            gap: 0.25,
            p: 0.375,
            borderRadius: 999,
            bgcolor: 'var(--ef-surface-secondary)',
          }}
        >
          <IconButton
            aria-label="Thème"
            onClick={(e) => setThemeAnchor(e.currentTarget)}
            size="small"
            sx={{ color: 'text.secondary', borderRadius: '50%' }}
          >
            {preference === 'dark' ? (
              <DarkModeOutlinedIcon fontSize="small" />
            ) : preference === 'light' ? (
              <LightModeOutlinedIcon fontSize="small" />
            ) : (
              <SettingsBrightnessOutlinedIcon fontSize="small" />
            )}
          </IconButton>

          <IconButton
            aria-label="Aide"
            onClick={() => navigate('/administration/parametres')}
            size="small"
            sx={{ color: 'text.secondary', borderRadius: '50%', display: { xs: 'none', sm: 'inline-flex' } }}
          >
            <HelpOutlineOutlinedIcon fontSize="small" />
          </IconButton>

          <IconButton
            aria-label="Notifications"
            onClick={(e) => setNotifAnchor(e.currentTarget)}
            size="small"
            sx={{ color: 'text.secondary', borderRadius: '50%' }}
          >
            <Badge badgeContent={3} color="error" max={9}>
              <NotificationsNoneIcon fontSize="small" />
            </Badge>
          </IconButton>
        </Box>

        <Box
          onClick={(e) => setProfileAnchor(e.currentTarget)}
          sx={{
            display: 'flex',
            alignItems: 'center',
            gap: 1,
            cursor: 'pointer',
            pl: 0.5,
            pr: { xs: 0.5, md: 1.25 },
            py: 0.5,
            ml: 0.5,
            borderRadius: 999,
            border: '1px solid',
            borderColor: { xs: 'transparent', md: 'var(--ef-border-subtle)' },
            transition: 'background-color 150ms ease, border-color 150ms ease',
            '&:hover': { bgcolor: 'action.hover' },
          }}
        >
          <Avatar
            sx={{
              width: 32,
              height: 32,
              bgcolor: 'var(--ef-primary)',
              fontSize: '0.75rem',
              fontWeight: 700,
              color: '#fff',
            }}
          >
            {initials || '?'}
          </Avatar>
          <Box sx={{ display: { xs: 'none', md: 'block' }, minWidth: 0 }}>
            <Typography variant="body2" noWrap sx={{ fontWeight: 700, lineHeight: 1.2, color: 'text.primary' }}>
              {displayName || 'Utilisateur'}
            </Typography>
            <Typography variant="caption" color="text.secondary" noWrap sx={{ lineHeight: 1.2 }}>
              {subtitle}
            </Typography>
          </Box>
        </Box>
      </Toolbar>

      <Menu
        anchorEl={themeAnchor}
        open={Boolean(themeAnchor)}
        onClose={() => setThemeAnchor(null)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
        transformOrigin={{ vertical: 'top', horizontal: 'right' }}
        slotProps={{ paper: { sx: { width: 200, mt: 1 } } }}
      >
        <Box sx={{ px: 2, py: 1 }}>
          <Typography variant="subtitle2">Thème</Typography>
        </Box>
        <Divider />
        {themeOptions.map((opt) => {
          const Icon = opt.icon;
          return (
            <MenuItem
              key={opt.value}
              selected={preference === opt.value}
              onClick={() => {
                setPreference(opt.value);
                setThemeAnchor(null);
              }}
            >
              <ListItemIcon>
                <Icon fontSize="small" />
              </ListItemIcon>
              <ListItemText>{opt.label}</ListItemText>
              <Radio size="small" checked={preference === opt.value} />
            </MenuItem>
          );
        })}
      </Menu>

      <Menu
        anchorEl={notifAnchor}
        open={Boolean(notifAnchor)}
        onClose={() => setNotifAnchor(null)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
        transformOrigin={{ vertical: 'top', horizontal: 'right' }}
        slotProps={{ paper: { sx: { width: 320, mt: 1 } } }}
      >
        <Box sx={{ px: 2, py: 1.25 }}>
          <Typography sx={{ fontWeight: 700 }}>Notifications</Typography>
        </Box>
        <Divider />
        <MenuItem onClick={() => setNotifAnchor(null)}>
          <ListItemText
            primary="3 paiements en attente de validation"
            secondary="Il y a 12 min"
            slotProps={{ primary: { variant: 'body2' }, secondary: { variant: 'caption' } }}
          />
        </MenuItem>
        <MenuItem onClick={() => setNotifAnchor(null)}>
          <ListItemText
            primary="Alerte exécution budgétaire UB-014"
            secondary="Il y a 1 h"
            slotProps={{ primary: { variant: 'body2' }, secondary: { variant: 'caption' } }}
          />
        </MenuItem>
        <MenuItem onClick={() => setNotifAnchor(null)}>
          <ListItemText
            primary="Rapprochement bancaire à finaliser"
            secondary="Aujourd’hui"
            slotProps={{ primary: { variant: 'body2' }, secondary: { variant: 'caption' } }}
          />
        </MenuItem>
      </Menu>

      <Menu
        anchorEl={profileAnchor}
        open={Boolean(profileAnchor)}
        onClose={() => setProfileAnchor(null)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
        transformOrigin={{ vertical: 'top', horizontal: 'right' }}
        slotProps={{ paper: { sx: { width: 260, mt: 1 } } }}
      >
        <Box sx={{ px: 2, py: 1.5 }}>
          <Typography sx={{ fontWeight: 700 }}>{displayName || 'Utilisateur'}</Typography>
          <Typography variant="caption" color="text.secondary">
            {user?.email || user?.nomUtilisateur || BRAND_NAME}
          </Typography>
        </Box>
        <Divider />
        <MenuItem
          onClick={() => {
            setProfileAnchor(null);
            navigate('/administration/utilisateurs');
          }}
        >
          <ListItemIcon>
            <PersonOutlinedIcon fontSize="small" />
          </ListItemIcon>
          <ListItemText>Mon profil</ListItemText>
        </MenuItem>
        <MenuItem
          onClick={() => {
            setProfileAnchor(null);
            setChangePasswordOpen(true);
          }}
        >
          <ListItemIcon>
            <LockOutlinedIcon fontSize="small" />
          </ListItemIcon>
          <ListItemText>Changer le mot de passe</ListItemText>
        </MenuItem>
        <MenuItem
          onClick={() => {
            setProfileAnchor(null);
            navigate('/administration/parametres');
          }}
        >
          <ListItemIcon>
            <SettingsOutlinedIcon fontSize="small" />
          </ListItemIcon>
          <ListItemText>Paramètres</ListItemText>
        </MenuItem>
        <Divider />
        <Box sx={{ px: 2, py: 0.75 }}>
          <Typography variant="caption" color="text.secondary">
            Thème d’affichage
          </Typography>
        </Box>
        {themeOptions.map((opt) => {
          const Icon = opt.icon;
          return (
            <MenuItem
              key={opt.value}
              selected={preference === opt.value}
              onClick={() => setPreference(opt.value)}
            >
              <ListItemIcon>
                <Icon fontSize="small" />
              </ListItemIcon>
              <ListItemText>{opt.label}</ListItemText>
              <Radio size="small" checked={preference === opt.value} />
            </MenuItem>
          );
        })}
        <Divider />
        <MenuItem
          onClick={() => {
            setProfileAnchor(null);
            logout();
            redirectToLoginAfterLogout();
          }}
          sx={{ color: 'error.main' }}
        >
          <ListItemIcon sx={{ color: 'error.main' }}>
            <LogoutIcon fontSize="small" />
          </ListItemIcon>
          <ListItemText>Déconnexion</ListItemText>
        </MenuItem>
      </Menu>

      <ChangePasswordDialog open={changePasswordOpen} onClose={() => setChangePasswordOpen(false)} />
    </AppBar>
  );
}
