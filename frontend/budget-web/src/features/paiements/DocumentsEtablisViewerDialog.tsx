import CloseIcon from '@mui/icons-material/Close';
import DownloadOutlinedIcon from '@mui/icons-material/DownloadOutlined';
import PrintOutlinedIcon from '@mui/icons-material/PrintOutlined';
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  IconButton,
  List,
  ListItemButton,
  ListItemText,
  Stack,
  Typography,
} from '@mui/material';
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  documentLoadErrorMessage,
  downloadBlob,
  ensureBlobMime,
} from '../../components/documentViewer';
import { printFromIframe } from '../../components/documentViewer/documentBlobUtils';
import { downloadDocumentsEtablisDocumentsPdf, type DocumentEtabliListItem } from '../../services/apiClient';
import { formatDateFr, formatMontantDevise } from './paiementUtils';

interface DocumentsEtablisViewerDialogProps {
  open: boolean;
  rows: DocumentEtabliListItem[];
  dateDebut: string;
  dateFin: string;
  typeDocument?: string;
  selection?: string;
  onClose: () => void;
}

export function DocumentsEtablisViewerDialog({
  open,
  rows,
  dateDebut,
  dateFin,
  typeDocument,
  selection,
  onClose,
}: DocumentsEtablisViewerDialogProps) {
  const [loading, setLoading] = useState(false);
  const [busy, setBusy] = useState<'print' | 'download' | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [blob, setBlob] = useState<Blob | null>(null);
  const [objectUrl, setObjectUrl] = useState<string | null>(null);
  const [activeIndex, setActiveIndex] = useState(0);
  const iframeRef = useRef<HTMLIFrameElement>(null);

  const fileName = `documents-etablis-${dateDebut}-${dateFin}.pdf`;
  const title =
    rows.length <= 1 ? 'Document établi' : `${rows.length} documents établis`;

  const params = useMemo(
    () => ({
      dateDebut,
      dateFin,
      typeDocument: typeDocument || undefined,
      selection: selection || undefined,
    }),
    [dateDebut, dateFin, typeDocument, selection],
  );

  useEffect(() => {
    if (!open) {
      setBlob(null);
      setError(null);
      setLoading(false);
      setActiveIndex(0);
      return;
    }

    let cancelled = false;
    setLoading(true);
    setError(null);
    setBlob(null);
    setActiveIndex(0);

    void downloadDocumentsEtablisDocumentsPdf(params)
      .then((raw) => {
        if (cancelled) return;
        setBlob(ensureBlobMime(raw, fileName, 'application/pdf'));
      })
      .catch((err) => {
        if (!cancelled) setError(documentLoadErrorMessage(err));
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [open, params, fileName]);

  useEffect(() => {
    if (!blob) {
      setObjectUrl(null);
      return;
    }
    const url = URL.createObjectURL(blob);
    setObjectUrl(url);
    return () => URL.revokeObjectURL(url);
  }, [blob]);

  const handleDownload = useCallback(() => {
    if (!blob) return;
    setBusy('download');
    try {
      downloadBlob(blob, fileName);
    } finally {
      setBusy(null);
    }
  }, [blob, fileName]);

  const handlePrint = useCallback(() => {
    if (!blob) return;
    setBusy('print');
    try {
      printFromIframe(iframeRef.current);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Impression impossible.');
    } finally {
      setBusy(null);
    }
  }, [blob]);

  return (
    <Dialog
      open={open}
      onClose={onClose}
      fullWidth
      maxWidth="lg"
      aria-labelledby="documents-etablis-viewer-title"
      slotProps={{
        paper: {
          sx: {
            height: { xs: '92vh', md: '88vh' },
            maxHeight: '92vh',
            display: 'flex',
            flexDirection: 'column',
          },
        },
      }}
    >
      <DialogTitle
        id="documents-etablis-viewer-title"
        sx={{ display: 'flex', alignItems: 'flex-start', gap: 1, pr: 1 }}
      >
        <Box sx={{ flex: 1, minWidth: 0 }}>
          <Typography variant="h6" component="span">
            {title}
          </Typography>
          <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 0.5 }}>
            Du {formatDateFr(dateDebut)} au {formatDateFr(dateFin)} · aperçu puis imprimer ou
            télécharger
          </Typography>
        </Box>
        <IconButton aria-label="Fermer" onClick={onClose} edge="end">
          <CloseIcon />
        </IconButton>
      </DialogTitle>

      <DialogContent
        dividers
        sx={{
          flex: 1,
          p: 0,
          display: 'flex',
          flexDirection: { xs: 'column', md: 'row' },
          overflow: 'hidden',
          minHeight: 0,
        }}
      >
        <Box
          sx={{
            width: { xs: '100%', md: 280 },
            maxHeight: { xs: 180, md: 'none' },
            borderRight: { md: 1 },
            borderBottom: { xs: 1, md: 0 },
            borderColor: 'divider',
            overflow: 'auto',
            bgcolor: 'background.paper',
            flexShrink: 0,
          }}
        >
          <Typography variant="caption" color="text.secondary" sx={{ display: 'block', px: 1.5, pt: 1.25, pb: 0.5 }}>
            Tous ces documents sont dans l&apos;aperçu.
          </Typography>
          <List dense disablePadding>
            {rows.map((row, index) => (
              <ListItemButton
                key={`${row.idDemandePaiement}-${row.typeDocument}-${row.numeroDocument}-${index}`}
                selected={index === activeIndex}
                onClick={() => setActiveIndex(index)}
              >
                <ListItemText
                  primary={row.reference}
                  secondary={`${row.libelleTypeDocument}${row.numeroDocument ? ` · ${row.numeroDocument}` : ''} · ${formatMontantDevise(row.montant, row.devise)}`}
                  slotProps={{
                    primary: { variant: 'body2', noWrap: true },
                    secondary: { variant: 'caption' },
                  }}
                />
              </ListItemButton>
            ))}
          </List>
        </Box>

        <Box sx={{ flex: 1, minWidth: 0, display: 'flex', flexDirection: 'column', bgcolor: 'action.hover' }}>
          {loading && (
            <Stack sx={{ flex: 1, py: 6, alignItems: 'center', justifyContent: 'center' }} spacing={2}>
              <CircularProgress size={36} />
              <Typography color="text.secondary">Chargement des documents…</Typography>
            </Stack>
          )}
          {!loading && error && (
            <Stack sx={{ flex: 1, p: 3, alignItems: 'center', justifyContent: 'center' }}>
              <Alert severity="error" sx={{ maxWidth: 480, width: '100%' }}>
                {error}
              </Alert>
            </Stack>
          )}
          {!loading && !error && objectUrl && (
            <Box
              component="iframe"
              ref={iframeRef}
              src={objectUrl}
              title={fileName}
              sx={{ flex: 1, width: '100%', border: 0, bgcolor: 'background.paper' }}
            />
          )}
        </Box>
      </DialogContent>

      <DialogActions sx={{ px: 2, py: 1.5, flexWrap: 'wrap', gap: 1 }}>
        <Button onClick={onClose}>Fermer</Button>
        <Box sx={{ flex: 1 }} />
        <Button
          startIcon={busy === 'print' ? <CircularProgress size={14} /> : <PrintOutlinedIcon />}
          onClick={handlePrint}
          disabled={loading || !!error || !blob || busy !== null}
        >
          Imprimer
        </Button>
        <Button
          variant="contained"
          startIcon={
            busy === 'download' ? <CircularProgress size={14} color="inherit" /> : <DownloadOutlinedIcon />
          }
          onClick={handleDownload}
          disabled={loading || !blob || busy !== null}
        >
          Télécharger
        </Button>
      </DialogActions>
    </Dialog>
  );
}
