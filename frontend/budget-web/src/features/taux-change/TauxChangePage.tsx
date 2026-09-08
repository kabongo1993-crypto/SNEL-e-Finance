import AddIcon from '@mui/icons-material/Add';
import EditOutlinedIcon from '@mui/icons-material/EditOutlined';
import {
  Alert,
  Box,
  Button,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  MenuItem,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { useLocation } from 'react-router-dom';
import {
  DataTable,
  ErrorState,
  FilterBar,
  LoadingState,
  PageHeader,
  PrimaryButton,
  SecondaryButton,
  AmountField,
  useMsgBox,
  type DataTableAction,
  type DataTableColumn,
} from '../../components';
import {
  createTauxChange,
  fetchDevises,
  fetchTauxChangeList,
  fetchTauxChangePaires,
  updateTauxChange,
  type DeviseDto,
  type PaireTauxChangeDto,
  type TauxChangeRemplacementPropose,
} from '../../services/apiClient';
import { BRAND_NAME } from '../../theme';
import { workspaceReferentielBreadcrumbs } from '../../layout/workspace';
import { useAuth, canWriteReferentiels } from '../auth';
import {
  buildApercuPaire,
  buildTauxChangeListQuery,
  formatDateFr,
  formatDateTimeFr,
  formatPaireLibelle,
  formatTaux,
  helperTextTauxReference,
  paireKeyFromCodes,
  parseCreatePayload,
  parseUpdatePayload,
  toTauxChangeRows,
  validateCreateTauxChangeForm,
  validateUpdateTauxChangeForm,
  type TauxChangeRow,
} from './tauxChangeUtils';

function apiErrorMessage(err: unknown, fallback: string): string {
  if (err && typeof err === 'object' && 'response' in err) {
    const data = (err as { response?: { data?: { title?: string; detail?: string; message?: string } } })
      .response?.data;
    return data?.detail || data?.title || data?.message || fallback;
  }
  if (err instanceof Error) return err.message;
  return fallback;
}

function extractRemplacementPropose(err: unknown): TauxChangeRemplacementPropose | null {
  if (!err || typeof err !== 'object' || !('response' in err)) return null;
  const data = (err as { response?: { status?: number; data?: {
    code?: string;
    message?: string;
    remplacement?: TauxChangeRemplacementPropose;
  } } }).response;
  if (data?.status !== 409) return null;
  if (data.data?.code === 'TAUX_REMPLACEMENT_REQUIS' && data.data.remplacement) {
    return data.data.remplacement;
  }
  return null;
}

function formatRemplacementConfirmMessage(r: TauxChangeRemplacementPropose): string {
  const mode =
    r.modePropose === 'CLOTURER_ET_CREER'
      ? 'Ce taux a déjà été utilisé. Il sera clôturé et le nouveau taux sera enregistré.'
      : "Ce taux n'a pas encore été utilisé. Il sera écrasé par le nouveau taux.";
  return (
    `Taux actuel ${r.deviseBase}/${r.deviseQuote} : 1 ${r.deviseBase} = ${formatTaux(r.tauxReferenceExistant)} ${r.deviseQuote}` +
    ` (effet ${formatDateFr(r.dateEffetExistante)}).\n\n${mode}\n\nConfirmez-vous l'enregistrement ?`
  );
}

export function TauxChangePage() {
  const { user } = useAuth();
  const location = useLocation();
  const msgBox = useMsgBox();
  const canWrite = canWriteReferentiels(user);

  const [rows, setRows] = useState<TauxChangeRow[]>([]);
  const [paires, setPaires] = useState<PaireTauxChangeDto[]>([]);
  const [devises, setDevises] = useState<DeviseDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [filterPaire, setFilterPaire] = useState('');
  const [filterStatut, setFilterStatut] = useState('all');
  const [filterDateMin, setFilterDateMin] = useState('');
  const [filterDateMax, setFilterDateMax] = useState('');

  const [dialogOpen, setDialogOpen] = useState(false);
  const [editRow, setEditRow] = useState<TauxChangeRow | null>(null);
  const [deviseBaseForm, setDeviseBaseForm] = useState('');
  const [deviseQuoteForm, setDeviseQuoteForm] = useState('');
  const [tauxReference, setTauxReference] = useState('');
  const [dateEffet, setDateEffet] = useState('');
  const [saving, setSaving] = useState(false);

  const deviseCodes = useMemo(
    () => devises.filter((d) => d.actif).map((d) => d.code),
    [devises],
  );

  const paireOptions = useMemo(
    () =>
      paires.map((p) => ({
        value: paireKeyFromCodes(p.deviseBase, p.deviseQuote),
        label: formatPaireLibelle(p.deviseBase, p.deviseQuote),
      })),
    [paires],
  );

  const apercu = useMemo(() => {
    if (!deviseBaseForm || !deviseQuoteForm) return null;
    return buildApercuPaire(
      Number(String(tauxReference).replace(',', '.')),
      deviseBaseForm,
      deviseQuoteForm,
    );
  }, [deviseBaseForm, deviseQuoteForm, tauxReference]);

  const loadDevises = useCallback(async () => {
    try {
      const data = await fetchDevises(true);
      setDevises(data);
    } catch {
      setDevises([]);
    }
  }, []);

  const loadPaires = useCallback(async () => {
    try {
      const data = await fetchTauxChangePaires();
      setPaires(data);
      if (data.length > 0) {
        setFilterPaire((prev) => prev || paireKeyFromCodes(data[0].deviseBase, data[0].deviseQuote));
      }
    } catch {
      setPaires([]);
    }
  }, []);

  const load = useCallback(async () => {
    if (!canWrite) return;
    setLoading(true);
    setError(null);
    try {
      const query = buildTauxChangeListQuery({
        paireKey: filterPaire,
        statut: filterStatut,
        dateEffetMin: filterDateMin,
        dateEffetMax: filterDateMax,
      });
      const data = await fetchTauxChangeList(query);
      setRows(toTauxChangeRows(data));
    } catch {
      setError('Impossible de charger les taux de change.');
      setRows([]);
    } finally {
      setLoading(false);
    }
  }, [canWrite, filterPaire, filterStatut, filterDateMin, filterDateMax]);

  useEffect(() => {
    void loadDevises();
    void loadPaires();
  }, [loadDevises, loadPaires]);

  useEffect(() => {
    void load();
  }, [load]);

  const openCreate = () => {
    setEditRow(null);
    const codes = devises.filter((d) => d.actif).map((d) => d.code);
    setDeviseBaseForm(codes[0] ?? '');
    setDeviseQuoteForm(codes.find((c) => c !== codes[0]) ?? codes[1] ?? '');
    setTauxReference('');
    setDateEffet(new Date().toISOString().slice(0, 10));
    setDialogOpen(true);
  };

  const openEdit = (row: TauxChangeRow) => {
    if (!row.estModifiable) {
      void msgBox.warning(
        'Ce taux a déjà été appliqué par une procédure (demande de paiement) et ne peut plus être modifié.',
      );
      return;
    }
    setEditRow(row);
    setDeviseBaseForm(row.deviseBase);
    setDeviseQuoteForm(row.deviseQuote);
    setTauxReference(String(row.tauxReference));
    setDateEffet(row.dateEffet.slice(0, 10));
    setDialogOpen(true);
  };

  const saveVersion = async () => {
    if (editRow) {
      const validationError = validateUpdateTauxChangeForm({ tauxReference, dateEffet });
      if (validationError) {
        void msgBox.error(validationError);
        return;
      }
      setSaving(true);
      try {
        await updateTauxChange(editRow.idTauxChange, parseUpdatePayload({ tauxReference, dateEffet }));
        void msgBox.success('Taux modifié.');
        setDialogOpen(false);
        setEditRow(null);
        await load();
      } catch (err) {
        void msgBox.error(apiErrorMessage(err, 'Modification impossible.'));
      } finally {
        setSaving(false);
      }
      return;
    }

    const validationError = validateCreateTauxChangeForm(
      { deviseBase: deviseBaseForm, deviseQuote: deviseQuoteForm, tauxReference, dateEffet },
      deviseCodes,
    );
    if (validationError) {
      void msgBox.error(validationError);
      return;
    }

    setSaving(true);
    try {
      const form = {
        deviseBase: deviseBaseForm,
        deviseQuote: deviseQuoteForm,
        tauxReference,
        dateEffet,
      };
      try {
        await createTauxChange(parseCreatePayload(form));
      } catch (err) {
        const remplacement = extractRemplacementPropose(err);
        if (!remplacement) throw err;

        const ok = await msgBox.confirm({
          title: 'Remplacer le taux existant ?',
          message: formatRemplacementConfirmMessage(remplacement),
          confirmLabel: remplacement.modePropose === 'CLOTURER_ET_CREER' ? 'Clôturer et enregistrer' : 'Écraser',
          danger: remplacement.modePropose === 'CLOTURER_ET_CREER',
        });
        if (!ok) return;

        await createTauxChange(parseCreatePayload(form, { confirmerRemplacement: true }));
      }

      void msgBox.success('Nouvelle version de taux enregistrée.');
      setDialogOpen(false);
      await loadPaires();
      await load();
    } catch (err) {
      void msgBox.error(apiErrorMessage(err, 'Enregistrement impossible.'));
    } finally {
      setSaving(false);
    }
  };

  const tableActions = useMemo<DataTableAction<TauxChangeRow>[]>(
    () => [
      {
        id: 'modifier',
        label: 'Modifier',
        icon: <EditOutlinedIcon fontSize="small" />,
        hidden: (row) => !row.estModifiable,
        onClick: (row) => openEdit(row),
      },
    ],
    [msgBox],
  );

  const columns: DataTableColumn<TauxChangeRow>[] = [
    {
      id: 'paire',
      label: 'Paire',
      mobile: 'title',
      render: (r) => (
        <Typography sx={{ fontWeight: 700 }}>
          {formatPaireLibelle(r.deviseBase, r.deviseQuote)}
        </Typography>
      ),
      sortValue: (r) => `${r.deviseBase}/${r.deviseQuote}`,
    },
    {
      id: 'taux',
      label: 'Taux de référence',
      align: 'right',
      mobile: 'subtitle',
      render: (r) => formatTaux(r.tauxReference),
      sortValue: (r) => r.tauxReference,
    },
    {
      id: 'dateEffet',
      label: "Date d'effet",
      mobile: 'meta',
      render: (r) => formatDateFr(r.dateEffet),
      sortValue: (r) => r.dateEffet,
    },
    {
      id: 'statut',
      label: 'Statut',
      mobile: 'meta',
      render: (r) => (
        <Chip
          size="small"
          label={r.statut === 'ACTIF' ? 'Actif' : 'Inactif'}
          color={r.statut === 'ACTIF' ? 'success' : 'default'}
          variant={r.statut === 'ACTIF' ? 'outlined' : 'filled'}
        />
      ),
      sortValue: (r) => r.statut,
    },
    {
      id: 'auteur',
      label: 'Créé par',
      mobile: 'meta',
      render: (r) => r.libelleUtilisateurCreation ?? '—',
    },
    {
      id: 'dateCreation',
      label: 'Date création',
      mobile: 'meta',
      render: (r) => formatDateTimeFr(r.dateCreation),
      sortValue: (r) => r.dateCreation,
    },
    {
      id: 'modif',
      label: 'Modifié par',
      mobile: 'meta',
      render: (r) => r.libelleUtilisateurModification ?? '—',
    },
    {
      id: 'dateModification',
      label: 'Date modification',
      mobile: 'meta',
      render: (r) => formatDateTimeFr(r.dateModification),
      sortValue: (r) => r.dateModification ?? '',
    },
  ];

  const isEmptyReferentiel = !loading && !error && rows.length === 0;

  if (!canWrite) {
    return (
      <Box sx={{ p: 2 }}>
        <Alert severity="warning">
          Accès réservé aux utilisateurs disposant de la permission{' '}
          <strong>referentiels.ecrire</strong>.
        </Alert>
      </Box>
    );
  }

  return (
    <Box>
      <PageHeader
        title="Taux de change"
        subtitle="Paire canonique unique — le taux inverse est calculé automatiquement par le système."
        breadcrumbs={workspaceReferentielBreadcrumbs(location.pathname, 'Taux de change', BRAND_NAME)}
        actions={
          <Button variant="contained" startIcon={<AddIcon />} onClick={openCreate} disabled={deviseCodes.length < 2}>
            Nouvelle version
          </Button>
        }
      />

      <FilterBar
        extra={
          <Stack direction={{ xs: 'column', md: 'row' }} spacing={1.5} sx={{ flexWrap: 'wrap' }}>
            <TextField
              select
              size="small"
              label="Paire de devises"
              value={filterPaire}
              onChange={(e) => setFilterPaire(e.target.value)}
              sx={{ minWidth: 180 }}
            >
              <MenuItem value="">Toutes</MenuItem>
              {paireOptions.map((o) => (
                <MenuItem key={o.value} value={o.value}>
                  {o.label}
                </MenuItem>
              ))}
            </TextField>
            <TextField
              select
              size="small"
              label="Statut"
              value={filterStatut}
              onChange={(e) => setFilterStatut(e.target.value)}
              sx={{ minWidth: 140 }}
            >
              <MenuItem value="all">Tous</MenuItem>
              <MenuItem value="ACTIF">Actif</MenuItem>
              <MenuItem value="INACTIF">Inactif</MenuItem>
            </TextField>
            <TextField
              size="small"
              label="Date effet min"
              type="date"
              value={filterDateMin}
              onChange={(e) => setFilterDateMin(e.target.value)}
              slotProps={{ inputLabel: { shrink: true } }}
              sx={{ minWidth: 160 }}
            />
            <TextField
              size="small"
              label="Date effet max"
              type="date"
              value={filterDateMax}
              onChange={(e) => setFilterDateMax(e.target.value)}
              slotProps={{ inputLabel: { shrink: true } }}
              sx={{ minWidth: 160 }}
            />
          </Stack>
        }
      />

      {loading && <LoadingState />}
      {error && !loading && <ErrorState message={error} onRetry={() => void load()} />}

      {!loading && !error && (
        <DataTable
          columns={columns}
          rows={rows}
          actions={tableActions}
          emptyTitle={isEmptyReferentiel ? 'Aucun taux de change configuré' : 'Aucun résultat'}
          emptyDescription={
            isEmptyReferentiel
              ? "Aucun taux n'est configuré pour les paires supportées. Utilisez « Nouvelle version »."
              : 'Aucune ligne ne correspond aux filtres sélectionnés.'
          }
        />
      )}

      <Dialog
        open={dialogOpen}
        onClose={() => !saving && setDialogOpen(false)}
        fullWidth
        maxWidth="sm"
      >
        <DialogTitle>{editRow ? 'Modifier le taux' : 'Nouvelle version de taux'}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            {editRow ? (
              <Alert severity="info">
                Modification autorisée uniquement tant qu&apos;aucune demande de paiement n&apos;a appliqué ce taux.
                Les devises et l&apos;orientation ne peuvent pas être changées.
              </Alert>
            ) : (
              <Typography variant="body2" color="text.secondary">
                Choisissez la devise de base et la devise cotée. Le taux saisi signifie{' '}
                <strong>1 devise de base = X devise cotée</strong> (ex. 1 EUR = 3 450 USD). L&apos;inverse est
                calculé automatiquement. Si un taux ACTIF existe déjà, le système affichera l&apos;ancien
                taux et demandera confirmation (écrasement ou clôture selon l&apos;usage).
              </Typography>
            )}
            <TextField
              select={!editRow}
              label="Devise de base"
              value={deviseBaseForm}
              onChange={(e) => setDeviseBaseForm(e.target.value)}
              disabled={saving || !!editRow}
              required
              fullWidth
            >
              {editRow ? (
                <MenuItem value={deviseBaseForm}>{deviseBaseForm}</MenuItem>
              ) : (
                deviseCodes.map((code) => (
                  <MenuItem key={code} value={code}>
                    {code}
                  </MenuItem>
                ))
              )}
            </TextField>
            <TextField
              select={!editRow}
              label="Devise cotée"
              value={deviseQuoteForm}
              onChange={(e) => setDeviseQuoteForm(e.target.value)}
              disabled={saving || !!editRow}
              required
              fullWidth
            >
              {editRow ? (
                <MenuItem value={deviseQuoteForm}>{deviseQuoteForm}</MenuItem>
              ) : (
                deviseCodes.map((code) => (
                  <MenuItem key={code} value={code}>
                    {code}
                  </MenuItem>
                ))
              )}
            </TextField>
            <AmountField
              label="Taux de référence"
              value={tauxReference}
              onChange={setTauxReference}
              disabled={saving}
              required
              helperText={helperTextTauxReference(deviseBaseForm, deviseQuoteForm)}
              fullWidth
              currency={null}
              maxDecimals={8}
            />
            <TextField
              label="Date d'effet"
              type="date"
              value={dateEffet}
              onChange={(e) => setDateEffet(e.target.value)}
              disabled={saving}
              required
              slotProps={{ inputLabel: { shrink: true } }}
              fullWidth
            />
            {apercu && (
              <Alert severity="info" sx={{ '& .MuiAlert-message': { width: '100%' } }}>
                <Stack spacing={0.5}>
                  <Typography variant="body2">{apercu.direct}</Typography>
                  <Typography variant="body2" color="text.secondary">
                    {apercu.inverse} (calculé, lecture seule)
                  </Typography>
                </Stack>
              </Alert>
            )}
          </Stack>
        </DialogContent>
        <DialogActions>
          <SecondaryButton onClick={() => setDialogOpen(false)} disabled={saving}>
            Annuler
          </SecondaryButton>
          <PrimaryButton onClick={() => void saveVersion()} disabled={saving}>
            {editRow ? 'Enregistrer les modifications' : 'Enregistrer'}
          </PrimaryButton>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
