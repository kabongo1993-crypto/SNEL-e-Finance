import {
  Box,
  Button,
  Checkbox,
  Divider,
  IconButton,
  Menu,
  MenuItem,
  ListItemIcon,
  ListItemText,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TablePagination,
  TableRow,
  TableSortLabel,
  Typography,
} from '@mui/material';
import MoreVertIcon from '@mui/icons-material/MoreVert';
import VisibilityIcon from '@mui/icons-material/Visibility';
import EditIcon from '@mui/icons-material/Edit';
import CheckCircleOutlinedIcon from '@mui/icons-material/CheckCircleOutlined';
import CancelOutlinedIcon from '@mui/icons-material/CancelOutlined';
import DeleteOutlinedIcon from '@mui/icons-material/DeleteOutlined';
import ExpandLessIcon from '@mui/icons-material/ExpandLess';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import HistoryIcon from '@mui/icons-material/History';
import SwapVertIcon from '@mui/icons-material/SwapVert';
import { useEffect, useMemo, useState, type ReactNode } from 'react';
import { useResponsive } from '../theme/useResponsive';

export interface DataTableColumn<T> {
  id: string;
  label: string;
  align?: 'left' | 'right' | 'center';
  width?: number | string;
  sortable?: boolean;
  render: (row: T) => ReactNode;
  sortValue?: (row: T) => string | number;
  /**
   * Rôle de la colonne dans la carte affichée sous `md`.
   * Non renseigné = `meta` dès qu'au moins une colonne est annotée,
   * afin qu'aucune donnée ne disparaisse par oubli.
   */
  mobile?: 'title' | 'subtitle' | 'meta' | 'hidden';
}

export interface DataTableAction<T> {
  id: string;
  label: string;
  icon?: ReactNode;
  onClick: (row: T) => void;
  color?: 'inherit' | 'error' | 'primary' | 'success';
  hidden?: (row: T) => boolean;
  disabled?: (row: T) => boolean;
}

/** Rupture visuelle : une ligne d’en-tête à chaque changement de groupe. */
export interface DataTableGroupBy<T> {
  key: (row: T) => string;
  label: (row: T, countInGroup: number) => ReactNode;
  /** Défaut : true — clic sur la rupture pour plier / déplier. */
  collapsible?: boolean;
}

type DataTableDisplayItem<T> =
  | { kind: 'group'; key: string; row: T; count: number }
  | { kind: 'row'; row: T };

interface DataTableProps<T extends { id: string }> {
  columns: DataTableColumn<T>[];
  rows: T[];
  actions?: DataTableAction<T>[];
  groupBy?: DataTableGroupBy<T>;
  selectable?: boolean;
  emptyTitle?: string;
  emptyDescription?: string;
  defaultRowsPerPage?: number;
  onRowClick?: (row: T) => void;
  selectedRowId?: string | null;
  countLabel?: (count: number) => ReactNode;
  /** Notifie le parent des lignes cochées. */
  onSelectionChange?: (ids: string[]) => void;
  /** Contraint la hauteur du tableau et rend l'en-tête collant. */
  maxHeight?: number | string;
}

export const defaultActionIcons: Record<string, ReactNode> = {
  voir: <VisibilityIcon fontSize="small" />,
  modifier: <EditIcon fontSize="small" />,
  soumettre: <CheckCircleOutlinedIcon fontSize="small" />,
  valider: <CheckCircleOutlinedIcon fontSize="small" />,
  activer: <CheckCircleOutlinedIcon fontSize="small" />,
  desactiver: <CancelOutlinedIcon fontSize="small" />,
  rejeter: <CancelOutlinedIcon fontSize="small" />,
  supprimer: <DeleteOutlinedIcon fontSize="small" />,
  historique: <HistoryIcon fontSize="small" />,
};

export function DataTable<T extends { id: string }>({
  columns,
  rows,
  actions = [],
  groupBy,
  selectable,
  emptyTitle = 'Aucun résultat',
  emptyDescription = 'Aucune donnée ne correspond aux critères sélectionnés.',
  defaultRowsPerPage = 10,
  onRowClick,
  selectedRowId,
  countLabel,
  onSelectionChange,
  maxHeight,
}: DataTableProps<T>) {
  const { isMobile } = useResponsive();
  const [page, setPage] = useState(0);
  const [rowsPerPage, setRowsPerPage] = useState(defaultRowsPerPage);
  const [orderBy, setOrderBy] = useState<string | null>(null);
  const [order, setOrder] = useState<'asc' | 'desc'>('asc');
  const [selected, setSelected] = useState<string[]>([]);
  const [menuAnchor, setMenuAnchor] = useState<null | HTMLElement>(null);
  const [menuRow, setMenuRow] = useState<T | null>(null);
  const [sortAnchor, setSortAnchor] = useState<null | HTMLElement>(null);
  const [collapsed, setCollapsed] = useState<Set<string>>(new Set());
  const collapsible = groupBy != null && groupBy.collapsible !== false;

  const sorted = useMemo(() => {
    const copy = [...rows];
    const col = orderBy ? columns.find((c) => c.id === orderBy) : undefined;
    copy.sort((a, b) => {
      if (groupBy) {
        const ka = groupBy.key(a);
        const kb = groupBy.key(b);
        const gk = ka.localeCompare(kb, 'fr', { sensitivity: 'base' });
        if (gk !== 0) return gk;
      }
      if (!col?.sortValue) return 0;
      const av = col.sortValue(a);
      const bv = col.sortValue(b);
      if (av < bv) return order === 'asc' ? -1 : 1;
      if (av > bv) return order === 'asc' ? 1 : -1;
      return 0;
    });
    return copy;
  }, [rows, orderBy, order, columns, groupBy]);

  const groupCounts = useMemo(() => {
    const map = new Map<string, number>();
    if (!groupBy) return map;
    for (const row of sorted) {
      const k = groupBy.key(row);
      map.set(k, (map.get(k) ?? 0) + 1);
    }
    return map;
  }, [sorted, groupBy]);

  const displayItems = useMemo((): DataTableDisplayItem<T>[] => {
    if (!groupBy) return sorted.map((row) => ({ kind: 'row' as const, row }));
    const items: DataTableDisplayItem<T>[] = [];
    let currentKey: string | null = null;
    for (const row of sorted) {
      const k = groupBy.key(row);
      if (k !== currentKey) {
        currentKey = k;
        items.push({ kind: 'group', key: k, row, count: groupCounts.get(k) ?? 0 });
      }
      if (!collapsible || !collapsed.has(k)) {
        items.push({ kind: 'row', row });
      }
    }
    return items;
  }, [sorted, groupBy, groupCounts, collapsible, collapsed]);

  const paged = displayItems.slice(page * rowsPerPage, page * rowsPerPage + rowsPerPage);
  const pagedRows = paged.filter((item): item is { kind: 'row'; row: T } => item.kind === 'row').map((item) => item.row);
  const pagedForRender = useMemo((): DataTableDisplayItem<T>[] => {
    if (!groupBy || paged.length === 0 || paged[0].kind === 'group') return paged;
    const firstRow = paged[0].row;
    const key = groupBy.key(firstRow);
    return [
      { kind: 'group', key, row: firstRow, count: groupCounts.get(key) ?? 0 },
      ...paged,
    ];
  }, [paged, groupBy, groupCounts]);

  useEffect(() => {
    const maxPage = Math.max(0, Math.ceil(displayItems.length / rowsPerPage) - 1);
    if (page > maxPage) setPage(maxPage);
  }, [displayItems.length, rowsPerPage, page]);

  const toggleGroup = (key: string) => {
    setCollapsed((prev) => {
      const next = new Set(prev);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  };

  const allGroupKeys = useMemo(() => [...groupCounts.keys()], [groupCounts]);
  const allCollapsed = collapsible && allGroupKeys.length > 0 && allGroupKeys.every((k) => collapsed.has(k));

  const renderGroupHeader = (item: Extract<DataTableDisplayItem<T>, { kind: 'group' }>) => {
    const isCollapsed = collapsed.has(item.key);
    const label = groupBy!.label(item.row, item.count);
    if (!collapsible) return label;
    return (
      <Box
        component="button"
        type="button"
        onClick={() => toggleGroup(item.key)}
        aria-expanded={!isCollapsed}
        aria-label={isCollapsed ? 'Déplier le groupe' : 'Replier le groupe'}
        sx={{
          display: 'flex',
          alignItems: 'center',
          gap: 0.75,
          width: '100%',
          border: 0,
          background: 'transparent',
          p: 0,
          m: 0,
          cursor: 'pointer',
          textAlign: 'left',
          color: 'inherit',
          font: 'inherit',
        }}
      >
        {isCollapsed ? <ExpandMoreIcon fontSize="small" /> : <ExpandLessIcon fontSize="small" />}
        <Box sx={{ minWidth: 0, flex: 1 }}>{label}</Box>
      </Box>
    );
  };

  /** Répartition des colonnes dans la carte mobile. */
  const cardLayout = useMemo(() => {
    const annotated = columns.some((c) => c.mobile);
    if (!annotated) {
      return {
        title: columns.slice(0, 1),
        subtitle: [] as DataTableColumn<T>[],
        meta: columns.slice(1, 4),
      };
    }
    const title = columns.filter((c) => c.mobile === 'title');
    const meta = columns.filter((c) => c.mobile === 'meta' || c.mobile === undefined);
    return {
      title: title.length ? title : meta.slice(0, 1),
      subtitle: columns.filter((c) => c.mobile === 'subtitle'),
      meta: title.length ? meta : meta.slice(1),
    };
  }, [columns]);

  const sortableColumns = useMemo(
    () => columns.filter((c) => c.sortable && c.sortValue),
    [columns],
  );

  useEffect(() => {
    onSelectionChange?.(selected);
    // Le parent ne doit pas avoir à mémoïser son callback pour éviter une boucle.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selected]);

  const toggleAll = (checked: boolean) => {
    setSelected(checked ? pagedRows.map((r) => r.id) : []);
  };

  const toggleOne = (id: string) => {
    setSelected((prev) => (prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id]));
  };

  const applySort = (id: string) => {
    if (orderBy === id) {
      setOrder((o) => (o === 'asc' ? 'desc' : 'asc'));
    } else {
      setOrderBy(id);
      setOrder('asc');
    }
  };

  const openMenu = (event: React.MouseEvent<HTMLElement>, row: T) => {
    setMenuAnchor(event.currentTarget);
    setMenuRow(row);
  };

  const closeMenu = () => {
    setMenuAnchor(null);
    setMenuRow(null);
  };

  const visibleActions = menuRow
    ? actions.filter((a) => !(a.hidden?.(menuRow) ?? false))
    : [];

  const summary = (
    <Stack direction="row" sx={{ alignItems: 'center', gap: 1, flexWrap: 'wrap', flex: 1 }}>
      <Typography variant="body2" color="text.secondary">
        {countLabel ? (
          countLabel(rows.length)
        ) : (
          <>
            <strong>{rows.length}</strong> résultat{rows.length > 1 ? 's' : ''}
          </>
        )}
        {selected.length > 0 && ` · ${selected.length} sélectionné(s)`}
      </Typography>
      {collapsible && allGroupKeys.length > 0 && (
        <Button
          size="small"
          onClick={() => setCollapsed(allCollapsed ? new Set() : new Set(allGroupKeys))}
        >
          {allCollapsed ? 'Tout déplier' : 'Tout plier'}
        </Button>
      )}
    </Stack>
  );

  const emptyBlock = (
    <Box sx={{ py: 6, px: 2, textAlign: 'center' }}>
      <Typography sx={{ fontWeight: 600 }}>{emptyTitle}</Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
        {emptyDescription}
      </Typography>
    </Box>
  );

  const pagination = (
    <TablePagination
      component="div"
      count={displayItems.length}
      page={page}
      onPageChange={(_, p) => setPage(p)}
      rowsPerPage={rowsPerPage}
      onRowsPerPageChange={(e) => {
        setRowsPerPage(parseInt(e.target.value, 10));
        setPage(0);
      }}
      rowsPerPageOptions={[5, 10, 25, 50]}
      labelRowsPerPage="Lignes"
      labelDisplayedRows={({ from, to, count }) => `${from}–${to} sur ${count}`}
      sx={{
        borderTop: '1px solid',
        borderColor: 'divider',
        '& .MuiTablePagination-toolbar': { px: { xs: 1, sm: 2 }, flexWrap: 'wrap' },
      }}
    />
  );

  const actionsMenu = (
    <Menu anchorEl={menuAnchor} open={Boolean(menuAnchor)} onClose={closeMenu}>
      {visibleActions.map((action) => (
        <MenuItem
          key={action.id}
          onClick={() => {
            if (menuRow) action.onClick(menuRow);
            closeMenu();
          }}
          sx={action.color === 'error' ? { color: 'error.main' } : undefined}
        >
          <ListItemIcon sx={action.color === 'error' ? { color: 'error.main' } : undefined}>
            {action.icon ?? defaultActionIcons[action.id] ?? <MoreVertIcon fontSize="small" />}
          </ListItemIcon>
          <ListItemText>{action.label}</ListItemText>
        </MenuItem>
      ))}
    </Menu>
  );

  if (isMobile) {
    return (
      <Paper sx={{ overflow: 'hidden' }}>
        <Stack
          direction="row"
          sx={{
            justifyContent: 'space-between',
            alignItems: 'center',
            gap: 1,
            px: 2,
            py: 1.25,
            borderBottom: '1px solid',
            borderColor: 'divider',
          }}
        >
          {summary}
          <Stack direction="row" sx={{ alignItems: 'center', gap: 0.5 }}>
            {selectable && pagedRows.length > 0 && (
              <Checkbox
                size="small"
                indeterminate={selected.length > 0 && selected.length < pagedRows.length}
                checked={pagedRows.length > 0 && selected.length === pagedRows.length}
                onChange={(e) => toggleAll(e.target.checked)}
                slotProps={{ input: { 'aria-label': 'Tout sélectionner' } }}
              />
            )}
            {sortableColumns.length > 0 && (
              <IconButton
                size="small"
                aria-label="Trier"
                onClick={(e) => setSortAnchor(e.currentTarget)}
              >
                <SwapVertIcon fontSize="small" />
              </IconButton>
            )}
          </Stack>
        </Stack>

        {paged.length === 0 ? (
          emptyBlock
        ) : (
          <Stack sx={{ p: 1.25, gap: 1.25 }}>
            {pagedForRender.map((item, index) => {
              if (item.kind === 'group') {
                return (
                  <Box
                    key={`g-${item.key}-${index}`}
                    sx={{
                      px: 1.5,
                      py: 1,
                      borderRadius: 'var(--ef-radius)',
                      bgcolor: 'var(--ef-primary-soft)',
                      border: '1px solid',
                      borderColor: 'var(--ef-border-subtle)',
                      fontWeight: 700,
                      fontSize: '0.8125rem',
                    }}
                  >
                    {renderGroupHeader(item)}
                  </Box>
                );
              }
              const row = item.row;
              const isSelected = selectedRowId === row.id || selected.includes(row.id);
              return (
                <Box
                  key={row.id}
                  onClick={onRowClick ? () => onRowClick(row) : undefined}
                  sx={{
                    p: 1.5,
                    borderRadius: 'var(--ef-radius)',
                    border: '1px solid',
                    borderColor: isSelected ? 'primary.main' : 'var(--ef-border-subtle)',
                    bgcolor: isSelected ? 'var(--ef-primary-soft)' : 'var(--ef-surface)',
                    cursor: onRowClick ? 'pointer' : 'default',
                  }}
                >
                  <Stack direction="row" sx={{ alignItems: 'flex-start', gap: 1 }}>
                    {selectable && (
                      <Checkbox
                        size="small"
                        sx={{ mt: -0.75, ml: -1 }}
                        checked={selected.includes(row.id)}
                        onChange={() => toggleOne(row.id)}
                        onClick={(e) => e.stopPropagation()}
                      />
                    )}
                    <Box sx={{ flex: 1, minWidth: 0 }}>
                      {cardLayout.title.map((col) => (
                        <Box key={col.id} sx={{ fontWeight: 700, fontSize: '0.9375rem' }}>
                          {col.render(row)}
                        </Box>
                      ))}
                      {cardLayout.subtitle.map((col) => (
                        <Box
                          key={col.id}
                          sx={{ color: 'text.secondary', fontSize: '0.8125rem', mt: 0.25 }}
                        >
                          {col.render(row)}
                        </Box>
                      ))}
                    </Box>
                    {actions.length > 0 && (
                      <IconButton
                        size="small"
                        sx={{ mt: -0.5, mr: -0.5 }}
                        onClick={(e) => {
                          e.stopPropagation();
                          openMenu(e, row);
                        }}
                        aria-label="Actions"
                      >
                        <MoreVertIcon fontSize="small" />
                      </IconButton>
                    )}
                  </Stack>

                  {cardLayout.meta.length > 0 && (
                    <>
                      <Divider sx={{ my: 1.25 }} />
                      <Box
                        sx={{
                          display: 'grid',
                          gridTemplateColumns: 'repeat(auto-fit, minmax(120px, 1fr))',
                          gap: 1,
                        }}
                      >
                        {cardLayout.meta.map((col) => (
                          <Box key={col.id} sx={{ minWidth: 0 }}>
                            <Typography
                              variant="caption"
                              color="text.secondary"
                              sx={{ display: 'block', lineHeight: 1.3 }}
                            >
                              {col.label}
                            </Typography>
                            <Box sx={{ fontSize: '0.8125rem', fontVariantNumeric: 'tabular-nums' }}>
                              {col.render(row)}
                            </Box>
                          </Box>
                        ))}
                      </Box>
                    </>
                  )}
                </Box>
              );
            })}
          </Stack>
        )}

        {pagination}

        <Menu anchorEl={sortAnchor} open={Boolean(sortAnchor)} onClose={() => setSortAnchor(null)}>
          {sortableColumns.map((col) => (
            <MenuItem
              key={col.id}
              selected={orderBy === col.id}
              onClick={() => {
                applySort(col.id);
                setSortAnchor(null);
              }}
            >
              <ListItemText>
                {col.label}
                {orderBy === col.id ? (order === 'asc' ? ' ↑' : ' ↓') : ''}
              </ListItemText>
            </MenuItem>
          ))}
        </Menu>

        {actionsMenu}
      </Paper>
    );
  }

  return (
    <Paper sx={{ overflow: 'hidden' }}>
      <Stack
        direction="row"
        sx={{
          justifyContent: 'space-between',
          alignItems: 'center',
          px: 2,
          py: 1.25,
          borderBottom: '1px solid',
          borderColor: 'divider',
        }}
      >
        {summary}
      </Stack>
      <TableContainer sx={maxHeight ? { maxHeight } : undefined}>
        <Table size="small" stickyHeader={Boolean(maxHeight)}>
          <TableHead>
            <TableRow>
              {selectable && (
                <TableCell padding="checkbox">
                  <Checkbox
                    size="small"
                    indeterminate={selected.length > 0 && selected.length < pagedRows.length}
                    checked={pagedRows.length > 0 && selected.length === pagedRows.length}
                    onChange={(e) => toggleAll(e.target.checked)}
                  />
                </TableCell>
              )}
              {columns.map((col) => (
                <TableCell key={col.id} align={col.align} sx={{ width: col.width }}>
                  {col.sortable ? (
                    <TableSortLabel
                      active={orderBy === col.id}
                      direction={orderBy === col.id ? order : 'asc'}
                      onClick={() => applySort(col.id)}
                    >
                      {col.label}
                    </TableSortLabel>
                  ) : (
                    col.label
                  )}
                </TableCell>
              ))}
              {actions.length > 0 && <TableCell align="right">Actions</TableCell>}
            </TableRow>
          </TableHead>
          <TableBody>
            {paged.length === 0 ? (
              <TableRow>
                <TableCell colSpan={columns.length + (selectable ? 1 : 0) + (actions.length ? 1 : 0)}>
                  {emptyBlock}
                </TableCell>
              </TableRow>
            ) : (
              pagedForRender.map((item, index) => {
                const colSpan =
                  columns.length + (selectable ? 1 : 0) + (actions.length ? 1 : 0);
                if (item.kind === 'group') {
                  return (
                    <TableRow
                      key={`g-${item.key}-${index}`}
                      sx={{
                        bgcolor: 'var(--ef-primary-soft)',
                        '& td': {
                          fontWeight: 700,
                          borderBottomColor: 'var(--ef-border)',
                          py: 1,
                        },
                      }}
                    >
                      <TableCell colSpan={colSpan}>{renderGroupHeader(item)}</TableCell>
                    </TableRow>
                  );
                }
                const row = item.row;
                return (
                <TableRow
                  key={row.id}
                  hover
                  selected={selectedRowId === row.id || selected.includes(row.id)}
                  onClick={onRowClick ? () => onRowClick(row) : undefined}
                  sx={onRowClick ? { cursor: 'pointer' } : undefined}
                >
                  {selectable && (
                    <TableCell padding="checkbox">
                      <Checkbox
                        size="small"
                        checked={selected.includes(row.id)}
                        onChange={() => toggleOne(row.id)}
                        onClick={(e) => e.stopPropagation()}
                      />
                    </TableCell>
                  )}
                  {columns.map((col) => (
                    <TableCell key={col.id} align={col.align}>
                      {col.render(row)}
                    </TableCell>
                  ))}
                  {actions.length > 0 && (
                    <TableCell align="right">
                      <IconButton
                        size="small"
                        onClick={(e) => {
                          e.stopPropagation();
                          openMenu(e, row);
                        }}
                        aria-label="Actions"
                      >
                        <MoreVertIcon fontSize="small" />
                      </IconButton>
                    </TableCell>
                  )}
                </TableRow>
                );
              })
            )}
          </TableBody>
        </Table>
      </TableContainer>
      {pagination}
      {actionsMenu}
    </Paper>
  );
}
