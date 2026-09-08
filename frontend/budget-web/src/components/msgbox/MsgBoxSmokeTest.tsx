import { Button, Paper, Stack, Typography } from '@mui/material';
import { useMsgBox } from './MsgBoxContext';

/**
 * Sonde DEV uniquement — valide MsgBox sans migrer les pages métier.
 * Affiché seulement si `import.meta.env.DEV`.
 */
export function MsgBoxSmokeTest() {
  const msgBox = useMsgBox();

  if (!import.meta.env.DEV) return null;

  return (
    <Paper
      variant="outlined"
      sx={{ p: 2, mb: 2, borderStyle: 'dashed', borderColor: 'warning.main' }}
    >
      <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
        Test MsgBox (DEV)
      </Typography>
      <Stack direction="row" useFlexGap sx={{ flexWrap: 'wrap', gap: 1 }}>
        <Button
          size="small"
          variant="outlined"
          color="success"
          onClick={() =>
            void msgBox.success(
              "L'enregistrement a été effectué avec succès.\nVous pouvez poursuivre.",
              { title: 'Enregistrement' },
            )
          }
        >
          Success
        </Button>
        <Button
          size="small"
          variant="outlined"
          color="error"
          onClick={() => void msgBox.error("Impossible d'effectuer l'enregistrement.")}
        >
          Error
        </Button>
        <Button
          size="small"
          variant="outlined"
          color="warning"
          onClick={() =>
            void msgBox.warning('Cette opération nécessite une attention particulière.')
          }
        >
          Warning
        </Button>
        <Button
          size="small"
          variant="outlined"
          color="info"
          onClick={() => void msgBox.info('Information complémentaire pour l’utilisateur.')}
        >
          Info
        </Button>
        <Button
          size="small"
          variant="contained"
          color="error"
          onClick={() => {
            void (async () => {
              const ok = await msgBox.confirm({
                title: 'Supprimer',
                message:
                  'Voulez-vous réellement supprimer cet élément ?\nCette action est définitive.',
                danger: true,
                confirmLabel: 'Supprimer',
              });
              if (ok) {
                await msgBox.success('Suppression confirmée (test).');
              } else {
                await msgBox.info('Suppression annulée (test).');
              }
            })();
          }}
        >
          Confirm (danger)
        </Button>
        <Button
          size="small"
          variant="outlined"
          onClick={() => {
            void msgBox.info('Message 1 — file d’attente');
            void msgBox.warning('Message 2 — file d’attente');
            void msgBox.success('Message 3 — file d’attente');
          }}
        >
          File (3 messages)
        </Button>
      </Stack>
    </Paper>
  );
}
