import ErrorOutlinedIcon from '@mui/icons-material/ErrorOutlined';
import { Alert, AlertTitle, Button } from '@mui/material';

interface ErrorStateProps {
  title?: string;
  message: string;
  onRetry?: () => void;
}

export function ErrorState({
  title = 'Une erreur est survenue',
  message,
  onRetry,
}: ErrorStateProps) {
  return (
    <Alert
      severity="error"
      icon={<ErrorOutlinedIcon />}
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
