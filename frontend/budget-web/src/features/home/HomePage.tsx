import { Box, Button, Stack, Typography } from '@mui/material';
import { Link as RouterLink } from 'react-router-dom';

/** Legacy landing — redirects conceptually to dashboard via router; kept for imports. */
export function HomePage() {
  return (
    <Box>
      <Typography variant="h4" gutterBottom>
        e-Finance
      </Typography>
      <Typography color="text.secondary" sx={{ mb: 2 }}>
        Plateforme intégrée de gestion financière et de traitement des paiements.
      </Typography>
      <Stack direction="row" spacing={1.5}>
        <Button variant="contained" component={RouterLink} to="/dashboard">
          Tableau de bord
        </Button>
        <Button variant="outlined" component={RouterLink} to="/referentiels/organisationnel">
          Référentiel organisationnel
        </Button>
      </Stack>
    </Box>
  );
}
