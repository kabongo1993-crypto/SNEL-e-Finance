import {
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Typography,
} from '@mui/material';
import { DataTable, PrimaryButton, SecondaryButton, type DataTableColumn } from '../../../components';
import type {
  ImportCompteCategorieLigneDto,
  ImportCompteCategoriesPreviewDto,
  ImportCompteCategoriesResultDto,
} from './comptesService';

function statutColor(statut: string): 'success' | 'warning' | 'error' | 'default' {
  if (statut === 'a_importer' || statut === 'importe') return 'success';
  if (statut === 'doublon_fichier' || statut === 'deja_existant') return 'warning';
  if (statut === 'erreur' || statut === 'conflit_periode') return 'error';
  return 'default';
}

const columns: DataTableColumn<ImportCompteCategorieLigneDto & { id: string }>[] = [
  { id: 'ligne', label: 'Ligne', mobile: 'meta', render: (r) => r.ligneExcel },
  { id: 'compte', label: 'Compte', mobile: 'title', render: (r) => r.compte || '—' },
  { id: 'categorie', label: 'Catégorie', mobile: 'subtitle', render: (r) => r.categorie || '—' },
  {
    id: 'periode',
    label: 'Période',
    mobile: 'meta',
    render: (r) => `${r.dateDebut || '—'} → ${r.dateFin || '—'}`,
  },
  { id: 'champ', label: 'Champ', mobile: 'meta', render: (r) => r.champ ?? '—' },
  { id: 'valeur', label: 'Valeur', mobile: 'meta', render: (r) => r.valeurRecue ?? '—' },
  {
    id: 'resultat',
    label: 'Raison',
    mobile: 'meta',
    render: (r) => <Chip size="small" label={r.resultat} color={statutColor(r.statut)} variant="outlined" />,
  },
];

interface PreviewProps {
  open: boolean;
  preview: ImportCompteCategoriesPreviewDto | null;
  importing: boolean;
  onClose: () => void;
  onConfirm: () => void;
}

export function CompteCategoriesImportPreviewDialog({
  open,
  preview,
  importing,
  onClose,
  onConfirm,
}: PreviewProps) {
  const rows = (preview?.lignes ?? []).map((l) => ({ ...l, id: String(l.ligneExcel) }));
  const canImport = Boolean(preview && preview.resume.aImporter > 0 && !importing);

  return (
    <Dialog open={open} onClose={() => !importing && onClose()} fullWidth maxWidth="lg">
      <DialogTitle>Prévisualisation — catégories des comptes</DialogTitle>
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
              emptyDescription="Le fichier ne contient aucune affectation à analyser."
            />
            <Typography variant="subtitle2" sx={{ mt: 2, fontWeight: 700 }}>
              Résumé
            </Typography>
            <Typography variant="body2" color="text.secondary">
              Nombre total de lignes : {preview.resume.analysees}
              <br />
              Nombre de lignes valides : {preview.resume.aImporter}
              <br />
              Nombre de lignes en erreur : {preview.resume.erreurs}
              <br />
              Nombre de doublons : {preview.resume.doublons}
              <br />
              Nombre de dates 1900-01-01 converties en NULL : {preview.resume.datesSentinelleConverties}
              <br />
              Nombre de conflits de période : {preview.resume.conflitsPeriode}
              <br />
              Déjà existantes : {preview.resume.dejaExistants}
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
            ? `Importer ${preview.resume.aImporter} affectation${preview.resume.aImporter > 1 ? 's' : ''}`
            : 'Importer'}
        </PrimaryButton>
      </DialogActions>
    </Dialog>
  );
}

interface ResultProps {
  open: boolean;
  result: ImportCompteCategoriesResultDto | null;
  onClose: () => void;
}

export function CompteCategoriesImportResultDialog({ open, result, onClose }: ResultProps) {
  const rows = (result?.details ?? []).map((l) => ({ ...l, id: `r-${l.ligneExcel}` }));

  return (
    <Dialog open={open} onClose={onClose} fullWidth maxWidth="lg">
      <DialogTitle>Import des catégories terminé</DialogTitle>
      <DialogContent>
        {result && (
          <>
            <Typography variant="body2" sx={{ mt: 1, mb: 2 }}>
              {result.analysees} lignes analysées
              <br />
              {result.importes} affectations importées
              <br />
              {result.ignores} lignes ignorées
              <br />
              {result.erreurs} lignes en erreur
              <br />
              {result.resume.datesSentinelleConverties} dates 1900-01-01 converties en NULL
              <br />
              {result.resume.conflitsPeriode} conflit{result.resume.conflitsPeriode > 1 ? 's' : ''} de
              période
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
