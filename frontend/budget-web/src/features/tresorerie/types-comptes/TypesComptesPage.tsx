import AddIcon from '@mui/icons-material/Add';
import {
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
import {
  DataTable,
  ErrorState,
  FilterBar,
  LoadingState,
  PageHeader,
  PrimaryButton,
  SearchableSelect,
  SecondaryButton,
  useMsgBox,
  type DataTableColumn,
} from '../../../components';
import { fetchGroupesTypesComptes, type GroupeTypeCompteDto } from '../groupes-types-comptes/groupesTypesComptesService';
import { canWriteReferentiels, useAuth } from '../../auth';
import {
  createTypeCompte,
  fetchTypesComptes,
  updateTypeCompte,
  type TypeCompteDto,
} from './typesComptesService';

function apiErrorMessage(err: unknown, fallback: string): string {
  if (err && typeof err === 'object' && 'response' in err) {
    const data = (err as { response?: { data?: { title?: string; detail?: string; message?: string } } })
      .response?.data;
    return data?.detail || data?.title || data?.message || fallback;
  }
  if (err instanceof Error) return err.message;
  return fallback;
}

function formatDateTimeFr(iso: string | null | undefined): string {
  if (!iso) return '—';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '—';
  return d.toLocaleString('fr-FR', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

type TypeRow = TypeCompteDto & { id: string };
type FormMode = 'create' | 'edit';

export function TypesComptesPage() {
  const { user } = useAuth();
  const msgBox = useMsgBox();
  const canWrite = canWriteReferentiels(user);

  const [rows, setRows] = useState<TypeRow[]>([]);
  const [groupes, setGroupes] = useState<GroupeTypeCompteDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [statutFilter, setStatutFilter] = useState('all');

  const [dialogOpen, setDialogOpen] = useState(false);
  const [formMode, setFormMode] = useState<FormMode>('create');
  const [editingCode, setEditingCode] = useState<string | null>(null);
  const [code, setCode] = useState('');
  const [libelle, setLibelle] = useState('');
  const [idGroupe, setIdGroupe] = useState('');
  const [actif, setActif] = useState(true);
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [types, groupesData] = await Promise.all([
        fetchTypesComptes(),
        fetchGroupesTypesComptes(),
      ]);
      setRows(types.map((t) => ({ ...t, id: t.code })));
      setGroupes(groupesData);
    } catch (err) {
      setError(apiErrorMessage(err, 'Impossible de charger les types de comptes.'));
      setRows([]);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const groupesActifs = useMemo(() => groupes.filter((g) => g.actif), [groupes]);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return rows.filter((r) => {
      const matchesSearch =
        !q ||
        r.code.toLowerCase().includes(q) ||
        r.libelle.toLowerCase().includes(q) ||
        r.groupeLibelle.toLowerCase().includes(q);
      const matchesStatut =
        statutFilter === 'all' ||
        (statutFilter === 'actif' && r.actif) ||
        (statutFilter === 'inactif' && !r.actif);
      return matchesSearch && matchesStatut;
    });
  }, [rows, search, statutFilter]);

  const groupeOptions = useMemo(() => {
    const source =
      formMode === 'edit' && idGroupe
        ? [
            ...groupesActifs,
            ...groupes.filter(
              (g) => !g.actif && String(g.idGroupeTypeCompte) === idGroupe,
            ),
          ]
        : groupesActifs;
    const seen = new Set<number>();
    return source
      .filter((g) => {
        if (seen.has(g.idGroupeTypeCompte)) return false;
        seen.add(g.idGroupeTypeCompte);
        return true;
      })
      .map((g) => ({
        value: String(g.idGroupeTypeCompte),
        label: g.libelle,
        disabled: !g.actif,
      }));
  }, [formMode, idGroupe, groupes, groupesActifs]);

  const openCreate = () => {
    setFormMode('create');
    setEditingCode(null);
    setCode('');
    setLibelle('');
    setIdGroupe(groupesActifs[0] ? String(groupesActifs[0].idGroupeTypeCompte) : '');
    setActif(true);
    setDialogOpen(true);
  };

  const openEdit = (row: TypeRow) => {
    setFormMode('edit');
    setEditingCode(row.code);
    setCode(row.code);
    setLibelle(row.libelle);
    setIdGroupe(String(row.idGroupeTypeCompte));
    setActif(row.actif);
    setDialogOpen(true);
  };

  const save = async () => {
    if (!code.trim()) {
      void msgBox.error('Le code est obligatoire.');
      return;
    }
    if (!libelle.trim()) {
      void msgBox.error('Le libellé est obligatoire.');
      return;
    }
    const idGroupeNum = Number(idGroupe);
    if (!idGroupeNum) {
      void msgBox.error('Le groupe de types de comptes est obligatoire.');
      return;
    }
    setSaving(true);
    try {
      if (formMode === 'create') {
        await createTypeCompte({
          code: code.trim(),
          libelle: libelle.trim(),
          idGroupeTypeCompte: idGroupeNum,
          actif,
        });
        void msgBox.success('Type de compte créé.');
      } else if (editingCode != null) {
        await updateTypeCompte(editingCode, {
          code: code.trim(),
          libelle: libelle.trim(),
          idGroupeTypeCompte: idGroupeNum,
          actif,
        });
        void msgBox.success('Type de compte mis à jour.');
      }
      setDialogOpen(false);
      await load();
    } catch (err) {
      void msgBox.error(apiErrorMessage(err, 'Enregistrement impossible.'));
    } finally {
      setSaving(false);
    }
  };

  const runDesactiver = async (row: TypeRow) => {
    const ok = await msgBox.confirm({
      title: `Désactiver « ${row.libelle} » ?`,
      message: 'Le type restera dans le référentiel, mais sera marqué inactif.',
      confirmLabel: 'Désactiver',
    });
    if (!ok) return;
    try {
      await updateTypeCompte(row.code, {
        code: row.code,
        libelle: row.libelle,
        idGroupeTypeCompte: row.idGroupeTypeCompte,
        actif: false,
      });
      void msgBox.success(`Type « ${row.libelle} » désactivé.`);
      await load();
    } catch (err) {
      void msgBox.error(apiErrorMessage(err, 'Action impossible.'));
    }
  };

  const runActiver = async (row: TypeRow) => {
    const ok = await msgBox.confirm({
      title: `Activer « ${row.libelle} » ?`,
      message: 'Le type sera de nouveau proposé dans les écrans de paramétrage.',
      confirmLabel: 'Activer',
    });
    if (!ok) return;
    try {
      await updateTypeCompte(row.code, {
        code: row.code,
        libelle: row.libelle,
        idGroupeTypeCompte: row.idGroupeTypeCompte,
        actif: true,
      });
      void msgBox.success(`Type « ${row.libelle} » activé.`);
      await load();
    } catch (err) {
      void msgBox.error(apiErrorMessage(err, 'Action impossible.'));
    }
  };

  const columns: DataTableColumn<TypeRow>[] = [
    {
      id: 'code',
      label: 'Code',
      mobile: 'title',
      render: (r) => (
        <Typography sx={{ fontWeight: 700, fontFamily: 'ui-monospace, monospace' }}>{r.code}</Typography>
      ),
    },
    {
      id: 'libelle',
      label: 'Libellé',
      mobile: 'subtitle',
      render: (r) => r.libelle,
    },
    {
      id: 'groupe',
      label: 'Groupe',
      mobile: 'meta',
      render: (r) => r.groupeLibelle || '—',
    },
    {
      id: 'actif',
      label: 'Statut',
      mobile: 'meta',
      render: (r) => (
        <Chip
          size="small"
          label={r.actif ? 'Actif' : 'Inactif'}
          color={r.actif ? 'success' : 'default'}
          variant={r.actif ? 'outlined' : 'filled'}
        />
      ),
    },
    {
      id: 'dateCreation',
      label: 'Création',
      mobile: 'meta',
      render: (r) => formatDateTimeFr(r.dateCreation),
    },
  ];

  return (
    <Box>
      <PageHeader
        title="Types de comptes"
        subtitle="Types de comptes financiers rattachés à un groupe."
        breadcrumbs={[
          { label: 'Trésorerie', to: '/tresorerie/dashboard' },
          { label: 'Référentiels' },
          { label: 'Types de comptes' },
        ]}
        actions={
          canWrite ? (
            <Button variant="contained" startIcon={<AddIcon />} onClick={openCreate}>
              Nouveau type de compte
            </Button>
          ) : undefined
        }
      />

      <FilterBar
        search={search}
        onSearchChange={setSearch}
        searchPlaceholder="Rechercher code, libellé, groupe…"
        extra={
          <TextField
            select
            size="small"
            label="Statut"
            value={statutFilter}
            onChange={(e) => setStatutFilter(e.target.value)}
            sx={{ minWidth: 140 }}
          >
            <MenuItem value="all">Tous</MenuItem>
            <MenuItem value="actif">Actifs</MenuItem>
            <MenuItem value="inactif">Inactifs</MenuItem>
          </TextField>
        }
      />

      {loading && <LoadingState label="Chargement des types de comptes…" />}
      {error && !loading && <ErrorState message={error} onRetry={() => void load()} />}
      {!loading && !error && (
        <DataTable
          columns={columns}
          rows={filtered}
          emptyTitle="Aucun type de compte"
          emptyDescription={
            rows.length === 0
              ? 'Le référentiel ne contient aucun type de compte. Créez-en un pour commencer.'
              : 'Aucun type ne correspond aux critères sélectionnés.'
          }
          actions={
            canWrite
              ? [
                  {
                    id: 'modifier',
                    label: 'Modifier',
                    onClick: (row) => openEdit(row),
                  },
                  {
                    id: 'desactiver',
                    label: 'Désactiver',
                    hidden: (row) => !row.actif,
                    onClick: (row) => void runDesactiver(row),
                  },
                  {
                    id: 'activer',
                    label: 'Activer',
                    hidden: (row) => row.actif,
                    onClick: (row) => void runActiver(row),
                  },
                ]
              : undefined
          }
        />
      )}

      <Dialog open={dialogOpen} onClose={() => !saving && setDialogOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>
          {formMode === 'create' ? 'Nouveau type de compte' : 'Modifier le type de compte'}
        </DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <TextField
              label="Code"
              value={code}
              onChange={(e) => setCode(e.target.value)}
              disabled={saving}
              required
              helperText={
                formMode === 'edit'
                  ? 'Identifiant métier. Modifiable uniquement si aucun compte financier ne l’utilise.'
                  : 'Identifiant métier unique (ex. FCT, CAISSE).'
              }
              slotProps={{ htmlInput: { maxLength: 20 } }}
            />
            <TextField
              label="Libellé"
              value={libelle}
              onChange={(e) => setLibelle(e.target.value)}
              disabled={saving}
              required
              slotProps={{ htmlInput: { maxLength: 200 } }}
            />
            <SearchableSelect
              label="Groupe de type de compte"
              value={idGroupe}
              onChange={setIdGroupe}
              options={groupeOptions}
              required
              disabled={saving || groupeOptions.length === 0}
              placeholder="Rechercher un groupe…"
              noOptionsText="Aucun groupe disponible"
              helperText={
                groupeOptions.length === 0
                  ? 'Aucun groupe actif. Créez d’abord un groupe de types de comptes.'
                  : undefined
              }
              fullWidth
            />
            <TextField
              select
              label="Actif"
              value={actif ? '1' : '0'}
              onChange={(e) => setActif(e.target.value === '1')}
              disabled={saving}
            >
              <MenuItem value="1">Actif</MenuItem>
              <MenuItem value="0">Inactif</MenuItem>
            </TextField>
            <Typography variant="caption" color="text.secondary">
              Un type de compte ne peut pas être supprimé : désactivez-le pour qu’il ne soit plus
              proposé. Le groupe reste obligatoire.
            </Typography>
          </Stack>
        </DialogContent>
        <DialogActions>
          <SecondaryButton onClick={() => setDialogOpen(false)} disabled={saving}>
            Annuler
          </SecondaryButton>
          <PrimaryButton
            onClick={() => void save()}
            disabled={saving || groupeOptions.length === 0}
          >
            Enregistrer
          </PrimaryButton>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
