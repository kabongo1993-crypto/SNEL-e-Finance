import { Stack, Typography } from '@mui/material';
import DownloadOutlinedIcon from '@mui/icons-material/DownloadOutlined';
import VisibilityOutlinedIcon from '@mui/icons-material/VisibilityOutlined';
import { GhostButton } from '../buttons';
import { useDocumentViewer, type DocumentViewerOpenOptions } from './DocumentViewerContext';
import { downloadBlob, ensureBlobMime } from './documentBlobUtils';
import { mimeTypeFromFileName } from './documentMime';

export interface DocumentActionsProps {
  title: string;
  fileName: string;
  mimeType?: string;
  fileSizeBytes?: number;
  loadPreview: () => Promise<Blob>;
  loadDownload?: () => Promise<Blob>;
  showFileName?: boolean;
  size?: 'small' | 'medium';
  disabled?: boolean;
}

export function DocumentActions({
  title,
  fileName,
  mimeType,
  fileSizeBytes,
  loadPreview,
  loadDownload,
  showFileName = true,
  size = 'small',
  disabled = false,
}: DocumentActionsProps) {
  const { openDocument } = useDocumentViewer();

  const buildOptions = (): DocumentViewerOpenOptions => ({
    title,
    fileName,
    mimeType: mimeType ?? mimeTypeFromFileName(fileName),
    fileSizeBytes,
    loadContent: loadPreview,
    loadDownload,
  });

  const handleView = () => openDocument(buildOptions());

  const handleDownload = async () => {
    const loader = loadDownload ?? loadPreview;
    const raw = await loader();
    downloadBlob(ensureBlobMime(raw, fileName, mimeType), fileName);
  };

  return (
    <Stack
      direction={{ xs: 'column', sm: 'row' }}
      spacing={0.5}
      useFlexGap
      sx={{
        flexWrap: 'wrap',
        alignItems: { xs: 'flex-start', sm: 'center' },
      }}
    >
      {showFileName && (
        <Typography variant="body2" sx={{ wordBreak: 'break-all', mr: { sm: 1 } }}>
          {fileName}
        </Typography>
      )}
      <Stack direction="row" spacing={0.5} useFlexGap>
        <GhostButton
          size={size}
          disabled={disabled}
          startIcon={<VisibilityOutlinedIcon fontSize="small" />}
          onClick={handleView}
        >
          Afficher
        </GhostButton>
        <GhostButton
          size={size}
          disabled={disabled}
          startIcon={<DownloadOutlinedIcon fontSize="small" />}
          onClick={() => void handleDownload().catch(() => undefined)}
        >
          Télécharger
        </GhostButton>
      </Stack>
    </Stack>
  );
}
