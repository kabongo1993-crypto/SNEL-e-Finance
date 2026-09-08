import { Box } from '@mui/material';
import { memo, useEffect, useState, type CSSProperties, type FocusEvent } from 'react';
import { sanitizeNumericInput } from '../../components/numericInput';

interface PrevisionMontantInputProps {
  value: number;
  disabled?: boolean;
  onCommit: (raw: string) => void;
  width?: number | string;
  /** Cible tactile élargie — vue mobile de la grille. */
  large?: boolean;
  'aria-label'?: string;
}

/** Cellule monétaire légère (input natif) — évite le coût MUI TextField × N mois. */
export const PrevisionMontantInput = memo(function PrevisionMontantInput({
  value,
  disabled,
  onCommit,
  width = 78,
  large,
  'aria-label': ariaLabel,
}: PrevisionMontantInputProps) {
  const [draft, setDraft] = useState(value ? String(value) : '');

  useEffect(() => {
    setDraft(value ? String(value) : '');
  }, [value]);

  const style: CSSProperties = {
    width,
    textAlign: 'right',
    padding: large ? '10px 12px' : '4px 6px',
    border: '1px solid var(--ef-border)',
    borderRadius: 8,
    background: 'var(--ef-surface)',
    fontSize: large ? '0.9375rem' : '0.8125rem',
    fontFamily: 'inherit',
    color: 'var(--ef-text)',
    outline: 'none',
  };

  if (disabled) {
    return (
      <Box component="span" sx={{ display: 'inline-block', minWidth: width, textAlign: 'right', fontSize: '0.8125rem' }}>
        {value ? value.toLocaleString('fr-FR') : '—'}
      </Box>
    );
  }

  const handleFocus = (e: FocusEvent<HTMLInputElement>) => {
    e.target.style.borderColor = 'var(--ef-primary)';
    e.target.style.boxShadow = '0 0 0 3px rgba(6, 67, 155, 0.12)';
    e.target.select();
  };

  const handleBlur = (e: FocusEvent<HTMLInputElement>) => {
    e.target.style.borderColor = 'var(--ef-border)';
    e.target.style.boxShadow = 'none';
    onCommit(draft);
  };

  return (
    <input
      aria-label={ariaLabel}
      inputMode="decimal"
      value={draft}
      style={style}
      onChange={(e) => setDraft(sanitizeNumericInput(e.target.value))}
      onPaste={(e) => {
        e.preventDefault();
        setDraft(sanitizeNumericInput(e.clipboardData.getData('text')));
      }}
      onFocus={handleFocus}
      onBlur={handleBlur}
      onKeyDown={(e) => {
        if (e.key === 'Enter') {
          (e.target as HTMLInputElement).blur();
        }
      }}
    />
  );
});
