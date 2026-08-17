import {
  Box,
  Checkbox,
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
import HistoryIcon from '@mui/icons-material/History';
import { useMemo, useState, type ReactNode } from 'react';

export interface DataTableColumn<T> {
  id: string;
  label: string;
  align?: 'left' | 'right' | 'center';
  width?: number | string;
  sortable?: boolean;
  render: (row: T) => ReactNode;
  sortValue?: (row: T) => string | number;
}

export interface DataTableAction<T> {
  id: string;
  label: string;
  icon?: ReactNode;
  onClick: (row: T) => void;
  color?: 'inherit' | 'error' | 'primary' | 'success';
  hidden?: (row: T) => boolean;
}

interface DataTableProps<T extends { id: string }> {
  columns: DataTableColumn<T>[];
  rows: T[];
  actions?: DataTableAction<T>[];
  selectable?: boolean;
  emptyTitle?: string;
  emptyDescription?: string;
  defaultRowsPerPage?: number;
}

const defaultActionIcons: Record<string, ReactNode> = {
  voir: <VisibilityIcon fontSize="small" />,
  modifier: <EditIcon fontSize="small" />,
  valider: <CheckCircleOutlinedIcon fontSize="small" />,
  rejeter: <CancelOutlinedIcon fontSize="small" />,
  supprimer: <DeleteOutlinedIcon fontSize="small" />,
  historique: <HistoryIcon fontSize="small" />,
};

export function DataTable<T extends { id: string }>({
  columns,
  rows,
  actions = [],
  selectable,
  emptyTitle = 'Aucun résultat',
  emptyDescription = 'Aucune donnée ne correspond aux critères sélectionnés.',
  defaultRowsPerPage = 10,
}: DataTableProps<T>) {
  const [page, setPage] = useState(0);
  const [rowsPerPage, setRowsPerPage] = useState(defaultRowsPerPage);
  const [orderBy, setOrderBy] = useState<string | null>(null);
  const [order, setOrder] = useState<'asc' | 'desc'>('asc');
  const [selected, setSelected] = useState<string[]>([]);
  const [menuAnchor, setMenuAnchor] = useState<null | HTMLElement>(null);
  const [menuRow, setMenuRow] = useState<T | null>(null);

  const sorted = useMemo(() => {
    if (!orderBy) return rows;
    const col = columns.find((c) => c.id === orderBy);
    if (!col?.sortValue) return rows;
    const copy = [...rows];
    copy.sort((a, b) => {
      const av = col.sortValue!(a);
      const bv = col.sortValue!(b);
      if (av < bv) return order === 'asc' ? -1 : 1;
      if (av > bv) return order === 'asc' ? 1 : -1;
      return 0;
    });
    return copy;
  }, [rows, orderBy, order, columns]);

  const paged = sorted.slice(page * rowsPerPage, page * rowsPerPage + rowsPerPage);

  const toggleAll = (checked: boolean) => {
    setSelected(checked ? paged.map((r) => r.id) : []);
  };

  const toggleOne = (id: string) => {
    setSelected((prev) => (prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id]));
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

  return (
    <Paper sx={{ overflow: 'hidden' }}>
      <Stack
        direction="row"
       
       
        sx={{ justifyContent: 'space-between', alignItems: 'center',  px: 2, py: 1.25, borderBottom: '1px solid', borderColor: 'divider' }}
      >
        <Typography variant="body2" color="text.secondary">
          <strong>{rows.length}</strong> résultat{rows.length > 1 ? 's' : ''}
          {selected.length > 0 && ` · ${selected.length} sélectionné(s)`}
        </Typography>
      </Stack>
      <TableContainer>
        <Table size="small">
          <TableHead>
            <TableRow>
              {selectable && (
                <TableCell padding="checkbox">
                  <Checkbox
                    size="small"
                    indeterminate={selected.length > 0 && selected.length < paged.length}
                    checked={paged.length > 0 && selected.length === paged.length}
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
                      onClick={() => {
                        if (orderBy === col.id) {
                          setOrder((o) => (o === 'asc' ? 'desc' : 'asc'));
                        } else {
                          setOrderBy(col.id);
                          setOrder('asc');
                        }
                      }}
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
                  <Box sx={{ py: 6, textAlign: 'center' }}>
                    <Typography sx={{ fontWeight: 600 }}>{emptyTitle}</Typography>
                    <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
                      {emptyDescription}
                    </Typography>
                  </Box>
                </TableCell>
              </TableRow>
            ) : (
              paged.map((row) => (
                <TableRow key={row.id} hover selected={selected.includes(row.id)}>
                  {selectable && (
                    <TableCell padding="checkbox">
                      <Checkbox
                        size="small"
                        checked={selected.includes(row.id)}
                        onChange={() => toggleOne(row.id)}
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
                      <IconButton size="small" onClick={(e) => openMenu(e, row)} aria-label="Actions">
                        <MoreVertIcon fontSize="small" />
                      </IconButton>
                    </TableCell>
                  )}
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </TableContainer>
      <TablePagination
        component="div"
        count={rows.length}
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
      />
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
    </Paper>
  );
}
