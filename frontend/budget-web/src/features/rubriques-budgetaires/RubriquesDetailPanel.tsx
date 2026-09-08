import AccountTreeOutlinedIcon from '@mui/icons-material/AccountTreeOutlined';
import CodeOutlinedIcon from '@mui/icons-material/CodeOutlined';
import EventOutlinedIcon from '@mui/icons-material/EventOutlined';
import PaymentsOutlinedIcon from '@mui/icons-material/PaymentsOutlined';
import { Box, Chip, Paper, Stack, Typography } from '@mui/material';
import type { ReactNode } from 'react';
import type { RubriqueBudgetaire } from '../../services/apiClient';
import { formatGroupeNiveau1, formatRubriqueParent, RUBRIQUE_RUPTURE } from './hierarchy';
import { formatDateFr, libelleStatut } from './rubriqueUtils';

interface RubriquesDetailPanelProps {
  selected: RubriqueBudgetaire | null;
}

function InfoRow({ icon, label, value }: { icon: ReactNode; label: string; value: string }) {
  return (
    <Stack direction="row" spacing={1.25} sx={{ alignItems: 'flex-start', py: 0.85 }}>
      <Box sx={{ color: 'text.secondary', mt: 0.15 }}>{icon}</Box>
      <Box sx={{ minWidth: 0 }}>
        <Typography variant="caption" color="text.secondary" sx={{ display: 'block' }}>
          {label}
        </Typography>
        <Typography variant="body2" sx={{ fontWeight: 600, wordBreak: 'break-word' }}>
          {value}
        </Typography>
      </Box>
    </Stack>
  );
}

export function RubriquesDetailPanel({ selected }: RubriquesDetailPanelProps) {
  if (!selected) {
    return (
      <Paper sx={{ height: '100%', minHeight: 280, display: 'grid', placeItems: 'center', p: 2 }}>
        <Typography color="text.secondary" variant="body2" sx={{ textAlign: 'center' }}>
          Sélectionnez une RB pour afficher ses détails.
        </Typography>
      </Paper>
    );
  }

  const hasGroupe = selected.idGroupeRB != null;
  const groupeLabel =
    hasGroupe
      ? formatGroupeNiveau1({
          codeGroupe: selected.codeGroupe ?? '',
          libelleGroupe: selected.libelleGroupe ?? '',
        })
      : '— (section technique / non rattaché)';

  return (
    <Paper sx={{ height: '100%', minHeight: 280, display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
      <Box
        sx={{
          px: 1.75,
          pt: 1.75,
          pb: 1.25,
          borderBottom: '1px solid',
          borderColor: 'divider',
          bgcolor: hasGroupe ? undefined : RUBRIQUE_RUPTURE.bgcolor,
          borderLeft: hasGroupe ? undefined : RUBRIQUE_RUPTURE.borderLeft,
        }}
      >
        <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
          {hasGroupe ? 'Rubrique budgétaire (RB)' : 'Section technique'}
        </Typography>
        <Typography sx={{ fontFamily: 'ui-monospace, monospace', fontWeight: 700, fontSize: '0.9rem' }}>
          {selected.codeRB}
        </Typography>
        <Typography sx={{ fontSize: '0.875rem', fontWeight: 650, lineHeight: 1.35 }}>
          {selected.libelle}
        </Typography>
        <Stack direction="row" spacing={1} sx={{ mt: 1, flexWrap: 'wrap' }}>
          <Chip
            size="small"
            label={hasGroupe ? 'RB' : 'Section'}
            variant="outlined"
            sx={{ fontWeight: 700 }}
          />
          <Chip
            size="small"
            label={libelleStatut(selected.actif)}
            color={selected.actif ? 'success' : 'default'}
            variant={selected.actif ? 'outlined' : 'filled'}
            sx={{ fontWeight: 700 }}
          />
        </Stack>
      </Box>
      <Box sx={{ flex: 1, overflow: 'auto', px: 1.75, py: 1 }}>
        <InfoRow icon={<CodeOutlinedIcon fontSize="small" />} label="Code" value={selected.codeRB} />
        <InfoRow icon={<CodeOutlinedIcon fontSize="small" />} label="Libellé" value={selected.libelle} />
        <InfoRow
          icon={<AccountTreeOutlinedIcon fontSize="small" />}
          label="Groupe niveau 1"
          value={groupeLabel}
        />
        <InfoRow
          icon={<AccountTreeOutlinedIcon fontSize="small" />}
          label="Section technique parente"
          value={formatRubriqueParent(selected)}
        />
        <InfoRow icon={<AccountTreeOutlinedIcon fontSize="small" />} label="Niveau" value={String(selected.niveau)} />
        <InfoRow icon={<EventOutlinedIcon fontSize="small" />} label="Statut" value={libelleStatut(selected.actif)} />
        <InfoRow
          icon={<EventOutlinedIcon fontSize="small" />}
          label="Date de création"
          value={formatDateFr(selected.dateCreation)}
        />
        <InfoRow
          icon={<AccountTreeOutlinedIcon fontSize="small" />}
          label="Enfants"
          value={String(selected.nombreEnfants)}
        />
        <InfoRow
          icon={<PaymentsOutlinedIcon fontSize="small" />}
          label="Prévisions rattachées"
          value={String(selected.nombrePrevisions)}
        />
      </Box>
    </Paper>
  );
}
