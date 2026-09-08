import FileUploadOutlinedIcon from '@mui/icons-material/FileUploadOutlined';
import {
  Alert,
  Box,
  Chip,
  Grid,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Typography,
} from '@mui/material';
import { useCallback, useState } from 'react';
import {
  ErrorState,
  LoadingState,
  PageHeader,
  PrimaryButton,
  SecondaryButton,
  StatCard,
  useMsgBox,
} from '../../components';
import {
  executeImportSnelComptes,
  previewImportSnelComptes,
  type SnelComptesNoeudPreview,
  type SnelComptesPreview,
} from '../../services/apiClient';
import { BRAND_NAME } from '../../theme';
import { RUBRIQUE_RUPTURE, resolveRubriqueBreakLevel, rubriqueRuptureRowSx } from '../rubriques-budgetaires/hierarchy';
import { apiErrorMessage } from '../rubriques-budgetaires/rubriqueUtils';

function statutColor(statut: string): 'default' | 'success' | 'warning' | 'error' {
  if (statut === 'A_CREER') return 'success';
  if (statut === 'DEJA_EXISTANT') return 'default';
  if (statut === 'CONFLIT') return 'error';
  return 'warning';
}

function PreviewTree({ nodes, styleRuptures = false }: { nodes: SnelComptesNoeudPreview[]; styleRuptures?: boolean }) {
  if (nodes.length === 0) {
    return (
      <Typography variant="body2" color="text.secondary">
        Aucun nœud.
      </Typography>
    );
  }

  const render = (list: SnelComptesNoeudPreview[], depth: number) =>
    list.map((node) => {
      const isRupture = styleRuptures && node.enfants.length > 0;
      const level = resolveRubriqueBreakLevel(depth, depth === 0 ? null : 1);
      return (
        <Box key={`${node.code}-${depth}`}>
          <Stack
            direction="row"
            spacing={1}
            sx={{
              alignItems: 'center',
              pl: 1 + depth * 1.75,
              py: isRupture ? 0.55 : 0.25,
              flexWrap: 'wrap',
              borderRadius: 1,
              ...(isRupture ? rubriqueRuptureRowSx(level) : null),
            }}
          >
            <Typography
              sx={{
                fontFamily: 'ui-monospace, monospace',
                fontWeight: 700,
                fontSize: '0.8rem',
                color: isRupture ? RUBRIQUE_RUPTURE.color : undefined,
              }}
            >
              {node.code}
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: isRupture ? 700 : 400 }}>
              {node.libelle}
            </Typography>
            {node.categorie && (
              <Typography variant="caption" color="text.secondary">
                {node.categorie}
              </Typography>
            )}
            <Chip size="small" label={node.statut} color={statutColor(node.statut)} />
          </Stack>
          {render(node.enfants, depth + 1)}
        </Box>
      );
    });

  return <Box>{render(nodes, 0)}</Box>;
}

export function SnelComptesImportPage() {
  const msgBox = useMsgBox();
  const [loading, setLoading] = useState(false);
  const [executing, setExecuting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [preview, setPreview] = useState<SnelComptesPreview | null>(null);

  const loadPreview = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await previewImportSnelComptes();
      setPreview(data);
    } catch (err) {
      setPreview(null);
      setError(apiErrorMessage(err, "Impossible de construire l'aperçu d'import."));
    } finally {
      setLoading(false);
    }
  }, []);

  const confirmImport = async () => {
    const ok = await msgBox.confirm({
      title: 'Confirmer l’import SYSCOHADA',
      message: preview
        ? `Insérer ${preview.rubriques.aCreer} rubriques et ${preview.itemsBI.aCreer} Items BI. Aucune donnée existante ne sera modifiée, ni les 7 produits.`
        : 'Confirmer l’import ?',
      confirmLabel: 'Importer',
    });
    if (!ok) return;

    setExecuting(true);
    try {
      const data = await executeImportSnelComptes(true);
      if (!data.succes) {
        void msgBox.error(data.message);
      } else {
        void msgBox.success(
          `${data.message} RB insérées : ${data.rubriquesInserees}. Items BI insérés : ${data.itemsBIInserees}. Lignes ignorées : ${data.lignesIgnorees}.`,
        );
        setPreview(await previewImportSnelComptes());
      }
    } catch (err) {
      void msgBox.error(apiErrorMessage(err, "L'import a échoué."));
    } finally {
      setExecuting(false);
    }
  };

  return (
    <Box>
      <PageHeader
        title="Import SYSCOHADA"
        subtitle="Aperçu obligatoire puis confirmation pour importer les 64 rubriques de charges et les 11 Items BI. Les 7 produits restent en attente de décision métier."
        breadcrumbs={[
          { label: BRAND_NAME, to: '/dashboard' },
          { label: 'Budget', to: '/budget' },
          { label: 'Import SYSCOHADA' },
        ]}
        actions={
          <Stack direction="row" spacing={1}>
            <SecondaryButton startIcon={<FileUploadOutlinedIcon />} onClick={() => void loadPreview()} disabled={loading}>
              Charger l’aperçu
            </SecondaryButton>
            <PrimaryButton
              onClick={() => void confirmImport()}
              disabled={!preview?.peutImporter || executing}
            >
              Confirmer l’import
            </PrimaryButton>
          </Stack>
        }
      />

      {error && (
        <ErrorState title="Import" message={error} onRetry={() => void loadPreview()} />
      )}

      {loading && <LoadingState label="Analyse du fichier Excel…" />}

      {preview && !loading && (
        <Stack spacing={2.5} sx={{ mt: 1 }}>
          <Alert severity={preview.peutImporter ? 'info' : 'warning'}>{preview.message}</Alert>
          <Typography variant="caption" color="text.secondary">
            Fichier : {preview.fichierSource} — feuille {preview.feuille}
          </Typography>

          <Typography variant="h6">RB</Typography>
          <Grid container spacing={1.5}>
            <Grid size={{ xs: 6, md: 2.4 }}>
              <StatCard title="Total détecté" value={String(preview.rubriques.totalDetecte)} />
            </Grid>
            <Grid size={{ xs: 6, md: 2.4 }}>
              <StatCard title="À créer" value={String(preview.rubriques.aCreer)} />
            </Grid>
            <Grid size={{ xs: 6, md: 2.4 }}>
              <StatCard title="Déjà existant" value={String(preview.rubriques.dejaExistant)} />
            </Grid>
            <Grid size={{ xs: 6, md: 2.4 }}>
              <StatCard title="En conflit" value={String(preview.rubriques.enConflit)} />
            </Grid>
            <Grid size={{ xs: 6, md: 2.4 }}>
              <StatCard title="Anomalies" value={String(preview.rubriques.anomalies)} />
            </Grid>
          </Grid>

          <Typography variant="h6">Items BI</Typography>
          <Grid container spacing={1.5}>
            <Grid size={{ xs: 6, md: 2.4 }}>
              <StatCard title="Total détecté" value={String(preview.itemsBI.totalDetecte)} />
            </Grid>
            <Grid size={{ xs: 6, md: 2.4 }}>
              <StatCard title="À créer" value={String(preview.itemsBI.aCreer)} />
            </Grid>
            <Grid size={{ xs: 6, md: 2.4 }}>
              <StatCard title="Déjà existant" value={String(preview.itemsBI.dejaExistant)} />
            </Grid>
            <Grid size={{ xs: 6, md: 2.4 }}>
              <StatCard title="En conflit" value={String(preview.itemsBI.enConflit)} />
            </Grid>
            <Grid size={{ xs: 6, md: 2.4 }}>
              <StatCard title="Anomalies" value={String(preview.itemsBI.anomalies)} />
            </Grid>
          </Grid>

          <Paper sx={{ p: 2 }}>
            <Typography variant="subtitle1" sx={{ mb: 1, fontWeight: 700 }}>
              Hiérarchie RB qui sera créée
            </Typography>
            <PreviewTree nodes={preview.arbreRubriques} styleRuptures />
          </Paper>

          <Paper sx={{ p: 2 }}>
            <Typography variant="subtitle1" sx={{ mb: 1, fontWeight: 700 }}>
              Hiérarchie Items BI qui sera créée
            </Typography>
            <PreviewTree nodes={preview.arbreItemsBI} />
          </Paper>

          <Paper sx={{ p: 2 }}>
            <Typography variant="subtitle1" sx={{ mb: 1, fontWeight: 700 }}>
              Lignes ignorées
            </Typography>
            <Box sx={{ overflowX: 'auto' }}>
            <Table size="small" sx={{ minWidth: 480 }}>
              <TableHead>
                <TableRow>
                  <TableCell>Ligne</TableCell>
                  <TableCell>Code</TableCell>
                  <TableCell>Libellé</TableCell>
                  <TableCell>Motif</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {preview.lignesIgnorees.map((ligne) => (
                  <TableRow key={`${ligne.numeroLigne}-${ligne.code}`}>
                    <TableCell>{ligne.numeroLigne}</TableCell>
                    <TableCell>{ligne.code}</TableCell>
                    <TableCell>{ligne.libelle}</TableCell>
                    <TableCell>{ligne.motif}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
            </Box>
          </Paper>

          {preview.anomalies.length > 0 && (
            <Paper sx={{ p: 2 }}>
              <Typography variant="subtitle1" sx={{ mb: 1, fontWeight: 700 }}>
                Anomalies
              </Typography>
              <Stack spacing={0.75}>
                {preview.anomalies.map((a, index) => (
                  <Alert
                    key={`${a.code}-${a.numeroLigne}-${index}`}
                    severity={a.severite === 'Error' ? 'error' : a.severite === 'Warning' ? 'warning' : 'info'}
                  >
                    {a.code} — {a.message}
                  </Alert>
                ))}
              </Stack>
            </Paper>
          )}
        </Stack>
      )}

    </Box>
  );
}
