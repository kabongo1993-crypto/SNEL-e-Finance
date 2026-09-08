import { Alert, Box } from '@mui/material';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ErrorState, LoadingState, PageHeader } from '../../components';
import { useAuth } from '../auth';
import {
  fetchDemandesPaiement,
  fetchUnitesBudgetaires,
  type DemandePaiementCompteursScope,
  type DemandePaiementListItem,
  type UniteBudgetaire,
} from '../../services/apiClient';
import { buildJuniorRowActions } from './demandeRowActions';
import { DemandeListRowActions } from './DemandeListRowActions';
import { DemandesParDepartementList } from './DemandesParDepartementList';
import {
  enrichDemandesWithDepartement,
  groupDemandesByDepartement,
} from './paiementBudgetUtils';
import {
  apiErrorMessage,
  canImputerAe,
  canImputerBi,
  canImputerDc,
  formatMontantDevise,
} from './paiementUtils';
import { useDemandePaiementListInvalidation } from './useDemandePaiementListInvalidation';

interface PaiementsJuniorPageProps {
  codeType: 'DC' | 'AE' | 'BI';
}

function juniorListScope(codeType: 'DC' | 'AE' | 'BI'): DemandePaiementCompteursScope {
  if (codeType === 'AE') return 'junior-ae';
  if (codeType === 'BI') return 'junior-bi';
  return 'junior-dc';
}

export function PaiementsJuniorPage({ codeType }: PaiementsJuniorPageProps) {
  const navigate = useNavigate();
  const { user } = useAuth();
  const canAccess =
    (codeType === 'DC' && canImputerDc(user)) ||
    (codeType === 'AE' && canImputerAe(user)) ||
    (codeType === 'BI' && canImputerBi(user));

  const [rows, setRows] = useState<DemandePaiementListItem[]>([]);
  const [ubs, setUbs] = useState<UniteBudgetaire[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!canAccess) return;
    setLoading(true);
    setError(null);
    try {
      const [ubList, list] = await Promise.all([
        fetchUnitesBudgetaires(),
        fetchDemandesPaiement({
          scope: juniorListScope(codeType),
          statut: 'EN_CONTROLE_BUDGETAIRE',
        }),
      ]);
      setUbs(ubList);
      setRows(list);
    } catch (err) {
      setError(apiErrorMessage(err, 'Impossible de charger la file Junior.'));
    } finally {
      setLoading(false);
    }
  }, [canAccess, codeType]);

  useEffect(() => {
    void load();
  }, [load]);

  useDemandePaiementListInvalidation(load);

  const groupes = useMemo(
    () => groupDemandesByDepartement(enrichDemandesWithDepartement(rows, ubs)),
    [rows, ubs],
  );

  const rowActions = useMemo(
    () => buildJuniorRowActions({ navigate, idUtilisateurCourant: user?.idUtilisateur }),
    [navigate, user?.idUtilisateur],
  );

  if (!canAccess) {
    return (
      <Box sx={{ p: 2 }}>
        <Alert severity="warning">Accès réservé au Gestionnaire Junior {codeType}.</Alert>
      </Box>
    );
  }

  return (
    <Box sx={{ p: { xs: 1.5, md: 2 }, display: 'flex', flexDirection: 'column', gap: 2 }}>
      <PageHeader
        breadcrumbs={[
          { label: 'Paiements', to: '/paiements' },
          { label: `Gestionnaire Junior ${codeType}` },
        ]}
      />
      {loading && <LoadingState />}
      {error && !loading && <ErrorState message={error} onRetry={() => void load()} />}
      {!loading && !error && (
        <DemandesParDepartementList
          groupes={groupes}
          showDemandeur
          showCas={false}
          showObjet={false}
          montantLabel="Montant"
          emptyMessage={`Aucune demande ${codeType} en contrôle.`}
          formatMontant={(d) => formatMontantDevise(d.montantBrut, d.devise)}
          idUtilisateurCourant={user?.idUtilisateur}
          renderActions={(d) => <DemandeListRowActions row={d} actions={rowActions} />}
        />
      )}
    </Box>
  );
}
