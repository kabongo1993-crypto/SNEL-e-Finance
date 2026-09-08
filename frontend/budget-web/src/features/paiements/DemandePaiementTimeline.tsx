import { Box, Button, Chip, Collapse, Stack, Typography } from '@mui/material';
import { useState } from 'react';
import type { DemandePaiementDetail, JournalAuditDemandeDto } from '../../services/apiClient';
import { useResponsive } from '../../theme/useResponsive';
import { buildTimelineSteps, formatTimelineLabel, labelOperationAudit } from './paiementBudgetUtils';
import { formatDateTimeFr } from './paiementUtils';

interface DemandePaiementTimelineProps {
  demande: DemandePaiementDetail;
  historique: JournalAuditDemandeDto[];
  /** Masque le journal d'audit derrière un bouton (écran d'imputation DC). */
  auditCollapsible?: boolean;
}

export function DemandePaiementTimeline({
  demande,
  historique,
  auditCollapsible = false,
}: DemandePaiementTimelineProps) {
  const { isMobile } = useResponsive();
  const [auditOpen, setAuditOpen] = useState(!auditCollapsible);
  const steps = buildTimelineSteps(demande, historique);

  return (
    <Stack spacing={1.5}>
      {isMobile ? (
        // Sous md, les puces tronqueraient « libellé · date » : on déroule un rail vertical.
        <Stack spacing={0}>
          {steps.map((step, index) => {
            const last = index === steps.length - 1;
            return (
              <Stack
                key={step.key}
                direction="row"
                spacing={1.25}
                sx={{ alignItems: 'stretch', minWidth: 0 }}
              >
                <Box
                  sx={{
                    display: 'flex',
                    flexDirection: 'column',
                    alignItems: 'center',
                    flexShrink: 0,
                    pt: 0.4,
                  }}
                >
                  <Box
                    sx={{
                      width: 10,
                      height: 10,
                      borderRadius: '50%',
                      border: '2px solid',
                      borderColor: step.done ? 'primary.main' : 'divider',
                      bgcolor: step.done ? 'primary.main' : 'transparent',
                    }}
                  />
                  {!last && (
                    <Box sx={{ width: '2px', flex: 1, minHeight: 16, my: 0.25, bgcolor: 'divider' }} />
                  )}
                </Box>
                <Box sx={{ minWidth: 0, pb: last ? 0 : 1.25 }}>
                  <Typography
                    variant="body2"
                    sx={{ fontWeight: step.done ? 700 : 500, lineHeight: 1.35, overflowWrap: 'anywhere' }}
                  >
                    {step.label}
                  </Typography>
                  <Typography
                    variant="caption"
                    color="text.secondary"
                    sx={{ display: 'block', lineHeight: 1.35, overflowWrap: 'anywhere' }}
                  >
                    {step.date ? formatDateTimeFr(step.date) : 'En attente'}
                    {step.detail ? ` · ${step.detail}` : ''}
                  </Typography>
                </Box>
              </Stack>
            );
          })}
        </Stack>
      ) : (
        <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: 'wrap' }}>
          {steps.map((step) => (
            <Chip
              key={step.key}
              size="small"
              variant={step.done ? 'filled' : 'outlined'}
              color={step.done ? 'primary' : 'default'}
              label={formatTimelineLabel(step)}
            />
          ))}
        </Stack>
      )}

      {historique.length > 0 && (
        <Stack spacing={0.75}>
          {auditCollapsible ? (
            <Button
              size="small"
              variant="text"
              onClick={() => setAuditOpen((open) => !open)}
              sx={{ alignSelf: 'flex-start', px: 0 }}
            >
              {auditOpen ? 'Masquer le journal d’audit' : 'Afficher le journal d’audit'}
            </Button>
          ) : (
            <Typography variant="subtitle2">Journal d&apos;audit</Typography>
          )}
          <Collapse in={auditOpen} timeout={180}>
            <Stack spacing={0.75}>
              {historique.map((h) => (
                <Typography
                  key={h.idAudit}
                  variant="body2"
                  color="text.secondary"
                  sx={{ overflowWrap: 'anywhere' }}
                >
                  {formatDateTimeFr(h.dateHeure)} · {labelOperationAudit(h.operation)}
                  {h.nouvellesValeurs ? ` · ${h.nouvellesValeurs}` : ''}
                </Typography>
              ))}
            </Stack>
          </Collapse>
        </Stack>
      )}
    </Stack>
  );
}
