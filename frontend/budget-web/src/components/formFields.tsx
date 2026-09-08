import {
  FormControl,
  InputAdornment,
  TextField,
  type TextFieldProps,
  type SxProps,
  type Theme,
} from '@mui/material';
import type { ReactNode } from 'react';
import { SearchableSelect } from './SearchableSelect';
import type { FormFieldDensity } from './formTokens';
import { sanitizeNumericInput, type NumericInputOptions } from './numericInput';

type CommonFieldProps = {
  label: string;
  helperText?: string;
  error?: boolean;
  required?: boolean;
  /** Affiche « Optionnel » si helperText non fourni. */
  optional?: boolean;
  fullWidth?: boolean;
  width?: number;
  disabled?: boolean;
};

function resolveHelper(helperText: string | undefined, optional?: boolean, required?: boolean) {
  if (helperText) return helperText;
  if (optional && !required) return 'Optionnel';
  return undefined;
}

export function FormField({
  label,
  helperText,
  error,
  required,
  optional,
  fullWidth = true,
  ...rest
}: CommonFieldProps & Omit<TextFieldProps, 'label' | 'helperText' | 'error' | 'required' | 'fullWidth'>) {
  return (
    <TextField
      label={label}
      helperText={resolveHelper(helperText, optional, required)}
      error={error}
      required={required}
      fullWidth={fullWidth}
      size="small"
      slotProps={{ inputLabel: { shrink: true } }}
      {...rest}
    />
  );
}

export function DateField(
  props: CommonFieldProps & { value: string; onChange: (value: string) => void },
) {
  const { label, helperText, error, required, optional, fullWidth = true, value, onChange, disabled, width } =
    props;
  return (
    <TextField
      type="date"
      label={label}
      value={value}
      onChange={(e) => onChange(e.target.value)}
      helperText={resolveHelper(helperText, optional, required)}
      error={error}
      required={required}
      fullWidth={fullWidth}
      disabled={disabled}
      size="small"
      slotProps={{ inputLabel: { shrink: true } }}
      sx={width && !fullWidth ? { width, minWidth: width, flex: '0 0 auto' } : undefined}
    />
  );
}

/**
 * Champ monétaire / numérique — refuse les lettres à la saisie.
 * La validation métier (positif, cohérence…) reste à la charge de l'écran / du serveur.
 */
export function AmountField(
  props: CommonFieldProps & {
    value: string;
    onChange: (value: string) => void;
    /** Devise affichée en suffixe ; omise si null/undefined/''. */
    currency?: string | null;
    placeholder?: string;
    size?: 'small' | 'medium';
    sx?: SxProps<Theme>;
    allowNegative?: boolean;
    maxDecimals?: number;
    align?: 'left' | 'right';
    autoFocus?: boolean;
    onKeyDown?: TextFieldProps['onKeyDown'];
  },
) {
  const {
    label,
    helperText,
    error,
    required,
    optional,
    fullWidth = true,
    value,
    onChange,
    currency,
    disabled,
    placeholder,
    size = 'small',
    sx,
    allowNegative = false,
    maxDecimals = 8,
    align = 'left',
    autoFocus,
    onKeyDown,
  } = props;

  const numericOpts: NumericInputOptions = { allowNegative, maxDecimals };
  const showCurrency = Boolean(currency && currency.trim());

  return (
    <TextField
      label={label}
      value={value}
      onChange={(e) => onChange(sanitizeNumericInput(e.target.value, numericOpts))}
      onPaste={(e) => {
        e.preventDefault();
        const text = e.clipboardData.getData('text');
        onChange(sanitizeNumericInput(text, numericOpts));
      }}
      onKeyDown={onKeyDown}
      autoFocus={autoFocus}
      helperText={resolveHelper(helperText, optional, required)}
      error={error}
      required={required}
      fullWidth={fullWidth}
      disabled={disabled}
      size={size}
      placeholder={placeholder}
      sx={sx}
      slotProps={{
        inputLabel: { shrink: true },
        htmlInput: {
          inputMode: 'decimal',
          autoComplete: 'off',
          'aria-label': label,
          style: align === 'right' ? { textAlign: 'right', fontVariantNumeric: 'tabular-nums' } : undefined,
        },
        input: showCurrency
          ? {
              endAdornment: <InputAdornment position="end">{currency}</InputAdornment>,
            }
          : undefined,
      }}
    />
  );
}

/** Select filtrable (même comportement que l’UB Prévisions). */
export function SelectField({
  label,
  value,
  onChange,
  options,
  helperText,
  error,
  required,
  optional,
  fullWidth = true,
  width,
  density,
  disabled,
  emptyLabel,
  placeholder,
}: CommonFieldProps & {
  value: string;
  onChange: (value: string) => void;
  options: { value: string; label: string }[];
  width?: number;
  density?: FormFieldDensity;
  emptyLabel?: string;
  placeholder?: string;
}) {
  const opts = emptyLabel ? [{ value: '', label: emptyLabel }, ...options] : options;
  return (
    <SearchableSelect
      label={label}
      value={value}
      onChange={onChange}
      options={opts}
      helperText={helperText}
      error={error}
      required={required}
      optional={optional}
      fullWidth={fullWidth}
      width={width}
      density={density}
      disabled={disabled}
      allowEmpty={!!emptyLabel}
      emptyLabel={emptyLabel}
      placeholder={placeholder}
    />
  );
}

export { SearchableSelect } from './SearchableSelect';
export type { SearchableSelectOption, SearchableSelectProps } from './SearchableSelect';

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
        flexWrap: 'wrap',
      }}
    >
      {children}
    </FormControl>
  );
}
