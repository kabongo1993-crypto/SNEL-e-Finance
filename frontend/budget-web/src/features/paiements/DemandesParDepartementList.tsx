import AddIcon from '@mui/icons-material/Add';
import RemoveIcon from '@mui/icons-material/Remove';
import {
  Box,
  Checkbox,
  Chip,
  Collapse,
  IconButton,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Tooltip,
  Typography,
} from '@mui/material';
import { useMemo, useState, type ReactNode } from 'react';
import { SecondaryButton } from '../../components';
import { formatDateFr, formatMontantUsd } from './paiementUtils';
import { DemandePaiementStatusBadge } from './DemandePaiementStatusBadge';
import { DemandePaiementAssignationBadge } from './DemandePaiementAssignationBadge';
import { buildAssignationView } from './demandePaiementAssignationFromApi';
import type { DemandeEnrichie, DeptDemandeGroup } from './paiementBudgetUtils';

const HIDE_MD = { display: { xs: 'none', md: 'table-cell' } } as const;

/** Colonnes cibles : [ ] | Réf | Date | Demandeur | Objet | Montant demandé | Devise | Statut | ⋮ */
const COL = {
  select: { width: '2.75rem', minWidth: '2.75rem' },
  reference: { width: '9.5rem', minWidth: '9rem' },
  date: { width: '5.75rem', minWidth: '5.5rem', ...HIDE_MD },
  demandeur: { width: '9rem', minWidth: '8rem', ...HIDE_MD },
  objet: { minWidth: '12rem', width: 'auto' },
  montant: { width: '7.5rem', minWidth: '7rem' },
  devise: { width: '4.25rem', minWidth: '3.75rem' },
  statut: { width: '8.5rem', minWidth: '7.5rem' },
  actions: { width: '3rem', minWidth: '3rem' },
} as const;

const ELLIPSIS_1 = {
  display: 'block',
  whiteSpace: 'nowrap',
  overflow: 'hidden',
  textOverflow: 'ellipsis',
  maxWidth: '100%',
} as const;

/** Objet : jusqu'à 2 lignes, puis ellipsis. */
const OBJET_SX = {
  display: '-webkit-box',
  WebkitLineClamp: 2,
  WebkitBoxOrient: 'vertical' as const,
  overflow: 'hidden',
  textOverflow: 'ellipsis',
  whiteSpace: 'normal',
  wordBreak: 'break-word' as const,
  lineHeight: 1.35,
  maxHeight: '2.7em',
} as const;

function EllipsisCell({ text }: { text: string | null | undefined }) {
  const label = (text ?? '').trim() || '—';
  return (
    <Tooltip title={label === '—' ? '' : label} enterDelay={400} disableHoverListener={label === '—'}>
      <Typography variant="body2" component="span" sx={ELLIPSIS_1}>
        {label}
      </Typography>
    </Tooltip>
  );
}

function ObjetCell({ text }: { text: string | null | undefined }) {
  const label = (text ?? '').trim() || '—';
  return (
    <Tooltip title={label === '—' ? '' : label} enterDelay={500} disableHoverListener={label === '—'}>
      <Typography variant="body2" component="span" sx={OBJET_SX}>
        {label}
      </Typography>
    </Tooltip>
  );
}

function formatMontantDemande(d: DemandeEnrichie): string {
  const n = d.montantBrut;
  if (n == null || Number.isNaN(Number(n))) return '—';
  return new Intl.NumberFormat('fr-FR', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(Number(n));
}

function codeDevise(d: DemandeEnrichie): string {
  const raw = (d.devise ?? '').trim().toUpperCase();
  if (!raw) return '—';
  if (raw === 'EURO') return 'EUR';
  return raw;
}

export interface DemandesParDepartementListProps {
  groupes: DeptDemandeGroup[];
  emptyMessage?: string;
  /** Conservé pour compat : Demandeur toujours affiché dans le format cible. */
  showDemandeur?: boolean;
  /** Ignoré — Cas retiré du format liste cible. */
  showCas?: boolean;
  /** Conservé pour compat : Objet toujours affiché. */
  showObjet?: boolean;
  /** Ignoré — libellé fixe « Montant demandé ». */
  montantLabel?: string;
  /** Ignoré — montant brut formaté + colonne Devise séparée. */
  formatMontant?: (d: DemandeEnrichie) => ReactNode;
  renderActions: (d: DemandeEnrichie) => ReactNode;
  idUtilisateurCourant?: number | null;
  /** Sélection multiple batch (Phase 1) — absente si filtre « Toutes ». */
  selectable?: boolean;
  selectedIds?: ReadonlySet<number>;
  onToggleSelect?: (idDemandePaiement: number) => void;
  onSelectAllVisible?: (ids: number[]) => void;
}

/**
 * Liste de demandes regroupées par département.
 * Colonnes : Référence | Date | Demandeur | Objet | Montant demandé | Devise | Statut | ⋮
 */
export function DemandesParDepartementList({
  groupes,
  emptyMessage = 'Aucune demande pour les filtres sélectionnés.',
  renderActions,
  idUtilisateurCourant,
  selectable = false,
  selectedIds,
  onToggleSelect,
  onSelectAllVisible,
}: DemandesParDepartementListProps) {
  const [expanded, setExpanded] = useState<Record<string, boolean>>({});

  const nbDemandes = groupes.reduce((n, g) => n + g.demandes.length, 0);
  const montantGlobal = groupes.reduce((n, g) => n + g.montantTotal, 0);
  const allVisibleIds = useMemo(
    () => groupes.flatMap((g) => g.demandes.map((d) => d.idDemandePaiement)),
    [groupes],
  );
  const allVisibleSelected =
    selectable &&
    allVisibleIds.length > 0 &&
    allVisibleIds.every((id) => selectedIds?.has(id));
  const someVisibleSelected =
    selectable && allVisibleIds.some((id) => selectedIds?.has(id)) && !allVisibleSelected;

  const developperTout = () =>
    setExpanded(Object.fromEntries(groupes.map((g) => [g.key, true])));
  const reduireTout = () =>
    setExpanded(Object.fromEntries(groupes.map((g) => [g.key, false])));

  if (groupes.length === 0) {
    return (
      <Typography variant="body2" color="text.secondary">
        {emptyMessage}
      </Typography>
    );
  }

  return (
    <>
      <Paper sx={{ p: 1.5, mb: 2 }}>
        <Stack
          direction={{ xs: 'column', md: 'row' }}
          spacing={2}
          sx={{ justifyContent: 'space-between', flexWrap: 'wrap', alignItems: { md: 'center' } }}
        >
          <Box>
            <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 650 }}>
              TOTAL (filtre courant)
            </Typography>
            <Stack direction="row" spacing={2} useFlexGap sx={{ flexWrap: 'wrap', mt: 0.5 }}>
              <Typography variant="body2">
                {groupes.length} département{groupes.length > 1 ? 's' : ''}
              </Typography>
              <Typography variant="body2">
                {nbDemandes} demande{nbDemandes > 1 ? 's' : ''}
              </Typography>
              <Typography variant="body2" sx={{ fontWeight: 700 }}>
                TOTAL {formatMontantUsd(montantGlobal)}
              </Typography>
            </Stack>
          </Box>
          <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: 'wrap' }}>
            <SecondaryButton size="small" onClick={developperTout}>
              Développer tout
            </SecondaryButton>
            <SecondaryButton size="small" onClick={reduireTout}>
              Réduire tout
            </SecondaryButton>
          </Stack>
        </Stack>
      </Paper>

      {groupes.map((g) => {
        const isOpen = expanded[g.key] !== false;
        return (
          <Paper key={g.key} sx={{ mb: 2, overflow: 'hidden' }}>
            <Box
              sx={{
                px: 2,
                py: 1.5,
                bgcolor: 'var(--ef-surface-secondary)',
                borderBottom: isOpen ? '1px solid var(--ef-border)' : 'none',
              }}
            >
              <Stack
                direction="row"
                spacing={1}
                sx={{ justifyContent: 'space-between', alignItems: 'flex-start' }}
              >
                <Box sx={{ minWidth: 0, flex: 1 }}>
                  <Typography variant="h6" sx={{ fontWeight: 700 }}>
                    {g.libelleDepartement}
                  </Typography>
                  <Typography variant="body2" color="text.secondary">
                    {g.codeDepartement} · {g.demandes.length} demande(s)
                  </Typography>
                  <Typography variant="body2" sx={{ fontWeight: 700, mt: 0.75 }}>
                    TOTAL {formatMontantUsd(g.montantTotal)}
                  </Typography>
                  <Stack direction="row" spacing={0.75} useFlexGap sx={{ flexWrap: 'wrap', mt: 1 }}>
                    {g.nbSoumises > 0 && (
                      <Chip size="small" label={`${g.nbSoumises} soumise(s)`} color="info" />
                    )}
                    {g.nbReceptionnees > 0 && (
                      <Chip
                        size="small"
                        label={`${g.nbReceptionnees} réceptionnée(s)`}
                        color="info"
                        variant="outlined"
                      />
                    )}
                    {g.nbEnControle > 0 && (
                      <Chip size="small" label={`${g.nbEnControle} en contrôle`} color="warning" />
                    )}
                    {g.nbACorriger > 0 && (
                      <Chip
                        size="small"
                        label={`${g.nbACorriger} à corriger`}
                        color="error"
                        variant="outlined"
                      />
                    )}
                    {g.nbVisees > 0 && (
                      <Chip
                        size="small"
                        label={`${g.nbVisees} visée(s)`}
                        color="success"
                        variant="outlined"
                      />
                    )}
                  </Stack>
                </Box>
                <IconButton
                  size="small"
                  aria-label={isOpen ? 'Réduire' : 'Développer'}
                  onClick={() => setExpanded((p) => ({ ...p, [g.key]: !isOpen }))}
                >
                  {isOpen ? <RemoveIcon /> : <AddIcon />}
                </IconButton>
              </Stack>
            </Box>
            <Collapse in={isOpen} unmountOnExit>
              <TableContainer sx={{ overflowX: 'auto' }}>
                <Table
                  size="small"
                  sx={{
                    tableLayout: 'fixed',
                    width: '100%',
                    minWidth: 720,
                    '& .MuiTableCell-root': { py: 0.85, verticalAlign: 'middle' },
                  }}
                >
                  <TableHead>
                    <TableRow>
                      {selectable && (
                        <TableCell padding="checkbox" sx={COL.select}>
                            <Checkbox
                            size="small"
                            checked={Boolean(allVisibleSelected)}
                            indeterminate={Boolean(someVisibleSelected)}
                            onChange={() => {
                              if (allVisibleSelected) onSelectAllVisible?.([]);
                              else onSelectAllVisible?.(allVisibleIds);
                            }}
                            slotProps={{ input: { 'aria-label': 'Sélectionner toutes les demandes visibles' } }}
                          />
                        </TableCell>
                      )}
                      <TableCell sx={COL.reference}>Référence</TableCell>
                      <TableCell sx={COL.date}>Date</TableCell>
                      <TableCell sx={COL.demandeur}>Demandeur</TableCell>
                      <TableCell sx={COL.objet}>Objet</TableCell>
                      <TableCell align="right" sx={COL.montant}>
                        Montant demandé
                      </TableCell>
                      <TableCell sx={COL.devise}>Devise</TableCell>
                      <TableCell sx={COL.statut}>Statut</TableCell>
                      <TableCell align="right" sx={COL.actions}>
                        Actions
                      </TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {g.demandes.map((d) => (
                      <TableRow key={d.idDemandePaiement} hover selected={selectable && selectedIds?.has(d.idDemandePaiement)}>
                        {selectable && (
                          <TableCell padding="checkbox" sx={COL.select}>
                            <Checkbox
                              size="small"
                              checked={Boolean(selectedIds?.has(d.idDemandePaiement))}
                              onChange={() => onToggleSelect?.(d.idDemandePaiement)}
                              slotProps={{
                                input: { 'aria-label': `Sélectionner ${d.reference}` },
                              }}
                            />
                          </TableCell>
                        )}
                        <TableCell sx={COL.reference}>
                          <Typography
                            variant="body2"
                            sx={{ fontWeight: 700, ...ELLIPSIS_1 }}
                            title={d.reference}
                          >
                            {d.reference}
                          </Typography>
                        </TableCell>
                        <TableCell sx={{ ...COL.date, whiteSpace: 'nowrap' }}>
                          {formatDateFr(d.dateEmission)}
                        </TableCell>
                        <TableCell sx={COL.demandeur}>
                          <EllipsisCell text={d.libelleDemandeur ?? d.codeDemandeur} />
                        </TableCell>
                        <TableCell sx={COL.objet}>
                          <ObjetCell text={d.objet} />
                        </TableCell>
                        <TableCell
                          align="right"
                          sx={{
                            ...COL.montant,
                            whiteSpace: 'nowrap',
                            fontVariantNumeric: 'tabular-nums',
                          }}
                        >
                          {formatMontantDemande(d)}
                        </TableCell>
                        <TableCell sx={{ ...COL.devise, whiteSpace: 'nowrap' }}>
                          {codeDevise(d)}
                        </TableCell>
                        <TableCell sx={COL.statut}>
                          <Stack spacing={0.75} sx={{ alignItems: 'flex-start' }}>
                            <DemandePaiementStatusBadge statut={d.statut} />
                            <DemandePaiementAssignationBadge
                              compact
                              assignation={buildAssignationView(d, idUtilisateurCourant)}
                            />
                          </Stack>
                        </TableCell>
                        <TableCell align="right" sx={COL.actions}>
                          {renderActions(d)}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </TableContainer>
            </Collapse>
          </Paper>
        );
      })}
    </>
  );
}
