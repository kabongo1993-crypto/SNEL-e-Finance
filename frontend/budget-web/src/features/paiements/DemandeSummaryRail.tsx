import CheckCircleOutlinedIcon from '@mui/icons-material/CheckCircleOutlined';
import RadioButtonUncheckedIcon from '@mui/icons-material/RadioButtonUnchecked';
import {
  Box,
  Divider,
  Paper,
  Stack,
  Typography,
} from '@mui/material';
import type { ReactNode } from 'react';
import { efRadius } from '../../theme';

export type DemandeSummaryChecklistItem = {
  id: string;
  label: string;
  done: boolean;
};

export type DemandeSummaryRailProps = {
  /** Référence dossier si déjà créée ; sinon null → libellé « à générer ». */
  reference: string | null;
  /** Montant + devise déjà formatés par le parent. */
  montantLabel: string;
  exerciceLabel: string;
  destinationLabel: string;
  modePaiementLabel: string;
  beneficiairesCount: number;
  checklist: DemandeSummaryChecklistItem[];
  /** Boutons d’action (mêmes handlers / conditions que le formulaire). */
  children?: ReactNode;
};

/**
 * Panneau récapitulatif sticky — purement présentatif.
 * Toute la logique métier reste dans DemandePaiementFormPage.
 */
export function DemandeSummaryRail({
  reference,
  montantLabel,
  exerciceLabel,
  destinationLabel,
  modePaiementLabel,
  beneficiairesCount,
  checklist,
  children,
}: DemandeSummaryRailProps) {
  return (
    <Paper
      variant="outlined"
      sx={{
        p: { xs: 2, md: 2.5 },
        borderRadius: `${efRadius.md}px`,
        bgcolor: 'background.paper',
      }}
    >
      <Stack spacing={2}>
        <Box>
          <Typography
            variant="overline"
            color="text.secondary"
            sx={{ display: 'block', mb: 0.5 }}
          >
            Numéro de dossier
          </Typography>
          <Typography variant="subtitle1" sx={{ fontWeight: 700, wordBreak: 'break-word' }}>
            {reference ? reference : 'Dossier N° — à générer'}
          </Typography>
        </Box>

        <Divider />

        <Box>
          <Typography
            variant="overline"
            color="text.secondary"
            sx={{ display: 'block', mb: 0.5 }}
          >
            Montant sollicité
          </Typography>
          <Typography
            variant="h4"
            sx={{
              fontWeight: 750,
              fontVariantNumeric: 'tabular-nums',
              color: 'text.primary',
              wordBreak: 'break-word',
            }}
          >
            {montantLabel}
          </Typography>
        </Box>

        <Divider />

        <Stack spacing={1}>
          <Typography variant="overline" color="text.secondary">
            Synthèse
          </Typography>
          <SummaryRow label="Exercice" value={exerciceLabel} />
          <SummaryRow label="Destination budgétaire" value={destinationLabel} />
          <SummaryRow label="Mode de paiement" value={modePaiementLabel} />
          <SummaryRow
            label="Bénéficiaires"
            value={String(beneficiairesCount)}
          />
        </Stack>

        <Divider />

        <Stack spacing={1}>
          <Typography variant="overline" color="text.secondary">
            Complétion
          </Typography>
          {checklist.map((item) => (
            <Stack
              key={item.id}
              direction="row"
              spacing={1}
              sx={{ alignItems: 'flex-start' }}
            >
              {item.done ? (
                <CheckCircleOutlinedIcon
                  fontSize="small"
                  sx={{ color: 'success.main', mt: 0.15, flexShrink: 0 }}
                  aria-hidden
                />
              ) : (
                <RadioButtonUncheckedIcon
                  fontSize="small"
                  sx={{ color: 'text.disabled', mt: 0.15, flexShrink: 0 }}
                  aria-hidden
                />
              )}
              <Typography
                variant="body2"
                sx={{
                  color: item.done ? 'text.primary' : 'text.secondary',
                  fontWeight: item.done ? 600 : 400,
                }}
              >
                {item.label}
              </Typography>
            </Stack>
          ))}
        </Stack>

        {children && (
          <>
            <Divider />
            <Stack
              direction={{ xs: 'column', sm: 'row', md: 'column' }}
              spacing={1}
              useFlexGap
              sx={{ flexWrap: 'wrap' }}
            >
              {children}
            </Stack>
          </>
        )}
      </Stack>
    </Paper>
  );
}

function SummaryRow({ label, value }: { label: string; value: string }) {
  return (
    <Stack
      direction="row"
      spacing={1}
      sx={{ justifyContent: 'space-between', alignItems: 'baseline', gap: 1 }}
    >
      <Typography variant="body2" color="text.secondary" sx={{ flexShrink: 0 }}>
        {label}
      </Typography>
      <Typography
        variant="body2"
        sx={{ fontWeight: 650, textAlign: 'right', wordBreak: 'break-word' }}
      >
        {value}
      </Typography>
    </Stack>
  );
}
