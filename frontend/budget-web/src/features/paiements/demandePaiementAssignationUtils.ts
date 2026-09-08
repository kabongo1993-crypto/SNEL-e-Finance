/**
 * Affichage assignation DPM — consomme les champs API lorsqu'ils sont exposés.
 * Sans `idUtilisateurAssigne`, aucun libellé pool/nominatif n'est produit (pas de heuristique métier).
 */
export type DemandeAssignationView = {
  idUtilisateurAssigne: number | null | undefined;
  nomUtilisateurAssigne?: string | null;
  prenomUtilisateurAssigne?: string | null;
  idUtilisateurCourant: number;
};

export type AssignationDisplay =
  | { kind: 'pool' }
  | { kind: 'yours' }
  | { kind: 'assigned'; label: string };

export function formatNomUtilisateur(
  nom?: string | null,
  prenom?: string | null,
): string | null {
  const parts = [prenom?.trim(), nom?.trim()].filter(Boolean);
  return parts.length > 0 ? parts.join(' ') : null;
}

export function resolveAssignationDisplay(
  view: DemandeAssignationView | null | undefined,
): AssignationDisplay | null {
  if (!view) return null;

  const assigne = view.idUtilisateurAssigne;
  if (assigne == null) {
    return { kind: 'pool' };
  }

  if (assigne === view.idUtilisateurCourant) {
    return { kind: 'yours' };
  }

  const nom =
    formatNomUtilisateur(view.prenomUtilisateurAssigne, view.nomUtilisateurAssigne) ??
    `Utilisateur #${assigne}`;
  return { kind: 'assigned', label: nom };
}

/**
 * Masque les actions métier si la DPM est nominativement assignée à un autre utilisateur.
 * Retourne `true` si les données d'assignation sont absentes (pas de filtrage côté client).
 */
export function peutAfficherActionsMetierAssignation(
  view: DemandeAssignationView | null | undefined,
): boolean {
  const display = resolveAssignationDisplay(view);
  if (!display) return true;
  if (display.kind === 'assigned') return false;
  return true;
}
