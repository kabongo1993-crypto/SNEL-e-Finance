import AccountBalanceWalletOutlinedIcon from '@mui/icons-material/AccountBalanceWalletOutlined';
import AttachMoneyOutlinedIcon from '@mui/icons-material/AttachMoneyOutlined';
import PersonOutlineOutlinedIcon from '@mui/icons-material/PersonOutlineOutlined';
import RouteOutlinedIcon from '@mui/icons-material/RouteOutlined';
import { KpiRow, StatCard } from '../../components';

export type ChargeDpmContextBandeauProps = {
  montantLabel: string;
  beneficiaireLabel: string;
  modePaiementLabel: string;
  destinationLabel: string;
};

/** Bandeau de contexte dossier — 4 indicateurs clés pour le Chargé DP. */
export function ChargeDpmContextBandeau({
  montantLabel,
  beneficiaireLabel,
  modePaiementLabel,
  destinationLabel,
}: ChargeDpmContextBandeauProps) {
  return (
    <KpiRow columns={4}>
      <StatCard
        title="Montant sollicité"
        value={montantLabel}
        icon={<AttachMoneyOutlinedIcon />}
      />
      <StatCard
        title="Bénéficiaire principal"
        value={beneficiaireLabel}
        icon={<PersonOutlineOutlinedIcon />}
      />
      <StatCard
        title="Mode de paiement"
        value={modePaiementLabel}
        icon={<AccountBalanceWalletOutlinedIcon />}
      />
      <StatCard
        title="Destination budgétaire"
        value={destinationLabel}
        icon={<RouteOutlinedIcon />}
      />
    </KpiRow>
  );
}
