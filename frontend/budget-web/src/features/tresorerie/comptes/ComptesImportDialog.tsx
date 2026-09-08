import {
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Typography,
} from '@mui/material';
import { DataTable, PrimaryButton, SecondaryButton, type DataTableColumn } from '../../../components';
import type { ImportCompteLigneDto, ImportComptesPreviewDto, ImportComptesResultDto } from './comptesService';

function statutColor(statut: string): 'success' | 'warning' | 'error' | 'default' {
  if (statut === 'a_importer' || statut === 'importe') return 'success';
  if (statut === 'doublon_fichier' || statut === 'deja_existant') return 'warning';
  if (statut === 'erreur') return 'error';
  return 'default';
}

const columns: DataTableColumn<ImportCompteLigneDto & { id: string }>[] = [
  { id: 'ligne', label: 'Ligne', mobile: 'meta', render: (r) => r.ligneExcel },
  {
    id: 'numero',
    label: 'N° compte',
    mobile: 'title',
    render: (r) => (
      <Typography sx={{ fontWeight: 700, fontFamily: 'ui-monospace, monospace' }}>
        {r.numeroCompte || '—'}
      </Typography>
    ),
  },
  { id: 'champ', label: 'Champ', mobile: 'meta', render: (r) => r.champ ?? '—' },
  { id: 'valeur', label: 'Valeur', mobile: 'subtitle', render: (r) => r.valeurRecue ?? '—' },
  {
    id: 'resultat',
    label: 'Raison',
    mobile: 'meta',
    render: (r) => <Chip size="small" label={r.resultat} color={statutColor(r.statut)} variant="outlined" />,
  },
];

interface ComptesImportPreviewDialogProps {
  open: boolean;
  preview: ImportComptesPreviewDto | null;
  importing: boolean;
  onClose: () => void;
  onConfirm: () => void;
}

export function ComptesImportPreviewDialog({
  open,
  preview,
  importing,
  onClose,
  onConfirm,
}: ComptesImportPreviewDialogProps) {
  const rows = (preview?.lignes ?? []).map((l) => ({ ...l, id: String(l.ligneExcel) }));
  const canImport = Boolean(preview && preview.resume.aImporter > 0 && !importing);

  return (
    <Dialog open={open} onClose={() => !importing && onClose()} fullWidth maxWidth="lg">
      <DialogTitle>Prévisualisation de l’import</DialogTitle>
      <DialogContent>
        {preview && (
          <>
            <Typography variant="body2" sx={{ mt: 1 }}>
              Fichier : {preview.nomFichier}
            </Typography>
            <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
              {preview.lignesDetectees} ligne{preview.lignesDetectees > 1 ? 's' : ''} détectée
              {preview.lignesDetectees > 1 ? 's' : ''} — confirmation obligatoire avant insertion.
            </Typography>
            <DataTable
              columns={columns}
              rows={rows}
              defaultRowsPerPage={10}
              emptyTitle="Aucune ligne"
              emptyDescription="Le fichier ne contient aucun compte à analyser."
            />
            <Typography variant="subtitle2" sx={{ mt: 2, fontWeight: 700 }}>
              Résumé
            </Typography>
            <Typography variant="body2" color="text.secondary">
              {preview.resume.analysees} lignes analysées
              <br />
              {preview.resume.aImporter} lignes valides à importer
              <br />
              {preview.resume.doublons} doublon{preview.resume.doublons > 1 ? 's' : ''}
              <br />
              {preview.resume.dejaExistants} déjà existant{preview.resume.dejaExistants > 1 ? 's' : ''}
              <br />
              {preview.resume.erreurs} ligne{preview.resume.erreurs > 1 ? 's' : ''} en erreur
            </Typography>
          </>
        )}
      </DialogContent>
      <DialogActions>
        <SecondaryButton onClick={onClose} disabled={importing}>
          Annuler
        </SecondaryButton>
        <PrimaryButton onClick={onConfirm} disabled={!canImport}>
          {preview
            ? `Importer ${preview.resume.aImporter} compte${preview.resume.aImporter > 1 ? 's' : ''}`
            : 'Importer'}
        </PrimaryButton>
      </DialogActions>
    </Dialog>
  );
}

interface ComptesImportResultDialogProps {
  open: boolean;
  result: ImportComptesResultDto | null;
  onClose: () => void;
}

export function ComptesImportResultDialog({ open, result, onClose }: ComptesImportResultDialogProps) {
  const rows = (result?.details ?? []).map((l) => ({ ...l, id: `r-${l.ligneExcel}` }));

  return (
    <Dialog open={open} onClose={onClose} fullWidth maxWidth="lg">
      <DialogTitle>Import terminé</DialogTitle>
      <DialogContent>
        {result && (
          <>
            <Typography variant="body2" sx={{ mt: 1, mb: 2 }}>
              {result.analysees} lignes analysées
              <br />
              {result.importes} comptes importés
              <br />
              {result.ignores} lignes ignorées
              <br />
              {result.erreurs} lignes en erreur
            </Typography>
            <DataTable
              columns={columns}
              rows={rows}
              defaultRowsPerPage={10}
              emptyTitle="Aucun détail"
              emptyDescription="Aucun détail d’import à afficher."
            />
          </>
        )}
      </DialogContent>
      <DialogActions>
        <PrimaryButton onClick={onClose}>Fermer</PrimaryButton>
      </DialogActions>
    </Dialog>
  );
}
