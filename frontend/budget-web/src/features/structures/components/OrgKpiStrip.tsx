import AccountBalanceOutlinedIcon from '@mui/icons-material/AccountBalanceOutlined';
import AccountTreeOutlinedIcon from '@mui/icons-material/AccountTreeOutlined';
import ApartmentOutlinedIcon from '@mui/icons-material/ApartmentOutlined';
import PaymentsOutlinedIcon from '@mui/icons-material/PaymentsOutlined';
import { Box, Grid, Paper, Stack, Typography } from '@mui/material';
import type { ReferentielOrganisationnelCompteurs } from '../../../services/apiClient';

const KPI_ITEMS = [
  {
    key: 'entites',
    label: 'Entités',
    accent: 'var(--ef-primary)',
    soft: 'var(--ef-primary-soft)',
    icon: <ApartmentOutlinedIcon fontSize="small" />,
    get: (c: ReferentielOrganisationnelCompteurs) => c.entites,
  },
  {
    key: 'departements',
    label: 'Départements',
    accent: 'var(--ef-success)',
    soft: 'rgba(30, 122, 70, 0.12)',
    icon: <AccountBalanceOutlinedIcon fontSize="small" />,
    get: (c: ReferentielOrganisationnelCompteurs) => c.departements,
  },
  {
    key: 'structures',
    label: 'Structures',
    accent: '#6B5B95',
    soft: 'rgba(107, 91, 149, 0.12)',
    icon: <AccountTreeOutlinedIcon fontSize="small" />,
    get: (c: ReferentielOrganisationnelCompteurs) => c.structures,
  },
  {
    key: 'ub',
    label: 'Unités budgétaires',
    accent: '#C47A3A',
    soft: 'rgba(196, 122, 58, 0.12)',
    icon: <PaymentsOutlinedIcon fontSize="small" />,
    get: (c: ReferentielOrganisationnelCompteurs) => c.unitesBudgetaires,
  },
] as const;

interface OrgKpiStripProps {
  compteurs: ReferentielOrganisationnelCompteurs;
}

export function OrgKpiStrip({ compteurs }: OrgKpiStripProps) {
  return (
    <Grid container spacing={1.25} sx={{ mb: 1.5 }}>
      {KPI_ITEMS.map((item) => (
        <Grid key={item.key} size={{ xs: 6, md: 3 }}>
          <Paper
            sx={{
              p: 1.5,
              position: 'relative',
              overflow: 'hidden',
              '&::before': {
                content: '""',
                position: 'absolute',
                left: 0,
                top: 0,
                bottom: 0,
                width: 3,
                bgcolor: item.accent,
              },
            }}
          >
            <Stack direction="row" spacing={1.25} sx={{ alignItems: 'center' }}>
              <Box
                sx={{
                  width: 32,
                  height: 32,
                  borderRadius: 1,
                  display: 'grid',
                  placeItems: 'center',
                  bgcolor: item.soft,
                  color: item.accent,
                  flexShrink: 0,
                }}
              >
                {item.icon}
              </Box>
              <Box sx={{ minWidth: 0 }}>
                <Typography variant="caption" color="text.secondary" sx={{ display: 'block', lineHeight: 1.2 }}>
                  {item.label}
                </Typography>
                <Typography sx={{ fontWeight: 700, fontSize: '1.25rem', lineHeight: 1.2, letterSpacing: '-0.02em' }}>
                  {item.get(compteurs)}
                </Typography>
                <Typography variant="caption" color="text.secondary">
                  Total
                </Typography>
              </Box>
            </Stack>
          </Paper>
        </Grid>
      ))}
    </Grid>
  );
}
