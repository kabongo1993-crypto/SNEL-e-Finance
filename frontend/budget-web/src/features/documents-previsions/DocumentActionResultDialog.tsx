import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Stack,
  Typography,
} from '@mui/material';
import CheckCircleOutlinedIcon from '@mui/icons-material/CheckCircleOutlined';
import { useCallback, useState } from 'react';
import {
  downloadDocumentPrevisionPdf,
  type DocumentPrevision,
} from '../../services/apiClient';
import { useDocumentViewer, buildDocumentPrevisionPdfViewerOptions, downloadBlob, printBlob } from '../../components';
import {
  documentActionTitle,
  documentViewLabel,
  formatDocumentShortRef,
} from './documentUtils';

export interface DocumentActionResultState {
  document: DocumentPrevision;
  /** Message court type « Prévision soumise » */
  headline: string;
}

interface DocumentActionResultDialogProps {
  open: boolean;
  state: DocumentActionResultState | null;
  onClose: () => void;
}

export function DocumentActionResultDialog({ open, state, onClose }: DocumentActionResultDialogProps) {
  const [busy, setBusy] = useState<'view' | 'print' | 'pdf' | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const { openDocument } = useDocumentViewer();

  const run = useCallback(
    async (mode: 'view' | 'print' | 'pdf') => {
      if (!state?.document) return;
      setBusy(mode);
      setActionError(null);
      try {
        if (mode === 'view') {
          openDocument(buildDocumentPrevisionPdfViewerOptions(state.document));
          return;
        }
        const blob = await downloadDocumentPrevisionPdf(state.document.idDocument);
        if (mode === 'print') {
          printBlob(blob, 'pdf');
        } else {
          downloadBlob(blob, `${state.document.reference.replace(/\//g, '_')}.pdf`);
        }
      } catch {
        setActionError('Impossible d’accéder au PDF du document.');
      } finally {
        setBusy(null);
      }
    },
    [state, openDocument],
  );

  const doc = state?.document;
  const isDept = doc?.portee?.toUpperCase() === 'DEPARTEMENT';

  return (
    <Dialog open={open && !!state} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
        <CheckCircleOutlinedIcon color="success" />
        {state?.headline ?? 'Action réussie'}
      </DialogTitle>
      <DialogContent dividers>
        {doc && (
          <Stack spacing={1.5}>
            <Box>
              <Typography variant="caption" color="text.secondary" sx={{ display: 'block' }}>
                Référence
              </Typography>
              <Typography
                variant="body1"
                sx={{ fontFamily: 'ui-monospace, monospace', fontWeight: 700, wordBreak: 'break-all' }}
              >
                {doc.reference}
              </Typography>
              <Typography variant="caption" color="text.secondary">
                {formatDocumentShortRef(doc.reference)} · {doc.titre}
              </Typography>
            </Box>

            {isDept && (
              <Alert severity="info" variant="outlined">
                <Typography variant="body2" sx={{ fontWeight: 700 }}>
                  PORTÉE : DÉPARTEMENT
                </Typography>
                <Typography variant="body2">
                  {doc.libelleDepartement} ({doc.codeDepartement}) — {doc.nbUbConcernees} UB concernée
                  {doc.nbUbConcernees > 1 ? 's' : ''}
                </Typography>
              </Alert>
            )}

            {doc.typeDocument === 'REJ' && doc.motif?.trim() && (
              <Alert severity="warning" variant="outlined">
                <Typography variant="caption" color="text.secondary">
                  Motif
                </Typography>
                <Typography variant="body2">{doc.motif}</Typography>
              </Alert>
            )}

            {actionError && <Alert severity="error">{actionError}</Alert>}
          </Stack>
        )}
      </DialogContent>
      <DialogActions sx={{ px: 3, py: 2, flexWrap: 'wrap', gap: 1 }}>
        <Button onClick={onClose} disabled={!!busy}>
          Fermer
        </Button>
        <Box sx={{ flex: 1 }} />
        <Button
          variant="outlined"
          disabled={!!busy || !doc}
          onClick={() => void run('view')}
          startIcon={busy === 'view' ? <CircularProgress size={14} /> : undefined}
        >
          {doc ? documentViewLabel(doc.typeDocument) : 'Voir le document'}
        </Button>
        <Button
          variant="outlined"
          disabled={!!busy || !doc}
          onClick={() => void run('print')}
          startIcon={busy === 'print' ? <CircularProgress size={14} /> : undefined}
        >
          Imprimer
        </Button>
        <Button
          variant="contained"
          disabled={!!busy || !doc}
          onClick={() => void run('pdf')}
          startIcon={busy === 'pdf' ? <CircularProgress size={14} /> : undefined}
        >
          PDF
        </Button>
      </DialogActions>
    </Dialog>
  );
}

/** Construit l’état de dialogue à partir d’un document API (ou null si absent). */
export function buildDocumentActionResult(
  document: DocumentPrevision | null | undefined,
  kind: 'SUB' | 'REJ' | 'CTL' | 'VAL',
): DocumentActionResultState | null {
  if (!document) return null;
  return {
    document,
    headline: documentActionTitle(kind),
  };
}
