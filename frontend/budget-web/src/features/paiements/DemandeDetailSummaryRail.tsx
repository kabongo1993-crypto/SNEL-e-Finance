import {
  Box,
  Divider,
  Paper,
  Stack,
  Typography,
} from '@mui/material';
import { efRadius } from '../../theme';

export type DemandeDetailSummaryRailProps = {
  reference: string;
  montantLabel: string;
  montantUsdLabel: string;
  exerciceLabel: string;
  destinationLabel: string;
  modePaiementLabel: string;
  beneficiairesCount: number;
  demandeurLabel: string;
  ubLabel: string;
  departementLabel: string;
  casDossierLabel: string;
};

/** Rail récapitulatif consultation — purement présentatif, sans action métier. */
export function DemandeDetailSummaryRail({
  reference,
  montantLabel,
  montantUsdLabel,
  exerciceLabel,
  destinationLabel,
  modePaiementLabel,
  beneficiairesCount,
  demandeurLabel,
  ubLabel,
  departementLabel,
  casDossierLabel,
}: DemandeDetailSummaryRailProps) {
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
            {reference}
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
          <Typography
            variant="body2"
            color="text.secondary"
            sx={{ mt: 0.75, fontVariantNumeric: 'tabular-nums' }}
          >
            Montant USD : {montantUsdLabel}
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
          <SummaryRow label="Bénéficiaires" value={String(beneficiairesCount)} />
          <SummaryRow label="Demandeur" value={demandeurLabel} />
          <SummaryRow label="Unité budgétaire" value={ubLabel} />
          <SummaryRow label="Département" value={departementLabel} />
          <SummaryRow label="Cas de dossier" value={casDossierLabel} />
        </Stack>
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
