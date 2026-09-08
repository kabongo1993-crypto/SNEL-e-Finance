import {
  Alert,
  Box,
  Chip,
  FormControlLabel,
  Paper,
  Stack,
  Switch,
  Tab,
  Tabs,
  Typography,
} from '@mui/material';
import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  ErrorState,
  FormField,
  AmountField,
  FormGrid,
  LoadingState,
  PageHeader,
  PrimaryButton,
  useMsgBox,
} from '../../components';
import {
  fetchParametresInstrumentPaiement,
  upsertParametreInstrumentPaiement,
  type ParametreInstrumentPaiementDto,
} from '../../services/apiClient';
import { BRAND_NAME } from '../../theme';
import { useAuth, canWriteReferentiels } from '../auth';
import { apiErrorMessage } from '../paiements/paiementUtils';
import {
  emptyParametreForm,
  formToUpsertPayload,
  INSTRUMENT_PAIEMENT_LABELS,
  INSTRUMENT_PAIEMENT_TYPES,
  parametreDtoToForm,
  type InstrumentPaiementType,
  type ParametreInstrumentFormState,
} from './parametreInstrumentUtils';

function ConfigStatusChip({ configured, actif }: { configured: boolean; actif: boolean }) {
  if (!actif) {
    return <Chip size="small" label="Inactif" color="default" variant="outlined" />;
  }
  if (configured) {
    return <Chip size="small" label="Configuré" color="success" variant="outlined" />;
  }
  return <Chip size="small" label="Incomplet" color="warning" variant="outlined" />;
}

function InstrumentForm({
  type,
  form,
  configured,
  canWrite,
  busy,
  onChange,
  onSave,
}: {
  type: InstrumentPaiementType;
  form: ParametreInstrumentFormState;
  configured: boolean;
  canWrite: boolean;
  busy: boolean;
  onChange: (patch: Partial<ParametreInstrumentFormState>) => void;
  onSave: () => void;
}) {
  const isPiece = type === 'PIECE_CAISSE';
  const isBon = type === 'BON_PROVISOIRE';
  const isMinute = type === 'MINUTE_CHEQUE';

  return (
    <Stack spacing={2}>
      <Stack direction="row" spacing={1} sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
        <Typography variant="subtitle2">{INSTRUMENT_PAIEMENT_LABELS[type]}</Typography>
        <ConfigStatusChip configured={configured} actif={form.actif} />
      </Stack>

      <Alert severity="info">
        Ces valeurs comptables sont recopiées dans le document au moment de son établissement.
        Les documents déjà établis conservent les valeurs figées à la date d&apos;établissement.
      </Alert>

      <FormControlLabel
        control={
          <Switch
            checked={form.actif}
            disabled={!canWrite || busy}
            onChange={(e) => onChange({ actif: e.target.checked })}
          />
        }
        label="Paramétrage actif"
      />

      <Paper variant="outlined" sx={{ p: 2 }}>
        <FormGrid>
          {isPiece && (
            <>
              <FormField
                label="SR"
                required
                value={form.sr}
                disabled={!canWrite || busy}
                onChange={(e) => onChange({ sr: e.target.value })}
                sx={{ minWidth: 200, flex: '1 1 220px' }}
              />
              <FormField
                label="Comptabilité générale"
                required
                value={form.comptabiliteGenerale}
                disabled={!canWrite || busy}
                onChange={(e) => onChange({ comptabiliteGenerale: e.target.value })}
                sx={{ minWidth: 220, flex: '1 1 240px' }}
              />
              <FormField
                label="CP"
                required
                value={form.cp}
                disabled={!canWrite || busy}
                onChange={(e) => onChange({ cp: e.target.value })}
                sx={{ minWidth: 160, flex: '1 1 180px' }}
              />
              <FormField
                label="CPA"
                required
                value={form.cpa}
                disabled={!canWrite || busy}
                onChange={(e) => onChange({ cpa: e.target.value })}
                sx={{ minWidth: 160, flex: '1 1 180px' }}
              />
              <FormField
                label="Reçu institutionnel"
                required
                value={form.recuInstitutionnel}
                disabled={!canWrite || busy}
                onChange={(e) => onChange({ recuInstitutionnel: e.target.value })}
                sx={{ minWidth: 260, flex: '1 1 280px' }}
              />
            </>
          )}

          {isBon && (
            <>
              <FormField
                label="Compte général"
                required
                value={form.compteGeneral}
                disabled={!canWrite || busy}
                onChange={(e) => onChange({ compteGeneral: e.target.value })}
                sx={{ minWidth: 200, flex: '1 1 220px' }}
              />
              <FormField
                label="Compte particulier"
                required
                value={form.compteParticulier}
                disabled={!canWrite || busy}
                onChange={(e) => onChange({ compteParticulier: e.target.value })}
                sx={{ minWidth: 200, flex: '1 1 220px' }}
              />
              <FormField
                label="Reçu institutionnel"
                required
                value={form.recuInstitutionnel}
                disabled={!canWrite || busy}
                onChange={(e) => onChange({ recuInstitutionnel: e.target.value })}
                sx={{ minWidth: 260, flex: '1 1 280px' }}
              />
            </>
          )}

          {isMinute && (
            <>
              <FormField
                label="Compte général"
                required
                value={form.compteGeneral}
                disabled={!canWrite || busy}
                onChange={(e) => onChange({ compteGeneral: e.target.value })}
                sx={{ minWidth: 200, flex: '1 1 220px' }}
              />
              <FormField
                label="CP/CA"
                required
                value={form.cpCa}
                disabled={!canWrite || busy}
                onChange={(e) => onChange({ cpCa: e.target.value })}
                sx={{ minWidth: 160, flex: '1 1 180px' }}
              />
              <FormField
                label="L/S"
                required
                value={form.ls}
                disabled={!canWrite || busy}
                onChange={(e) => onChange({ ls: e.target.value })}
                sx={{ minWidth: 160, flex: '1 1 180px' }}
              />
              <FormField
                label="Suivi extra-comptable"
                required
                value={form.suiviExtraComptable}
                disabled={!canWrite || busy}
                onChange={(e) => onChange({ suiviExtraComptable: e.target.value })}
                sx={{ minWidth: 220, flex: '1 1 240px' }}
              />
              <AmountField
                label="Montant suivi extra-comptable"
                optional
                value={form.montantSuiviExtraComptable}
                disabled={!canWrite || busy}
                onChange={(value) => onChange({ montantSuiviExtraComptable: value })}
                currency={null}
                sx={{ minWidth: 220, flex: '1 1 240px' }}
              />
            </>
          )}

          <FormField
            label="N° d'appariement"
            required
            value={form.numeroAppariement}
            disabled={!canWrite || busy}
            onChange={(e) => onChange({ numeroAppariement: e.target.value })}
            sx={{ minWidth: 200, flex: '1 1 220px' }}
          />
        </FormGrid>
      </Paper>

      {canWrite && (
        <Box>
          <PrimaryButton disabled={busy} onClick={onSave}>
            Enregistrer
          </PrimaryButton>
        </Box>
      )}
    </Stack>
  );
}

export function ParametresInstrumentPaiementPage() {
  const { user } = useAuth();
  const msgBox = useMsgBox();
  const canWrite = canWriteReferentiels(user);

  const [tab, setTab] = useState<InstrumentPaiementType>('PIECE_CAISSE');
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [rows, setRows] = useState<ParametreInstrumentPaiementDto[]>([]);
  const [forms, setForms] = useState<Record<InstrumentPaiementType, ParametreInstrumentFormState>>(() => ({
    PIECE_CAISSE: emptyParametreForm('PIECE_CAISSE'),
    BON_PROVISOIRE: emptyParametreForm('BON_PROVISOIRE'),
    MINUTE_CHEQUE: emptyParametreForm('MINUTE_CHEQUE'),
  }));

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await fetchParametresInstrumentPaiement();
      setRows(data);
      setForms((prev) => {
        const next = { ...prev };
        for (const type of INSTRUMENT_PAIEMENT_TYPES) {
          const row = data.find((d) => d.typeInstrument === type);
          next[type] = row ? parametreDtoToForm(row) : emptyParametreForm(type);
        }
        return next;
      });
    } catch (err) {
      setError(apiErrorMessage(err, 'Impossible de charger le paramétrage.'));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const configuredByType = useMemo(() => {
    const map = {} as Record<InstrumentPaiementType, boolean>;
    for (const type of INSTRUMENT_PAIEMENT_TYPES) {
      map[type] = rows.find((r) => r.typeInstrument === type)?.estConfigure ?? false;
    }
    return map;
  }, [rows]);

  const handleSave = async (type: InstrumentPaiementType) => {
    setBusy(true);
    try {
      const saved = await upsertParametreInstrumentPaiement(type, formToUpsertPayload(forms[type]));
      setRows((prev) => {
        const others = prev.filter((r) => r.typeInstrument !== type);
        return [...others, saved];
      });
      setForms((prev) => ({ ...prev, [type]: parametreDtoToForm(saved) }));
      void msgBox.success('Paramétrage enregistré.');
    } catch (err) {
      void msgBox.error(apiErrorMessage(err, 'Enregistrement impossible.'));
    } finally {
      setBusy(false);
    }
  };

  if (loading) return <LoadingState label="Chargement du paramétrage…" />;
  if (error) return <ErrorState message={error} onRetry={() => void load()} />;

  return (
    <Box>
      <PageHeader
        title="Instruments de paiement"
        breadcrumbs={[
          { label: BRAND_NAME, to: '/dashboard' },
          { label: 'Référentiels' },
          { label: 'Instruments de paiement' },
        ]}
        subtitle="Paramétrage comptable des pièces de caisse, bons provisoires et minutes de chèque / O.P."
      />

      <Paper variant="outlined" sx={{ p: 2 }}>
        <Tabs
          value={tab}
          onChange={(_, value: InstrumentPaiementType) => setTab(value)}
          variant="scrollable"
          scrollButtons="auto"
          sx={{ mb: 2, borderBottom: 1, borderColor: 'divider' }}
        >
          {INSTRUMENT_PAIEMENT_TYPES.map((type) => (
            <Tab
              key={type}
              value={type}
              label={
                <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
                  <span>{INSTRUMENT_PAIEMENT_LABELS[type]}</span>
                  <ConfigStatusChip configured={configuredByType[type]} actif={forms[type].actif} />
                </Stack>
              }
            />
          ))}
        </Tabs>

        {INSTRUMENT_PAIEMENT_TYPES.map((type) =>
          tab === type ? (
            <InstrumentForm
              key={type}
              type={type}
              form={forms[type]}
              configured={configuredByType[type]}
              canWrite={canWrite}
              busy={busy}
              onChange={(patch) =>
                setForms((prev) => ({ ...prev, [type]: { ...prev[type], ...patch } }))
              }
              onSave={() => void handleSave(type)}
            />
          ) : null,
        )}
      </Paper>
    </Box>
  );
}
