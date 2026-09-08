import {
  Accordion,
  AccordionDetails,
  AccordionSummary,
  Alert,
  Box,
  Chip,
  Stack,
  Typography,
} from '@mui/material';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import { useCallback, useEffect, useState } from 'react';
import { ErrorState, LoadingState, PageHeader } from '../../components';
import { useAuth } from '../auth';
import { fetchAdminProfilsCatalogue, type ProfilsCatalogue } from '../../services/apiClient';
import { BRAND_NAME } from '../../theme';
import { apiErrorMessage, hasAdminProfils } from './adminUtils';

export function AdminProfilsPage() {
  const { user } = useAuth();
  const canView = hasAdminProfils(user);
  const [data, setData] = useState<ProfilsCatalogue | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!canView) {
      setLoading(false);
      setError('Permission insuffisante (`admin.profils`).');
      return;
    }
    setLoading(true);
    setError(null);
    try {
      setData(await fetchAdminProfilsCatalogue());
    } catch (err) {
      setError(apiErrorMessage(err, 'Impossible de charger le catalogue des profils.'));
      setData(null);
    } finally {
      setLoading(false);
    }
  }, [canView]);

  useEffect(() => {
    void load();
  }, [load]);

  if (loading) return <LoadingState />;
  if (error) return <ErrorState message={error} onRetry={() => void load()} />;
  if (!data) return null;

  return (
    <Box>
      <PageHeader
        title="Rôles et permissions"
        subtitle="Consultation des profils et des permissions héritées. Les permissions critiques ne sont pas modifiables ici."
        breadcrumbs={[
          { label: BRAND_NAME, to: '/dashboard' },
          { label: 'Administration', to: '/administration/utilisateurs' },
          { label: 'Rôles' },
        ]}
      />

      <Alert severity="info" sx={{ mb: 2 }}>
        Les profils historiques (DEMANDEUR, CHARGE_DPM, juniors DC/AE/BI, CONTROLE_BUDGET, ADMIN) coexistent avec
        les profils métier cibles. DEMANDEUR → SERVICE_DEMANDEUR ; CONTROLE_BUDGET → GESTIONNAIRE_SENIOR +
        CHEF_DIVISION. Entité initiatrice : SERVICE_DEMANDEUR / RESPONSABLE_* (périmètre ciblé). Direction des
        Budgets : seed recommandé TousDepartements + ToutesUnitesBudgetaires. DIRECTEUR_BUDGETS ≠ admin.all.
      </Alert>

      <Stack spacing={1}>
        {data.profils.map((p) => (
          <Accordion key={p.code} disableGutters>
            <AccordionSummary expandIcon={<ExpandMoreIcon />}>
              <Stack direction="row" spacing={1} useFlexGap sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
                <Typography sx={{ fontWeight: 600 }}>{p.code}</Typography>
                <Typography color="text.secondary">{p.libelle}</Typography>
                {p.historique && <Chip size="small" label="historique" variant="outlined" />}
                {!p.historique && <Chip size="small" label="cible" color="primary" variant="outlined" />}
                <Chip size="small" label={`${p.permissions.length} perm.`} />
              </Stack>
            </AccordionSummary>
            <AccordionDetails>
              {p.permissions.length === 0 ? (
                <Typography variant="body2" color="text.secondary">
                  Aucune permission héritée (à attribuer progressivement).
                </Typography>
              ) : (
                <Stack direction="row" useFlexGap sx={{ gap: 0.75, flexWrap: 'wrap' }}>
                  {p.permissions.map((code) => (
                    <Chip key={code} size="small" label={code} />
                  ))}
                </Stack>
              )}
            </AccordionDetails>
          </Accordion>
        ))}
      </Stack>

      <Typography variant="h6" sx={{ mt: 3, mb: 1 }}>
        Catalogue des permissions
      </Typography>
      <Stack spacing={0.75}>
        {data.permissions.map((perm) => (
          <Stack
            key={perm.code}
            direction={{ xs: 'column', sm: 'row' }}
            spacing={1}
            sx={{ py: 0.75, borderBottom: '1px solid', borderColor: 'divider' }}
          >
            <Typography variant="body2" sx={{ minWidth: 240, fontFamily: 'monospace' }}>
              {perm.code}
            </Typography>
            <Typography variant="body2" color="text.secondary" sx={{ flex: 1 }}>
              {perm.description ?? '—'}
            </Typography>
            {perm.complementaire && <Chip size="small" label="complémentaire" color="secondary" variant="outlined" />}
          </Stack>
        ))}
      </Stack>
    </Box>
  );
}
