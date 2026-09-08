import AccountBalanceOutlinedIcon from '@mui/icons-material/AccountBalanceOutlined';
import AccountTreeOutlinedIcon from '@mui/icons-material/AccountTreeOutlined';
import CategoryOutlinedIcon from '@mui/icons-material/CategoryOutlined';
import CodeOutlinedIcon from '@mui/icons-material/CodeOutlined';
import EventOutlinedIcon from '@mui/icons-material/EventOutlined';
import PaymentsOutlinedIcon from '@mui/icons-material/PaymentsOutlined';
import { Box, Chip, Paper, Stack, Typography } from '@mui/material';
import type { ReactNode } from 'react';
import { formatDateFr } from '../../mocks/types';
import type { UniteBudgetaire } from '../../services/apiClient';
import { extraireCodeAffichage, libelleDepartement, libelleStatut, libelleStructure } from './ubUtils';

interface UnitesBudgetairesDetailPanelProps {
  selected: UniteBudgetaire | null;
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

export function UnitesBudgetairesDetailPanel({ selected }: UnitesBudgetairesDetailPanelProps) {
  if (!selected) {
    return (
      <Paper sx={{ height: '100%', minHeight: 280, display: 'grid', placeItems: 'center', p: 2 }}>
        <Typography color="text.secondary" variant="body2" sx={{ textAlign: 'center' }}>
          Sélectionnez une unité budgétaire pour afficher ses détails.
        </Typography>
      </Paper>
    );
  }

  return (
    <Paper sx={{ height: '100%', minHeight: 280, display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
      <Box sx={{ px: 1.75, pt: 1.75, pb: 1.25, borderBottom: '1px solid', borderColor: 'divider' }}>
        <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
          Détails de l’unité budgétaire
        </Typography>
        <Typography sx={{ fontFamily: 'ui-monospace, monospace', fontWeight: 700, fontSize: '0.9rem' }}>
          {selected.codeUB}
        </Typography>
        <Typography sx={{ fontSize: '0.875rem', fontWeight: 650, lineHeight: 1.35 }}>
          {selected.libelle}
        </Typography>
        <Chip
          size="small"
          label={libelleStatut(selected.actif)}
          color={selected.actif ? 'success' : 'default'}
          variant="outlined"
          sx={{ mt: 1 }}
        />
      </Box>
      <Box sx={{ flex: 1, overflow: 'auto', px: 1.75, py: 1 }}>
        <InfoRow icon={<CodeOutlinedIcon fontSize="small" />} label="Code UB" value={selected.codeUB} />
        <InfoRow icon={<CategoryOutlinedIcon fontSize="small" />} label="Libellé" value={selected.libelle} />
        <InfoRow
          icon={<AccountBalanceOutlinedIcon fontSize="small" />}
          label="Département"
          value={libelleDepartement(selected.departementCode, selected.departementLibelle)}
        />
        <InfoRow
          icon={<AccountTreeOutlinedIcon fontSize="small" />}
          label="Structure organisationnelle"
          value={libelleStructure(selected.structureCode, selected.structureLibelle)}
        />
        <InfoRow icon={<PaymentsOutlinedIcon fontSize="small" />} label="Type de structure" value={selected.structureType} />
        <InfoRow
          icon={<EventOutlinedIcon fontSize="small" />}
          label="Statut"
          value={libelleStatut(selected.actif)}
        />
        <InfoRow
          icon={<EventOutlinedIcon fontSize="small" />}
          label="Date de création"
          value={formatDateFr(selected.dateCreation)}
        />
        {extraireCodeAffichage(selected.structureCode) !== selected.structureCode && (
          <InfoRow
            icon={<CodeOutlinedIcon fontSize="small" />}
            label="Code technique structure"
            value={selected.structureCode}
          />
        )}

        <Box sx={{ mt: 2, pt: 1.5, borderTop: '1px dashed', borderColor: 'divider' }}>
          <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 700, letterSpacing: 0.3 }}>
            Informations budgétaires
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mt: 0.75 }}>
            {selected.nombrePrevisions > 0
              ? `${selected.nombrePrevisions} prévision${selected.nombrePrevisions > 1 ? 's' : ''} budgétaire${selected.nombrePrevisions > 1 ? 's' : ''} liée${selected.nombrePrevisions > 1 ? 's' : ''}.`
              : 'Aucune prévision budgétaire liée à cette unité.'}
          </Typography>
        </Box>
      </Box>
    </Paper>
  );
}
