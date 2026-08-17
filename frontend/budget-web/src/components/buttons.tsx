import { Button, type ButtonProps } from '@mui/material';

type Props = Omit<ButtonProps, 'variant' | 'color'>;

export function PrimaryButton(props: Props) {
  return <Button variant="contained" color="primary" {...props} />;
}

export function SecondaryButton(props: Props) {
  return <Button variant="outlined" color="primary" {...props} />;
}

export function GhostButton(props: Props) {
  return <Button variant="text" color="inherit" {...props} />;
}

export function DangerButton(props: Props) {
  return <Button variant="contained" color="error" {...props} />;
}
