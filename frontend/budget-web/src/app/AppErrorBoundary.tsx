import { Box, Button, Typography } from '@mui/material';
import { Component, type ErrorInfo, type ReactNode } from 'react';

interface AppErrorBoundaryState {
  error: Error | null;
}

export class AppErrorBoundary extends Component<{ children: ReactNode }, AppErrorBoundaryState> {
  state: AppErrorBoundaryState = { error: null };

  static getDerivedStateFromError(error: Error): AppErrorBoundaryState {
    return { error };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error(error, info);
  }

  render() {
    if (!this.state.error) {
      return this.props.children;
    }

    return (
      <Box sx={{ minHeight: '100vh', display: 'grid', placeItems: 'center', p: 3, bgcolor: 'background.default' }}>
        <Box sx={{ maxWidth: 480, textAlign: 'center' }}>
          <Typography variant="h5" sx={{ fontWeight: 800, mb: 1 }}>
            L’écran n’a pas pu s’afficher
          </Typography>
          <Typography color="text.secondary" sx={{ mb: 2 }}>
            Un rechargement complet rétablit généralement l’application après une mise à jour.
          </Typography>
          <Button variant="contained" onClick={() => window.location.reload()}>
            Recharger e-Finance
          </Button>
        </Box>
      </Box>
    );
  }
}
