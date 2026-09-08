import { useCallback, useEffect, useRef, useState } from 'react';
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
  Stack,
  Typography,
} from '@mui/material';
import CloseIcon from '@mui/icons-material/Close';
import DownloadOutlinedIcon from '@mui/icons-material/DownloadOutlined';
import OpenInNewOutlinedIcon from '@mui/icons-material/OpenInNewOutlined';
import PrintOutlinedIcon from '@mui/icons-material/PrintOutlined';
import type { DocumentViewerOpenOptions } from './DocumentViewerContext';
import {
  documentLoadErrorMessage,
  downloadBlob,
  ensureBlobMime,
  printBlob,
  printFromIframe,
} from './documentBlobUtils';
import {
  formatFileSize,
  mimeTypeFromFileName,
  mimeTypeLabel,
  resolvePreviewKind,
} from './documentMime';
import { logDocumentViewerPerf } from './documentViewerPerf';

interface DocumentViewerDialogProps {
  open: boolean;
  options: DocumentViewerOpenOptions | null;
  onClose: () => void;
}

export function DocumentViewerDialog({ open, options, onClose }: DocumentViewerDialogProps) {
  const [loading, setLoading] = useState(false);
  const [busy, setBusy] = useState<'print' | 'download' | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [blob, setBlob] = useState<Blob | null>(null);
  const [objectUrl, setObjectUrl] = useState<string | null>(null);
  const iframeRef = useRef<HTMLIFrameElement>(null);
  const perfClickMsRef = useRef<number | null>(null);
  const perfDownloadMsRef = useRef<number | null>(null);

  const fileName = options?.fileName ?? 'document';
  const mimeType = options?.mimeType ?? mimeTypeFromFileName(fileName);
  const previewKind = resolvePreviewKind(fileName, mimeType);
  const canPreview = previewKind !== 'unsupported';

  useEffect(() => {
    if (!open || !options) {
      setBlob(null);
      setError(null);
      setLoading(false);
      return;
    }

    let cancelled = false;
    const clickMs = performance.now();
    perfClickMsRef.current = clickMs;
    perfDownloadMsRef.current = null;
    setLoading(true);
    setError(null);
    setBlob(null);

    const apiStart = performance.now();
    void options
      .loadContent()
      .then((raw) => {
        if (cancelled) return;
        const blobReceivedMs = performance.now();
        perfDownloadMsRef.current = blobReceivedMs - clickMs;
        const normalized = ensureBlobMime(raw, fileName, mimeType);
        const blobProcessingMs = Math.round(performance.now() - blobReceivedMs);
        if (options.perfLabel) {
          logDocumentViewerPerf(options.perfLabel, {
            clickMs,
            apiRequestMs: Math.round(blobReceivedMs - apiStart),
            downloadMs: Math.round(blobReceivedMs - clickMs),
            blobProcessingMs,
            blobSizeBytes: normalized.size,
          });
        }
        setBlob(normalized);
      })
      .catch((err) => {
        if (cancelled) return;
        setError(documentLoadErrorMessage(err));
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [open, options, fileName, mimeType]);

  useEffect(() => {
    if (!blob) {
      setObjectUrl(null);
      return;
    }
    const url = URL.createObjectURL(blob);
    setObjectUrl(url);
    if (options?.perfLabel && perfClickMsRef.current !== null) {
      logDocumentViewerPerf(options.perfLabel, {
        clickMs: perfClickMsRef.current,
        downloadMs: perfDownloadMsRef.current ?? undefined,
        createObjectUrlMs: performance.now(),
        blobSizeBytes: blob.size,
      });
    }
    return () => URL.revokeObjectURL(url);
  }, [blob, options?.perfLabel]);

  const handleDownload = useCallback(async () => {
    if (!options) return;
    setBusy('download');
    try {
      const raw = options.loadDownload
        ? await options.loadDownload()
        : blob ?? (await options.loadContent());
      downloadBlob(ensureBlobMime(raw, fileName, mimeType), fileName);
    } catch (err) {
      setError(documentLoadErrorMessage(err));
    } finally {
      setBusy(null);
    }
  }, [options, blob, fileName, mimeType]);

  const handlePrint = useCallback(async () => {
    if (!blob || previewKind === 'unsupported') return;
    setBusy('print');
    try {
      if (previewKind === 'pdf' && iframeRef.current) {
        printFromIframe(iframeRef.current);
      } else {
        printBlob(blob, previewKind);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Impression impossible.');
    } finally {
      setBusy(null);
    }
  }, [blob, previewKind]);

  const handleOpenNewTab = useCallback(() => {
    if (!objectUrl) return;
    window.open(objectUrl, '_blank', 'noopener,noreferrer');
  }, [objectUrl]);

  const sizeLabel = formatFileSize(options?.fileSizeBytes);
  const typeLabel = mimeTypeLabel(mimeType);

  return (
    <Dialog
      open={open}
      onClose={onClose}
      fullWidth
      maxWidth="lg"
      aria-labelledby="document-viewer-title"
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
        id="document-viewer-title"
        sx={{ display: 'flex', alignItems: 'flex-start', gap: 1, pr: 1 }}
      >
        <Box sx={{ flex: 1, minWidth: 0 }}>
          <Typography variant="h6" component="span" sx={{ wordBreak: 'break-word' }}>
            {options?.title ?? 'Document'}
          </Typography>
          <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 0.5 }}>
            {fileName}
            {sizeLabel ? ` · ${sizeLabel}` : ''} · {typeLabel}
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
          flexDirection: 'column',
          overflow: 'hidden',
          bgcolor: 'action.hover',
        }}
      >
        {loading && (
          <Stack sx={{ flex: 1, py: 6, alignItems: 'center', justifyContent: 'center' }} spacing={2}>
            <CircularProgress size={36} />
            <Typography color="text.secondary">Chargement du document…</Typography>
          </Stack>
        )}

        {!loading && error && (
          <Stack sx={{ flex: 1, p: 3, alignItems: 'center', justifyContent: 'center' }} spacing={2}>
            <Alert severity="error" sx={{ maxWidth: 480, width: '100%' }}>
              {error}
            </Alert>
            {options && (
              <Button
                variant="contained"
                startIcon={<DownloadOutlinedIcon />}
                onClick={() => void handleDownload()}
                disabled={busy === 'download'}
              >
                Télécharger le document
              </Button>
            )}
          </Stack>
        )}

        {!loading && !error && blob && canPreview && objectUrl && previewKind === 'pdf' && (
          <Box
            component="iframe"
            ref={iframeRef}
            src={objectUrl}
            title={fileName}
            onLoad={() => {
              if (!options?.perfLabel || perfClickMsRef.current === null) return;
              logDocumentViewerPerf(options.perfLabel, {
                clickMs: perfClickMsRef.current,
                downloadMs: perfDownloadMsRef.current ?? undefined,
                iframeLoadMs: performance.now(),
                blobSizeBytes: blob.size,
              });
            }}
            sx={{
              flex: 1,
              width: '100%',
              border: 0,
              bgcolor: 'background.paper',
            }}
          />
        )}

        {!loading && !error && blob && canPreview && objectUrl && previewKind === 'image' && (
          <Box
            sx={{
              flex: 1,
              overflow: 'auto',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              p: 2,
            }}
          >
            <Box
              component="img"
              src={objectUrl}
              alt={fileName}
              sx={{
                maxWidth: '100%',
                maxHeight: '100%',
                objectFit: 'contain',
                bgcolor: 'background.paper',
                boxShadow: 1,
              }}
            />
          </Box>
        )}

        {!loading && !error && blob && !canPreview && (
          <Stack sx={{ flex: 1, p: 3, alignItems: 'center', justifyContent: 'center' }} spacing={2}>
            <Alert severity="info" sx={{ maxWidth: 520, width: '100%' }}>
              Aperçu indisponible pour ce type de document.
            </Alert>
            <Typography variant="body2" color="text.secondary" sx={{ textAlign: 'center' }}>
              Les documents Office et certains formats ne peuvent pas être prévisualisés dans le navigateur.
            </Typography>
            <Button
              variant="contained"
              startIcon={<DownloadOutlinedIcon />}
              onClick={() => void handleDownload()}
              disabled={busy === 'download'}
            >
              Télécharger le document
            </Button>
          </Stack>
        )}
      </DialogContent>

      <DialogActions sx={{ px: 2, py: 1.5, flexWrap: 'wrap', gap: 1 }}>
        <Button onClick={onClose}>Fermer</Button>
        <Box sx={{ flex: 1 }} />
        {canPreview && objectUrl && (
          <Button
            startIcon={<OpenInNewOutlinedIcon />}
            onClick={handleOpenNewTab}
            disabled={loading || !!error}
          >
            Nouvel onglet
          </Button>
        )}
        {canPreview && (
          <Button
            startIcon={busy === 'print' ? <CircularProgress size={14} /> : <PrintOutlinedIcon />}
            onClick={() => void handlePrint()}
            disabled={loading || !!error || !blob || busy !== null}
          >
            Imprimer
          </Button>
        )}
        <Button
          variant="contained"
          startIcon={
            busy === 'download' ? <CircularProgress size={14} color="inherit" /> : <DownloadOutlinedIcon />
          }
          onClick={() => void handleDownload()}
          disabled={loading || busy !== null}
        >
          Télécharger
        </Button>
      </DialogActions>
    </Dialog>
  );
}
