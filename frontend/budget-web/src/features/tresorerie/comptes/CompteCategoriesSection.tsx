import AddIcon from '@mui/icons-material/Add';
import {
  Box,
  Button,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  DataTable,
  ErrorState,
  LoadingState,
  PrimaryButton,
  SearchableSelect,
  SecondaryButton,
  useMsgBox,
  type DataTableColumn,
} from '../../../components';
import {
  cloturerCompteCategorie,
  createCompteCategorie,
  fetchCategoriesComptes,
  fetchCompteCategories,
  updateCompteCategorie,
  type CategorieCompteDto,
  type CompteCategorieDto,
} from './comptesService';

function apiErrorMessage(err: unknown, fallback: string): string {
  if (err && typeof err === 'object' && 'response' in err) {
    const data = (err as { response?: { data?: { title?: string; detail?: string; message?: string } } })
      .response?.data;
    return data?.detail || data?.title || data?.message || fallback;
  }
  if (err instanceof Error) return err.message;
  return fallback;
}

function formatDateOnlyFr(iso: string | null | undefined): string {
  if (!iso) return '—';
  const m = iso.match(/^(\d{4})-(\d{2})-(\d{2})/);
  if (m) return `${m[3]}/${m[2]}/${m[1]}`;
  return iso;
}

function todayIso(): string {
  const d = new Date();
  const mm = String(d.getMonth() + 1).padStart(2, '0');
  const dd = String(d.getDate()).padStart(2, '0');
  return `${d.getFullYear()}-${mm}-${dd}`;
}

type CatRow = CompteCategorieDto & { id: string };
type FormMode = 'create' | 'edit' | 'cloturer';

interface CompteCategoriesSectionProps {
  compteId: number;
  compteActif: boolean;
  canWrite: boolean;
}

export function CompteCategoriesSection({
  compteId,
  compteActif,
  canWrite,
}: CompteCategoriesSectionProps) {
  const msgBox = useMsgBox();
  const [rows, setRows] = useState<CatRow[]>([]);
  const [categories, setCategories] = useState<CategorieCompteDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [formMode, setFormMode] = useState<FormMode>('create');
  const [editingId, setEditingId] = useState<number | null>(null);
  const [categorieId, setCategorieId] = useState('');
  const [dateDebut, setDateDebut] = useState(todayIso());
  const [dateFin, setDateFin] = useState('');
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [affectations, cats] = await Promise.all([
        fetchCompteCategories(compteId),
        fetchCategoriesComptes(false),
      ]);
      setRows(affectations.map((r) => ({ ...r, id: String(r.idCompteCategorie) })));
      setCategories(cats);
    } catch (err) {
      setError(apiErrorMessage(err, 'Impossible de charger les catégories du compte.'));
    } finally {
      setLoading(false);
    }
  }, [compteId]);

  useEffect(() => {
    void load();
  }, [load]);

  const active = rows.find((r) => r.active) ?? null;

  const categorieOptions = useMemo(() => {
    const current = rows.find((r) => r.idCompteCategorie === editingId);
    return categories
      .filter((c) => c.actif || c.idCategorieCompte === current?.idCategorieCompte)
      .map((c) => ({
        value: String(c.idCategorieCompte),
        label: c.actif ? c.libelle : `${c.libelle} (inactive)`,
      }));
  }, [categories, editingId, rows]);

  const openCreate = () => {
    setFormMode('create');
    setEditingId(null);
    setCategorieId('');
    setDateDebut(todayIso());
    setDateFin('');
    setDialogOpen(true);
  };

  const openEdit = (row: CatRow) => {
    setFormMode('edit');
    setEditingId(row.idCompteCategorie);
    setCategorieId(String(row.idCategorieCompte));
    setDateDebut(row.dateDebut.slice(0, 10));
    setDateFin(row.dateFin ? row.dateFin.slice(0, 10) : '');
    setDialogOpen(true);
  };

  const openCloturer = (row: CatRow) => {
    setFormMode('cloturer');
    setEditingId(row.idCompteCategorie);
    setCategorieId(String(row.idCategorieCompte));
    setDateDebut(row.dateDebut.slice(0, 10));
    setDateFin(todayIso());
    setDialogOpen(true);
  };

  const save = async () => {
    if (!categorieId && formMode !== 'cloturer') {
      void msgBox.error('La catégorie est obligatoire.');
      return;
    }
    if (formMode !== 'cloturer' && !dateDebut) {
      void msgBox.error('La date de début est obligatoire.');
      return;
    }
    if (formMode === 'cloturer' && !dateFin) {
      void msgBox.error('La date de fin est obligatoire pour clôturer.');
      return;
    }
    if (dateFin && dateDebut && dateFin < dateDebut) {
      void msgBox.error('La date de fin doit être postérieure ou égale à la date de début.');
      return;
    }

    setSaving(true);
    try {
      if (formMode === 'create') {
        await createCompteCategorie(compteId, {
          categorieId: Number(categorieId),
          dateDebut,
          dateFin: dateFin || null,
        });
      } else if (formMode === 'edit' && editingId != null) {
        await updateCompteCategorie(compteId, editingId, {
          categorieId: Number(categorieId),
          dateDebut,
          dateFin: dateFin || null,
        });
      } else if (formMode === 'cloturer' && editingId != null) {
        await cloturerCompteCategorie(compteId, editingId, dateFin);
      }
      setDialogOpen(false);
      await load();
    } catch (err) {
      void msgBox.error(apiErrorMessage(err, 'Enregistrement impossible.'));
    } finally {
      setSaving(false);
    }
  };

  const columns: DataTableColumn<CatRow>[] = [
    { id: 'categorie', label: 'Catégorie', mobile: 'title', render: (r) => r.categorieLibelle },
    { id: 'debut', label: 'Début', mobile: 'meta', render: (r) => formatDateOnlyFr(r.dateDebut) },
    { id: 'fin', label: 'Fin', mobile: 'meta', render: (r) => formatDateOnlyFr(r.dateFin) },
    {
      id: 'etat',
      label: 'État',
      mobile: 'subtitle',
      render: (r) => (
        <Chip
          size="small"
          label={r.active ? 'Active' : 'Historique'}
          color={r.active ? 'success' : 'default'}
          variant={r.active ? 'outlined' : 'filled'}
        />
      ),
    },
  ];

  return (
    <Box sx={{ mt: 2 }}>
      <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
        Catégories du compte
      </Typography>
      {active ? (
        <Typography variant="body2" sx={{ mb: 1.5 }}>
          <strong>Catégorie actuelle :</strong> {active.categorieLibelle}
          {' · '}
          {formatDateOnlyFr(active.dateDebut)} → {formatDateOnlyFr(active.dateFin)}
        </Typography>
      ) : (
        <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>
          Aucune affectation active.
        </Typography>
      )}

      {canWrite && (
        <Button
          size="small"
          startIcon={<AddIcon />}
          onClick={openCreate}
          disabled={!compteActif}
          sx={{ mb: 1.5 }}
        >
          Ajouter une catégorie
        </Button>
      )}
      {canWrite && !compteActif && (
        <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1.5 }}>
          Le compte est inactif : une nouvelle affectation active n’est pas autorisée. L’historique
          reste consultable.
        </Typography>
      )}

      {loading && <LoadingState label="Chargement des catégories…" />}
      {error && !loading && <ErrorState message={error} onRetry={() => void load()} />}
      {!loading && !error && (
        <DataTable
          columns={columns}
          rows={rows}
          defaultRowsPerPage={10}
          emptyTitle="Aucune catégorie"
          emptyDescription="Aucune affectation n’est enregistrée pour ce compte."
          actions={
            canWrite
              ? [
                  {
                    id: 'modifier',
                    label: 'Modifier',
                    onClick: (row: CatRow) => openEdit(row),
                  },
                  {
                    id: 'cloturer',
                    label: 'Clôturer',
                    hidden: (row: CatRow) => !row.active,
                    onClick: (row: CatRow) => openCloturer(row),
                  },
                ]
              : []
          }
        />
      )}

      <Dialog open={dialogOpen} onClose={() => !saving && setDialogOpen(false)} fullWidth maxWidth="xs">
        <DialogTitle>
          {formMode === 'create'
            ? 'Ajouter une catégorie'
            : formMode === 'edit'
              ? 'Modifier l’affectation'
              : 'Clôturer l’affectation'}
        </DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            {formMode !== 'cloturer' && (
              <SearchableSelect
                label="Catégorie"
                value={categorieId}
                onChange={setCategorieId}
                options={categorieOptions}
                required
                disabled={saving}
                fullWidth
                placeholder="Rechercher une catégorie…"
              />
            )}
            <TextField
              label="Date de début"
              type="date"
              value={dateDebut}
              onChange={(e) => setDateDebut(e.target.value)}
              disabled={saving || formMode === 'cloturer'}
              required
              slotProps={{ inputLabel: { shrink: true } }}
            />
            <TextField
              label="Date de fin"
              type="date"
              value={dateFin}
              onChange={(e) => setDateFin(e.target.value)}
              disabled={saving}
              required={formMode === 'cloturer'}
              helperText={
                formMode === 'cloturer'
                  ? 'La ligne reste dans l’historique.'
                  : 'Vide = affectation actuellement active.'
              }
              slotProps={{ inputLabel: { shrink: true } }}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <SecondaryButton onClick={() => setDialogOpen(false)} disabled={saving}>
            Annuler
          </SecondaryButton>
          <PrimaryButton onClick={() => void save()} disabled={saving}>
            {formMode === 'cloturer' ? 'Clôturer' : 'Enregistrer'}
          </PrimaryButton>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
