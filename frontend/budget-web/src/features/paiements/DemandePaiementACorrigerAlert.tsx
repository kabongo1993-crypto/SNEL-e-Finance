import { Alert, Typography } from '@mui/material';
import type { DemandePaiementRetourDestinataire } from '../../services/apiClient';
import { formatRetourDestinataireLabel, findRetourPourStatutACorriger } from './demandePaiementRoutageUtils';
import { messageAlerteACorriger } from './demandePaiementRetourLabels';
import { formatDateFr } from './paiementUtils';

type DemandeACorrigerFields = {
  motifRetour?: string | null;
  commentaireRetour?: string | null;
  dateRetour?: string | null;
};

type DemandePaiementACorrigerAlertProps = {
  demande: DemandeACorrigerFields;
  retoursDestinataires?: DemandePaiementRetourDestinataire[] | null;
  /** Texte complémentaire sous le titre principal. */
  description?: string;
};

export function DemandePaiementACorrigerAlert({
  demande,
  retoursDestinataires,
  description,
}: DemandePaiementACorrigerAlertProps) {
  const retour = findRetourPourStatutACorriger(retoursDestinataires);
  const retourLabel = retour ? formatRetourDestinataireLabel(retour) : null;

  return (
    <Alert severity="warning" sx={{ mb: 2 }}>
      <Typography sx={{ fontWeight: 700 }}>{messageAlerteACorriger()}</Typography>
      {description && (
        <Typography variant="body2" sx={{ mt: 0.5 }}>
          {description}
        </Typography>
      )}
      {retourLabel && (
        <Typography variant="body2" sx={{ mt: 0.5, fontWeight: 650 }}>
          {retourLabel}
        </Typography>
      )}
      <Typography variant="body2" sx={{ mt: 0.5 }}>
        Motif : {demande.motifRetour ?? '—'}
      </Typography>
      {demande.commentaireRetour && (
        <Typography variant="body2">Commentaire : {demande.commentaireRetour}</Typography>
      )}
      {demande.dateRetour && (
        <Typography variant="body2">Date : {formatDateFr(demande.dateRetour)}</Typography>
      )}
    </Alert>
  );
}
