import ExpandLessIcon from '@mui/icons-material/ExpandLess';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import MoreVertIcon from '@mui/icons-material/MoreVert';
import {
  Box,
  Button,
  Chip,
  Collapse,
  Divider,
  IconButton,
  Menu,
  MenuItem,
  Paper,
  Stack,
  Typography,
} from '@mui/material';
import { memo, useState, type MouseEvent } from 'react';
import type { DraftLigne } from './PrevisionGrilleVirtual';
import { PrevisionMontantInput } from './PrevisionMontantInput';
import { formatMontantBudget, hasRepartitionNonVide, moisMontant, MOIS_LABELS } from './previsionUtils';

interface PrevisionCartesMobileProps {
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
  onRepartirAnnuel?: (idRB: number) => void;
  onEffacerRepartition?: (idRB: number) => void;
}

/**
 * Vue mobile de la grille de prévisions : une carte par rubrique.
 * La grille 12 mois perdrait son sens en colonnes sur téléphone, donc les mois
 * sont saisis dans un panneau dépliable, verticalement, rubrique par rubrique.
 */
export const PrevisionCartesMobile = memo(function PrevisionCartesMobile({
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
}: PrevisionCartesMobileProps) {
  const mensuel = codeMode === 'MENSUEL';
  const isBi = codeType === 'BI';
  const showRbActions = mensuel && !isBi && modifiable && !!onRepartirAnnuel;

  const [expanded, setExpanded] = useState<Set<string>>(new Set());
  const [menuAnchor, setMenuAnchor] = useState<null | HTMLElement>(null);
  const [menuIdRB, setMenuIdRB] = useState<number | null>(null);

  const toggleExpanded = (key: string) => {
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
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

  if (lignes.length === 0) {
    return (
      <Paper sx={{ p: 3, textAlign: 'center' }}>
        <Typography variant="body2" color="text.secondary">
          {isBi
            ? 'Aucun détail. Ajoutez un investissement sous l’item sélectionné.'
            : 'Aucune rubrique pour ce filtre.'}
        </Typography>
      </Paper>
    );
  }

  return (
    // Pas de hauteur imposée : sur mobile la page défile, pas la grille.
    <Box sx={{ pb: 1 }}>
      <Stack spacing={1.25}>
        {lignes.map((ligne, index) => {
          const isGroupe = Boolean(ligne.estSection) && !isBi;

          if (isGroupe) {
            const collapsed =
              ligne.idGroupeRB != null && Boolean(groupesReplies?.has(ligne.idGroupeRB));
            return (
              <Box
                key={`g-${ligne.idGroupeRB ?? index}`}
                onClick={
                  ligne.idGroupeRB != null && onToggleGroupe
                    ? () => onToggleGroupe(ligne.idGroupeRB!)
                    : undefined
                }
                sx={{
                  px: 1.5,
                  py: 1.25,
                  borderRadius: 'var(--ef-radius)',
                  bgcolor: 'var(--ef-primary-soft)',
                  borderLeft: '4px solid var(--ef-primary)',
                  cursor: onToggleGroupe ? 'pointer' : 'default',
                }}
              >
                <Stack direction="row" sx={{ alignItems: 'center', gap: 1 }}>
                  <Box sx={{ flex: 1, minWidth: 0 }}>
                    <Typography variant="caption" sx={{ fontFamily: 'ui-monospace, monospace', fontWeight: 800 }}>
                      {ligne.codeRB ?? ligne.codeGroupe}
                    </Typography>
                    <Typography variant="body2" sx={{ fontWeight: 700 }}>
                      {ligne.libelleRB ?? ligne.libelleGroupe}
                    </Typography>
                  </Box>
                  <Typography variant="body2" sx={{ fontWeight: 800, whiteSpace: 'nowrap' }}>
                    {formatMontantBudget(mensuel ? ligne.cumulMensuel : ligne.montantAnnuel)}
                  </Typography>
                  {onToggleGroupe &&
                    (collapsed ? (
                      <ExpandMoreIcon fontSize="small" />
                    ) : (
                      <ExpandLessIcon fontSize="small" />
                    ))}
                </Stack>
              </Box>
            );
          }

          const key = `rb-${ligne.idRB ?? ligne.detailBI ?? index}`;
          const editable = modifiable;
          const hasRepartition = hasRepartitionNonVide(ligne.repartitions);
          const annualOnly = mensuel && !hasRepartition && ligne.montantAnnuel > 0;
          const isOpen = expanded.has(key);

          return (
            <Paper key={key} sx={{ p: 1.5 }}>
              <Stack direction="row" sx={{ alignItems: 'flex-start', gap: 1 }}>
                <Box sx={{ flex: 1, minWidth: 0 }}>
                  {isBi ? (
                    <Typography variant="caption" color="text.secondary">
                      Détail n° {index + 1}
                    </Typography>
                  ) : (
                    <Typography
                      variant="caption"
                      sx={{ fontFamily: 'ui-monospace, monospace', fontWeight: 700 }}
                    >
                      {ligne.codeRB}
                    </Typography>
                  )}
                  <Typography variant="body2" sx={{ fontWeight: 650 }}>
                    {isBi ? (ligne.detailBI ?? '—') : (ligne.libelleRB ?? '—')}
                  </Typography>
                </Box>
                {showRbActions && ligne.idRB != null && (
                  <IconButton
                    size="small"
                    aria-label="Actions rubrique"
                    disabled={!editable}
                    onClick={(e) => openMenu(e, ligne.idRB!)}
                    sx={{ mt: -0.5, mr: -0.5 }}
                  >
                    <MoreVertIcon fontSize="small" />
                  </IconButton>
                )}
              </Stack>

              <Divider sx={{ my: 1.25 }} />

              {mensuel ? (
                <>
                  <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between', gap: 1 }}>
                    <Typography variant="caption" color="text.secondary">
                      {annualOnly ? 'Montant annuel — non réparti' : 'Cumul mensuel'}
                    </Typography>
                    <Typography variant="body2" sx={{ fontWeight: 700 }}>
                      {formatMontantBudget(annualOnly ? ligne.montantAnnuel : ligne.cumulMensuel)}
                    </Typography>
                  </Stack>
                  {annualOnly && (
                    <Chip
                      size="small"
                      color="warning"
                      variant="outlined"
                      label="Répartition à définir"
                      sx={{ mt: 1 }}
                    />
                  )}
                  <Button
                    fullWidth
                    size="small"
                    variant="outlined"
                    onClick={() => toggleExpanded(key)}
                    endIcon={isOpen ? <ExpandLessIcon /> : <ExpandMoreIcon />}
                    sx={{ mt: 1.25 }}
                  >
                    {isOpen ? 'Masquer les 12 mois' : 'Saisir les 12 mois'}
                  </Button>
                  <Collapse in={isOpen} timeout="auto" unmountOnExit>
                    <Stack sx={{ mt: 1 }}>
                      {MOIS_LABELS.map((label, mi) => {
                        const mois = mi + 1;
                        return (
                          <Stack
                            key={mois}
                            direction="row"
                            sx={{
                              alignItems: 'center',
                              justifyContent: 'space-between',
                              gap: 1,
                              py: 0.75,
                              borderBottom: '1px solid',
                              borderColor: 'var(--ef-border-subtle)',
                              '&:last-of-type': { borderBottom: 'none' },
                            }}
                          >
                            <Typography variant="body2" sx={{ fontWeight: 600, minWidth: 44 }}>
                              {label}
                            </Typography>
                            <PrevisionMontantInput
                              value={moisMontant(ligne.repartitions, mois)}
                              disabled={!editable}
                              large
                              width={150}
                              aria-label={`Mois ${mois}`}
                              onCommit={(raw) => onMontantMois(ligne.idRB, index, mois, raw)}
                            />
                          </Stack>
                        );
                      })}
                    </Stack>
                  </Collapse>
                </>
              ) : (
                <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between', gap: 1 }}>
                  <Typography variant="caption" color="text.secondary">
                    Montant annuel
                  </Typography>
                  {editable ? (
                    <PrevisionMontantInput
                      value={ligne.montantAnnuel}
                      large
                      width={170}
                      aria-label="Montant annuel"
                      onCommit={(raw) => onMontantAnnuel(ligne.idRB, index, raw)}
                    />
                  ) : (
                    <Typography variant="body2" sx={{ fontWeight: 700 }}>
                      {formatMontantBudget(ligne.montantAnnuel)}
                    </Typography>
                  )}
                </Stack>
              )}
            </Paper>
          );
        })}
      </Stack>

      {hasNextPage && (
        <Button fullWidth variant="outlined" onClick={onLoadMore} disabled={loadingMore} sx={{ mt: 1.5 }}>
          {loadingMore ? 'Chargement…' : 'Charger les rubriques suivantes'}
        </Button>
      )}

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
          disabled={!lignes.find((l) => l.idRB === menuIdRB && hasRepartitionNonVide(l.repartitions))}
          onClick={() => {
            if (menuIdRB != null) onEffacerRepartition?.(menuIdRB);
            closeMenu();
          }}
        >
          Effacer la répartition
        </MenuItem>
      </Menu>
    </Box>
  );
});
