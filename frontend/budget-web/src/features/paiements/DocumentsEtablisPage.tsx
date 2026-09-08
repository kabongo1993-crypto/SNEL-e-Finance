import DescriptionOutlinedIcon from '@mui/icons-material/DescriptionOutlined';
import PictureAsPdfOutlinedIcon from '@mui/icons-material/PictureAsPdfOutlined';
import PrintOutlinedIcon from '@mui/icons-material/PrintOutlined';
import {
  Alert,
  Box,
  Button,
  MenuItem,
  Paper,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { useCallback, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  DataTable,
  ErrorState,
  FilterZone,
  LoadingState,
  PageHeader,
  SecondaryButton,
  useDocumentViewer,
  useMsgBox,
  type DataTableColumn,
  type DataTableAction,
} from '../../components';
import {
  buildBonProvisoirePdfViewerOptions,
  buildBilletConversionPdfViewerOptions,
  buildDocumentsEtablisListePdfViewerOptions,
  buildMinuteChequePdfViewerOptions,
  buildPieceCaissePdfViewerOptions,
} from '../../components/documentViewer';
import { useAuth } from '../auth';
import {
  fetchDocumentsEtablis,
  type DocumentEtabliListItem,
  type DocumentsEtablisQueryParams,
} from '../../services/apiClient';
import { DocumentsEtablisViewerDialog } from './DocumentsEtablisViewerDialog';
import { apiErrorMessage, canChargeDpm, formatDateFr, formatMontantDevise } from './paiementUtils';
import {
  defaultDocumentsEtablisPeriode,
  documentsEtablisRowId,
  documentsEtablisSelectionParam,
  TYPE_DOCUMENT_ETABLI_OPTIONS,
} from './documentsEtablisUtils';

type Row = DocumentEtabliListItem & { id: string };

function openDocumentViewerOptions(row: DocumentEtabliListItem) {
  switch (row.pdfRouteSegment) {
    case 'billet-conversion':
      return buildBilletConversionPdfViewerOptions(row.idDemandePaiement, row.reference);
    case 'piece-caisse':
      return buildPieceCaissePdfViewerOptions(row.idDemandePaiement);
    case 'bon-provisoire':
      return buildBonProvisoirePdfViewerOptions(row.idDemandePaiement);
    case 'minute-cheque':
      return buildMinuteChequePdfViewerOptions(row.idDemandePaiement);
    default:
      return null;
  }
}

export function DocumentsEtablisPage() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const msgBox = useMsgBox();
  const { openDocument } = useDocumentViewer();
  const defaults = useMemo(() => defaultDocumentsEtablisPeriode(), []);
  const [dateDebut, setDateDebut] = useState(defaults.dateDebut);
  const [dateFin, setDateFin] = useState(defaults.dateFin);
  const [typeDocument, setTypeDocument] = useState('');
  const [rows, setRows] = useState<Row[]>([]);
  const [selectedIds, setSelectedIds] = useState<string[]>([]);
  const [tableKey, setTableKey] = useState(0);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [loaded, setLoaded] = useState(false);
  const [viewerOpen, setViewerOpen] = useState(false);

  const query = useMemo<DocumentsEtablisQueryParams>(
    () => ({
      dateDebut,
      dateFin,
      typeDocument: typeDocument || undefined,
    }),
    [dateDebut, dateFin, typeDocument],
  );

  const targetRows = useMemo(() => {
    if (selectedIds.length === 0) return rows;
    const selected = new Set(selectedIds);
    return rows.filter((r) => selected.has(r.id));
  }, [rows, selectedIds]);

  const exportQuery = useMemo<DocumentsEtablisQueryParams>(
    () => ({
      ...query,
      selection:
        selectedIds.length > 0 ? documentsEtablisSelectionParam(targetRows) : undefined,
    }),
    [query, selectedIds.length, targetRows],
  );

  const load = useCallback(async () => {
    if (!dateDebut || !dateFin) {
      setError('Indiquez une période (date de début et date de fin).');
      return;
    }
    if (dateDebut > dateFin) {
      setError('La date de début doit précéder la date de fin.');
      return;
    }
    setLoading(true);
    setError(null);
    try {
      const list = await fetchDocumentsEtablis(query);
      setRows(
        list.map((r) => ({
          ...r,
          id: documentsEtablisRowId(r.idDemandePaiement, r.typeDocument, r.numeroDocument),
        })),
      );
      setSelectedIds([]);
      setTableKey((k) => k + 1);
      setLoaded(true);
    } catch (err) {
      setError(apiErrorMessage(err, 'Impossible de charger les documents établis.'));
    } finally {
      setLoading(false);
    }
  }, [dateDebut, dateFin, query]);

  const handlePrintListe = () => {
    if (targetRows.length === 0) {
      void msgBox.info('Aucun document à imprimer sur cette période.');
      return;
    }
    openDocument(buildDocumentsEtablisListePdfViewerOptions(exportQuery));
  };

  const handleShowDocuments = () => {
    if (targetRows.length === 0) {
      void msgBox.info('Aucun document à afficher. Cochez au moins une ligne, ou affichez d’abord la période.');
      return;
    }
    setViewerOpen(true);
  };

  const columns = useMemo<DataTableColumn<Row>[]>(
    () => [
      {
        id: 'reference',
        label: 'Référence',
        mobile: 'title',
        render: (r) => r.reference,
        sortValue: (r) => r.reference,
      },
      {
        id: 'type',
        label: 'Document',
        mobile: 'subtitle',
        render: (r) => r.libelleTypeDocument,
        sortValue: (r) => r.libelleTypeDocument,
      },
      {
        id: 'numero',
        label: 'N°',
        render: (r) => r.numeroDocument || '—',
      },
      {
        id: 'dateEtabli',
        label: 'Établi le',
        render: (r) => formatDateFr(r.dateEtabli),
        sortValue: (r) => r.dateEtabli,
      },
      {
        id: 'montant',
        label: 'Montant',
        align: 'right',
        render: (r) => formatMontantDevise(r.montant, r.devise),
        sortValue: (r) => r.montant,
      },
      {
        id: 'beneficiaire',
        label: 'Bénéficiaire',
        render: (r) => r.beneficiaireAffichage || '—',
      },
      {
        id: 'etabliPar',
        label: 'Établi par',
        render: (r) => r.nomUtilisateurEtabli || '—',
      },
    ],
    [],
  );

  const actions = useMemo<DataTableAction<Row>[]>(
    () => [
      {
        id: 'pdf',
        label: 'Ouvrir le PDF',
        icon: <PictureAsPdfOutlinedIcon fontSize="small" />,
        onClick: (row) => {
          const opts = openDocumentViewerOptions(row);
          if (opts) openDocument(opts);
        },
      },
      {
        id: 'dpm',
        label: 'Ouvrir la DPM',
        onClick: (row) => {
          navigate(`/paiements/charge-dpm/${row.idDemandePaiement}`);
        },
      },
    ],
    [openDocument, navigate],
  );

  if (!canChargeDpm(user)) {
    return (
      <Box sx={{ p: 2 }}>
        <Alert severity="warning">Accès réservé au profil Chargé DP.</Alert>
      </Box>
    );
  }

  const selectionLabel =
    selectedIds.length > 0
      ? `${selectedIds.length} document(s) sélectionné(s)`
      : rows.length > 0
        ? `Toute la liste (${rows.length})`
        : 'Aucun document';

  return (
    <Box sx={{ p: { xs: 1.5, md: 2 }, display: 'flex', flexDirection: 'column', gap: 2 }}>
      <PageHeader
        entityLabel="Documents établis"
        breadcrumbs={[
          { label: 'Paiements', to: '/paiements' },
          { label: 'Chargé DP', to: '/paiements/charge-dpm' },
          { label: 'Documents établis' },
        ]}
        actions={
          <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: 'wrap' }}>
            <Button
              variant="outlined"
              startIcon={<PrintOutlinedIcon />}
              onClick={handlePrintListe}
              disabled={!loaded || rows.length === 0}
            >
              Imprimer la liste
            </Button>
            <Button
              variant="contained"
              startIcon={<PictureAsPdfOutlinedIcon />}
              onClick={handleShowDocuments}
              disabled={!loaded || rows.length === 0}
            >
              Afficher / imprimer
            </Button>
          </Stack>
        }
      />

      <Paper variant="outlined" sx={{ p: 2 }}>
        <FilterZone
          columns={{ xs: 1, sm: 2, md: 4 }}
          actions={<SecondaryButton onClick={() => void load()}>Afficher</SecondaryButton>}
        >
          <TextField
            size="small"
            type="date"
            label="Du"
            value={dateDebut}
            onChange={(e) => setDateDebut(e.target.value)}
            slotProps={{ inputLabel: { shrink: true } }}
          />
          <TextField
            size="small"
            type="date"
            label="Au"
            value={dateFin}
            onChange={(e) => setDateFin(e.target.value)}
            slotProps={{ inputLabel: { shrink: true } }}
          />
          <TextField
            select
            size="small"
            label="Type de document"
            value={typeDocument}
            onChange={(e) => setTypeDocument(e.target.value)}
          >
            {TYPE_DOCUMENT_ETABLI_OPTIONS.map((opt) => (
              <MenuItem key={opt.value || 'all'} value={opt.value}>
                {opt.label}
              </MenuItem>
            ))}
          </TextField>
        </FilterZone>
      </Paper>

      {error && <ErrorState message={error} />}
      {loading && <LoadingState />}
      {!loading && loaded && (
        <>
          <Typography variant="body2" color="text.secondary">
            {rows.length} document(s) établi(s) du {formatDateFr(dateDebut)} au {formatDateFr(dateFin)}.
            Cochez les lignes à imprimer — {selectionLabel}.
          </Typography>
          <DataTable
            key={tableKey}
            columns={columns}
            rows={rows}
            actions={actions}
            selectable
            onSelectionChange={setSelectedIds}
            emptyTitle="Aucun document établi"
            emptyDescription="Aucun document établi sur cette période."
          />
        </>
      )}
      {!loading && !loaded && (
        <Alert severity="info" icon={<DescriptionOutlinedIcon />}>
          Choisissez une période puis cliquez sur Afficher. Cochez ensuite les documents à
          imprimer : ils s’ouvrent dans une fenêtre où vous pouvez imprimer ou télécharger.
        </Alert>
      )}
      <DocumentsEtablisViewerDialog
        open={viewerOpen}
        rows={targetRows}
        dateDebut={dateDebut}
        dateFin={dateFin}
        typeDocument={typeDocument || undefined}
        selection={exportQuery.selection}
        onClose={() => setViewerOpen(false)}
      />
    </Box>
  );
}
