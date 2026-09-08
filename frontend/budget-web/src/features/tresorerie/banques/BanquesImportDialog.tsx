import {
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Typography,
} from '@mui/material';
import { DataTable, PrimaryButton, SecondaryButton, type DataTableColumn } from '../../../components';
import type { BanqueImportLigne, BanqueImportPreview } from './banqueExcelImport';

function statutColor(statut: BanqueImportLigne['statut']): 'success' | 'warning' | 'error' | 'default' {
  if (statut === 'a_importer') return 'success';
  if (statut === 'doublon_fichier' || statut === 'deja_existante') return 'warning';
  if (statut === 'erreur') return 'error';
  return 'default';
}

const columns: DataTableColumn<BanqueImportLigne & { id: string }>[] = [
  {
    id: 'idBanque',
    label: 'ID_Banque',
    mobile: 'title',
    render: (r) => (
      <Typography sx={{ fontWeight: 700, fontFamily: 'ui-monospace, monospace' }}>{r.idBanque || '—'}</Typography>
    ),
  },
  { id: 'libelle', label: 'Libellé', mobile: 'subtitle', render: (r) => r.libelleBanque || '—' },
  { id: 'pays', label: 'Pays', mobile: 'meta', render: (r) => r.pays ?? '—' },
  {
    id: 'resultat',
    label: 'Résultat',
    mobile: 'meta',
    render: (r) => <Chip size="small" label={r.resultat} color={statutColor(r.statut)} variant="outlined" />,
  },
];

interface BanquesImportDialogProps {
  open: boolean;
  preview: BanqueImportPreview | null;
  importing: boolean;
  onClose: () => void;
  onConfirm: () => void;
}

export function BanquesImportDialog({
  open,
  preview,
  importing,
  onClose,
  onConfirm,
}: BanquesImportDialogProps) {
  const rows = (preview?.lignes ?? []).map((l) => ({ ...l, id: String(l.ligneExcel) }));
  const canImport = Boolean(preview && preview.resume.aImporter > 0 && !importing);

  return (
    <Dialog open={open} onClose={() => !importing && onClose()} fullWidth maxWidth="md">
      <DialogTitle>Importer des banques</DialogTitle>
      <DialogContent>
        {preview && (
          <>
            <Typography variant="body2" sx={{ mt: 1 }}>
              Fichier : {preview.nomFichier}
            </Typography>
            <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
              {preview.lignesDetectees} ligne{preview.lignesDetectees > 1 ? 's' : ''} détectée
              {preview.lignesDetectees > 1 ? 's' : ''}
            </Typography>
            <DataTable
              columns={columns}
              rows={rows}
              defaultRowsPerPage={10}
              emptyTitle="Aucune ligne"
              emptyDescription="Le fichier ne contient aucune banque à analyser."
            />
            <Typography variant="subtitle2" sx={{ mt: 2, fontWeight: 700 }}>
              Résumé
            </Typography>
            <Typography variant="body2" color="text.secondary">
              {preview.resume.analysees} lignes analysées
              <br />
              {preview.resume.aImporter} lignes à importer
              <br />
              {preview.resume.doublons} doublon{preview.resume.doublons > 1 ? 's' : ''}
              <br />
              {preview.resume.dejaExistantes} déjà existante{preview.resume.dejaExistantes > 1 ? 's' : ''}
              <br />
              {preview.resume.erreurs} erreur{preview.resume.erreurs > 1 ? 's' : ''}
            </Typography>
          </>
        )}
      </DialogContent>
      <DialogActions>
        <SecondaryButton onClick={onClose} disabled={importing}>
          Annuler
        </SecondaryButton>
        <PrimaryButton onClick={onConfirm} disabled={!canImport}>
          {preview ? `Importer ${preview.resume.aImporter} banque${preview.resume.aImporter > 1 ? 's' : ''}` : 'Importer'}
        </PrimaryButton>
      </DialogActions>
    </Dialog>
  );
}
