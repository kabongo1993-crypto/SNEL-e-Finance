import AddIcon from '@mui/icons-material/Add';
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
import { isAxiosError } from 'axios';
import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  DataTable,
  ErrorState,
  FilterBar,
  LoadingState,
  MasterDetailLayout,
  PageHeader,
  PrimaryButton,
  SearchableSelect,
  SecondaryButton,
  useMsgBox,
  type DataTableColumn,
} from '../../components';
import {
  createVersionBudgetaire,
  deleteVersionBudgetaire,
  fetchExercices,
  fetchUtilisateursLookup,
  fetchVersionsBudgetaires,
  updateVersionBudgetaire,
  type Exercice,
  type UtilisateurLookup,
  type VersionBudgetaire,
} from '../../services/apiClient';
import { BRAND_NAME } from '../../theme';
import { useAuth, hasPerm } from '../auth';
import { VersionsBudgetairesDetailPanel } from './VersionsBudgetairesDetailPanel';
import {
  STATUTS_VERSION,
  apiErrorMessage,
  extraireDateIso,
  formatDateFr,
  libelleUtilisateur,
} from './versionUtils';

type VersionRow = VersionBudgetaire & { id: string };
type FormMode = 'create' | 'edit';

function statutColor(statut: string): 'success' | 'warning' | 'info' | 'error' | 'default' {
  switch (statut.toUpperCase()) {
    case 'VALIDEE':
      return 'success';
    case 'SOUMISE':
      return 'info';
    case 'CONTROLEE':
      return 'warning';
    case 'REJETEE':
      return 'error';
    default:
      return 'default';
  }
}

export function VersionsBudgetairesPage() {
  const { user } = useAuth();
  const msgBox = useMsgBox();
  const canWrite = hasPerm(user, 'versions.ecrire');
  const [rows, setRows] = useState<VersionRow[]>([]);
  const [exercices, setExercices] = useState<Exercice[]>([]);
  const [utilisateurs, setUtilisateurs] = useState<UtilisateurLookup[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [exerciceFilter, setExerciceFilter] = useState('all');
  const [statutFilter, setStatutFilter] = useState('all');
  const [selectedId, setSelectedId] = useState<number | null>(null);

  const [dialogOpen, setDialogOpen] = useState(false);
  const [formMode, setFormMode] = useState<FormMode>('create');
  const [editingId, setEditingId] = useState<number | null>(null);
  const [idExercice, setIdExercice] = useState('');
  const [numeroVersion, setNumeroVersion] = useState('1');
  const [libelle, setLibelle] = useState('');
  const [idVersionPrecedente, setIdVersionPrecedente] = useState('');
  const [dateDebutEffet, setDateDebutEffet] = useState('');
  const [dateFinEffet, setDateFinEffet] = useState('');
  const [motif, setMotif] = useState('');
  const [idUtilisateurCreation, setIdUtilisateurCreation] = useState('');
  const [fieldErrors, setFieldErrors] = useState<{
    idExercice?: string;
    numeroVersion?: string;
    libelle?: string;
    dateDebutEffet?: string;
    dateFinEffet?: string;
    idUtilisateurCreation?: string;
  }>({});
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [versions, exos, users] = await Promise.all([
        fetchVersionsBudgetaires(),
        fetchExercices(),
        fetchUtilisateursLookup(),
      ]);
      setRows(versions.map((v) => ({ ...v, id: String(v.idVersion) })));
      setExercices(exos);
      setUtilisateurs(users);
    } catch {
      setError('Impossible de charger les versions budgétaires.');
      setRows([]);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return rows.filter((r) => {
      const matchesSearch =
        !q ||
        r.libelle.toLowerCase().includes(q) ||
        String(r.numeroVersion).includes(q) ||
        String(r.anneeExercice).includes(q) ||
        r.statut.toLowerCase().includes(q);
      const matchesExercice = exerciceFilter === 'all' || String(r.idExercice) === exerciceFilter;
      const matchesStatut = statutFilter === 'all' || r.statut.toUpperCase() === statutFilter;
      return matchesSearch && matchesExercice && matchesStatut;
    });
  }, [rows, search, exerciceFilter, statutFilter]);

  const selected = useMemo(
    () => rows.find((r) => r.idVersion === selectedId) ?? null,
    [rows, selectedId],
  );

  const parentOptions = useMemo(
    () =>
      rows.filter(
        (v) => String(v.idExercice) === idExercice && v.idVersion !== editingId,
      ),
    [rows, idExercice, editingId],
  );

  const hasActiveFilter = search.trim() !== '' || exerciceFilter !== 'all' || statutFilter !== 'all';

  const columns: DataTableColumn<VersionRow>[] = [
    {
      id: 'exercice',
      label: 'Exercice',
      sortable: true,
      sortValue: (r) => r.anneeExercice,
      mobile: 'title',
      render: (r) => (
        <Typography sx={{ fontWeight: 800 }}>
          {r.anneeExercice}
        </Typography>
      ),
    },
    {
      id: 'numero',
      label: 'N°',
      sortable: true,
      sortValue: (r) => r.numeroVersion,
      mobile: 'meta',
      render: (r) => r.numeroVersion,
    },
    {
      id: 'libelle',
      label: 'Libellé',
      sortable: true,
      sortValue: (r) => r.libelle,
      mobile: 'subtitle',
      render: (r) => r.libelle,
    },
    {
      id: 'statut',
      label: 'Statut',
      sortable: true,
      sortValue: (r) => r.statut,
      mobile: 'meta',
      render: (r) => (
        <Chip
          size="small"
          label={r.statut}
          color={statutColor(r.statut)}
          variant="outlined"
          sx={{ fontWeight: 700 }}
        />
      ),
    },
    {
      id: 'debut',
      label: 'Début d’effet',
      sortable: true,
      sortValue: (r) => r.dateDebutEffet,
      mobile: 'meta',
      render: (r) => formatDateFr(r.dateDebutEffet),
    },
  ];

  const openCreate = () => {
    const exo = exercices[0];
    const sameExo = rows.filter((r) => exo && r.idExercice === exo.idExercice);
    const nextNumero = sameExo.length === 0 ? 1 : Math.max(...sameExo.map((r) => r.numeroVersion)) + 1;
    setFormMode('create');
    setEditingId(null);
    setIdExercice(exo ? String(exo.idExercice) : '');
    setNumeroVersion(String(nextNumero));
    setLibelle('');
    setIdVersionPrecedente('');
    setDateDebutEffet(exo?.dateOuverture ? extraireDateIso(exo.dateOuverture) : `${exo?.annee ?? new Date().getFullYear()}-01-01`);
    setDateFinEffet('');
    setMotif('');
    setIdUtilisateurCreation(utilisateurs[0] ? String(utilisateurs[0].idUtilisateur) : '');
    setFieldErrors({});
    setDialogOpen(true);
  };

  const openEdit = (row: VersionRow) => {
    setFormMode('edit');
    setEditingId(row.idVersion);
    setIdExercice(String(row.idExercice));
    setNumeroVersion(String(row.numeroVersion));
    setLibelle(row.libelle);
    setIdVersionPrecedente(row.idVersionPrecedente != null ? String(row.idVersionPrecedente) : '');
    setDateDebutEffet(extraireDateIso(row.dateDebutEffet));
    setDateFinEffet(extraireDateIso(row.dateFinEffet));
    setMotif(row.motif ?? '');
    setIdUtilisateurCreation(String(row.idUtilisateurCreation));
    setFieldErrors({});
    setDialogOpen(true);
  };

  const validate = () => {
    const next: typeof fieldErrors = {};
    if (!idExercice) next.idExercice = 'L’exercice est obligatoire.';
    if (!numeroVersion.trim() || Number(numeroVersion) <= 0) {
      next.numeroVersion = 'Le numéro de version doit être supérieur à 0.';
    }
    if (!libelle.trim()) next.libelle = 'Le libellé est obligatoire.';
    if (!dateDebutEffet) next.dateDebutEffet = 'La date de début d’effet est obligatoire.';
    if (dateDebutEffet && dateFinEffet && dateFinEffet < dateDebutEffet) {
      next.dateFinEffet = 'La date de fin d’effet ne peut pas être antérieure au début d’effet.';
    }
    if (!idUtilisateurCreation) {
      next.idUtilisateurCreation = 'L’utilisateur de création est obligatoire.';
    }
    setFieldErrors(next);
    return Object.keys(next).length === 0;
  };

  const handleSave = async () => {
    if (!validate()) return;

    setSaving(true);
    try {
      const payload = {
        idExercice: Number(idExercice),
        numeroVersion: Number(numeroVersion),
        libelle: libelle.trim(),
        idVersionPrecedente: idVersionPrecedente ? Number(idVersionPrecedente) : null,
        dateDebutEffet,
        dateFinEffet: dateFinEffet || null,
        motif: motif.trim() || null,
        idUtilisateurCreation: Number(idUtilisateurCreation),
      };
      if (formMode === 'edit' && editingId != null) {
        const updated = await updateVersionBudgetaire(editingId, payload);
        void msgBox.success('Version budgétaire modifiée avec succès.');
        setSelectedId(updated.idVersion);
      } else {
        const created = await createVersionBudgetaire({ ...payload, statut: 'BROUILLON' });
        void msgBox.success('Version budgétaire créée avec succès (BROUILLON).');
        setSelectedId(created.idVersion);
      }
      setDialogOpen(false);
      await load();
    } catch (err) {
      void msgBox.error(
        isAxiosError(err)
          ? apiErrorMessage(err, 'Impossible d’enregistrer la version budgétaire.')
          : 'Impossible d’enregistrer la version budgétaire.',
      );
    } finally {
      setSaving(false);
    }
  };

  const runDelete = async (row: VersionRow) => {
    const ok = await msgBox.confirm({
      title: `Supprimer « ${row.libelle} » ?`,
      message: 'Cette action est définitive.',
      danger: true,
      confirmLabel: 'Supprimer',
    });
    if (!ok) return;
    try {
      await deleteVersionBudgetaire(row.idVersion);
      void msgBox.success(`Version « ${row.libelle} » supprimée.`);
      setSelectedId((current) => (current === row.idVersion ? null : current));
      await load();
    } catch (err) {
      void msgBox.error(
        isAxiosError(err)
          ? apiErrorMessage(err, 'Impossible de supprimer la version.')
          : 'Impossible de supprimer la version.',
      );
    }
  };

  const emptyTitle = hasActiveFilter
    ? 'Aucune version ne correspond à votre recherche.'
    : 'Aucune version budgétaire trouvée.';
  const emptyDescription = hasActiveFilter
    ? 'Modifiez les critères de recherche ou les filtres.'
    : 'Créez une version budgétaire pour commencer.';

  return (
    <Box>
      <PageHeader
        title="Versions budgétaires"
        subtitle="Versions d’un exercice budgétaire — données réelles BD_SNEL."
        breadcrumbs={[
          { label: BRAND_NAME, to: '/dashboard' },
          { label: 'Budget', to: '/budget' },
          { label: 'Versions budgétaires' },
        ]}
        actions={
          canWrite ? (
            <PrimaryButton startIcon={<AddIcon />} onClick={openCreate}>
              Nouvelle version budgétaire
            </PrimaryButton>
          ) : undefined
        }
      />

      <FilterBar
        search={search}
        onSearchChange={setSearch}
        searchPlaceholder="Rechercher par exercice, numéro, libellé ou statut…"
        extra={
          <>
            <SearchableSelect
              label="Exercice"
              value={exerciceFilter}
              onChange={setExerciceFilter}
              allowEmpty
              density="sm"
              options={[
                { value: 'all', label: 'Tous' },
                ...exercices.map((e) => ({
                  value: String(e.idExercice),
                  label: String(e.annee),
                })),
              ]}
            />
            <TextField select size="small"
              label="Statut"
              value={statutFilter}
              onChange={(e) => setStatutFilter(e.target.value)}
              sx={{ minWidth: 160 }}
            >
              <MenuItem value="all">Tous</MenuItem>
              {STATUTS_VERSION.map((s) => (
                <MenuItem key={s} value={s}>
                  {s}
                </MenuItem>
              ))}
            </TextField>
          </>
        }
      />

      {loading ? (
        <LoadingState label="Chargement des versions budgétaires…" />
      ) : error ? (
        <ErrorState message={error} onRetry={() => void load()} />
      ) : (
        <MasterDetailLayout
          detailOpen={selectedId != null}
          detailTitle="Détails de la version"
          onCloseDetail={() => setSelectedId(null)}
          detail={<VersionsBudgetairesDetailPanel selected={selected} />}
          master={
            <DataTable
              columns={columns}
              rows={filtered}
              emptyTitle={emptyTitle}
              emptyDescription={emptyDescription}
              selectedRowId={selectedId != null ? String(selectedId) : null}
              onRowClick={(row) => setSelectedId(row.idVersion)}
              countLabel={(count) => (
                <>
                  <strong>{count}</strong> version{count > 1 ? 's' : ''}
                </>
              )}
              actions={[
                {
                  id: 'voir',
                  label: 'Voir',
                  onClick: (row) => setSelectedId(row.idVersion),
                },
                {
                  id: 'modifier',
                  label: 'Modifier',
                  hidden: () => !canWrite,
                  onClick: (row) => openEdit(row),
                },
                {
                  id: 'supprimer',
                  label: 'Supprimer',
                  color: 'error',
                  hidden: () => !canWrite,
                  onClick: (row) => void runDelete(row),
                },
              ]}
            />
          }
        />
      )}

      <Dialog open={dialogOpen} onClose={() => !saving && setDialogOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>
          {formMode === 'edit' ? 'Modifier la version budgétaire' : 'Nouvelle version budgétaire'}
        </DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            {utilisateurs.length === 0 && (
              <Alert severity="warning">
                Aucun utilisateur n’existe dans BD_SNEL. La création d’une version exige un utilisateur
                de création (colonne obligatoire FK_UtilisateurCreation).
              </Alert>
            )}
            <SearchableSelect
              required
              label="Exercice"
              value={idExercice}
              onChange={(v) => {
                setIdExercice(v);
                setIdVersionPrecedente('');
              }}
              error={Boolean(fieldErrors.idExercice)}
              helperText={fieldErrors.idExercice}
              disabled={saving}
              allowEmpty
              options={[
                { value: '', label: 'Sélectionner un exercice' },
                ...exercices.map((e) => ({
                  value: String(e.idExercice),
                  label: `${e.annee} — ${e.statut}`,
                })),
              ]}
              placeholder="Rechercher un exercice…"
              fullWidth
            />
            <TextField
              required
              type="number"
              label="Numéro de version"
              value={numeroVersion}
              onChange={(e) => setNumeroVersion(e.target.value)}
              error={Boolean(fieldErrors.numeroVersion)}
              helperText={fieldErrors.numeroVersion}
              disabled={saving}
              slotProps={{ htmlInput: { min: 1, step: 1 } }}
            />
            <TextField
              required
              label="Libellé"
              value={libelle}
              onChange={(e) => setLibelle(e.target.value)}
              error={Boolean(fieldErrors.libelle)}
              helperText={fieldErrors.libelle}
              disabled={saving}
              slotProps={{ htmlInput: { maxLength: 200 } }}
            />
            <SearchableSelect
              label="Version précédente"
              value={idVersionPrecedente}
              onChange={setIdVersionPrecedente}
              disabled={saving || !idExercice}
              allowEmpty
              options={[
                { value: '', label: 'Aucune' },
                ...parentOptions.map((v) => ({
                  value: String(v.idVersion),
                  label: `n° ${v.numeroVersion} — ${v.libelle}`,
                })),
              ]}
              helperText="Optionnel — versions du même exercice."
              placeholder="Rechercher une version…"
              fullWidth
            />
            {formMode === 'edit' && editingId != null && (
              <Alert severity="info" sx={{ py: 0.5 }}>
                Statut géré par le workflow (Soumettre / Contrôler / Valider / Rejeter) — non modifiable ici.
                {rows.find((r) => r.idVersion === editingId)?.statut
                  ? ` Statut actuel : ${rows.find((r) => r.idVersion === editingId)?.statut}.`
                  : ''}
              </Alert>
            )}
            <TextField
              required
              type="date"
              label="Date de début d’effet"
              value={dateDebutEffet}
              onChange={(e) => setDateDebutEffet(e.target.value)}
              error={Boolean(fieldErrors.dateDebutEffet)}
              helperText={fieldErrors.dateDebutEffet}
              disabled={saving}
              slotProps={{ inputLabel: { shrink: true } }}
            />
            <TextField
              type="date"
              label="Date de fin d’effet"
              value={dateFinEffet}
              onChange={(e) => setDateFinEffet(e.target.value)}
              error={Boolean(fieldErrors.dateFinEffet)}
              helperText={fieldErrors.dateFinEffet}
              disabled={saving}
              slotProps={{ inputLabel: { shrink: true } }}
            />
            <TextField
              label="Motif"
              value={motif}
              onChange={(e) => setMotif(e.target.value)}
              disabled={saving}
              multiline
              minRows={2}
              slotProps={{ htmlInput: { maxLength: 1000 } }}
            />
            <SearchableSelect
              required
              label="Utilisateur de création"
              value={idUtilisateurCreation}
              onChange={setIdUtilisateurCreation}
              error={Boolean(fieldErrors.idUtilisateurCreation)}
              helperText={fieldErrors.idUtilisateurCreation}
              disabled={saving || utilisateurs.length === 0}
              allowEmpty
              options={[
                { value: '', label: 'Sélectionner un utilisateur' },
                ...utilisateurs.map((u) => ({
                  value: String(u.idUtilisateur),
                  label: libelleUtilisateur(u.nomUtilisateur, u.nom, u.prenom),
                })),
              ]}
              placeholder="Rechercher un utilisateur…"
              fullWidth
            />
          </Stack>
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2 }}>
          <SecondaryButton onClick={() => setDialogOpen(false)} disabled={saving}>
            Annuler
          </SecondaryButton>
          <Button variant="contained" onClick={() => void handleSave()} disabled={saving}>
            {saving ? 'Enregistrement…' : formMode === 'edit' ? 'Enregistrer' : 'Créer'}
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
