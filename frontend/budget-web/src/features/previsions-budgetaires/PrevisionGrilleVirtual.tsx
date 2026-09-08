import MoreVertIcon from '@mui/icons-material/MoreVert';
import {
  Box,
  IconButton,
  Menu,
  MenuItem,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Tooltip,
  Typography,
} from '@mui/material';
import { useVirtualizer } from '@tanstack/react-virtual';
import { memo, useRef, useState, type MouseEvent } from 'react';
import type { PrevisionGrilleLigne } from '../../services/apiClient';
import { useResponsive } from '../../theme/useResponsive';
import { BudgetBreakRow } from './BudgetBreakRow';
import { PrevisionCartesMobile } from './PrevisionCartesMobile';
import { PrevisionMontantInput } from './PrevisionMontantInput';
import { formatMontantBudget, hasRepartitionNonVide, moisMontant, MOIS_LABELS } from './previsionUtils';

export type DraftLigne = PrevisionGrilleLigne & { dirty?: boolean; nouveau?: boolean };

interface PrevisionGrilleVirtualProps {
  lignes: DraftLigne[];
  codeType: string;
  codeMode: string;
  modifiable: boolean;
  hasNextPage: boolean;
  loadingMore: boolean;
  groupesReplies?: ReadonlySet<number>;
  onToggleGroupe?: (idGroupeRB: number) => void;
  onLoadMore: () => void;
  onMontantAnnuel: (idRB: number | null, index: number, raw: string) => void;
  onMontantMois: (idRB: number | null, index: number, mois: number, raw: string) => void;
  /** Mode MENSUEL DC/AE : ouvrir la répartition annuelle → 12 mois. */
  onRepartirAnnuel?: (idRB: number) => void;
  onEffacerRepartition?: (idRB: number) => void;
}

const ROW_HEIGHT = 38;
const GROUPE_HEIGHT = 46;

/** Largeurs fixes (tableLayout) — la désignation ne déforme pas les mois. */
const COL = {
  rb: 100,
  actions: 40,
  designation: 300,
  cumul: 104,
  mois: 88,
} as const;

function DesignationCell({ text, bold }: { text: string | null | undefined; bold?: boolean }) {
  const label = text ?? '';
  return (
    <Tooltip title={label || ''} enterDelay={400} disableHoverListener={!label}>
      <Typography
        component="span"
        sx={{
          display: 'block',
          fontWeight: bold ? 700 : 400,
          fontSize: '0.875rem',
          whiteSpace: 'nowrap',
          overflow: 'hidden',
          textOverflow: 'ellipsis',
          maxWidth: '100%',
        }}
      >
        {label || '—'}
      </Typography>
    </Tooltip>
  );
}

export const PrevisionGrilleVirtual = memo(function PrevisionGrilleVirtual(
  props: PrevisionGrilleVirtualProps,
) {
  const { isMobile } = useResponsive();
  // Sous md la grille 12 mois n'a plus de sens : saisie carte par carte.
  if (isMobile) {
    return <PrevisionCartesMobile {...props} />;
  }
  return <PrevisionGrilleTable {...props} />;
});

const PrevisionGrilleTable = memo(function PrevisionGrilleTable({
  lignes,
  codeType,
  codeMode,
  modifiable,
  hasNextPage,
  loadingMore,
  groupesReplies,
  onToggleGroupe,
  onLoadMore,
  onMontantAnnuel,
  onMontantMois,
  onRepartirAnnuel,
  onEffacerRepartition,
}: PrevisionGrilleVirtualProps) {
  const parentRef = useRef<HTMLDivElement>(null);
  const mensuel = codeMode === 'MENSUEL';
  const isBi = codeType === 'BI';
  const showRbActions = mensuel && !isBi && modifiable && !!onRepartirAnnuel;
  const colCount = mensuel ? (isBi ? 15 : showRbActions ? 16 : 15) : isBi ? 3 : 3;
  const tableMinWidth = isBi
    ? mensuel
      ? 48 + 280 + COL.cumul + 12 * COL.mois
      : 560
    : COL.rb +
      (showRbActions ? COL.actions : 0) +
      COL.designation +
      COL.cumul +
      (mensuel ? 12 * COL.mois : 0);

  const [menuAnchor, setMenuAnchor] = useState<null | HTMLElement>(null);
  const [menuIdRB, setMenuIdRB] = useState<number | null>(null);

  const virtualizer = useVirtualizer({
    count: lignes.length,
    getScrollElement: () => parentRef.current,
    estimateSize: (i) => (lignes[i]?.estSection && !isBi ? GROUPE_HEIGHT : ROW_HEIGHT),
    overscan: 12,
  });

  const items = virtualizer.getVirtualItems();

  const onScroll = () => {
    const el = parentRef.current;
    if (!el || !hasNextPage || loadingMore) return;
    if (el.scrollTop + el.clientHeight >= el.scrollHeight - 240) {
      onLoadMore();
    }
  };

  const openMenu = (e: MouseEvent<HTMLElement>, idRB: number) => {
    e.stopPropagation();
    setMenuAnchor(e.currentTarget);
    setMenuIdRB(idRB);
  };

  const closeMenu = () => {
    setMenuAnchor(null);
    setMenuIdRB(null);
  };

  return (
    <TableContainer
      component={Paper}
      ref={parentRef}
      onScroll={onScroll}
      sx={{
        height: '100%',
        maxHeight: '100%',
        overflow: 'auto',
        display: 'flex',
        flexDirection: 'column',
      }}
    >
      <Table stickyHeader size="small" sx={{ tableLayout: 'fixed', minWidth: tableMinWidth }}>
        <TableHead>
          <TableRow>
            {isBi ? (
              <>
                <TableCell sx={{ fontWeight: 700, width: 48 }}>N°</TableCell>
                <TableCell sx={{ fontWeight: 700, width: COL.designation }}>Détail</TableCell>
              </>
            ) : (
              <>
                <TableCell sx={{ fontWeight: 700, width: COL.rb }}>RB</TableCell>
                {showRbActions && <TableCell sx={{ fontWeight: 700, width: COL.actions, p: 0 }} />}
                <TableCell sx={{ fontWeight: 700, width: COL.designation }}>Désignation</TableCell>
              </>
            )}
            <TableCell align="right" sx={{ fontWeight: 700, width: COL.cumul }}>
              {mensuel ? 'Cumul' : 'Montant annuel'}
            </TableCell>
            {mensuel &&
              MOIS_LABELS.map((m) => (
                <TableCell key={m} align="right" sx={{ fontWeight: 700, width: COL.mois }}>
                  {m}
                </TableCell>
              ))}
          </TableRow>
        </TableHead>
        <TableBody>
          {lignes.length === 0 && (
            <TableRow>
              <TableCell colSpan={colCount}>
                <Typography variant="body2" color="text.secondary">
                  {isBi
                    ? 'Aucun détail. Ajoutez un investissement sous l’item sélectionné.'
                    : 'Aucune rubrique pour ce filtre.'}
                </Typography>
              </TableCell>
            </TableRow>
          )}
          {lignes.length > 0 && (
            <TableRow>
              <TableCell colSpan={colCount} sx={{ p: 0, border: 0, height: virtualizer.getTotalSize() }}>
                <Box sx={{ position: 'relative', height: virtualizer.getTotalSize() }}>
                  {items.map((vRow) => {
                    const ligne = lignes[vRow.index]!;
                    const index = vRow.index;
                    const isGroupe = !!ligne.estSection && !isBi;
                    const editable = modifiable && !isGroupe;
                    const collapsed =
                      isGroupe &&
                      ligne.idGroupeRB != null &&
                      !!groupesReplies?.has(ligne.idGroupeRB);

                    const hasRepartition = hasRepartitionNonVide(ligne.repartitions);
                    const annualOnly =
                      mensuel && !isGroupe && !hasRepartition && ligne.montantAnnuel > 0;

                    const monthCells = mensuel
                      ? MOIS_LABELS.map((_, mi) => {
                          const mois = mi + 1;
                          const val = moisMontant(ligne.repartitions, mois);
                          return (
                            <TableCell
                              key={mois}
                              align="right"
                              sx={{
                                py: 0.5,
                                width: COL.mois,
                                fontWeight: isGroupe ? 700 : 400,
                                whiteSpace: 'nowrap',
                              }}
                            >
                              {isGroupe ? (
                                formatMontantBudget(val)
                              ) : (
                                <PrevisionMontantInput
                                  // 0 → affichage vide (pas une répartition à zéro).
                                  value={val}
                                  disabled={!editable}
                                  aria-label={`Mois ${mois}`}
                                  onCommit={(raw) => onMontantMois(ligne.idRB, index, mois, raw)}
                                />
                              )}
                            </TableCell>
                          );
                        })
                      : null;

                    const actionsCell =
                      showRbActions && !isGroupe ? (
                        <TableCell sx={{ width: COL.actions, p: 0, textAlign: 'center' }}>
                          {ligne.idRB != null && (
                            <IconButton
                              size="small"
                              aria-label="Actions rubrique"
                              disabled={!editable}
                              onClick={(e) => openMenu(e, ligne.idRB!)}
                            >
                              <MoreVertIcon fontSize="small" />
                            </IconButton>
                          )}
                        </TableCell>
                      ) : showRbActions && isGroupe ? (
                        <TableCell sx={{ width: COL.actions, p: 0 }} />
                      ) : null;

                    const rowInner = isGroupe ? (
                      <BudgetBreakRow
                        code={ligne.codeRB ?? ligne.codeGroupe}
                        label={ligne.libelleRB ?? ligne.libelleGroupe}
                        cumul={formatMontantBudget(mensuel ? ligne.cumulMensuel : ligne.montantAnnuel)}
                        monthCells={monthCells}
                        height={vRow.size}
                        collapsed={collapsed}
                        showActionsSpacer={showRbActions}
                        onToggleCollapse={
                          ligne.idGroupeRB != null && onToggleGroupe
                            ? () => onToggleGroupe(ligne.idGroupeRB!)
                            : undefined
                        }
                      />
                    ) : (
                      <TableRow
                        sx={{
                          height: vRow.size,
                          bgcolor: index % 2 === 0 ? 'background.paper' : 'action.hover',
                          '& td': { py: 0.5 },
                        }}
                      >
                        {isBi ? (
                          <>
                            <TableCell sx={{ width: 48 }}>{index + 1}</TableCell>
                            <TableCell sx={{ width: COL.designation }}>
                              <DesignationCell text={ligne.detailBI} />
                            </TableCell>
                          </>
                        ) : (
                          <>
                            <TableCell
                              sx={{
                                fontFamily: 'ui-monospace, monospace',
                                width: COL.rb,
                                pl: 3,
                                whiteSpace: 'nowrap',
                              }}
                            >
                              {ligne.codeRB}
                            </TableCell>
                            {actionsCell}
                            <TableCell sx={{ width: COL.designation, maxWidth: COL.designation }}>
                              <DesignationCell text={ligne.libelleRB} />
                            </TableCell>
                          </>
                        )}
                        <TableCell align="right" sx={{ width: COL.cumul, whiteSpace: 'nowrap' }}>
                          {mensuel ? (
                            isGroupe ? (
                              formatMontantBudget(ligne.cumulMensuel || ligne.montantAnnuel)
                            ) : annualOnly ? (
                              <Tooltip title="Montant annuel existant — répartition non définie">
                                <Box>
                                  <Typography variant="body2" sx={{ fontWeight: 600 }}>
                                    {formatMontantBudget(ligne.montantAnnuel)}
                                  </Typography>
                                  <Typography variant="caption" color="text.secondary" sx={{ display: 'block' }}>
                                    Non réparti
                                  </Typography>
                                </Box>
                              </Tooltip>
                            ) : (
                              formatMontantBudget(ligne.cumulMensuel)
                            )
                          ) : editable ? (
                            <PrevisionMontantInput
                              value={ligne.montantAnnuel}
                              width={110}
                              aria-label="Montant annuel"
                              onCommit={(raw) => onMontantAnnuel(ligne.idRB, index, raw)}
                            />
                          ) : (
                            formatMontantBudget(ligne.montantAnnuel)
                          )}
                        </TableCell>
                        {monthCells}
                      </TableRow>
                    );

                    const rowKey = isGroupe
                      ? `g-${ligne.idGroupeRB ?? index}`
                      : `rb-${ligne.idRB ?? ligne.detailBI ?? index}`;

                    return (
                      <Box
                        key={rowKey}
                        data-index={vRow.index}
                        ref={virtualizer.measureElement}
                        sx={{
                          position: 'absolute',
                          top: 0,
                          left: 0,
                          width: '100%',
                          transform: `translateY(${vRow.start}px)`,
                        }}
                      >
                        <Table size="small" sx={{ tableLayout: 'fixed', width: '100%', minWidth: tableMinWidth }}>
                          <TableBody>{rowInner}</TableBody>
                        </Table>
                      </Box>
                    );
                  })}
                </Box>
              </TableCell>
            </TableRow>
          )}
        </TableBody>
      </Table>

      <Menu anchorEl={menuAnchor} open={Boolean(menuAnchor)} onClose={closeMenu}>
        <MenuItem
          onClick={() => {
            if (menuIdRB != null) onRepartirAnnuel?.(menuIdRB);
            closeMenu();
          }}
        >
          {(() => {
            const ligne = lignes.find((l) => l.idRB === menuIdRB);
            return ligne && hasRepartitionNonVide(ligne.repartitions)
              ? 'Remplacer la répartition…'
              : 'Répartir sur 12 mois…';
          })()}
        </MenuItem>
        <MenuItem
          disabled={
            !lignes.find((l) => l.idRB === menuIdRB && hasRepartitionNonVide(l.repartitions))
          }
          onClick={() => {
            if (menuIdRB != null) onEffacerRepartition?.(menuIdRB);
            closeMenu();
          }}
        >
          Effacer la répartition
        </MenuItem>
      </Menu>

      {loadingMore && (
        <Typography variant="caption" sx={{ display: 'block', p: 1, textAlign: 'center' }} color="text.secondary">
          Chargement des rubriques suivantes…
        </Typography>
      )}
    </TableContainer>
  );
});
