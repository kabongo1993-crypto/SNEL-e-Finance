import { Box, MenuItem, Stack, TextField } from '@mui/material';
import type { ReactNode } from 'react';
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
      <Stack
        direction={{ xs: 'column', md: 'row' }}
        spacing={1.5}
        sx={{ alignItems: { md: 'center' } }}
      >
        {onSearchChange != null && (
          <Box sx={{ flex: 1, minWidth: { md: 220 } }}>
            <SearchInput
              value={search ?? ''}
              onChange={onSearchChange}
              placeholder={searchPlaceholder}
            />
          </Box>
        )}
        {showPeriodFilters && (
          <>
            <TextField
              select
              size="small"
              label="Exercice"
              value={exercice ?? '2026'}
              onChange={(e) => onExerciceChange?.(e.target.value)}
              sx={{ minWidth: 120 }}
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
              label="Période"
              value={periode ?? 'annee'}
              onChange={(e) => onPeriodeChange?.(e.target.value)}
              sx={{ minWidth: 160 }}
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
              label="Département"
              value={departement ?? 'all'}
              onChange={(e) => onDepartementChange?.(e.target.value)}
              sx={{ minWidth: 180 }}
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
              label="Unité budgétaire"
              value={unite ?? 'all'}
              onChange={(e) => onUniteChange?.(e.target.value)}
              sx={{ minWidth: 180 }}
            >
              <MenuItem value="all">Toutes</MenuItem>
              {MOCK_UNITES.map((u) => (
                <MenuItem key={u.id} value={u.id}>
                  {u.id}
                </MenuItem>
              ))}
            </TextField>
          </>
        )}
        {extra}
      </Stack>
    </Box>
  );
}
