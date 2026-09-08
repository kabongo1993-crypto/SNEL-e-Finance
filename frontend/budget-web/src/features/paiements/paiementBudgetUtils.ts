import type {
  ControleBudgetaireDto,
  ControleImputationDto,
  DemandePaiementDetail,
  DemandePaiementListItem,
  JournalAuditDemandeDto,
  UniteBudgetaire,
} from '../../services/apiClient';
import { labelTimelineRetourDemandeur } from './demandePaiementRetourLabels';
import {
  STATUTS_FILE_BUDGETS,
  formatDateTimeFr,
  formatMontantUsd,
  labelStatutDpm,
  normalizeStatutDpm,
} from './paiementUtils';

export { STATUTS_FILE_BUDGETS };

export type DemandeEnrichie = DemandePaiementListItem & {
  idDepartement: number;
  codeDepartement: string;
  libelleDepartement: string;
  idTypeBudget?: number | null;
  codeTypeBudget?: string | null;
};

export type DeptDemandeGroup = {
  key: string;
  idDepartement: number;
  libelleDepartement: string;
  codeDepartement: string;
  demandes: DemandeEnrichie[];
  nbSoumises: number;
  nbReceptionnees: number;
  nbEnControle: number;
  nbACorriger: number;
  nbVisees: number;
  montantTotal: number;
};

export function enrichDemandesWithDepartement(
  demandes: DemandePaiementListItem[],
  ubs: UniteBudgetaire[],
): DemandeEnrichie[] {
  const ubMap = new Map<number, UniteBudgetaire>();
  for (const u of ubs) ubMap.set(u.idUB, u);
  return demandes.map((d) => {
    const ub = ubMap.get(d.idUB);
    return {
      ...d,
      idDepartement: d.idDepartement ?? ub?.idDepartement ?? 0,
      codeDepartement: d.codeDepartement ?? ub?.departementCode ?? '—',
      libelleDepartement: d.libelleDepartement ?? ub?.departementLibelle ?? 'Non renseigné',
    };
  });
}

export function groupDemandesByDepartement(demandes: DemandeEnrichie[]): DeptDemandeGroup[] {
  const map = new Map<string, DeptDemandeGroup>();
  for (const row of demandes) {
    const key = String(row.idDepartement || 0);
    let g = map.get(key);
    if (!g) {
      g = {
        key,
        idDepartement: row.idDepartement,
        libelleDepartement: row.libelleDepartement,
        codeDepartement: row.codeDepartement,
        demandes: [],
        nbSoumises: 0,
        nbReceptionnees: 0,
        nbEnControle: 0,
        nbACorriger: 0,
        nbVisees: 0,
        montantTotal: 0,
      };
      map.set(key, g);
    }
    g.demandes.push(row);
    g.montantTotal += row.montantUsd ?? 0;
    const s = normalizeStatutDpm(row.statut);
    if (s === 'SOUMISE') g.nbSoumises += 1;
    else if (s === 'EN_TRAITEMENT_DPM') g.nbReceptionnees += 1;
    else if (s === 'EN_CONTROLE_BUDGETAIRE') g.nbEnControle += 1;
    else if (s === 'A_CORRIGER') g.nbACorriger += 1;
    else if (s === 'VISEE_BUDGETAIREMENT') g.nbVisees += 1;
  }
  return [...map.values()].sort((a, b) =>
    a.libelleDepartement.localeCompare(b.libelleDepartement, 'fr'),
  );
}

export function filtreFileBudgets(d: DemandeEnrichie, mode: 'FILE' | 'TOUTES'): boolean {
  if (mode === 'TOUTES') return true;
  return STATUTS_FILE_BUDGETS.includes(normalizeStatutDpm(d.statut) as (typeof STATUTS_FILE_BUDGETS)[number]);
}

/** @deprecated Budget utilise reference/beneficiaire API (Étape 14). Conservé pour référence. */
export function filtreRechercheLocale(
  d: DemandeEnrichie,
  q: string,
  beneficiaire?: string,
): boolean {
  const query = q.trim().toLowerCase();
  const ben = (beneficiaire ?? '').trim().toLowerCase();
  if (ben && !`${d.reference} ${d.objet}`.toLowerCase().includes(ben)) {
    return false;
  }
  if (!query) return true;
  const hay = `${d.reference} ${d.objet} ${d.codeUB} ${d.libelleUB} ${d.libelleCasDossier} ${d.libelleDepartement}`.toLowerCase();
  return hay.includes(query);
}

export interface ControleSummary {
  budgetAnnuel: number;
  montantPrevision: number;
  creditEngageAnnuel: number;
  engagementEnCours: number;
  creditDisponibleAnnuel: number;
  montantDemande: number;
  estValide: boolean;
  motifRejet: string | null;
  deficitAnnuel: number;
}

export function buildControleSummary(
  controle: ControleBudgetaireDto | null | undefined,
  montantDemande: number,
): ControleSummary {
  const imputations = controle?.imputations ?? [];
  const budgetAnnuel = imputations.reduce((s, i) => s + (i.budgetAnnuel ?? 0), 0);
  const montantPrevision = imputations.reduce((s, i) => s + (i.montantPrevision ?? 0), 0);
  const creditEngageAnnuel = imputations.reduce((s, i) => s + (i.creditEngageAnnuel ?? 0), 0);
  const engagementEnCours = imputations.reduce((s, i) => s + (i.engagementEnCours ?? 0), 0);
  const creditDisponibleAnnuel = imputations.reduce((s, i) => s + (i.creditDisponibleAnnuel ?? 0), 0);
  const estValide = controle?.estValide ?? false;
  const motifRejet = controle?.motifRejet ?? null;
  const deficitAnnuel = estValide ? 0 : Math.max(0, montantDemande - Math.max(0, creditDisponibleAnnuel));
  return {
    budgetAnnuel,
    montantPrevision,
    creditEngageAnnuel,
    engagementEnCours,
    creditDisponibleAnnuel,
    montantDemande,
    estValide,
    motifRejet,
    deficitAnnuel,
  };
}

export function labelPrevision(montant: number): string {
  if (!montant || montant === 0) return 'Prévision initiale : 0 USD';
  return formatMontantUsd(montant);
}

export interface TimelineStep {
  key: string;
  label: string;
  done: boolean;
  date: string | null;
  detail?: string | null;
}

export function labelOperationAudit(op: string): string {
  switch ((op ?? '').toUpperCase()) {
    case 'CREER':
      return 'Création';
    case 'MODIFIER':
      return 'Modification';
    case 'SOUMETTRE':
      return 'Soumission';
    case 'RECEPTIONNER':
      return 'Réception Chargé DP';
    case 'ENTRER_TRAITEMENT':
      return 'Entrée en traitement DPM';
    case 'TRAITER':
      return 'Traitement Chargé DP';
    case 'ORIENTER':
      return 'Orientation DC / AE / BI';
    case 'IMPUTER':
      return 'Imputation';
    case 'CONTROLER':
      return 'Contrôle budgétaire';
    case 'RETOURNER':
      return 'Retour métier';
    case 'ENVOYER_EN_VALIDATION_N1':
      return 'Envoi en validation N1';
    case 'VALIDER_N1':
      return 'Validation électronique N1';
    case 'VALIDER_N2':
      return 'Validation électronique N2';
    case 'DECLARER_VALIDATION_PHYSIQUE_N1':
      return 'Validation physique déclarée (N1)';
    case 'DECLARER_VALIDATION_PHYSIQUE_N2':
      return 'Validation physique déclarée (N2)';
    case 'IMPRIMER':
      return 'Impression DPM';
    case 'AJOUTER_DOCUMENT_SIGNE':
      return 'Document DPM signé ajouté';
    case 'VISER':
      return 'Visa budgétaire';
    default:
      return op || '—';
  }
}

export function buildTimelineSteps(
  demande: DemandePaiementDetail,
  historique: JournalAuditDemandeDto[],
): TimelineStep[] {
  const retourAudit = historique.find((h) => (h.operation ?? '').toUpperCase() === 'RETOURNER');
  const validations = demande.validationsEntite ?? [];
  const n1 = validations.find((v) => v.niveau === 1);
  const n2 = validations.find((v) => v.niveau === 2);
  const n1Validee = (n1?.statut ?? '').toUpperCase() === 'VALIDEE';
  const n2Validee = (n2?.statut ?? '').toUpperCase() === 'VALIDEE';

  const steps: TimelineStep[] = [
    {
      key: 'creation',
      label: 'DPM initiée',
      done: true,
      date: demande.dateCreation,
    },
    {
      key: 'validation_n1',
      label: n1?.modeValidation === 'PHYSIQUE' && n1Validee
        ? 'N1 — validation physique déclarée'
        : 'N1 validée',
      done: n1Validee,
      date: n1?.dateValidation ?? null,
      detail: n1Validee
        ? n1 && n1.modeValidation === 'PHYSIQUE'
          ? n1.nomSignatairePhysique ?? undefined
          : n1?.nomUtilisateurValidateur ?? undefined
        : null,
    },
    {
      key: 'validation_n2',
      label: n2?.modeValidation === 'PHYSIQUE' && n2Validee
        ? 'N2 — validation physique déclarée'
        : 'N2 validée',
      done: n2Validee,
      date: n2?.dateValidation ?? null,
      detail: n2Validee
        ? n2 && n2.modeValidation === 'PHYSIQUE'
          ? n2.nomSignatairePhysique ?? undefined
          : n2?.nomUtilisateurValidateur ?? undefined
        : null,
    },
    {
      key: 'validee_entite',
      label: 'Validée par l\'entité',
      done:
        n1Validee &&
        n2Validee &&
        ['VALIDEE_ENTITE', 'SOUMISE', 'EN_TRAITEMENT_DPM', 'EN_CONTROLE_BUDGETAIRE', 'VISEE_BUDGETAIREMENT'].includes(
          normalizeStatutDpm(demande.statut),
        ),
      date: n2Validee ? n2?.dateValidation ?? null : null,
    },
    {
      key: 'soumission',
      label: 'Soumise au Budget',
      done: !!demande.dateSoumission,
      date: demande.dateSoumission,
    },
    {
      key: 'reception',
      label: 'Réception Chargé DP',
      done: !!demande.dateReception,
      date: demande.dateReception,
    },
    {
      key: 'controle',
      label: 'Contrôle Junior / visa',
      done: !!demande.dateControle,
      date: demande.dateControle,
      detail:
        normalizeStatutDpm(demande.statut) === 'EN_CONTROLE_BUDGETAIRE'
          ? 'En cours de contrôle budgétaire'
          : null,
    },
    {
      key: 'retour',
      label: labelTimelineRetourDemandeur(),
      done: !!demande.dateRetour && normalizeStatutDpm(demande.statut) === 'A_CORRIGER',
      date: demande.dateRetour,
      detail: demande.motifRetour
        ? `${demande.motifRetour}${demande.commentaireRetour ? ` — ${demande.commentaireRetour}` : ''}`
        : null,
    },
    {
      key: 'visa',
      label: 'Visa budgétaire',
      done: !!demande.dateVisa,
      date: demande.dateVisa,
    },
  ];
  if (!retourAudit && !demande.dateRetour) {
    return steps.filter((s) => s.key !== 'retour');
  }
  return steps;
}

export function formatTimelineLabel(step: TimelineStep): string {
  const datePart = step.date ? formatDateTimeFr(step.date) : 'En attente';
  return `${step.label} · ${datePart}${step.detail ? ` · ${step.detail}` : ''}`;
}

export function dcControlesOk(imputation: ControleImputationDto): {
  mensuelOk: boolean;
  annuelOk: boolean;
} {
  const mensuelOk =
    imputation.creditDisponibleMensuel == null || imputation.creditDisponibleMensuel >= 0;
  const annuelOk = imputation.creditDisponibleAnnuel >= 0;
  return { mensuelOk, annuelOk };
}

export function statutLabelForBudget(statut: string | null | undefined): string {
  return labelStatutDpm(statut);
}
