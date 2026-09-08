import type {
  DemandePaiementRetourDestinataire,
  DemandePaiementRoutage,
} from '../../services/apiClient';
import {
  labelRetourAction,
  labelRetourDestinataireHint,
  type RetourActionContext,
} from './demandePaiementRetourLabels';
import { labelStatutDpm, normalizeStatutDpm } from './paiementUtils';

/** Types de retour API (Lot 3.6.3) — lecture seule. */
export const DemandePaiementRetourType = {
  N2VersN1: 'N2_N1',
  N1VersDemandeur: 'N1_X',
  ChargeVersDemandeur: 'Y_X',
  JuniorVersCharge: 'Z_Y',
  VisaVersControle: 'V_Z',
} as const;

/** Actions routage persistées (miroir backend). */
export const DemandePaiementRoutageAction = {
  Receptionner: 'RECEPTIONNER',
  EntrerTraitement: 'ENTRER_TRAITEMENT',
  Controler: 'CONTROLER',
  Orienter: 'ORIENTER',
  RetourDemandeur: 'RETOUR_DEMANDEUR',
  RetourInterEtapes: 'RETOUR_INTER_ETAPES',
  RetourVisa: 'RETOUR_VISA',
  RejetValidationEntite: 'REJET_VALIDATION_ENTITE',
} as const;

const ACTIONS_RETOUR = new Set<string>([
  DemandePaiementRoutageAction.RejetValidationEntite,
  DemandePaiementRoutageAction.RetourDemandeur,
  DemandePaiementRoutageAction.RetourInterEtapes,
  DemandePaiementRoutageAction.RetourVisa,
]);

export function isRoutageRetourAction(action: string): boolean {
  return ACTIONS_RETOUR.has(action.toUpperCase());
}

export function typeRetourToContext(typeRetour: string | null | undefined): RetourActionContext | null {
  switch (typeRetour) {
    case DemandePaiementRetourType.N2VersN1:
      return 'validation_n2';
    case DemandePaiementRetourType.N1VersDemandeur:
      return 'validation_n1';
    case DemandePaiementRetourType.ChargeVersDemandeur:
      return 'charge_dpm';
    case DemandePaiementRetourType.JuniorVersCharge:
      return 'controle_junior';
    case DemandePaiementRetourType.VisaVersControle:
      return 'controle_viseur';
    default:
      return null;
  }
}

export function formatDestinataireNom(
  prenom?: string | null,
  nom?: string | null,
): string | null {
  const parts = [prenom?.trim(), nom?.trim()].filter(Boolean);
  return parts.length > 0 ? parts.join(' ') : null;
}

export function formatRetourDestinataireLabel(
  retour: Pick<
    DemandePaiementRetourDestinataire,
    'typeRetour' | 'prenomUtilisateurDestinataire' | 'nomUtilisateurDestinataire'
  >,
): string {
  const context = typeRetourToContext(retour.typeRetour);
  const base = context ? labelRetourAction(context) : 'Retour';
  const name = formatDestinataireNom(
    retour.prenomUtilisateurDestinataire,
    retour.nomUtilisateurDestinataire,
  );
  return name ? `${base} — ${name}` : base;
}

/** Alimente `labelRetourDestinataireHint` avec les données API (Lot 3.6.4). */
export function labelRetourDestinataireHintFromApi(
  retour: Pick<
    DemandePaiementRetourDestinataire,
    'typeRetour' | 'prenomUtilisateurDestinataire' | 'nomUtilisateurDestinataire'
  >,
): string | null {
  const context = typeRetourToContext(retour.typeRetour);
  if (!context) return null;
  return labelRetourDestinataireHint(
    context,
    formatDestinataireNom(retour.prenomUtilisateurDestinataire, retour.nomUtilisateurDestinataire),
  );
}

export function findRetourByRoutageId(
  retours: DemandePaiementRetourDestinataire[] | null | undefined,
  idRoutage: number,
): DemandePaiementRetourDestinataire | null {
  return retours?.find((r) => r.idRoutage === idRoutage) ?? null;
}

export function findRetourPourStatutACorriger(
  retours: DemandePaiementRetourDestinataire[] | null | undefined,
): DemandePaiementRetourDestinataire | null {
  if (!retours?.length) return null;
  const versDemandeur = retours.filter(
    (r) => normalizeStatutDpm(r.statutCible) === 'A_CORRIGER',
  );
  return versDemandeur.length > 0 ? versDemandeur[versDemandeur.length - 1]! : null;
}

export function labelRoutageAction(
  action: string,
  statutSource?: string,
  statutCible?: string,
): string {
  const a = action.toUpperCase();
  switch (a) {
    case DemandePaiementRoutageAction.Receptionner:
      return 'Réception Chargé DP';
    case DemandePaiementRoutageAction.EntrerTraitement:
      return 'Entrée en traitement DPM';
    case DemandePaiementRoutageAction.Controler:
      return 'Assignation contrôle budgétaire';
    case DemandePaiementRoutageAction.Orienter:
      return 'Orientation contrôle budgétaire';
    case DemandePaiementRoutageAction.RetourDemandeur:
      return labelRetourAction('charge_dpm');
    case DemandePaiementRoutageAction.RetourInterEtapes:
      return labelRetourAction('controle_junior');
    case DemandePaiementRoutageAction.RetourVisa:
      return labelRetourAction('controle_viseur');
    case DemandePaiementRoutageAction.RejetValidationEntite: {
      const src = normalizeStatutDpm(statutSource);
      const dst = normalizeStatutDpm(statutCible);
      if (src === 'EN_VALIDATION_N2' && dst === 'EN_VALIDATION_N1') {
        return labelRetourAction('validation_n2');
      }
      if (src === 'EN_VALIDATION_N1' && dst === 'A_CORRIGER') {
        return labelRetourAction('validation_n1');
      }
      return 'Rejet validation entité';
    }
    default:
      return action.replace(/_/g, ' ').toLowerCase();
  }
}

export function formatTransmissionActeurs(routage: DemandePaiementRoutage): string | null {
  const source = formatDestinataireNom(
    routage.prenomUtilisateurSource,
    routage.nomUtilisateurSource,
  );
  const cible = formatDestinataireNom(
    routage.prenomUtilisateurCible,
    routage.nomUtilisateurCible,
  );

  const parts: string[] = [];
  if (source) parts.push(`De : ${source}`);
  if (cible) parts.push(`Vers : ${cible}`);
  return parts.length > 0 ? parts.join(' · ') : null;
}

export type WorkflowRoutageItem = {
  idRoutage: number;
  actionLabel: string;
  statutTransition: string;
  acteurs: string | null;
  retourLabel: string | null;
  dateRoutage: string;
  estActif: boolean;
  isRetour: boolean;
  motif: string | null;
};

export function buildWorkflowRoutageItems(
  routages: DemandePaiementRoutage[] | null | undefined,
  retours: DemandePaiementRetourDestinataire[] | null | undefined,
): WorkflowRoutageItem[] {
  if (!routages?.length) return [];

  const retourById = new Map(
    (retours ?? []).map((r) => [r.idRoutage, r] as const),
  );

  return routages.map((r) => {
    const retour = retourById.get(r.idRoutage) ?? null;
    const isRetour = isRoutageRetourAction(r.action);
    return {
      idRoutage: r.idRoutage,
      actionLabel: labelRoutageAction(r.action, r.statutSource, r.statutCible),
      statutTransition: `${labelStatutDpm(r.statutSource)} → ${labelStatutDpm(r.statutCible)}`,
      acteurs: formatTransmissionActeurs(r),
      retourLabel: retour ? formatRetourDestinataireLabel(retour) : null,
      dateRoutage: r.dateRoutage,
      estActif: r.estActif,
      isRetour,
      motif: r.motif ?? retour?.motif ?? null,
    };
  });
}
