import AccountTreeOutlinedIcon from '@mui/icons-material/AccountTreeOutlined';
import CategoryOutlinedIcon from '@mui/icons-material/CategoryOutlined';
import CodeOutlinedIcon from '@mui/icons-material/CodeOutlined';
import FolderOutlinedIcon from '@mui/icons-material/FolderOutlined';
import PaymentsOutlinedIcon from '@mui/icons-material/PaymentsOutlined';
import PeopleOutlinedIcon from '@mui/icons-material/PeopleOutlined';
import {
  Box,
  Chip,
  Paper,
  Stack,
  Tab,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Tabs,
  Typography,
} from '@mui/material';
import { useState, type ReactNode } from 'react';
import type {
  StructureOrganisationnelle,
  UniteBudgetaireOrganisation,
} from '../../../services/apiClient';
import { getTypeVisual } from '../orgUtils';

interface OrgDetailPanelProps {
  selected: StructureOrganisationnelle | null;
  unites: UniteBudgetaireOrganisation[];
  childCount: number;
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

export function OrgDetailPanel({ selected, unites, childCount }: OrgDetailPanelProps) {
  const [tab, setTab] = useState(0);

  if (!selected) {
    return (
      <Paper sx={{ height: '100%', display: 'grid', placeItems: 'center', p: 2 }}>
        <Typography color="text.secondary" variant="body2" sx={{ textAlign: 'center' }}>
          Sélectionnez une structure pour afficher ses détails.
        </Typography>
      </Paper>
    );
  }

  const visual = getTypeVisual(selected.typeStructure);

  return (
    <Paper sx={{ height: '100%', display: 'flex', flexDirection: 'column', minHeight: 0, overflow: 'hidden' }}>
      <Box sx={{ px: 1.5, pt: 1.5, pb: 1, borderBottom: '1px solid', borderColor: 'divider' }}>
        <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
          Détails de la structure
        </Typography>
        <Chip
          size="small"
          label={selected.typeStructure}
          sx={{ bgcolor: visual.soft, color: visual.color, fontWeight: 700, mb: 1 }}
        />
        <Typography sx={{ fontFamily: 'ui-monospace, monospace', fontWeight: 700, fontSize: '0.85rem' }}>
          {selected.code}
        </Typography>
        <Typography sx={{ fontSize: '0.875rem', fontWeight: 650, lineHeight: 1.35 }}>
          {selected.libelle}
        </Typography>
      </Box>

      <Tabs
        value={tab}
        onChange={(_, v) => setTab(v)}
        variant="scrollable"
        scrollButtons="auto"
        sx={{
          minHeight: 36,
          borderBottom: '1px solid',
          borderColor: 'divider',
          '& .MuiTab-root': { minHeight: 36, py: 0.5, fontSize: '0.75rem', textTransform: 'none' },
        }}
      >
        <Tab label="Informations" />
        <Tab label={`Unités budgétaires (${unites.length})`} />
        <Tab label="Responsables" />
        <Tab label="Documents" />
      </Tabs>

      <Box sx={{ flex: 1, overflow: 'auto', px: 1.5, py: 1 }}>
        {tab === 0 && (
          <Box>
            <InfoRow icon={<CodeOutlinedIcon fontSize="small" />} label="Code" value={selected.code} />
            <InfoRow
              icon={<CodeOutlinedIcon fontSize="small" />}
              label="Code technique"
              value={selected.codeTechnique || '—'}
            />
            <InfoRow
              icon={<CategoryOutlinedIcon fontSize="small" />}
              label="Type"
              value={selected.typeStructure}
            />
            <InfoRow
              icon={<FolderOutlinedIcon fontSize="small" />}
              label="Parent"
              value={
                selected.parentCode
                  ? `${selected.parentCode} — ${selected.parentLibelle ?? ''}`
                  : '—'
              }
            />
            <InfoRow
              icon={<AccountTreeOutlinedIcon fontSize="small" />}
              label="Département"
              value={
                selected.departementCode
                  ? `${selected.departementCode} — ${selected.departementLibelle ?? ''}`
                  : '—'
              }
            />
            <InfoRow
              icon={<Chip size="small" label="Actif" color="success" variant="outlined" sx={{ height: 20 }} />}
              label="Statut"
              value="Actif"
            />
          </Box>
        )}

        {tab === 1 &&
          (unites.length === 0 ? (
            <Typography variant="body2" color="text.secondary">
              Aucune UB directement rattachée à cette structure.
            </Typography>
          ) : (
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Code</TableCell>
                  <TableCell>Libellé</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {unites.map((ub) => (
                  <TableRow key={ub.idUB} hover>
                    <TableCell>
                      <Typography variant="body2" sx={{ fontWeight: 700 }}>
                        {ub.codeUB}
                      </Typography>
                    </TableCell>
                    <TableCell>{ub.libelle}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          ))}

        {tab === 2 && (
          <Typography variant="body2" color="text.secondary">
            Les responsables ne sont pas fournis par l’API actuelle. Aucune donnée inventée.
          </Typography>
        )}

        {tab === 3 && (
          <Typography variant="body2" color="text.secondary">
            Aucun document n’est associé via l’API actuelle.
          </Typography>
        )}
      </Box>

      <Box sx={{ p: 1.25, borderTop: '1px solid', borderColor: 'divider' }}>
        <Typography
          variant="caption"
          color="text.secondary"
          sx={{ fontWeight: 650, mb: 1, display: 'block' }}
        >
          Indicateurs
        </Typography>
        <Stack direction="row" spacing={1}>
          {[
            {
              label: 'Structures',
              value: childCount,
              icon: <AccountTreeOutlinedIcon sx={{ fontSize: 14 }} />,
            },
            {
              label: 'UB',
              value: selected.nombreUnitesBudgetaires,
              icon: <PaymentsOutlinedIcon sx={{ fontSize: 14 }} />,
            },
            {
              label: 'Responsables',
              value: '—',
              icon: <PeopleOutlinedIcon sx={{ fontSize: 14 }} />,
            },
          ].map((kpi) => (
            <Box
              key={kpi.label}
              sx={{
                flex: 1,
                p: 1,
                borderRadius: 1,
                border: '1px solid',
                borderColor: 'divider',
                textAlign: 'center',
              }}
            >
              <Stack
                direction="row"
                spacing={0.5}
                sx={{ justifyContent: 'center', alignItems: 'center', mb: 0.25, color: 'text.secondary' }}
              >
                {kpi.icon}
                <Typography variant="caption">{kpi.label}</Typography>
              </Stack>
              <Typography sx={{ fontWeight: 700, fontSize: '1rem' }}>{kpi.value}</Typography>
            </Box>
          ))}
        </Stack>
      </Box>
    </Paper>
  );
}
