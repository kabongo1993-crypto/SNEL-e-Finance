import AddIcon from '@mui/icons-material/Add';
import FileUploadOutlinedIcon from '@mui/icons-material/FileUploadOutlined';
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
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
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
import { canWriteReferentiels, useAuth } from '../../auth';
import { extraireLignesCompteCategoriesExcel } from './compteCategorieExcelImport';
import { extraireLignesComptesExcel } from './compteExcelImport';
import { CompteCategoriesSection } from './CompteCategoriesSection';
import {
  CompteCategoriesImportPreviewDialog,
  CompteCategoriesImportResultDialog,
} from './CompteCategoriesImportDialog';
import { ComptesImportPreviewDialog, ComptesImportResultDialog } from './ComptesImportDialog';
import {
  createCompteFinancier,
  fetchBanques,
  fetchComptesFinanciers,
  fetchDevises,
  fetchDirections,
  fetchProvinces,
  fetchTypesComptes,
  fetchUtilisateursLookup,
  importCompteCategories,
  importComptes,
  previewImportCompteCategories,
  previewImportComptes,
  updateCompteFinancier,
  type BanqueDto,
  type CompteFinancierDto,
  type DirectionDto,
  type ImportCompteCategorieRawPayload,
  type ImportCompteCategoriesPreviewDto,
  type ImportCompteCategoriesResultDto,
  type ImportCompteRawPayload,
  type ImportComptesPreviewDto,
  type ImportComptesResultDto,
  type ProvinceDto,
  type TypeCompteDto,
} from './comptesService';
import { readFirstXlsxSheet } from '../banques/xlsxSheetReader';
import type { DeviseDto, UtilisateurLookup } from '../../../services/apiClient';

function apiErrorMessage(err: unknown, fallback: string): string {
  if (err && typeof err === 'object' && 'response' in err) {
    const data = (err as { response?: { data?: { title?: string; detail?: string; message?: string } } })
      .response?.data;
    return data?.detail || data?.title || data?.message || fallback;
  }
  if (err instanceof Error) return err.message;
  return fallback;
}

function formatDateFr(iso: string | null | undefined): string {
  if (!iso) return '—';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '—';
  return d.toLocaleDateString('fr-FR', { day: '2-digit', month: '2-digit', year: 'numeric' });
}

function utilisateurLabel(u: UtilisateurLookup): string {
  const nom = [u.prenom, u.nom].filter(Boolean).join(' ').trim();
  return nom ? `${nom} (${u.nomUtilisateur})` : u.nomUtilisateur;
}

type CompteRow = CompteFinancierDto & { id: string };
type FormMode = 'create' | 'edit';

export function ComptesPage() {
  const { user } = useAuth();
  const msgBox = useMsgBox();
  const canWrite = canWriteReferentiels(user);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const categoriesFileInputRef = useRef<HTMLInputElement>(null);

  const [rows, setRows] = useState<CompteRow[]>([]);
  const [banques, setBanques] = useState<BanqueDto[]>([]);
  const [directions, setDirections] = useState<DirectionDto[]>([]);
  const [types, setTypes] = useState<TypeCompteDto[]>([]);
  const [devises, setDevises] = useState<DeviseDto[]>([]);
  const [provinces, setProvinces] = useState<ProvinceDto[]>([]);
  const [utilisateurs, setUtilisateurs] = useState<UtilisateurLookup[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [banqueFilter, setBanqueFilter] = useState('all');
  const [typeFilter, setTypeFilter] = useState('all');
  const [deviseFilter, setDeviseFilter] = useState('all');
  const [provinceFilter, setProvinceFilter] = useState('all');
  const [directionFilter, setDirectionFilter] = useState('all');
  const [statutFilter, setStatutFilter] = useState('all');

  const [dialogOpen, setDialogOpen] = useState(false);
  const [detailOpen, setDetailOpen] = useState(false);
  const [detailRow, setDetailRow] = useState<CompteRow | null>(null);
  const [formMode, setFormMode] = useState<FormMode>('create');
  const [editingId, setEditingId] = useState<number | null>(null);
  const [numeroCompte, setNumeroCompte] = useState('');
  const [libelleCompte, setLibelleCompte] = useState('');
  const [idBanque, setIdBanque] = useState('');
  const [idDirection, setIdDirection] = useState('');
  const [codeTypeCompte, setCodeTypeCompte] = useState('');
  const [idDevise, setIdDevise] = useState('');
  const [idProvince, setIdProvince] = useState('');
  const [idUtilisateur, setIdUtilisateur] = useState('');
  const [actif, setActif] = useState(true);
  const [saving, setSaving] = useState(false);

  const [importPreview, setImportPreview] = useState<ImportComptesPreviewDto | null>(null);
  const [importRaw, setImportRaw] = useState<ImportCompteRawPayload[]>([]);
  const [importOpen, setImportOpen] = useState(false);
  const [importing, setImporting] = useState(false);
  const [importResult, setImportResult] = useState<ImportComptesResultDto | null>(null);
  const [resultOpen, setResultOpen] = useState(false);
  const [catImportPreview, setCatImportPreview] = useState<ImportCompteCategoriesPreviewDto | null>(null);
  const [catImportRaw, setCatImportRaw] = useState<ImportCompteCategorieRawPayload[]>([]);
  const [catImportOpen, setCatImportOpen] = useState(false);
  const [catImporting, setCatImporting] = useState(false);
  const [catImportResult, setCatImportResult] = useState<ImportCompteCategoriesResultDto | null>(null);
  const [catResultOpen, setCatResultOpen] = useState(false);

  const loadLookups = useCallback(async () => {
    const [banquesData, directionsData, typesData, devisesData, provincesData] = await Promise.all([
      fetchBanques(),
      fetchDirections(),
      fetchTypesComptes(),
      fetchDevises(false),
      fetchProvinces(),
    ]);
    setBanques(banquesData);
    setDirections(directionsData);
    setTypes(typesData);
    setDevises(devisesData);
    setProvinces(provincesData);
    try {
      setUtilisateurs(await fetchUtilisateursLookup());
    } catch {
      setUtilisateurs([]);
    }
  }, []);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const comptes = await fetchComptesFinanciers();
      setRows(comptes.map((c) => ({ ...c, id: String(c.idCompte) })));
    } catch (err) {
      setError(apiErrorMessage(err, 'Impossible de charger les comptes.'));
      setRows([]);
    } finally {
      setLoading(false);
    }
    void loadLookups();
  }, [loadLookups]);

  useEffect(() => {
    void load();
  }, [load]);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return rows.filter((r) => {
      const matchesSearch =
        !q ||
        r.numeroCompte.toLowerCase().includes(q) ||
        r.libelleCompte.toLowerCase().includes(q) ||
        r.banqueLibelle.toLowerCase().includes(q) ||
        r.idBanque.toLowerCase().includes(q) ||
        r.typeCompteLibelle.toLowerCase().includes(q) ||
        r.codeTypeCompte.toLowerCase().includes(q) ||
        r.deviseCode.toLowerCase().includes(q) ||
        (r.provinceLibelle ?? '').toLowerCase().includes(q) ||
        r.directionLibelle.toLowerCase().includes(q);
      return (
        matchesSearch &&
        (banqueFilter === 'all' || r.idBanque === banqueFilter) &&
        (typeFilter === 'all' || r.codeTypeCompte === typeFilter) &&
        (deviseFilter === 'all' || String(r.idDevise) === deviseFilter) &&
        (provinceFilter === 'all' || (r.idProvince ?? '') === provinceFilter) &&
        (directionFilter === 'all' || String(r.idDirection) === directionFilter) &&
        (statutFilter === 'all' ||
          (statutFilter === 'actif' && r.actif) ||
          (statutFilter === 'inactif' && !r.actif))
      );
    }).sort((a, b) => {
      const banque = a.idBanque.localeCompare(b.idBanque, 'fr', { sensitivity: 'base' });
      if (banque !== 0) return banque;
      return a.numeroCompte.localeCompare(b.numeroCompte, 'fr', { sensitivity: 'base' });
    });
  }, [rows, search, banqueFilter, typeFilter, deviseFilter, provinceFilter, directionFilter, statutFilter]);

  const banquesActives = useMemo(() => banques.filter((b) => b.actif), [banques]);
  const directionsActives = useMemo(() => directions.filter((d) => d.actif), [directions]);
  const typesActifs = useMemo(() => types.filter((t) => t.actif), [types]);
  const devisesActives = useMemo(() => devises.filter((d) => d.actif), [devises]);
  const provincesActives = useMemo(() => provinces.filter((p) => p.actif), [provinces]);

  const banqueOptions = useMemo(() => {
    const source =
      formMode === 'edit' && idBanque
        ? [...banquesActives, ...banques.filter((b) => !b.actif && b.idBanque === idBanque)]
        : banquesActives;
    return source.map((b) => ({
      value: b.idBanque,
      label: `${b.idBanque} — ${b.libelleBanque}`,
      disabled: !b.actif,
    }));
  }, [formMode, idBanque, banques, banquesActives]);

  const directionOptions = useMemo(() => {
    const source =
      formMode === 'edit' && idDirection
        ? [
            ...directionsActives,
            ...directions.filter((d) => !d.actif && String(d.idDirection) === idDirection),
          ]
        : directionsActives;
    return source.map((d) => ({
      value: String(d.idDirection),
      label: d.libelle,
      disabled: !d.actif,
    }));
  }, [formMode, idDirection, directions, directionsActives]);

  const typeOptions = useMemo(() => {
    const source =
      formMode === 'edit' && codeTypeCompte
        ? [...typesActifs, ...types.filter((t) => !t.actif && t.code === codeTypeCompte)]
        : typesActifs;
    return source.map((t) => ({
      value: t.code,
      label: `${t.code} — ${t.libelle}`,
      disabled: !t.actif,
    }));
  }, [formMode, codeTypeCompte, types, typesActifs]);

  const deviseOptions = useMemo(() => {
    const source =
      formMode === 'edit' && idDevise
        ? [...devisesActives, ...devises.filter((d) => !d.actif && String(d.idDevise) === idDevise)]
        : devisesActives;
    return source.map((d) => ({
      value: String(d.idDevise),
      label: `${d.code} — ${d.libelle}`,
      disabled: !d.actif,
    }));
  }, [formMode, idDevise, devises, devisesActives]);

  const provinceOptions = useMemo(() => {
    const source =
      formMode === 'edit' && idProvince
        ? [...provincesActives, ...provinces.filter((p) => !p.actif && p.idProvince === idProvince)]
        : provincesActives;
    return source.map((p) => ({
      value: p.idProvince,
      label: `${p.idProvince} — ${p.libelle}`,
      disabled: !p.actif,
    }));
  }, [formMode, idProvince, provinces, provincesActives]);

  const utilisateurOptions = useMemo(
    () =>
      utilisateurs.map((u) => ({
        value: String(u.idUtilisateur),
        label: utilisateurLabel(u),
        disabled: !u.actif,
      })),
    [utilisateurs],
  );

  const resetForm = () => {
    setNumeroCompte('');
    setLibelleCompte('');
    setIdBanque(banquesActives[0]?.idBanque ?? '');
    setIdDirection(directionsActives[0] ? String(directionsActives[0].idDirection) : '');
    setCodeTypeCompte(typesActifs[0]?.code ?? '');
    setIdDevise(devisesActives[0] ? String(devisesActives[0].idDevise) : '');
    setIdProvince('');
    setIdUtilisateur('');
    setActif(true);
  };

  const openCreate = () => {
    setFormMode('create');
    setEditingId(null);
    resetForm();
    setDialogOpen(true);
  };

  const openEdit = (row: CompteRow) => {
    setFormMode('edit');
    setEditingId(row.idCompte);
    setNumeroCompte(row.numeroCompte);
    setLibelleCompte(row.libelleCompte);
    setIdBanque(row.idBanque);
    setIdDirection(String(row.idDirection));
    setCodeTypeCompte(row.codeTypeCompte);
    setIdDevise(String(row.idDevise));
    setIdProvince(row.idProvince ?? '');
    setIdUtilisateur(row.idUtilisateur != null ? String(row.idUtilisateur) : '');
    setActif(row.actif);
    setDialogOpen(true);
  };

  const payload = () => ({
    numeroCompte: numeroCompte.trim(),
    libelleCompte: libelleCompte.trim(),
    idBanque,
    idDirection: Number(idDirection),
    codeTypeCompte,
    idDevise: Number(idDevise),
    idProvince: idProvince.trim() ? idProvince : null,
    idUtilisateur: idUtilisateur ? Number(idUtilisateur) : null,
    actif,
  });

  const save = async () => {
    if (!numeroCompte.trim()) {
      void msgBox.error('Le numéro de compte est obligatoire.');
      return;
    }
    if (!libelleCompte.trim()) {
      void msgBox.error('Le libellé est obligatoire.');
      return;
    }
    if (!idBanque || !idDirection || !codeTypeCompte || !idDevise) {
      void msgBox.error('Banque, direction, type de compte et devise sont obligatoires.');
      return;
    }
    setSaving(true);
    try {
      if (formMode === 'create') {
        await createCompteFinancier(payload());
        void msgBox.success('Compte créé.');
      } else if (editingId != null) {
        await updateCompteFinancier(editingId, payload());
        void msgBox.success('Compte mis à jour.');
      }
      setDialogOpen(false);
      await load();
    } catch (err) {
      void msgBox.error(apiErrorMessage(err, 'Enregistrement impossible.'));
    } finally {
      setSaving(false);
    }
  };

  const bodyFromRow = (row: CompteRow, nextActif: boolean) => ({
    numeroCompte: row.numeroCompte,
    libelleCompte: row.libelleCompte,
    idBanque: row.idBanque,
    idDirection: row.idDirection,
    codeTypeCompte: row.codeTypeCompte,
    idDevise: row.idDevise,
    idProvince: row.idProvince,
    idUtilisateur: row.idUtilisateur,
    actif: nextActif,
  });

  const runDesactiver = async (row: CompteRow) => {
    const ok = await msgBox.confirm({
      title: `Désactiver « ${row.numeroCompte} » ?`,
      message: 'Le compte restera dans le référentiel. Une date de clôture sera enregistrée. Aucune suppression physique n’est effectuée.',
      confirmLabel: 'Désactiver',
    });
    if (!ok) return;
    try {
      await updateCompteFinancier(row.idCompte, bodyFromRow(row, false));
      void msgBox.success(`Compte « ${row.numeroCompte} » désactivé.`);
      await load();
    } catch (err) {
      void msgBox.error(apiErrorMessage(err, 'Action impossible.'));
    }
  };

  const runActiver = async (row: CompteRow) => {
    const ok = await msgBox.confirm({
      title: `Réactiver « ${row.numeroCompte} » ?`,
      message: 'La date de clôture sera effacée et le compte sera de nouveau proposé.',
      confirmLabel: 'Activer',
    });
    if (!ok) return;
    try {
      await updateCompteFinancier(row.idCompte, bodyFromRow(row, true));
      void msgBox.success(`Compte « ${row.numeroCompte} » activé.`);
      await load();
    } catch (err) {
      void msgBox.error(apiErrorMessage(err, 'Action impossible.'));
    }
  };

  const onPickExcel = async (file: File | undefined) => {
    if (!file) return;
    try {
      const sheet = await readFirstXlsxSheet(file);
      const lignes = extraireLignesComptesExcel(sheet);
      const preview = await previewImportComptes(file.name, lignes);
      setImportRaw(lignes);
      setImportPreview(preview);
      setImportOpen(true);
    } catch (err) {
      void msgBox.error(apiErrorMessage(err, 'Analyse du fichier Excel impossible.'));
    } finally {
      if (fileInputRef.current) fileInputRef.current.value = '';
    }
  };

  const confirmImport = async () => {
    if (!importPreview || importRaw.length === 0) return;
    if (importPreview.resume.aImporter <= 0) return;
    const ok = await msgBox.confirm({
      title: 'Confirmer l’import',
      message: `Importer ${importPreview.resume.aImporter} compte${importPreview.resume.aImporter > 1 ? 's' : ''} ? Les doublons et erreurs ne seront pas enregistrés. Les identifiants historiques Id_Compte seront conservés s’ils sont libres.`,
      confirmLabel: 'Importer',
    });
    if (!ok) return;
    setImporting(true);
    try {
      const result = await importComptes(importPreview.nomFichier, importRaw);
      setImportOpen(false);
      setImportPreview(null);
      setImportRaw([]);
      setImportResult(result);
      setResultOpen(true);
      await load();
    } catch (err) {
      void msgBox.error(apiErrorMessage(err, 'Import impossible.'));
    } finally {
      setImporting(false);
    }
  };

  const onPickCategoriesExcel = async (file?: File) => {
    if (!file) return;
    try {
      const sheet = await readFirstXlsxSheet(file);
      const lignes = extraireLignesCompteCategoriesExcel(sheet);
      const preview = await previewImportCompteCategories(file.name, lignes);
      setCatImportRaw(lignes);
      setCatImportPreview(preview);
      setCatImportOpen(true);
    } catch (err) {
      void msgBox.error(apiErrorMessage(err, 'Analyse du fichier Excel impossible.'));
    } finally {
      if (categoriesFileInputRef.current) categoriesFileInputRef.current.value = '';
    }
  };

  const confirmCategoriesImport = async () => {
    if (!catImportPreview || catImportRaw.length === 0) return;
    if (catImportPreview.resume.aImporter <= 0) return;
    const ok = await msgBox.confirm({
      title: 'Confirmer l’import',
      message: `Importer ${catImportPreview.resume.aImporter} affectation${catImportPreview.resume.aImporter > 1 ? 's' : ''} ? Les doublons, conflits et erreurs ne seront pas enregistrés. Les identifiants historiques IDT_COMPTE_AVEC_CATEGORIE seront conservés s’ils sont libres. Les dates 1900-01-01 deviennent une affectation active.`,
      confirmLabel: 'Importer',
    });
    if (!ok) return;
    setCatImporting(true);
    try {
      const result = await importCompteCategories(catImportPreview.nomFichier, catImportRaw);
      setCatImportOpen(false);
      setCatImportPreview(null);
      setCatImportRaw([]);
      setCatImportResult(result);
      setCatResultOpen(true);
    } catch (err) {
      void msgBox.error(apiErrorMessage(err, 'Import impossible.'));
    } finally {
      setCatImporting(false);
    }
  };

  const columns: DataTableColumn<CompteRow>[] = [
    {
      id: 'numero',
      label: 'N° compte',
      mobile: 'title',
      sortable: true,
      sortValue: (r) => r.numeroCompte,
      render: (r) => (
        <Typography sx={{ fontWeight: 700, fontFamily: 'ui-monospace, monospace' }}>{r.numeroCompte}</Typography>
      ),
    },
    { id: 'libelle', label: 'Libellé', mobile: 'subtitle', render: (r) => r.libelleCompte },
    { id: 'type', label: 'Type', mobile: 'meta', render: (r) => r.codeTypeCompte },
    { id: 'devise', label: 'Devise', mobile: 'meta', render: (r) => r.deviseCode || '—' },
    { id: 'province', label: 'Province', mobile: 'meta', render: (r) => r.provinceLibelle || r.idProvince || '—' },
    { id: 'direction', label: 'Direction', mobile: 'meta', render: (r) => r.directionLibelle || '—' },
    { id: 'utilisateur', label: 'Utilisateur', mobile: 'meta', render: (r) => r.utilisateurLibelle || '—' },
    {
      id: 'actif',
      label: 'État',
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

  return (
    <Box>
      <PageHeader
        title="Comptes"
        subtitle="Référentiel des comptes financiers, affiché en rupture par banque."
        breadcrumbs={[
          { label: 'Trésorerie', to: '/tresorerie/dashboard' },
          { label: 'Référentiels' },
          { label: 'Comptes' },
        ]}
        actions={
          canWrite ? (
            <>
              <input
                ref={fileInputRef}
                type="file"
                accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                hidden
                onChange={(e) => void onPickExcel(e.target.files?.[0])}
              />
              <input
                ref={categoriesFileInputRef}
                type="file"
                accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                hidden
                onChange={(e) => void onPickCategoriesExcel(e.target.files?.[0])}
              />
              <SecondaryButton
                startIcon={<FileUploadOutlinedIcon />}
                onClick={() => fileInputRef.current?.click()}
              >
                Importer Excel
              </SecondaryButton>
              <SecondaryButton
                startIcon={<FileUploadOutlinedIcon />}
                onClick={() => categoriesFileInputRef.current?.click()}
              >
                Importer catégories
              </SecondaryButton>
              <Button variant="contained" startIcon={<AddIcon />} onClick={openCreate}>
                Nouveau compte
              </Button>
            </>
          ) : undefined
        }
      />

      <FilterBar
        search={search}
        onSearchChange={setSearch}
        searchPlaceholder="Rechercher n° compte, libellé, banque…"
        extra={
          <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 1 }}>
            <TextField
              select
              size="small"
              label="Banque"
              value={banqueFilter}
              onChange={(e) => setBanqueFilter(e.target.value)}
              sx={{ minWidth: 160 }}
            >
              <MenuItem value="all">Toutes</MenuItem>
              {banques.map((b) => (
                <MenuItem key={b.idBanque} value={b.idBanque}>
                  {b.idBanque}
                </MenuItem>
              ))}
            </TextField>
            <TextField
              select
              size="small"
              label="Type"
              value={typeFilter}
              onChange={(e) => setTypeFilter(e.target.value)}
              sx={{ minWidth: 140 }}
            >
              <MenuItem value="all">Tous</MenuItem>
              {types.map((t) => (
                <MenuItem key={t.code} value={t.code}>
                  {t.code}
                </MenuItem>
              ))}
            </TextField>
            <TextField
              select
              size="small"
              label="Devise"
              value={deviseFilter}
              onChange={(e) => setDeviseFilter(e.target.value)}
              sx={{ minWidth: 120 }}
            >
              <MenuItem value="all">Toutes</MenuItem>
              {devises.map((d) => (
                <MenuItem key={d.idDevise} value={String(d.idDevise)}>
                  {d.code}
                </MenuItem>
              ))}
            </TextField>
            <TextField
              select
              size="small"
              label="Province"
              value={provinceFilter}
              onChange={(e) => setProvinceFilter(e.target.value)}
              sx={{ minWidth: 140 }}
            >
              <MenuItem value="all">Toutes</MenuItem>
              {provinces.map((p) => (
                <MenuItem key={p.idProvince} value={p.idProvince}>
                  {p.idProvince}
                </MenuItem>
              ))}
            </TextField>
            <TextField
              select
              size="small"
              label="Direction"
              value={directionFilter}
              onChange={(e) => setDirectionFilter(e.target.value)}
              sx={{ minWidth: 160 }}
            >
              <MenuItem value="all">Toutes</MenuItem>
              {directions.map((d) => (
                <MenuItem key={d.idDirection} value={String(d.idDirection)}>
                  {d.libelle}
                </MenuItem>
              ))}
            </TextField>
            <TextField
              select
              size="small"
              label="État"
              value={statutFilter}
              onChange={(e) => setStatutFilter(e.target.value)}
              sx={{ minWidth: 130 }}
            >
              <MenuItem value="all">Tous</MenuItem>
              <MenuItem value="actif">Actifs</MenuItem>
              <MenuItem value="inactif">Inactifs</MenuItem>
            </TextField>
          </Box>
        }
      />

      {loading && <LoadingState label="Chargement des comptes…" />}
      {error && !loading && <ErrorState message={error} onRetry={() => void load()} />}
      {!loading && !error && (
        <DataTable
          columns={columns}
          rows={filtered}
          defaultRowsPerPage={25}
          groupBy={{
            key: (r) => r.idBanque,
            collapsible: true,
            label: (r, count) => (
              <Typography component="span" sx={{ fontWeight: 700 }}>
                {r.idBanque}
                {r.banqueLibelle && r.banqueLibelle !== r.idBanque ? ` — ${r.banqueLibelle}` : ''}
                <Typography component="span" sx={{ fontWeight: 500, color: 'text.secondary', ml: 1 }}>
                  {count} compte{count > 1 ? 's' : ''}
                </Typography>
              </Typography>
            ),
          }}
          countLabel={(n) => {
            const nbBanques = new Set(filtered.map((r) => r.idBanque)).size;
            return (
              <>
                <strong>{n}</strong> compte{n > 1 ? 's' : ''} · <strong>{nbBanques}</strong> banque
                {nbBanques > 1 ? 's' : ''}
              </>
            );
          }}
          emptyTitle="Aucun compte"
          emptyDescription={
            rows.length === 0
              ? 'Le référentiel ne contient aucun compte. Créez-en un ou importez le fichier Excel historique.'
              : 'Aucun compte ne correspond aux critères sélectionnés.'
          }
          actions={[
            {
              id: 'detail',
              label: 'Détail',
              onClick: (row) => {
                setDetailRow(row);
                setDetailOpen(true);
              },
            },
            ...(canWrite
              ? [
                  {
                    id: 'modifier',
                    label: 'Modifier',
                    onClick: (row: CompteRow) => openEdit(row),
                  },
                  {
                    id: 'desactiver',
                    label: 'Désactiver',
                    hidden: (row: CompteRow) => !row.actif,
                    onClick: (row: CompteRow) => void runDesactiver(row),
                  },
                  {
                    id: 'activer',
                    label: 'Activer',
                    hidden: (row: CompteRow) => row.actif,
                    onClick: (row: CompteRow) => void runActiver(row),
                  },
                ]
              : []),
          ]}
        />
      )}

      <Dialog open={dialogOpen} onClose={() => !saving && setDialogOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>{formMode === 'create' ? 'Nouveau compte' : 'Modifier le compte'}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <TextField
              label="Numéro de compte"
              value={numeroCompte}
              onChange={(e) => setNumeroCompte(e.target.value)}
              disabled={saving}
              required
              slotProps={{ htmlInput: { maxLength: 50 } }}
            />
            <TextField
              label="Libellé"
              value={libelleCompte}
              onChange={(e) => setLibelleCompte(e.target.value)}
              disabled={saving}
              required
              slotProps={{ htmlInput: { maxLength: 200 } }}
            />
            <SearchableSelect
              label="Banque"
              value={idBanque}
              onChange={setIdBanque}
              options={banqueOptions}
              required
              disabled={saving}
              fullWidth
              placeholder="Rechercher une banque…"
            />
            <SearchableSelect
              label="Direction"
              value={idDirection}
              onChange={setIdDirection}
              options={directionOptions}
              required
              disabled={saving}
              fullWidth
            />
            <SearchableSelect
              label="Type de compte"
              value={codeTypeCompte}
              onChange={setCodeTypeCompte}
              options={typeOptions}
              required
              disabled={saving}
              fullWidth
            />
            <SearchableSelect
              label="Devise"
              value={idDevise}
              onChange={setIdDevise}
              options={deviseOptions}
              required
              disabled={saving}
              fullWidth
            />
            <SearchableSelect
              label="Province"
              value={idProvince}
              onChange={setIdProvince}
              options={provinceOptions}
              optional
              allowEmpty
              emptyLabel="Aucune"
              disabled={saving}
              fullWidth
            />
            <SearchableSelect
              label="Utilisateur responsable"
              value={idUtilisateur}
              onChange={setIdUtilisateur}
              options={utilisateurOptions}
              optional
              allowEmpty
              emptyLabel="Aucun"
              disabled={saving}
              fullWidth
              helperText="Facultatif. L’affectation Trésorerie sera gérée ultérieurement."
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
              Un compte ne peut pas être supprimé : désactivez-le. L’identifiant technique n’est pas saisi à la
              création.
            </Typography>
          </Stack>
        </DialogContent>
        <DialogActions>
          <SecondaryButton onClick={() => setDialogOpen(false)} disabled={saving}>
            Annuler
          </SecondaryButton>
          <PrimaryButton onClick={() => void save()} disabled={saving}>
            Enregistrer
          </PrimaryButton>
        </DialogActions>
      </Dialog>

      <Dialog open={detailOpen} onClose={() => setDetailOpen(false)} fullWidth maxWidth="md">
        <DialogTitle>Détail du compte</DialogTitle>
        <DialogContent>
          {detailRow && (
            <Stack spacing={1.2} sx={{ mt: 1 }}>
              <Typography variant="body2">
                <strong>N° compte :</strong> {detailRow.numeroCompte}
              </Typography>
              <Typography variant="body2">
                <strong>Libellé :</strong> {detailRow.libelleCompte}
              </Typography>
              <Typography variant="body2">
                <strong>Banque :</strong> {detailRow.banqueLibelle} ({detailRow.idBanque})
              </Typography>
              <Typography variant="body2">
                <strong>Type :</strong> {detailRow.typeCompteLibelle} ({detailRow.codeTypeCompte})
              </Typography>
              <Typography variant="body2">
                <strong>Devise :</strong> {detailRow.deviseCode} — {detailRow.deviseLibelle}
              </Typography>
              <Typography variant="body2">
                <strong>Direction :</strong> {detailRow.directionLibelle}
              </Typography>
              <Typography variant="body2">
                <strong>Province :</strong> {detailRow.provinceLibelle || detailRow.idProvince || '—'}
              </Typography>
              <Typography variant="body2">
                <strong>Utilisateur :</strong> {detailRow.utilisateurLibelle || '—'}
              </Typography>
              <Typography variant="body2">
                <strong>État :</strong> {detailRow.actif ? 'Actif' : 'Inactif'}
              </Typography>
              <Typography variant="body2">
                <strong>Création :</strong> {formatDateFr(detailRow.dateCreation)}
              </Typography>
              <Typography variant="body2">
                <strong>Clôture :</strong> {formatDateFr(detailRow.dateCloture)}
              </Typography>
              <CompteCategoriesSection
                compteId={detailRow.idCompte}
                compteActif={detailRow.actif}
                canWrite={canWrite}
              />
            </Stack>
          )}
        </DialogContent>
        <DialogActions>
          <PrimaryButton onClick={() => setDetailOpen(false)}>Fermer</PrimaryButton>
        </DialogActions>
      </Dialog>

      <ComptesImportPreviewDialog
        open={importOpen}
        preview={importPreview}
        importing={importing}
        onClose={() => {
          setImportOpen(false);
          setImportPreview(null);
          setImportRaw([]);
        }}
        onConfirm={() => void confirmImport()}
      />
      <ComptesImportResultDialog
        open={resultOpen}
        result={importResult}
        onClose={() => {
          setResultOpen(false);
          setImportResult(null);
        }}
      />
      <CompteCategoriesImportPreviewDialog
        open={catImportOpen}
        preview={catImportPreview}
        importing={catImporting}
        onClose={() => {
          setCatImportOpen(false);
          setCatImportPreview(null);
          setCatImportRaw([]);
        }}
        onConfirm={() => void confirmCategoriesImport()}
      />
      <CompteCategoriesImportResultDialog
        open={catResultOpen}
        result={catImportResult}
        onClose={() => {
          setCatResultOpen(false);
          setCatImportResult(null);
        }}
      />
    </Box>
  );
}
