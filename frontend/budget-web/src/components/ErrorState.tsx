import ErrorOutlinedIcon from '@mui/icons-material/ErrorOutlined';
import InfoOutlinedIcon from '@mui/icons-material/InfoOutlined';
import WarningAmberOutlinedIcon from '@mui/icons-material/WarningAmberOutlined';
import { Alert, AlertTitle, Button } from '@mui/material';

interface ErrorStateProps {
  title?: string;
  message: string;
  onRetry?: () => void;
  /** warning = alertes métier (ex. contexte incomplet) — ambre institutionnel */
  severity?: 'error' | 'warning' | 'info';
}

export function ErrorState({
  title = 'Une erreur est survenue',
  message,
  onRetry,
  severity = 'error',
}: ErrorStateProps) {
  const icon =
    severity === 'warning' ? (
      <WarningAmberOutlinedIcon />
    ) : severity === 'info' ? (
      <InfoOutlinedIcon />
    ) : (
      <ErrorOutlinedIcon />
    );

  return (
    <Alert
      severity={severity}
      icon={icon}
      action={
        onRetry ? (
          <Button color="inherit" size="small" onClick={onRetry}>
            Réessayer
          </Button>
        ) : undefined
      }
    >
      <AlertTitle>{title}</AlertTitle>
      {message}
    </Alert>
  );
}
