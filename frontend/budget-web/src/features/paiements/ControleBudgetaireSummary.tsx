import CheckCircleOutlinedIcon from '@mui/icons-material/CheckCircleOutlined';
import ErrorOutlineOutlinedIcon from '@mui/icons-material/ErrorOutlineOutlined';
import { Box, Stack, Typography } from '@mui/material';
import { formatMontantUsd } from './paiementUtils';
import { type ControleSummary } from './paiementBudgetUtils';

interface ControleBudgetaireSummaryProps {
  summary: ControleSummary;
  compact?: boolean;
}

export function ControleBudgetaireSummary({ summary, compact }: ControleBudgetaireSummaryProps) {
  const ok = summary.estValide;
  return (
    <Box
      sx={{
        p: compact ? 1.5 : 2,
        borderRadius: 1,
        border: '2px solid',
        borderColor: ok ? 'var(--ef-success)' : 'var(--ef-danger)',
        bgcolor: ok ? 'var(--ef-success-soft)' : 'var(--ef-danger-soft)',
      }}
    >
      <Stack direction="row" spacing={1} sx={{ alignItems: 'center', mb: 1 }}>
        {ok ? (
          <CheckCircleOutlinedIcon sx={{ color: 'var(--ef-success)' }} />
        ) : (
          <ErrorOutlineOutlinedIcon sx={{ color: 'var(--ef-danger)' }} />
        )}
        <Typography variant="subtitle1" sx={{ fontWeight: 800 }}>
          CONTRÔLE BUDGÉTAIRE
        </Typography>
      </Stack>

      {ok ? (
        <Stack spacing={0.5}>
          {[
            { label: 'Budget', value: summary.budgetAnnuel },
            { label: 'Prévision', value: summary.montantPrevision },
            { label: 'Engagé', value: summary.creditEngageAnnuel },
            { label: 'En cours', value: summary.engagementEnCours },
            { label: 'Disponible', value: summary.creditDisponibleAnnuel },
            { label: 'Demande', value: summary.montantDemande },
          ].map((row) => (
            <Typography key={row.label} variant="body2" sx={{ fontVariantNumeric: 'tabular-nums' }}>
              {row.label} : <strong>{formatMontantUsd(row.value)}</strong>
            </Typography>
          ))}
          <Typography variant="body2" sx={{ fontWeight: 700, color: 'var(--ef-success)', mt: 0.5 }}>
            ✓ CRÉDIT DISPONIBLE
          </Typography>
        </Stack>
      ) : (
        <Stack spacing={0.5}>
          <Typography variant="body2" sx={{ fontWeight: 700, color: 'var(--ef-danger)' }}>
            ✕ CRÉDIT INSUFFISANT
          </Typography>
          {summary.motifRejet && (
            <Typography variant="body2" color="text.secondary">
              {summary.motifRejet}
            </Typography>
          )}
          <Typography variant="body2" sx={{ fontVariantNumeric: 'tabular-nums' }}>
            Déficit estimé : <strong>{formatMontantUsd(summary.deficitAnnuel)}</strong>
          </Typography>
        </Stack>
      )}
    </Box>
  );
}
