import { Box } from '@mui/material';
import { useEffect, useState } from 'react';
import { Outlet } from 'react-router-dom';
import { Sidebar, SIDEBAR_COLLAPSED_WIDTH, SIDEBAR_WIDTH } from './Sidebar';
import { TOPBAR_HEIGHT, TopBar } from './TopBar';

const COLLAPSE_KEY = 'efinance-sidebar-collapsed';

export function AppShell() {
  const [mobileOpen, setMobileOpen] = useState(false);
  const [collapsed, setCollapsed] = useState(() => {
    try {
      return localStorage.getItem(COLLAPSE_KEY) === '1';
    } catch {
      return false;
    }
  });

  useEffect(() => {
    try {
      localStorage.setItem(COLLAPSE_KEY, collapsed ? '1' : '0');
    } catch {
      /* ignore */
    }
  }, [collapsed]);

  const sidebarWidth = collapsed ? SIDEBAR_COLLAPSED_WIDTH : SIDEBAR_WIDTH;

  return (
    <Box sx={{ display: 'flex', minHeight: '100vh', bgcolor: 'background.default' }}>
      <TopBar onMenuClick={() => setMobileOpen(true)} sidebarCollapsed={collapsed} />
      <Sidebar
        mobileOpen={mobileOpen}
        onMobileClose={() => setMobileOpen(false)}
        collapsed={collapsed}
        onToggleCollapsed={() => setCollapsed((v) => !v)}
      />
      <Box
        component="main"
        sx={{
          flexGrow: 1,
          width: { md: `calc(100% - ${sidebarWidth}px)` },
          minWidth: 0,
          px: { xs: 1.5, sm: 2, md: 3 },
          pb: { xs: 4, md: 5 },
          transition: 'width 180ms ease',
        }}
      >
        <Box sx={{ height: `${TOPBAR_HEIGHT}px`, flexShrink: 0 }} />
        <Box sx={{ pt: { xs: 2, md: 2.5 }, maxWidth: 1560, mx: 'auto' }}>
          <Outlet />
        </Box>
      </Box>
    </Box>
  );
}

export { AppShell as AppLayout };
