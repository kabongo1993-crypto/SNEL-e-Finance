import {
  FormControl,
  FormHelperText,
  InputAdornment,
  InputLabel,
  MenuItem,
  Select,
  TextField,
  type SelectChangeEvent,
  type TextFieldProps,
} from '@mui/material';
import type { ReactNode } from 'react';

type CommonFieldProps = {
  label: string;
  helperText?: string;
  error?: boolean;
  required?: boolean;
  fullWidth?: boolean;
  disabled?: boolean;
};

export function FormField({
  label,
  helperText,
  error,
  required,
  fullWidth = true,
  ...rest
}: CommonFieldProps & Omit<TextFieldProps, 'label' | 'helperText' | 'error' | 'required' | 'fullWidth'>) {
  return (
    <TextField
      label={label}
      helperText={helperText}
      error={error}
      required={required}
      fullWidth={fullWidth}
      size="small"
      {...rest}
    />
  );
}

export function DateField(props: CommonFieldProps & { value: string; onChange: (value: string) => void }) {
  const { label, helperText, error, required, fullWidth = true, value, onChange, disabled } = props;
  return (
    <TextField
      type="date"
      label={label}
      value={value}
      onChange={(e) => onChange(e.target.value)}
      helperText={helperText}
      error={error}
      required={required}
      fullWidth={fullWidth}
      disabled={disabled}
      size="small"
      slotProps={{ inputLabel: { shrink: true } }}
    />
  );
}

export function AmountField(
  props: CommonFieldProps & {
    value: string;
    onChange: (value: string) => void;
    currency?: string;
  },
) {
  const { label, helperText, error, required, fullWidth = true, value, onChange, currency = 'CDF', disabled } =
    props;
  return (
    <TextField
      label={label}
      value={value}
      onChange={(e) => onChange(e.target.value)}
      helperText={helperText}
      error={error}
      required={required}
      fullWidth={fullWidth}
      disabled={disabled}
      size="small"
      slotProps={{
        input: {
          endAdornment: <InputAdornment position="end">{currency}</InputAdornment>,
        },
      }}
    />
  );
}

export function SelectField({
  label,
  value,
  onChange,
  options,
  helperText,
  error,
  required,
  fullWidth = true,
  disabled,
  emptyLabel,
}: CommonFieldProps & {
  value: string;
  onChange: (value: string) => void;
  options: { value: string; label: string }[];
  emptyLabel?: string;
}) {
  return (
    <FormControl fullWidth={fullWidth} size="small" error={error} required={required} disabled={disabled}>
      <InputLabel>{label}</InputLabel>
      <Select
        label={label}
        value={value}
        onChange={(e: SelectChangeEvent) => onChange(e.target.value)}
      >
        {emptyLabel && (
          <MenuItem value="">
            <em>{emptyLabel}</em>
          </MenuItem>
        )}
        {options.map((opt) => (
          <MenuItem key={opt.value} value={opt.value}>
            {opt.label}
          </MenuItem>
        ))}
      </Select>
      {helperText && <FormHelperText>{helperText}</FormHelperText>}
    </FormControl>
  );
}

/** Searchable select shell — ready for API-backed options later. */
export function SearchableSelect({
  label,
  value,
  onChange,
  options,
  helperText,
  error,
  required,
  fullWidth = true,
  disabled,
  placeholder = 'Rechercher…',
}: CommonFieldProps & {
  value: string;
  onChange: (value: string) => void;
  options: { value: string; label: string }[];
  placeholder?: string;
}) {
  return (
    <TextField
      select
      label={label}
      value={value}
      onChange={(e) => onChange(e.target.value)}
      helperText={helperText ?? placeholder}
      error={error}
      required={required}
      fullWidth={fullWidth}
      disabled={disabled}
      size="small"
    >
      {options.map((opt) => (
        <MenuItem key={opt.value} value={opt.value}>
          {opt.label}
        </MenuItem>
      ))}
    </TextField>
  );
}

export function FormActions({ children }: { children: ReactNode }) {
  return (
    <FormControl
      component="div"
      sx={{
        mt: 2.5,
        display: 'flex',
        flexDirection: 'row',
        justifyContent: 'flex-end',
        gap: 1,
        width: '100%',
      }}
    >
      {children}
    </FormControl>
  );
}
