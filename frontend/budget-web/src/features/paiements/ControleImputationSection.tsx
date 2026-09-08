import {
  Alert,
  Box,
  Chip,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Typography,
} from '@mui/material';
import type {
  ControleImputationDto,
  DemandePaiementImputation,
  GroupeItemAE,
  ItemBI,
  RubriqueBudgetaire,
  SnapshotDto,
} from '../../services/apiClient';
import { MOIS_LABELS, formatMontantUsd } from './paiementUtils';
import { dcControlesOk, labelPrevision } from './paiementBudgetUtils';

interface ControleImputationSectionProps {
  imputation: DemandePaiementImputation;
  controle?: ControleImputationDto | null;
  snapshot?: SnapshotDto | null;
  rubriques: RubriqueBudgetaire[];
  itemsBI: ItemBI[];
  groupesAE: GroupeItemAE[];
  anneeExercice: number;
  readOnly?: boolean;
}

function MontantRow({ label, value, emphasize }: { label: string; value: number; emphasize?: boolean }) {
  return (
    <TableRow>
      <TableCell>{label}</TableCell>
      <TableCell align="right" sx={{ fontWeight: emphasize ? 700 : 400, fontVariantNumeric: 'tabular-nums' }}>
        {formatMontantUsd(value)}
      </TableCell>
    </TableRow>
  );
}

export function ControleImputationSection({
  imputation,
  controle,
  snapshot,
  rubriques,
  itemsBI,
  groupesAE,
  anneeExercice,
}: ControleImputationSectionProps) {
  const code = (imputation.codeTypeBudget ?? '').toUpperCase();
  const rb = rubriques.find((r) => r.idRB === imputation.idRubriqueBudgetaire);
  const itemBi = itemsBI.find((i) => i.idItemBI === imputation.idItemBI);
  const groupeAe = groupesAE.find((g) => g.idGroupeItemAE === imputation.idGroupeItemAE);
  const moisLabel = MOIS_LABELS.find((m) => m.value === imputation.mois)?.label ?? `Mois ${imputation.mois}`;

  const budgetAnnuel = controle?.budgetAnnuel ?? snapshot?.budgetAnnuel ?? 0;
  const budgetMensuel = controle?.budgetMensuel ?? snapshot?.budgetMensuel ?? null;
  const prevision = controle?.montantPrevision ?? snapshot?.montantPrevision ?? 0;
  const engageAnnuel = controle?.creditEngageAnnuel ?? snapshot?.creditEngageAnnuel ?? 0;
  const engageMensuel = controle?.creditEngageMensuel ?? snapshot?.creditEngageMensuel ?? null;
  const enCours = controle?.engagementEnCours ?? imputation.montantUsd;
  const dispoAnnuel = controle?.creditDisponibleAnnuel ?? snapshot?.creditDisponibleAnnuelAvantVisa ?? 0;
  const dispoMensuel = controle?.creditDisponibleMensuel ?? snapshot?.creditDisponibleMensuelAvantVisa ?? null;
  const montantDemande = controle?.montantCourantUsd ?? imputation.montantUsd;
  const estValide = controle?.estValide ?? (snapshot ? true : undefined);
  const { mensuelOk, annuelOk } = controle ? dcControlesOk(controle) : { mensuelOk: true, annuelOk: true };

  return (
    <Box
      sx={{
        p: { xs: 1.5, md: 2 },
        mb: 2,
        borderRadius: 1,
        border: '1px solid',
        borderColor: 'divider',
        bgcolor: 'var(--ef-surface-secondary)',
      }}
    >
      <Stack direction="row" spacing={1} sx={{ alignItems: 'center', mb: 1, flexWrap: 'wrap' }} useFlexGap>
        <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
          Imputation #{imputation.ordre} — {code}
        </Typography>
        {estValide === true && <Chip size="small" color="success" label="Crédit OK" />}
        {estValide === false && <Chip size="small" color="error" label="Crédit insuffisant" />}
        {controle?.motifRejet && (
          <Chip size="small" color="warning" variant="outlined" label={controle.motifRejet} />
        )}
      </Stack>

      <Typography variant="body2" color="text.secondary" sx={{ mb: 1, overflowWrap: 'anywhere' }}>
        {code === 'DC' && (
          <>
            UB {imputation.idUB} · RB {rb ? `${rb.codeRB} — ${rb.libelle}` : imputation.idRubriqueBudgetaire} ·{' '}
            {moisLabel}
          </>
        )}
        {code === 'AE' && (
          <>
            UB {imputation.idUB} · RB {rb ? `${rb.codeRB}` : '—'} · Item AE {imputation.libelleItemAE}
            {groupeAe ? ` · Groupe ${groupeAe.libelle}` : ''} · Année {anneeExercice}
          </>
        )}
        {code === 'BI' && (
          <>
            UB {imputation.idUB} · Item BI {itemBi ? `${itemBi.codeItem} — ${itemBi.libelle}` : imputation.idItemBI} ·
            Détail {imputation.detailBI} · Année {anneeExercice}
          </>
        )}
      </Typography>

      {!prevision && (
        <Alert severity="info" sx={{ mb: 1.5 }}>
          {labelPrevision(0)} — l&apos;absence de prévision n&apos;est pas bloquante. La question déterminante est le
          crédit disponible.
        </Alert>
      )}

      {code === 'DC' && (
        <Stack spacing={2}>
          <Box>
            <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 0.5 }}>
              A. Situation mensuelle
            </Typography>
            <Table size="small">
              <TableBody>
                <MontantRow label="Budget mensuel" value={budgetMensuel ?? 0} />
                <MontantRow label="Engagé mensuel" value={engageMensuel ?? 0} />
                <MontantRow label="En cours (demande)" value={enCours} />
                <MontantRow label="Disponible mensuel" value={dispoMensuel ?? 0} emphasize />
              </TableBody>
            </Table>
            <Chip
              size="small"
              sx={{ mt: 0.75 }}
              color={mensuelOk ? 'success' : 'error'}
              label={mensuelOk ? 'Mensuel OK' : 'Mensuel insuffisant'}
            />
          </Box>
          <Box>
            <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 0.5 }}>
              B. Situation annuelle
            </Typography>
            <Table size="small">
              <TableBody>
                <MontantRow label="Budget annuel" value={budgetAnnuel} />
                <MontantRow label="Engagé annuel" value={engageAnnuel} />
                <MontantRow label="En cours (demande)" value={enCours} />
                <MontantRow label="Disponible annuel" value={dispoAnnuel} emphasize />
              </TableBody>
            </Table>
            <Chip
              size="small"
              sx={{ mt: 0.75 }}
              color={annuelOk ? 'success' : 'error'}
              label={annuelOk ? 'Annuel OK' : 'Annuel insuffisant'}
            />
          </Box>
          <Typography variant="caption" color="text.secondary">
            Visa possible uniquement si mensuel OK et annuel OK.
          </Typography>
        </Stack>
      )}

      {(code === 'AE' || code === 'BI') && (
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Indicateur</TableCell>
              <TableCell align="right">Montant USD</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            <MontantRow label="Budget annuel" value={budgetAnnuel} />
            <MontantRow label="Prévision annuelle" value={prevision} />
            <MontantRow label="Engagé annuel" value={engageAnnuel} />
            <MontantRow label="En cours" value={enCours} />
            <MontantRow label="Disponible" value={dispoAnnuel} />
            <MontantRow label="Montant demandé" value={montantDemande} emphasize />
            <MontantRow label="Solde après imputation" value={dispoAnnuel} emphasize />
          </TableBody>
        </Table>
      )}

      <Stack
        direction={{ xs: 'column', md: 'row' }}
        spacing={{ xs: 0.5, md: 2 }}
        sx={{ mt: 1.5, flexWrap: 'wrap' }}
        useFlexGap
      >
        <Typography variant="caption" color="text.secondary">
          Prévision : {labelPrevision(prevision)}
        </Typography>
        <Typography variant="caption" color="text.secondary">
          Écart prévision/imputation : {formatMontantUsd(controle?.ecartPrevisionImputation ?? prevision - montantDemande)}
        </Typography>
        {imputation.idBudgetLigne && (
          <Typography variant="caption" color="text.secondary">
            Ligne budgétaire : {imputation.idBudgetLigne}
          </Typography>
        )}
      </Stack>
    </Box>
  );
}
