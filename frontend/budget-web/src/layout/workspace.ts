/** Espaces métier distincts au sein de Budget Web. */
export type Workspace = 'budget' | 'tresorerie';

export const WORKSPACE_HOME: Record<Workspace, string> = {
  budget: '/dashboard',
  tresorerie: '/tresorerie/dashboard',
};

export const WORKSPACE_LABELS: Record<Workspace, string> = {
  budget: 'Budget',
  tresorerie: 'Trésorerie',
};

/** Déduit l'espace courant à partir du pathname (prefix `/tresorerie`). */
export function getWorkspaceFromPath(pathname: string): Workspace {
  const normalized =
    pathname.length > 1 && pathname.endsWith('/') ? pathname.slice(0, -1) : pathname;
  if (normalized === '/tresorerie' || normalized.startsWith('/tresorerie/')) {
    return 'tresorerie';
  }
  return 'budget';
}

/** Fil d'Ariane des référentiels communs (Devises, Taux de change) selon l'espace. */
export function workspaceReferentielBreadcrumbs(
  pathname: string,
  leafLabel: string,
  budgetHomeLabel: string,
): { label: string; to?: string }[] {
  if (getWorkspaceFromPath(pathname) === 'tresorerie') {
    return [
      { label: 'Trésorerie', to: '/tresorerie/dashboard' },
      { label: 'Référentiels' },
      { label: leafLabel },
    ];
  }
  return [
    { label: budgetHomeLabel, to: '/dashboard' },
    { label: 'Référentiels' },
    { label: leafLabel },
  ];
}
