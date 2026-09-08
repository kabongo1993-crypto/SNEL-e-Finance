import { ConfirmDialog } from '../../../components';
import type { DemandePaiementBatchOpDef } from './demandePaiementBatchConfig';

export interface DemandePaiementBatchConfirmDialogProps {
  open: boolean;
  busy?: boolean;
  selectedCount: number;
  statutLabel: string;
  operation: DemandePaiementBatchOpDef | null;
  onConfirm: () => void;
  onClose: () => void;
}

function buildConfirmMessage(
  selectedCount: number,
  statutLabel: string,
  operation: DemandePaiementBatchOpDef | null,
): string {
  const label = operation?.label ?? 'Traitement par lot';
  if (operation?.operation === 'receptionner') {
    return (
      `Vous êtes sur le point de réceptionner ${selectedCount} demande${selectedCount > 1 ? 's' : ''} de paiement.\n` +
      `Elles passeront au statut « En traitement DPM ».`
    );
  }
  if (operation?.operation === 'supprimer') {
    return (
      `Vous êtes sur le point de supprimer définitivement ${selectedCount} brouillon${selectedCount > 1 ? 's' : ''}.\n` +
      `Cette action est irréversible (pièces jointes incluses).\n` +
      `Chaque demande est contrôlée individuellement.`
    );
  }
  return `Vous êtes sur le point de traiter ${selectedCount} demande${selectedCount > 1 ? 's' : ''}.\n\nStatut : ${statutLabel}\nOpération : ${label}`;
}

export function DemandePaiementBatchConfirmDialog({
  open,
  busy,
  selectedCount,
  statutLabel,
  operation,
  onConfirm,
  onClose,
}: DemandePaiementBatchConfirmDialogProps) {
  const label =
    operation?.operation === 'receptionner'
      ? 'Réceptionner'
      : operation?.operation === 'supprimer'
        ? 'Supprimer définitivement'
        : (operation?.label ?? 'Traitement par lot');
  const title =
    operation?.operation === 'receptionner'
      ? 'Réceptionner les DPM sélectionnées ?'
      : operation?.operation === 'supprimer'
        ? 'Supprimer définitivement les brouillons sélectionnés ?'
        : `${label} ?`;
  return (
    <ConfirmDialog
      open={open}
      busy={busy}
      title={title}
      message={buildConfirmMessage(selectedCount, statutLabel, operation)}
      confirmLabel={label}
      onConfirm={onConfirm}
      onClose={onClose}
    />
  );
}