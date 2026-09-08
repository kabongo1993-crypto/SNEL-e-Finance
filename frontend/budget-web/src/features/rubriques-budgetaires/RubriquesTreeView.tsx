import {
  RubriqueHierarchyTree,
  type RubriqueHierarchyNode,
} from './hierarchy';

interface RubriquesTreeViewProps {
  nodes: RubriqueHierarchyNode[];
  expanded: Set<number>;
  selectedId: number | null;
  onToggle: (id: number) => void;
  onSelect: (id: number) => void;
  emptyTitle: string;
  emptyDescription: string;
}

/** Wrapper référentiel — délègue au composant hiérarchie commun. */
export function RubriquesTreeView(props: RubriquesTreeViewProps) {
  return <RubriqueHierarchyTree {...props} />;
}
