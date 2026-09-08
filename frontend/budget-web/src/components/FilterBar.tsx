import { Box, MenuItem, TextField } from '@mui/material';
import type { ReactNode } from 'react';
import { FilterSearchRow, FilterZone } from './FilterZone';
import { SearchInput } from './SearchInput';
import { MOCK_DEPARTEMENTS, MOCK_EXERCICES, MOCK_PERIODES, MOCK_UNITES } from '../mocks/types';

interface FilterBarProps {
  search?: string;
  onSearchChange?: (value: string) => void;
  searchPlaceholder?: string;
  showPeriodFilters?: boolean;
  exercice?: string;
  onExerciceChange?: (value: string) => void;
  periode?: string;
  onPeriodeChange?: (value: string) => void;
  departement?: string;
  onDepartementChange?: (value: string) => void;
  unite?: string;
  onUniteChange?: (value: string) => void;
  extra?: ReactNode;
}

/**
 * Barre de filtres standard — grille CSS alignée + recherche / extras en rangée dédiée.
 */
export function FilterBar({
  search,
  onSearchChange,
  searchPlaceholder,
  showPeriodFilters,
  exercice,
  onExerciceChange,
  periode,
  onPeriodeChange,
  departement,
  onDepartementChange,
  unite,
  onUniteChange,
  extra,
}: FilterBarProps) {
  const searchNode =
    onSearchChange != null ? (
      <SearchInput
        value={search ?? ''}
        onChange={onSearchChange}
        placeholder={searchPlaceholder}
      />
    ) : undefined;

  return (
    <Box
      sx={{
        p: 2,
        mb: 2,
        borderRadius: 2,
        bgcolor: 'background.paper',
        border: '1px solid',
        borderColor: 'divider',
      }}
    >
      {showPeriodFilters ? (
        <FilterZone
          columns={{ xs: 1, sm: 2, md: 2, lg: 4, xl: 4 }}
          search={searchNode}
          actions={extra}
        >
          <TextField
            select
            size="small"
            fullWidth
            label="Exercice"
            value={exercice ?? '2026'}
            onChange={(e) => onExerciceChange?.(e.target.value)}
          >
            {MOCK_EXERCICES.map((ex) => (
              <MenuItem key={ex} value={ex}>
                {ex}
              </MenuItem>
            ))}
          </TextField>
          <TextField
            select
            size="small"
            fullWidth
            label="Période"
            value={periode ?? 'annee'}
            onChange={(e) => onPeriodeChange?.(e.target.value)}
          >
            {MOCK_PERIODES.map((p) => (
              <MenuItem key={p.value} value={p.value}>
                {p.label}
              </MenuItem>
            ))}
          </TextField>
          <TextField
            select
            size="small"
            fullWidth
            label="Département"
            value={departement ?? 'all'}
            onChange={(e) => onDepartementChange?.(e.target.value)}
          >
            <MenuItem value="all">Tous</MenuItem>
            {MOCK_DEPARTEMENTS.map((d) => (
              <MenuItem key={d.id} value={d.id}>
                {d.id}
              </MenuItem>
            ))}
          </TextField>
          <TextField
            select
            size="small"
            fullWidth
            label="Unité budgétaire"
            value={unite ?? 'all'}
            onChange={(e) => onUniteChange?.(e.target.value)}
          >
            <MenuItem value="all">Toutes</MenuItem>
            {MOCK_UNITES.map((u) => (
              <MenuItem key={u.id} value={u.id}>
                {u.id}
              </MenuItem>
            ))}
          </TextField>
        </FilterZone>
      ) : (
        <FilterSearchRow search={searchNode} actions={extra} />
      )}
    </Box>
  );
}
