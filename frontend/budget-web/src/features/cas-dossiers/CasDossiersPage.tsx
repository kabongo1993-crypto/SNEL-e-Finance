import AddIcon from '@mui/icons-material/Add';
import {
  Box,
  Button,
  Checkbox,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControlLabel,
  MenuItem,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
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
  SecondaryButton,
  useMsgBox,
  type DataTableColumn,
} from '../../components';
import {
  addCasDossierPiece,
  createCasDossier,
  fetchCasDossier,
  fetchCasDossiers,
  updateCasDossier,
  updateCasDossierPiece,
  type CasDossier,
  type CasDossierPieceObligatoire,
} from '../../services/apiClient';
import { BRAND_NAME } from '../../theme';
import { useAuth, canWriteReferentiels } from '../auth';

type CasRow = CasDossier & { id: string };
type FormMode = 'create' | 'edit';
type PieceFormMode = 'create' | 'edit';

function apiErrorMessage(err: unknown, fallback: string): string {
  if (err && typeof err === 'object' && 'response' in err) {
    const data = (err as { response?: { data?: { title?: string; detail?: string; message?: string } } })
      .response?.data;
    return data?.detail || data?.title || data?.message || fallback;
  }
  if (err instanceof Error) return err.message;
  return fallback;
}

export function CasDossiersPage() {
  const { user } = useAuth();
  const msgBox = useMsgBox();
  const canWrite = canWriteReferentiels(user);

  const [rows, setRows] = useState<CasRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [statutFilter, setStatutFilter] = useState('all');
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [detail, setDetail] = useState<CasDossier | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);

  const [dialogOpen, setDialogOpen] = useState(false);
  const [formMode, setFormMode] = useState<FormMode>('create');
  const [editingId, setEditingId] = useState<number | null>(null);
  const [code, setCode] = useState('');
  const [libelle, setLibelle] = useState('');
  const [ordre, setOrdre] = useState('1');
  const [actif, setActif] = useState(true);
  const [saving, setSaving] = useState(false);

  const [pieceDialogOpen, setPieceDialogOpen] = useState(false);
  const [pieceFormMode, setPieceFormMode] = useState<PieceFormMode>('create');
  const [editingPieceId, setEditingPieceId] = useState<number | null>(null);
  const [pieceCode, setPieceCode] = useState('');
  const [pieceLibelle, setPieceLibelle] = useState('');
  const [pieceOrdre, setPieceOrdre] = useState('1');
  const [pieceActif, setPieceActif] = useState(true);
  const [pieceObligatoire, setPieceObligatoire] = useState(true);
  const [pieceSaving, setPieceSaving] = useState(false);

  const loadList = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await fetchCasDossiers(false);
      setRows(data.map((d) => ({ ...d, id: String(d.idCasDossier) })));
    } catch {
      setError('Impossible de charger les cas de dossiers.');
    } finally {
      setLoading(false);
    }
  }, []);

  const loadDetail = useCallback(
    async (id: number) => {
      setDetailLoading(true);
      try {
        const data = await fetchCasDossier(id, false);
        setDetail(data);
      } catch {
        setDetail(null);
        void msgBox.error('Impossible de charger le détail du cas.');
      } finally {
        setDetailLoading(false);
      }
    },
    [msgBox],
  );

  useEffect(() => {
    void loadList();
  }, [loadList]);

  useEffect(() => {
    if (selectedId != null) void loadDetail(selectedId);
    else setDetail(null);
  }, [selectedId, loadDetail]);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return rows.filter((r) => {
      const matchesSearch =
        !q || r.code.toLowerCase().includes(q) || r.libelle.toLowerCase().includes(q);
      const matchesStatut =
        statutFilter === 'all' ||
        (statutFilter === 'actif' && r.actif) ||
        (statutFilter === 'inactif' && !r.actif);
      return matchesSearch && matchesStatut;
    });
  }, [rows, search, statutFilter]);

  const columns: DataTableColumn<CasRow>[] = [
    {
      id: 'code',
      label: 'Code',
      mobile: 'title',
      render: (r) => <Typography sx={{ fontWeight: 700 }}>{r.code}</Typography>,
    },
    { id: 'libelle', label: 'Libellé', mobile: 'subtitle', render: (r) => r.libelle },
    { id: 'ordre', label: 'Ordre', mobile: 'meta', render: (r) => r.ordre },
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
  ];

  const openCreate = () => {
    setFormMode('create');
    setEditingId(null);
    setCode('');
    setLibelle('');
    setOrdre(String((rows.length || 0) + 1));
    setActif(true);
    setDialogOpen(true);
  };

  const openEdit = (row: CasRow) => {
    setFormMode('edit');
    setEditingId(row.idCasDossier);
    setCode(row.code);
    setLibelle(row.libelle);
    setOrdre(String(row.ordre));
    setActif(row.actif);
    setDialogOpen(true);
  };

  const handleSaveCas = async () => {
    if (!canWrite) return;
    const ordreNum = Number(ordre);
    if (!libelle.trim() || !Number.isFinite(ordreNum) || ordreNum < 1) {
      void msgBox.error('Libellé et ordre (≥ 1) sont obligatoires.');
      return;
    }
    setSaving(true);
    try {
      if (formMode === 'create') {
        if (!code.trim()) {
          void msgBox.error('Le code est obligatoire.');
          return;
        }
        const created = await createCasDossier({
          code: code.trim(),
          libelle: libelle.trim(),
          ordre: ordreNum,
          actif,
        });
        void msgBox.success('Cas de dossier créé.');
        setSelectedId(created.idCasDossier);
      } else if (editingId != null) {
        await updateCasDossier(editingId, {
          libelle: libelle.trim(),
          ordre: ordreNum,
          actif,
        });
        void msgBox.success('Cas de dossier mis à jour.');
      }
      setDialogOpen(false);
      await loadList();
      if (selectedId != null) await loadDetail(selectedId);
    } catch (err) {
      void msgBox.error(apiErrorMessage(err, 'Enregistrement impossible.'));
    } finally {
      setSaving(false);
    }
  };

  const openCreatePiece = () => {
    if (!selectedId) return;
    setPieceFormMode('create');
    setEditingPieceId(null);
    setPieceCode('');
    setPieceLibelle('');
    setPieceOrdre(String((detail?.piecesObligatoires.length ?? 0) + 1));
    setPieceActif(true);
    setPieceObligatoire(true);
    setPieceDialogOpen(true);
  };

  const openEditPiece = (piece: CasDossierPieceObligatoire) => {
    setPieceFormMode('edit');
    setEditingPieceId(piece.idPieceObligatoire);
    setPieceCode(piece.codeTypePiece);
    setPieceLibelle(piece.libelle);
    setPieceOrdre(String(piece.ordre));
    setPieceActif(piece.actif);
    setPieceObligatoire(piece.obligatoire);
    setPieceDialogOpen(true);
  };

  const handlePieceActifChange = (checked: boolean) => {
    setPieceActif(checked);
    if (!checked) setPieceObligatoire(false);
  };

  const handleSavePiece = async () => {
    if (!canWrite || selectedId == null) return;
    const ordreNum = Number(pieceOrdre);
    if (!pieceLibelle.trim() || !Number.isFinite(ordreNum) || ordreNum < 1) {
      void msgBox.error('Libellé et ordre (≥ 1) sont obligatoires.');
      return;
    }
    if (!pieceActif && pieceObligatoire) {
      void msgBox.error('Une pièce désactivée ne peut pas être obligatoire.');
      return;
    }
    setPieceSaving(true);
    try {
      if (pieceFormMode === 'create') {
        if (!pieceCode.trim()) {
          void msgBox.error('Le code type pièce est obligatoire.');
          return;
        }
        await addCasDossierPiece(selectedId, {
          codeTypePiece: pieceCode.trim(),
          libelle: pieceLibelle.trim(),
          ordre: ordreNum,
          actif: pieceActif,
          obligatoire: pieceObligatoire,
        });
        void msgBox.success('Pièce ajoutée.');
      } else if (editingPieceId != null) {
        await updateCasDossierPiece(selectedId, editingPieceId, {
          libelle: pieceLibelle.trim(),
          ordre: ordreNum,
          actif: pieceActif,
          obligatoire: pieceObligatoire,
        });
        void msgBox.success('Pièce mise à jour.');
      }
      setPieceDialogOpen(false);
      await loadDetail(selectedId);
    } catch (err) {
      void msgBox.error(apiErrorMessage(err, 'Enregistrement de la pièce impossible.'));
    } finally {
      setPieceSaving(false);
    }
  };

  return (
    <Box>
      <PageHeader
        title="Cas de dossiers"
        subtitle="Configuration des cas de dossier et de leurs pièces justificatives."
        breadcrumbs={[
          { label: BRAND_NAME, to: '/dashboard' },
          { label: 'Référentiels' },
          { label: 'Cas de dossiers' },
        ]}
        actions={
          canWrite ? (
            <Button variant="contained" startIcon={<AddIcon />} onClick={openCreate}>
              Nouveau cas
            </Button>
          ) : undefined
        }
      />

      <FilterBar
        search={search}
        onSearchChange={setSearch}
        searchPlaceholder="Rechercher code, libellé…"
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

      {loading && <LoadingState />}
      {error && !loading && <ErrorState message={error} onRetry={() => void loadList()} />}

      {!loading && !error && (
        <Stack direction={{ xs: 'column', lg: 'row' }} spacing={2}>
          <Box sx={{ flex: 1, minWidth: 0 }}>
            <DataTable
              columns={columns}
              rows={filtered}
              onRowClick={(row) => setSelectedId(row.idCasDossier)}
              selectedRowId={selectedId != null ? String(selectedId) : null}
              emptyTitle="Aucun cas de dossier"
              actions={
                canWrite
                  ? [
                      {
                        id: 'modifier',
                        label: 'Modifier',
                        onClick: (row) => openEdit(row),
                      },
                    ]
                  : undefined
              }
            />
          </Box>

          <Paper variant="outlined" sx={{ flex: 1, minWidth: 0, p: 2 }}>
            {!selectedId ? (
              <Typography variant="body2" color="text.secondary">
                Sélectionnez un cas pour configurer ses pièces justificatives.
              </Typography>
            ) : detailLoading ? (
              <LoadingState label="Chargement du détail…" />
            ) : detail ? (
              <Stack spacing={2}>
                <Stack direction="row" sx={{ justifyContent: 'space-between', alignItems: 'center' }}>
                  <Box>
                    <Typography variant="h6">{detail.libelle}</Typography>
                    <Typography variant="caption" color="text.secondary">
                      {detail.code} · ordre {detail.ordre}
                    </Typography>
                  </Box>
                  {canWrite && (
                    <Button size="small" startIcon={<AddIcon />} onClick={openCreatePiece}>
                      Ajouter une pièce
                    </Button>
                  )}
                </Stack>

                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell>Pièce justificative</TableCell>
                      <TableCell align="center">Active</TableCell>
                      <TableCell align="center">Obligatoire</TableCell>
                      <TableCell align="center">Ordre</TableCell>
                      {canWrite && <TableCell align="right">Actions</TableCell>}
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {(detail.piecesObligatoires ?? []).map((p) => (
                      <TableRow key={p.idPieceObligatoire}>
                        <TableCell>
                          {p.libelle}
                          <Typography component="span" variant="caption" sx={{ display: 'block' }} color="text.secondary">
                            {p.codeTypePiece}
                          </Typography>
                        </TableCell>
                        <TableCell align="center">{p.actif ? '☑' : '☐'}</TableCell>
                        <TableCell align="center">{p.obligatoire ? '☑' : '☐'}</TableCell>
                        <TableCell align="center">{p.ordre}</TableCell>
                        {canWrite && (
                          <TableCell align="right">
                            <Button size="small" onClick={() => openEditPiece(p)}>
                              Modifier
                            </Button>
                          </TableCell>
                        )}
                      </TableRow>
                    ))}
                    {(detail.piecesObligatoires ?? []).length === 0 && (
                      <TableRow>
                        <TableCell colSpan={canWrite ? 5 : 4}>
                          <Typography variant="body2" color="text.secondary">
                            Aucune pièce configurée.
                          </Typography>
                        </TableCell>
                      </TableRow>
                    )}
                  </TableBody>
                </Table>
              </Stack>
            ) : null}
          </Paper>
        </Stack>
      )}

      <Dialog open={dialogOpen} onClose={() => setDialogOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>{formMode === 'create' ? 'Nouveau cas de dossier' : 'Modifier le cas'}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <TextField
              label="Code"
              value={code}
              onChange={(e) => setCode(e.target.value.toUpperCase())}
              disabled={formMode === 'edit'}
              required
              fullWidth
            />
            <TextField
              label="Libellé"
              value={libelle}
              onChange={(e) => setLibelle(e.target.value)}
              required
              fullWidth
            />
            <TextField
              label="Ordre"
              type="number"
              value={ordre}
              onChange={(e) => setOrdre(e.target.value)}
              required
              fullWidth
            />
            <FormControlLabel
              control={<Checkbox checked={actif} onChange={(e) => setActif(e.target.checked)} />}
              label="Actif"
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <SecondaryButton onClick={() => setDialogOpen(false)}>Annuler</SecondaryButton>
          <PrimaryButton onClick={() => void handleSaveCas()} disabled={saving}>
            Enregistrer
          </PrimaryButton>
        </DialogActions>
      </Dialog>

      <Dialog open={pieceDialogOpen} onClose={() => setPieceDialogOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>{pieceFormMode === 'create' ? 'Nouvelle pièce' : 'Modifier la pièce'}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <TextField
              label="Code type pièce"
              value={pieceCode}
              onChange={(e) => setPieceCode(e.target.value.toUpperCase())}
              disabled={pieceFormMode === 'edit'}
              required
              fullWidth
            />
            <TextField
              label="Libellé"
              value={pieceLibelle}
              onChange={(e) => setPieceLibelle(e.target.value)}
              required
              fullWidth
            />
            <TextField
              label="Ordre"
              type="number"
              value={pieceOrdre}
              onChange={(e) => setPieceOrdre(e.target.value)}
              required
              fullWidth
            />
            <FormControlLabel
              control={
                <Checkbox checked={pieceActif} onChange={(e) => handlePieceActifChange(e.target.checked)} />
              }
              label="Active"
            />
            <FormControlLabel
              control={
                <Checkbox
                  checked={pieceObligatoire}
                  onChange={(e) => setPieceObligatoire(e.target.checked)}
                  disabled={!pieceActif}
                />
              }
              label="Obligatoire"
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <SecondaryButton onClick={() => setPieceDialogOpen(false)}>Annuler</SecondaryButton>
          <PrimaryButton onClick={() => void handleSavePiece()} disabled={pieceSaving}>
            Enregistrer
          </PrimaryButton>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
