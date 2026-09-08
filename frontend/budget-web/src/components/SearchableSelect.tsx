import {
  Autocomplete,
  TextField,
  type SxProps,
  type Theme,
} from '@mui/material';
import { useMemo } from 'react';
import {
  formFieldGrowMax,
  formFieldSize,
  type FormFieldDensity,
} from './formTokens';

export type SearchableSelectOption = {
  value: string;
  label: string;
  disabled?: boolean;
};

export type SearchableSelectProps = {
  label: string;
  value: string;
  onChange: (value: string) => void;
  options: SearchableSelectOption[];
  helperText?: string;
  error?: boolean;
  required?: boolean;
  optional?: boolean;
  fullWidth?: boolean;
  disabled?: boolean;
  allowEmpty?: boolean;
  emptyLabel?: string;
  placeholder?: string;
  size?: 'small' | 'medium';
  /** Famille de largeur standard (préférée). */
  density?: FormFieldDensity;
  /** Largeur de base explicite (px) si density absente. */
  width?: number;
  /** Plafond si la valeur sélectionnée est longue. */
  maxWidth?: number;
  sx?: SxProps<Theme>;
  noOptionsText?: string;
};

/** Chrome Autocomplete small : clear + popup + paddings. */
const AUTOCOMPLETE_CHROME_PX = 108;
const VALUE_CHAR_PX = 8.2;
const LABEL_CHAR_PX = 7.4;

/**
 * Largeur de contrôle = famille fixe, jamais réduite aux valeurs courtes (« 2026 », « Tous »).
 * Peut croître un peu si la valeur affichée est longue, jusqu’à maxWidth.
 * Le label flottant impose aussi un plancher pour éviter « Cas de dos… ».
 */
function resolveControlWidth(args: {
  label: string;
  shownValue: string;
  familyWidth: number;
  maxWidth: number;
}): number {
  const { label, shownValue, familyWidth, maxWidth } = args;
  const labelFloor = Math.ceil(label.length * LABEL_CHAR_PX) + 28;
  const valueNeed =
    AUTOCOMPLETE_CHROME_PX + Math.ceil(Math.max(shownValue.length, 4) * VALUE_CHAR_PX);
  const floor = Math.max(familyWidth, Math.min(labelFloor, maxWidth));
  const grown = Math.max(floor, Math.min(valueNeed, maxWidth));
  return Math.min(maxWidth, grown);
}

/**
 * Select filtrable standard e-Finance.
 * Conserve le comportement Autocomplete (options, filtre, sélection).
 * Largeur = famille de densité, pas 1fr / estimate trop agressif.
 */
export function SearchableSelect({
  label,
  value,
  onChange,
  options,
  helperText,
  error,
  required,
  optional,
  fullWidth = false,
  disabled,
  allowEmpty = false,
  emptyLabel,
  placeholder = 'Rechercher…',
  size = 'small',
  density,
  width,
  maxWidth: maxWidthProp,
  sx,
  noOptionsText = 'Aucun résultat',
}: SearchableSelectProps) {
  const selected = options.find((o) => o.value === value) ?? null;
  const canClear = allowEmpty || options.some((o) => o.value === '');

  const familyWidth = density
    ? formFieldSize[density]
    : (width ?? formFieldSize.md);

  const maxWidth =
    maxWidthProp ??
    (density ? formFieldGrowMax[density] : Math.max(familyWidth + 80, 320));

  const fieldWidth = useMemo(() => {
    if (fullWidth) return undefined;
    const shown = selected?.label || placeholder || 'Tous';
    return resolveControlWidth({
      label,
      shownValue: shown,
      familyWidth,
      maxWidth,
    });
  }, [fullWidth, familyWidth, maxWidth, selected?.label, placeholder, label]);

  const resolvedHelper =
    helperText ?? (optional && !required ? 'Optionnel' : undefined);

  /** Dropdown au moins aussi large que le champ ; un peu plus pour les libellés longs. */
  const listMinWidth = fullWidth
    ? undefined
    : Math.max(fieldWidth ?? familyWidth, Math.min(maxWidth, familyWidth + 40));

  const baseSx: SxProps<Theme> = fullWidth
    ? { width: '100%', minWidth: 0, maxWidth: '100%', flex: '1 1 auto' }
    : {
        flex: '0 0 auto',
        flexShrink: 0,
        alignSelf: 'flex-start',
        width: fieldWidth,
        minWidth: fieldWidth,
        maxWidth,
        boxSizing: 'border-box',
      };

  return (
    <Autocomplete
      size={size}
      options={options}
      value={selected}
      onChange={(_, opt) => onChange(opt?.value ?? '')}
      getOptionLabel={(o) => (o?.label ? o.label : '')}
      isOptionEqualToValue={(a, b) => a.value === b.value}
      getOptionDisabled={(o) => !!o.disabled}
      disabled={disabled}
      disableClearable={!canClear}
      fullWidth={fullWidth}
      autoHighlight
      clearOnBlur={false}
      handleHomeEndKeys
      sx={[baseSx, ...(Array.isArray(sx) ? sx : sx ? [sx] : [])]}
      slotProps={{
        paper: {
          sx: {
            minWidth: listMinWidth,
            maxWidth: Math.max(maxWidth + 80, listMinWidth ?? maxWidth),
          },
        },
        listbox: {
          sx: {
            maxHeight: 280,
            '& .MuiAutocomplete-option': {
              whiteSpace: 'normal',
              wordBreak: 'break-word',
              lineHeight: 1.35,
              py: 1,
              alignItems: 'flex-start',
            },
          },
        },
      }}
      filterOptions={(opts, state) => {
        const q = state.inputValue.trim().toLowerCase();
        if (!q) return opts;
        return opts.filter(
          (o) =>
            o.label.toLowerCase().includes(q) || o.value.toLowerCase().includes(q),
        );
      }}
      renderOption={(props, option) => (
        <li {...props} key={option.value} title={option.label}>
          {option.label}
        </li>
      )}
      renderInput={(params) => {
        const paramsAny = params as typeof params & {
          slotProps?: Record<string, unknown>;
        };
        const paramsSlotProps = paramsAny.slotProps;
        return (
          <TextField
            {...params}
            label={label}
            required={required}
            error={error}
            helperText={resolvedHelper}
            placeholder={placeholder}
            fullWidth={fullWidth || !fieldWidth}
            title={selected?.label || undefined}
            slotProps={{
              ...(paramsSlotProps ?? {}),
              inputLabel: {
                ...((paramsSlotProps?.inputLabel as object) ?? {}),
                shrink: true,
              },
            }}
            sx={{
              width: fullWidth ? '100%' : fieldWidth,
              '& .MuiInputBase-input': {
                overflow: 'hidden',
                textOverflow: 'ellipsis',
              },
              '& .MuiInputLabel-root': {
                maxWidth: 'calc(100% - 10px)',
              },
            }}
          />
        );
      }}
      noOptionsText={
        options.length === 0
          ? emptyLabel
            ? emptyLabel
            : 'Aucune donnée disponible'
          : noOptionsText
      }
    />
  );
}
