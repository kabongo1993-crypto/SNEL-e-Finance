import DownloadIcon from '@mui/icons-material/Download';
import PrintIcon from '@mui/icons-material/Print';
import {
  Box,
  Button,
  Chip,
  Grid,
  Stack,
  Step,
  StepLabel,
  Stepper,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Typography,
} from '@mui/material';
import { useMemo, useState } from 'react';
import { Link as RouterLink, useParams } from 'react-router-dom';
import { ConfirmDialog, DetailPanel, ErrorState, PageHeader, StatusBadge } from '../../components';
import {
  getPaiementById,
  paiementCircuit,
  paiementCommentaires,
  paiementPieces,
} from '../../mocks/paiements';
import { formatDateFr, formatMontant } from '../../mocks/types';
import type { EntityStatus } from '../../types/status';

function actionsForStatus(statut: EntityStatus) {
  const base = [
    { id: 'download', label: 'Télécharger', icon: <DownloadIcon />, variant: 'outlined' as const },
    { id: 'print', label: 'Imprimer', icon: <PrintIcon />, variant: 'outlined' as const },
  ];
  switch (statut) {
    case 'brouillon':
      return [
        { id: 'edit', label: 'Modifier', variant: 'outlined' as const },
        { id: 'submit', label: 'Soumettre', variant: 'contained' as const },
        { id: 'cancel', label: 'Annuler', variant: 'outlined' as const, color: 'error' as const },
        ...base,
      ];
    case 'soumis':
    case 'en_validation':
      return [
        { id: 'validate', label: 'Valider', variant: 'contained' as const, color: 'success' as const },
        { id: 'reject', label: 'Rejeter', variant: 'outlined' as const, color: 'error' as const },
        { id: 'cancel', label: 'Annuler', variant: 'outlined' as const, color: 'error' as const },
        ...base,
      ];
    case 'valide':
      return [
        { id: 'cancel', label: 'Annuler', variant: 'outlined' as const, color: 'error' as const },
        ...base,
      ];
    default:
      return base;
  }
}

export function PaiementDetailPage() {
  const { id } = useParams();
  const paiement = useMemo(() => (id ? getPaiementById(id) : undefined), [id]);
  const [confirm, setConfirm] = useState<{ title: string; message: string } | null>(null);

  if (!paiement) {
    return (
      <ErrorState
        title="Paiement introuvable"
        message="La demande demandée n'existe pas dans les données mock."
      />
    );
  }

  const actions = actionsForStatus(paiement.statut);

  return (
    <Box>
      <PageHeader
        title={paiement.numero}
        subtitle={paiement.objet}
        breadcrumbs={[
          { label: 'e-Finance', to: '/dashboard' },
          { label: 'Paiements', to: '/paiements/demandes' },
          { label: paiement.numero },
        ]}
        actions={
          <>
            <StatusBadge status={paiement.statut} size="medium" />
            {actions.map((a) => (
              <Button
                key={a.id}
                variant={a.variant}
                color={'color' in a ? a.color : 'primary'}
                startIcon={'icon' in a ? a.icon : undefined}
                onClick={() =>
                  setConfirm({
                    title: a.label,
                    message: `Action « ${a.label} » — maquette UI (aucune écriture BD).`,
                  })
                }
              >
                {a.label}
              </Button>
            ))}
          </>
        }
      />

      <Grid container spacing={2}>
        <Grid size={{ xs: 12, md: 8 }}>
          <DetailPanel title="Informations générales">
            <Grid container spacing={2}>
              {[
                ['Numéro', paiement.numero],
                ['Date', formatDateFr(paiement.date)],
                ['Demandeur', paiement.demandeur],
                ['Bénéficiaire', paiement.beneficiaire],
                ['Montant', formatMontant(paiement.montant, paiement.devise)],
                ['Devise', paiement.devise],
                ['Département', paiement.departement],
                ['Unité budgétaire', paiement.uniteBudgetaire],
              ].map(([label, value]) => (
                <Grid key={label} size={{ xs: 12, sm: 6 }}>
                  <Typography variant="caption" color="text.secondary">
                    {label}
                  </Typography>
                  <Typography sx={{ fontWeight: 600 }}>{value}</Typography>
                </Grid>
              ))}
              <Grid size={{ xs: 12 }}>
                <Typography variant="caption" color="text.secondary">
                  Objet
                </Typography>
                <Typography sx={{ fontWeight: 600 }}>{paiement.objet}</Typography>
              </Grid>
            </Grid>
          </DetailPanel>

          <DetailPanel title="Informations financières">
            <Grid container spacing={2}>
              <Grid size={{ xs: 6, sm: 3 }}>
                <Typography variant="caption" color="text.secondary">
                  Montant TTC
                </Typography>
                <Typography sx={{ fontWeight: 700 }}>{formatMontant(paiement.montant)}</Typography>
              </Grid>
              <Grid size={{ xs: 6, sm: 3 }}>
                <Typography variant="caption" color="text.secondary">
                  Mode de paiement
                </Typography>
                <Typography sx={{ fontWeight: 600 }}>Virement</Typography>
              </Grid>
              <Grid size={{ xs: 6, sm: 3 }}>
                <Typography variant="caption" color="text.secondary">
                  Imputation
                </Typography>
                <Typography sx={{ fontWeight: 600 }}>61.01.001</Typography>
              </Grid>
              <Grid size={{ xs: 6, sm: 3 }}>
                <Typography variant="caption" color="text.secondary">
                  Disponible UB
                </Typography>
                <Typography sx={{ fontWeight: 600 }}>{formatMontant(180_000_000)}</Typography>
              </Grid>
            </Grid>
          </DetailPanel>

          <DetailPanel title="Pièces justificatives">
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Document</TableCell>
                  <TableCell>Type</TableCell>
                  <TableCell>Taille</TableCell>
                  <TableCell>Date</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {paiementPieces.map((p) => (
                  <TableRow key={p.id}>
                    <TableCell>{p.nom}</TableCell>
                    <TableCell>{p.type}</TableCell>
                    <TableCell>{p.taille}</TableCell>
                    <TableCell>{formatDateFr(p.date)}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </DetailPanel>
        </Grid>

        <Grid size={{ xs: 12, md: 4 }}>
          <DetailPanel title="Circuit de validation">
            <Stepper orientation="vertical" activeStep={2}>
              {paiementCircuit.map((step) => (
                <Step key={step.etape} completed={step.statut === 'fait'}>
                  <StepLabel
                    optional={
                      <Typography variant="caption">
                        {step.acteur} · {step.date}
                      </Typography>
                    }
                  >
                    {step.role} — {step.action}
                  </StepLabel>
                </Step>
              ))}
            </Stepper>
          </DetailPanel>

          <DetailPanel title="Commentaires">
            <Stack spacing={1.5}>
              {paiementCommentaires.map((c) => (
                <Box key={c.id} sx={{ p: 1.5, bgcolor: 'action.hover', borderRadius: 1.5 }}>
                  <Stack direction="row" spacing={1} sx={{ justifyContent: 'space-between' }}>
                    <Typography variant="body2" sx={{ fontWeight: 700 }}>
                      {c.auteur}
                    </Typography>
                    <Typography variant="caption" color="text.secondary">
                      {c.date}
                    </Typography>
                  </Stack>
                  <Typography variant="body2" sx={{ mt: 0.5 }}>
                    {c.texte}
                  </Typography>
                </Box>
              ))}
            </Stack>
          </DetailPanel>

          <DetailPanel
            title="Historique"
            actions={
              <Button size="small" component={RouterLink} to="/paiements/historique">
                Voir tout
              </Button>
            }
          >
            <Stack spacing={1}>
              {['Création', 'Visa hiérarchique', 'En contrôle'].map((h) => (
                <Chip key={h} label={h} size="small" variant="outlined" sx={{ justifyContent: 'flex-start' }} />
              ))}
            </Stack>
          </DetailPanel>
        </Grid>
      </Grid>

      <ConfirmDialog
        open={Boolean(confirm)}
        title={confirm?.title ?? ''}
        message={confirm?.message ?? ''}
        onClose={() => setConfirm(null)}
        onConfirm={() => setConfirm(null)}
      />
    </Box>
  );
}
