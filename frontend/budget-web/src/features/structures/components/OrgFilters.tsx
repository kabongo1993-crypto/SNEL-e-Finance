import RestartAltIcon from '@mui/icons-material/RestartAlt';
import SearchIcon from '@mui/icons-material/Search';
import {
  Box,
  Button,
  InputAdornment,
  MenuItem,
  Stack,
  TextField,
} from '@mui/material';

interface OrgFiltersProps {
  search: string;
  onSearchChange: (value: string) => void;
  typeFilter: string;
  onTypeChange: (value: string) => void;
  departementFilter: string;
  onDepartementChange: (value: string) => void;
  statutFilter: string;
  onStatutChange: (value: string) => void;
  types: string[];
  departements: { code: string; libelle: string }[];
  onReset: () => void;
}

export function OrgFilters({
  search,
  onSearchChange,
  typeFilter,
  onTypeChange,
  departementFilter,
  onDepartementChange,
  statutFilter,
  onStatutChange,
  types,
  departements,
  onReset,
}: OrgFiltersProps) {
  return (
    <Box
      sx={{
        p: 1.5,
        mb: 1.5,
        borderRadius: 2,
        bgcolor: 'var(--ef-surface)',
        border: '1px solid',
        borderColor: 'divider',
      }}
    >
      <Stack direction={{ xs: 'column', md: 'row' }} spacing={1.25} sx={{ alignItems: { md: 'center' } }}>
        <TextField
          size="small"
          value={search}
          onChange={(e) => onSearchChange(e.target.value)}
          placeholder="Rechercher par code, libellé ou responsable…"
          fullWidth
          sx={{ flex: 1.6, minWidth: 220 }}
          slotProps={{
            input: {
              startAdornment: (
                <InputAdornment position="start">
                  <SearchIcon fontSize="small" color="action" />
                </InputAdornment>
              ),
            },
          }}
        />
        <TextField
          select
          size="small"
          label="Type"
          value={typeFilter}
          onChange={(e) => onTypeChange(e.target.value)}
          sx={{ minWidth: 140 }}
        >
          <MenuItem value="all">Tous</MenuItem>
          {types.map((t) => (
            <MenuItem key={t} value={t}>
              {t}
            </MenuItem>
          ))}
        </TextField>
        <TextField
          select
          size="small"
          label="Département"
          value={departementFilter}
          onChange={(e) => onDepartementChange(e.target.value)}
          sx={{ minWidth: 160 }}
        >
          <MenuItem value="all">Tous</MenuItem>
          {departements.map((d) => (
            <MenuItem key={d.code} value={d.code}>
              {d.code}
            </MenuItem>
          ))}
        </TextField>
        <TextField
          select
          size="small"
          label="Statut"
          value={statutFilter}
          onChange={(e) => onStatutChange(e.target.value)}
          sx={{ minWidth: 120 }}
        >
          <MenuItem value="all">Tous</MenuItem>
          <MenuItem value="actif">Actif</MenuItem>
        </TextField>
        <Button size="small" startIcon={<RestartAltIcon />} onClick={onReset} variant="outlined">
          Réinitialiser
        </Button>
      </Stack>
    </Box>
  );
}
