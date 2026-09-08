export type { RubriqueHierarchyNode } from './types';
export { estRuptureNoeud, estRuptureRubrique } from './types';

export {
  buildRubriqueHierarchy,
  filterRubriqueHierarchy,
  collectRubriqueAncestorIds,
  collectExpandedIdsForFilter,
  flattenRubriqueHierarchy,
} from './buildHierarchy';

export {
  formatRubrique,
  formatRubriqueCodeLibelle,
  formatRubriqueParent,
  formatRubriqueAvecRupture,
} from './formatLabel';

export {
  RUBRIQUE_RUPTURE,
  RUBRIQUE_LEAF,
  GROUPE_NIVEAU1_PREVISION,
  resolveRubriqueBreakLevel,
  rubriqueRuptureRowSx,
  rubriqueRuptureTreeRowSx,
  type RubriqueBreakVisualLevel,
} from './styles';

export {
  RubriqueHierarchyTree,
  RubriqueHierarchyLegend,
} from './RubriqueHierarchyTree';

export { RubriqueHierarchySelect } from './RubriqueHierarchySelect';
export { RubriqueHierarchyList } from './RubriqueHierarchyList';
export type { RubriqueHierarchyListAction } from './RubriqueHierarchyList';

export {
  buildRubriqueGroupesNiveau1,
  filterGroupesNiveau1ForReferentiel,
  countRubriquesInGroupes,
  formatGroupeNiveau1,
  type RubriqueGroupeNiveau1,
} from './buildGroupeNiveau1';

export {
  RubriqueGroupeNiveau1Tree,
  RubriqueGroupeNiveau1Legend,
} from './RubriqueGroupeNiveau1Tree';

export { RubriqueGroupeNiveau1List } from './RubriqueGroupeNiveau1List';
export type { RubriqueGroupeNiveau1ListAction } from './RubriqueGroupeNiveau1List';
