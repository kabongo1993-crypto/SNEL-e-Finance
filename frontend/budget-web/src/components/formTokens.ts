/**
 * Familles de largeur des champs e-Finance.
 * Valeurs pensées pour Autocomplete (chrome icônes) + labels flottants lisibles.
 * Ne pas utiliser 1fr sur les champs métier courts/moyens.
 */
export const formFieldSize = {
  /** Année, codes très courts (min. confort Autocomplete) */
  xs: 132,
  /** Exercice, Version, Type, Mode, Statut court */
  sm: 156,
  /** Mode/statut, dates, filtres moyens */
  md: 200,
  /** Département, Cas (filtre), Devise */
  lg: 280,
  /** Demandeur, Cas de dossier (formulaire), UB moyenne */
  xl: 340,
  /** UB / libellés longs (sans absorber toute la ligne) */
  xxl: 420,
} as const;

/** Croissance max pour une valeur sélectionnée longue (hors fullWidth). */
export const formFieldGrowMax = {
  xs: 180,
  sm: 200,
  md: 260,
  lg: 360,
  xl: 400,
  xxl: 520,
} as const;

/**
 * Espacement horizontal entre champs (thème spacing units).
 * Conservé modéré pour ne pas « étirer » artificiellement la ligne.
 */
export const formGridColumnGap = 2; // 16px

/**
 * Espacement vertical entre lignes wrappées.
 * Doit absorber : label flottant outlined (~10px au-dessus) + helper text éventuel.
 * Trop faible → chevauchement des labels sur la ligne précédente.
 */
export const formGridRowGap = 3.5; // 28px

/** Alias historique (utilisé comme column gap par défaut). */
export const formGridGap = formGridColumnGap;

/**
 * Padding-top réservé dans chaque cellule pour le label flottant outlined
 * (position absolute qui déborde au-dessus du FormControl).
 */
export const formFieldLabelClearance = 1.25; // 10px

/** Espace vertical entre blocs successifs dans une FormSection. */
export const formSectionBodyGap = 2.5; // 20px

/** Espace entre la grille de filtres et la rangée Recherche / actions. */
export const filterZoneGap = 2.5; // 20px

export const formSectionPad = { xs: 2, md: 2.5 } as const;

/**
 * Colonnes de la grille de filtres (axes verticaux stables).
 * xs→1, sm→2, md→3, xl→6 (une ligne si assez large).
 */
export const filterGridColumns = {
  xs: 1,
  sm: 2,
  md: 3,
  lg: 3,
  xl: 6,
} as const;

export type FormFieldDensity = keyof typeof formFieldSize;
