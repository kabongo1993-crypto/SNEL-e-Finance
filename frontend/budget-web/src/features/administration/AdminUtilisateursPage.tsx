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
  FormControlLabel,
  MenuItem,
  Radio,
  RadioGroup,
  Stack,
  TextField,
  Typography,
  Checkbox,
  FormGroup,
  Divider,
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
} from '../../components';
import { useAuth } from '../auth';
import {
  createAdminUtilisateur,
  fetchAdminProfilsCatalogue,
  fetchAdminUtilisateur,
  fetchAdminUtilisateurs,
  fetchDepartements,
  fetchStructures,
  fetchUnitesBudgetaires,
  resetAdminUtilisateurMotDePasse,
  setAdminUtilisateurActif,
  updateAdminUtilisateur,
  type Departement,
  type PermissionCatalogueItem,
  type PerimetreUtilisateur,
  type ProfilCatalogueItem,
  type Structure,
  type UniteBudgetaire,
  type UtilisateurAdminListItem,
} from '../../services/apiClient';
import { BRAND_NAME } from '../../theme';
import { apiErrorMessage, formatDateTimeFr, hasAdminUtilisateurs } from './adminUtils';
import {
  DIRECTION_BUDGETS_CODES,
  IMPUTER_PERMISSIONS,
  JUNIOR_CODES,
  groupProfilsForForm,
  isPerimetreConfigure,
  libelleRole,
  permissionsHeriteesDesProfils,
} from './adminRolesConfig';

type Row = UtilisateurAdminListItem & { id: string };
type FormMode = 'create' | 'edit';

const emptyPerimetre = (): PerimetreUtilisateur => ({
  tousDepartements: false,
  toutesUnitesBudgetaires: false,
  idDepartements: [],
  idUnitesBudgetaires: [],
});

export function AdminUtilisateursPage() {
  const { user } = useAuth();
  const msgBox = useMsgBox();
  const canAdmin = hasAdminUtilisateurs(user);

  const [rows, setRows] = useState<Row[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [statutFilter, setStatutFilter] = useState('all');

  const [departements, setDepartements] = useState<Departement[]>([]);
  const [structures, setStructures] = useState<Structure[]>([]);
  const [ubs, setUbs] = useState<UniteBudgetaire[]>([]);
  const [profilsCatalogue, setProfilsCatalogue] = useState<ProfilCatalogueItem[]>([]);
  const [permissionsCatalogue, setPermissionsCatalogue] = useState<PermissionCatalogueItem[]>([]);

  const [dialogOpen, setDialogOpen] = useState(false);
  const [formMode, setFormMode] = useState<FormMode>('create');
  const [editingId, setEditingId] = useState<number | null>(null);
  const [saving, setSaving] = useState(false);

  const [matricule, setMatricule] = useState('');
  const [nom, setNom] = useState('');
  const [postnom, setPostnom] = useState('');
  const [prenom, setPrenom] = useState('');
  const [nomUtilisateur, setNomUtilisateur] = useState('');
  const [email, setEmail] = useState('');
  const [actif, setActif] = useState(true);
  const [motDePasse, setMotDePasse] = useState('');
  const [idStructure, setIdStructure] = useState('');
  const [idDeptPrincipal, setIdDeptPrincipal] = useState('');
  const [idService, setIdService] = useState('');
  const [profils, setProfils] = useState<string[]>([]);
  const [permissionsIndiv, setPermissionsIndiv] = useState<string[]>([]);
  const [perimetre, setPerimetre] = useState<PerimetreUtilisateur>(emptyPerimetre());

  const [resetOpen, setResetOpen] = useState(false);
  const [resetRow, setResetRow] = useState<Row | null>(null);
  const [resetPwd, setResetPwd] = useState('');
  const [resetBusy, setResetBusy] = useState(false);

  const load = useCallback(async () => {
    if (!canAdmin) {
      setLoading(false);
      setError('Permission insuffisante (`admin.utilisateurs`).');
      return;
    }
    setLoading(true);
    setError(null);
    try {
      const [users, deps, structs, unites, catalogue] = await Promise.all([
        fetchAdminUtilisateurs(),
        fetchDepartements(),
        fetchStructures(),
        fetchUnitesBudgetaires(),
        fetchAdminProfilsCatalogue(),
      ]);
      setRows(users.map((u) => ({ ...u, id: String(u.idUtilisateur) })));
      setDepartements(deps);
      setStructures(structs);
      setUbs(unites);
      setProfilsCatalogue(catalogue.profils);
      setPermissionsCatalogue(catalogue.permissions.filter((p) => p.complementaire));
    } catch (err) {
      setError(apiErrorMessage(err, 'Impossible de charger les utilisateurs.'));
      setRows([]);
    } finally {
      setLoading(false);
    }
  }, [canAdmin]);

  useEffect(() => {
    void load();
  }, [load]);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return rows.filter((r) => {
      const rolesBlob = r.profils.map((c) => `${c} ${libelleRole(c, profilsCatalogue)}`).join(' ');
      const blob = `${r.matricule} ${r.nomComplet} ${r.nomUtilisateur} ${rolesBlob}`.toLowerCase();
      const matchesSearch = !q || blob.includes(q);
      const matchesStatut =
        statutFilter === 'all' ||
        (statutFilter === 'actif' && r.actif) ||
        (statutFilter === 'inactif' && !r.actif);
      return matchesSearch && matchesStatut;
    });
  }, [rows, search, statutFilter, profilsCatalogue]);

  const ubsByDept = useMemo(() => {
    const map = new Map<number, UniteBudgetaire[]>();
    for (const ub of ubs.filter((u) => u.actif)) {
      const list = map.get(ub.idDepartement) ?? [];
      list.push(ub);
      map.set(ub.idDepartement, list);
    }
    return map;
  }, [ubs]);

  const services = useMemo(
    () =>
      structures.filter(
        (s) =>
          s.typeStructure?.toUpperCase() === 'SERVICE' ||
          s.typeStructure?.toLowerCase() === 'service',
      ),
    [structures],
  );

  const roleGroups = useMemo(() => groupProfilsForForm(profilsCatalogue), [profilsCatalogue]);

  const heritees = useMemo(
    () => permissionsHeriteesDesProfils(profils, profilsCatalogue),
    [profils, profilsCatalogue],
  );

  const permissionsEffectives = useMemo(() => {
    const set = new Set<string>([...heritees, ...permissionsIndiv]);
    return [...set].sort((a, b) => a.localeCompare(b));
  }, [heritees, permissionsIndiv]);

  const hasDirectionRole = profils.some((c) => DIRECTION_BUDGETS_CODES.has(c.toUpperCase()));
  const hasJuniorRole = profils.some((c) => JUNIOR_CODES.has(c.toUpperCase()));
  const hasImputer = IMPUTER_PERMISSIONS.some(
    (p) => permissionsIndiv.includes(p) || heritees.has(p),
  );
  const perimetreConfigure = isPerimetreConfigure(perimetre);

  const resetForm = () => {
    setMatricule('');
    setNom('');
    setPostnom('');
    setPrenom('');
    setNomUtilisateur('');
    setEmail('');
    setActif(true);
    setMotDePasse('');
    setIdStructure('');
    setIdDeptPrincipal('');
    setIdService('');
    setProfils([]);
    setPermissionsIndiv([]);
    setPerimetre(emptyPerimetre());
  };

  const openCreate = () => {
    resetForm();
    setFormMode('create');
    setEditingId(null);
    setDialogOpen(true);
  };

  const openEdit = async (row: Row) => {
    resetForm();
    setFormMode('edit');
    setEditingId(row.idUtilisateur);
    setDialogOpen(true);
    try {
      const detail = await fetchAdminUtilisateur(row.idUtilisateur);
      setMatricule(detail.matricule);
      setNom(detail.nom);
      setPostnom(detail.postnom ?? '');
      setPrenom(detail.prenom ?? '');
      setNomUtilisateur(detail.nomUtilisateur);
      setEmail(detail.email ?? '');
      setActif(detail.actif);
      setIdStructure(
        detail.idStructureOrganisationnelle ? String(detail.idStructureOrganisationnelle) : '',
      );
      setIdDeptPrincipal(
        detail.idDepartementPrincipal ? String(detail.idDepartementPrincipal) : '',
      );
      setIdService(detail.idStructureService ? String(detail.idStructureService) : '');
      setProfils([...detail.profils]);
      setPermissionsIndiv([...detail.permissionsIndividuelles]);
      setPerimetre({ ...detail.perimetre });
    } catch (err) {
      void msgBox.error(apiErrorMessage(err, 'Impossible de charger la fiche.'));
    }
  };

  const toggleProfil = (code: string) => {
    setProfils((prev) => (prev.includes(code) ? prev.filter((c) => c !== code) : [...prev, code]));
  };

  const togglePermission = (code: string) => {
    setPermissionsIndiv((prev) =>
      prev.includes(code) ? prev.filter((c) => c !== code) : [...prev, code],
    );
  };

  const toggleDept = (id: number) => {
    setPerimetre((p) => {
      const has = p.idDepartements.includes(id);
      const idDepartements = has
        ? p.idDepartements.filter((x) => x !== id)
        : [...p.idDepartements, id];
      const idUnitesBudgetaires = p.idUnitesBudgetaires.filter((ubId) => {
        const ub = ubs.find((u) => u.idUB === ubId);
        return ub && idDepartements.includes(ub.idDepartement);
      });
      return { ...p, idDepartements, idUnitesBudgetaires };
    });
  };

  const toggleUb = (id: number) => {
    setPerimetre((p) => ({
      ...p,
      idUnitesBudgetaires: p.idUnitesBudgetaires.includes(id)
        ? p.idUnitesBudgetaires.filter((x) => x !== id)
        : [...p.idUnitesBudgetaires, id],
    }));
  };

  const applyPresetDirectionTous = () => {
    setPerimetre({
      tousDepartements: true,
      toutesUnitesBudgetaires: true,
      idDepartements: [],
      idUnitesBudgetaires: [],
    });
  };

  const validateForm = (): string | null => {
    if (!matricule.trim()) return 'Le matricule est obligatoire.';
    if (!nom.trim()) return 'Le nom est obligatoire.';
    if (nomUtilisateur.trim().length < 3) {
      return 'Le nom d’utilisateur doit contenir au moins 3 caractères.';
    }
    if (formMode === 'create' && motDePasse.length < 8) {
      return 'Le mot de passe initial doit contenir au moins 8 caractères.';
    }
    return null;
  };

  const save = async () => {
    const validation = validateForm();
    if (validation) {
      void msgBox.error(validation);
      return;
    }
    setSaving(true);
    try {
      const perimetrePayload: PerimetreUtilisateur = {
        tousDepartements: perimetre.tousDepartements,
        toutesUnitesBudgetaires: perimetre.toutesUnitesBudgetaires,
        idDepartements: perimetre.tousDepartements ? [] : perimetre.idDepartements,
        idUnitesBudgetaires: perimetre.toutesUnitesBudgetaires ? [] : perimetre.idUnitesBudgetaires,
      };
      const base = {
        matricule: matricule.trim(),
        nom: nom.trim(),
        postnom: postnom.trim() || null,
        prenom: prenom.trim() || null,
        nomUtilisateur: nomUtilisateur.trim(),
        email: email.trim() || null,
        actif,
        idStructureOrganisationnelle: idStructure ? Number(idStructure) : null,
        idDepartementPrincipal: idDeptPrincipal ? Number(idDeptPrincipal) : null,
        idStructureService: idService ? Number(idService) : null,
        profils,
        permissionsIndividuelles: permissionsIndiv,
        perimetre: perimetrePayload,
      };
      if (formMode === 'create') {
        await createAdminUtilisateur({ ...base, motDePasseInitial: motDePasse });
        void msgBox.success('Utilisateur créé.');
      } else if (editingId != null) {
        await updateAdminUtilisateur(editingId, base);
        void msgBox.success('Utilisateur mis à jour.');
      }
      setDialogOpen(false);
      await load();
    } catch (err) {
      void msgBox.error(apiErrorMessage(err, 'Enregistrement impossible.'));
    } finally {
      setSaving(false);
    }
  };

  const runActiver = async (row: Row) => {
    const ok = await msgBox.confirm({
      title: 'Activer le compte ?',
      message: `${row.nomComplet} (${row.nomUtilisateur})`,
      confirmLabel: 'Activer',
    });
    if (!ok) return;
    try {
      await setAdminUtilisateurActif(row.idUtilisateur, true);
      void msgBox.success('Compte activé.');
      await load();
    } catch (err) {
      void msgBox.error(apiErrorMessage(err, 'Action impossible.'));
    }
  };

  const runDesactiver = async (row: Row) => {
    const ok = await msgBox.confirm({
      title: 'Désactiver le compte ?',
      message: `${row.nomComplet} (${row.nomUtilisateur})`,
      danger: true,
      confirmLabel: 'Désactiver',
    });
    if (!ok) return;
    try {
      await setAdminUtilisateurActif(row.idUtilisateur, false);
      void msgBox.success('Compte désactivé.');
      await load();
    } catch (err) {
      void msgBox.error(apiErrorMessage(err, 'Action impossible.'));
    }
  };

  const doResetPwd = async () => {
    if (!resetRow) return;
    setResetBusy(true);
    try {
      await resetAdminUtilisateurMotDePasse(resetRow.idUtilisateur, resetPwd);
      void msgBox.success('Mot de passe réinitialisé.');
      setResetOpen(false);
      setResetPwd('');
      setResetRow(null);
    } catch (err) {
      void msgBox.error(apiErrorMessage(err, 'Réinitialisation impossible.'));
    } finally {
      setResetBusy(false);
    }
  };

  const columns: DataTableColumn<Row>[] = [
    { id: 'matricule', label: 'Matricule', mobile: 'meta', render: (r) => r.matricule },
    { id: 'nom', label: 'Nom complet', mobile: 'title', render: (r) => r.nomComplet },
    { id: 'login', label: 'Nom utilisateur', mobile: 'subtitle', render: (r) => r.nomUtilisateur },
    {
      id: 'dept',
      label: 'Département',
      mobile: 'meta',
      render: (r) => r.codeDepartementPrincipal ?? r.libelleDepartementPrincipal ?? '—',
    },
    {
      id: 'roles',
      label: 'Rôles',
      mobile: 'meta',
      render: (r) => (
        <Stack direction="row" useFlexGap sx={{ gap: 0.5, flexWrap: 'wrap' }}>
          {r.profils.length === 0 ? (
            <Typography variant="body2" color="text.secondary">
              —
            </Typography>
          ) : (
            r.profils.map((p) => (
              <Chip
                key={p}
                size="small"
                label={libelleRole(p, profilsCatalogue)}
                title={p}
                variant="outlined"
              />
            ))
          )}
        </Stack>
      ),
    },
    {
      id: 'statut',
      label: 'Statut',
      mobile: 'meta',
      render: (r) => (
        <Chip size="small" color={r.actif ? 'success' : 'default'} label={r.actif ? 'Actif' : 'Inactif'} />
      ),
    },
    {
      id: 'last',
      label: 'Dernière connexion',
      mobile: 'hidden',
      render: (r) => formatDateTimeFr(r.dateDerniereConnexion),
    },
  ];

  if (loading) return <LoadingState />;
  if (error) return <ErrorState message={error} onRetry={() => void load()} />;

  const deptsForUb = perimetre.tousDepartements
    ? departements.filter((d) => d.actif)
    : departements.filter((d) => perimetre.idDepartements.includes(d.idDepartement));

  const permDesc = (code: string) =>
    permissionsCatalogue.find((p) => p.code === code)?.description ?? null;

  return (
    <Box>
      <PageHeader
        title="Utilisateurs"
        subtitle="Comptes, rôles métier, permissions complémentaires et périmètres Département / UB."
        breadcrumbs={[
          { label: BRAND_NAME, to: '/dashboard' },
          { label: 'Administration', to: '/administration/utilisateurs' },
          { label: 'Utilisateurs' },
        ]}
        actions={
          <PrimaryButton startIcon={<AddIcon />} onClick={openCreate}>
            Nouvel utilisateur
          </PrimaryButton>
        }
      />

      <FilterBar
        search={search}
        onSearchChange={setSearch}
        searchPlaceholder="Matricule, nom, login, rôle…"
        extra={
          <TextField select size="small"
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

      <DataTable
        columns={columns}
        rows={filtered}
        actions={[
          { id: 'edit', label: 'Modifier', onClick: (r) => void openEdit(r) },
          {
            id: 'activer',
            label: 'Activer',
            hidden: (r) => r.actif,
            onClick: (r) => void runActiver(r),
          },
          {
            id: 'desactiver',
            label: 'Désactiver',
            hidden: (r) => !r.actif,
            onClick: (r) => void runDesactiver(r),
          },
          {
            id: 'reset',
            label: 'Réinit. mot de passe',
            onClick: (r) => {
              setResetRow(r);
              setResetPwd('');
              setResetOpen(true);
            },
          },
        ]}
      />

      <Dialog
        open={dialogOpen}
        onClose={() => !saving && setDialogOpen(false)}
        maxWidth="md"
        fullWidth
      >
        <DialogTitle>
          {formMode === 'create' ? 'Nouvel utilisateur' : 'Modifier l’utilisateur'}
        </DialogTitle>
        <DialogContent dividers>
          <Typography variant="subtitle2" gutterBottom>
            Identité
          </Typography>
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} sx={{ mb: 2 }}>
            <TextField
              label="Matricule"
              value={matricule}
              onChange={(e) => setMatricule(e.target.value)}
              fullWidth
              required
            />
            <TextField
              label="Nom utilisateur"
              value={nomUtilisateur}
              onChange={(e) => setNomUtilisateur(e.target.value)}
              fullWidth
              required
            />
          </Stack>
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} sx={{ mb: 2 }}>
            <TextField
              label="Nom"
              value={nom}
              onChange={(e) => setNom(e.target.value)}
              fullWidth
              required
            />
            <TextField
              label="Postnom"
              value={postnom}
              onChange={(e) => setPostnom(e.target.value)}
              fullWidth
            />
            <TextField
              label="Prénom"
              value={prenom}
              onChange={(e) => setPrenom(e.target.value)}
              fullWidth
            />
          </Stack>
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} sx={{ mb: 2 }}>
            <TextField
              label="Email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              fullWidth
            />
            <TextField select label="Statut"
              value={actif ? '1' : '0'}
              onChange={(e) => setActif(e.target.value === '1')}
              fullWidth
            >
              <MenuItem value="1">Actif</MenuItem>
              <MenuItem value="0">Inactif</MenuItem>
            </TextField>
          </Stack>
          {formMode === 'create' && (
            <TextField
              label="Mot de passe initial"
              type="password"
              value={motDePasse}
              onChange={(e) => setMotDePasse(e.target.value)}
              fullWidth
              required
              helperText="Minimum 8 caractères"
              sx={{ mb: 2 }}
            />
          )}

          <Divider sx={{ my: 2 }} />
          <Typography variant="subtitle2" gutterBottom>
            Affectation administrative
          </Typography>
          <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1 }}>
            Fiche organisationnelle (Structure / Département / Service). Distincte du périmètre de
            sécurité ci-dessous.
          </Typography>
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} sx={{ mb: 2 }}>
            <SearchableSelect
              label="Structure / Entité"
              value={idStructure}
              onChange={setIdStructure}
              allowEmpty
              options={[
                { value: '', label: '—' },
                ...structures.map((s) => ({
                  value: String(s.idStructure),
                  label: `${s.code} — ${s.libelle}`,
                })),
              ]}
              placeholder="Rechercher une structure…"
              width={200}
            />
            <SearchableSelect
              label="Département principal"
              value={idDeptPrincipal}
              onChange={setIdDeptPrincipal}
              allowEmpty
              options={[
                { value: '', label: '—' },
                ...departements.map((d) => ({
                  value: String(d.idDepartement),
                  label: `${d.code} — ${d.libelle}`,
                })),
              ]}
              placeholder="Rechercher un département…"
              width={200}
            />
            <SearchableSelect
              label="Service"
              value={idService}
              onChange={setIdService}
              allowEmpty
              options={[
                { value: '', label: '—' },
                ...(services.length > 0 ? services : structures).map((s) => ({
                  value: String(s.idStructure),
                  label: `${s.code} — ${s.libelle}`,
                })),
              ]}
              placeholder="Rechercher un service…"
              width={200}
            />
          </Stack>

          <Divider sx={{ my: 2 }} />
          <Typography variant="subtitle2" gutterBottom>
            Rôles métier
          </Typography>
          <Stack spacing={2} sx={{ mb: 2 }}>
            {roleGroups.map(({ group, items }) => (
              <Box
                key={group.id + group.title}
                sx={{
                  p: 1.5,
                  border: '1px solid',
                  borderColor: 'divider',
                  borderRadius: 1,
                  bgcolor: group.id === 'historique' || group.id === 'admin' ? 'action.hover' : 'background.paper',
                }}
              >
                <Stack direction="row" spacing={1} sx={{ alignItems: 'center', mb: 0.5 }}>
                  <Typography variant="subtitle2">{group.title}</Typography>
                  {group.id === 'historique' && (
                    <Chip size="small" label="Historique" variant="outlined" />
                  )}
                  {group.id === 'admin' && (
                    <Chip size="small" label="Technique" color="warning" variant="outlined" />
                  )}
                </Stack>
                {group.hint && (
                  <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1 }}>
                    {group.hint}
                  </Typography>
                )}
                <FormGroup sx={{ gap: 0.25 }}>
                  {items.map((p) => (
                    <FormControlLabel
                      key={p.code}
                      control={
                        <Checkbox
                          checked={profils.includes(p.code)}
                          onChange={() => toggleProfil(p.code)}
                          size="small"
                        />
                      }
                      label={
                        <Box>
                          <Typography variant="body2" sx={{ fontWeight: 500 }}>
                            {libelleRole(p.code, profilsCatalogue)}
                          </Typography>
                          <Typography variant="caption" color="text.secondary" sx={{ fontFamily: 'monospace' }}>
                            {p.code}
                          </Typography>
                        </Box>
                      }
                    />
                  ))}
                </FormGroup>
              </Box>
            ))}
          </Stack>

          {hasJuniorRole && !hasImputer && (
            <Alert severity="warning" sx={{ mb: 2 }}>
              Un Gestionnaire Junior doit disposer d’au moins une permission de filière :
              <strong> paiements.imputer_dc</strong>, <strong>imputer_ae</strong> ou{' '}
              <strong>imputer_bi</strong> (section Permissions complémentaires).
            </Alert>
          )}

          <Divider sx={{ my: 2 }} />
          <Typography variant="subtitle2" gutterBottom>
            Permissions complémentaires
          </Typography>
          <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1 }}>
            Attribution individuelle (en plus de l’héritage des rôles).{' '}
            <code>admin.all</code> n’est pas attribuable ici.
          </Typography>
          <Stack spacing={0.75} sx={{ mb: 2, maxHeight: 240, overflow: 'auto' }}>
            {permissionsCatalogue.map((perm) => {
              const heriteeProfil = heritees.has(perm.code);
              const indiv = permissionsIndiv.includes(perm.code);
              return (
                <FormControlLabel
                  key={perm.code}
                  control={
                    <Checkbox
                      size="small"
                      checked={indiv}
                      onChange={() => togglePermission(perm.code)}
                    />
                  }
                  label={
                    <Box>
                      <Stack direction="row" spacing={1} useFlexGap sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
                        <Typography variant="body2" sx={{ fontFamily: 'monospace' }}>
                          {perm.code}
                        </Typography>
                        {heriteeProfil && (
                          <Chip size="small" label="héritée" color="info" variant="outlined" />
                        )}
                        {indiv && (
                          <Chip size="small" label="individuelle" color="secondary" variant="outlined" />
                        )}
                        {!heriteeProfil && !indiv && (
                          <Chip size="small" label="non accordée" variant="outlined" />
                        )}
                      </Stack>
                      {perm.description && (
                        <Typography variant="caption" color="text.secondary">
                          {perm.description}
                        </Typography>
                      )}
                    </Box>
                  }
                />
              );
            })}
          </Stack>

          <Typography variant="subtitle2" gutterBottom>
            Permissions effectives
          </Typography>
          <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1 }}>
            Union des permissions héritées des rôles et des permissions individuelles (aperçu).
          </Typography>
          <Stack direction="row" useFlexGap sx={{ gap: 0.75, flexWrap: 'wrap', mb: 2 }}>
            {permissionsEffectives.length === 0 ? (
              <Typography variant="body2" color="text.secondary">
                Aucune permission effective.
              </Typography>
            ) : (
              permissionsEffectives.map((code) => (
                <Chip
                  key={code}
                  size="small"
                  label={code}
                  title={permDesc(code) ?? undefined}
                  color={permissionsIndiv.includes(code) && !heritees.has(code) ? 'secondary' : 'default'}
                  variant="outlined"
                />
              ))
            )}
          </Stack>

          <Divider sx={{ my: 2 }} />
          <Stack
            direction={{ xs: 'column', sm: 'row' }}
            spacing={1}
            sx={{ justifyContent: 'space-between', alignItems: { sm: 'center' }, mb: 1 }}
          >
            <Typography variant="subtitle2">Périmètre de sécurité (Département / UB)</Typography>
            {hasDirectionRole && (
              <Button size="small" variant="outlined" onClick={applyPresetDirectionTous}>
                Preset Direction : Tous départements + Toutes UB
              </Button>
            )}
          </Stack>

          {!perimetreConfigure && hasDirectionRole && (
            <Alert severity="warning" sx={{ mb: 2 }}>
              Rôle Direction des Budgets : le périmètre « Tous départements + Toutes UB » sera
              appliqué automatiquement à l&apos;enregistrement si aucun périmètre n&apos;est
              sélectionné. Utilisez le preset ci-dessous pour le prévisualiser.
            </Alert>
          )}
          {!perimetreConfigure && !hasDirectionRole && (
            <Alert severity="warning" sx={{ mb: 2 }}>
              Périmètre non configuré : l’accès DPM restera en{' '}
              <strong>fallback historique</strong> (proxy prévision / créateur de la demande). Pour
              activer le vrai périmètre, cochez « Tous » ou sélectionnez au moins un département ou
              une UB.
            </Alert>
          )}
          {hasDirectionRole && perimetreConfigure && !(perimetre.tousDepartements && perimetre.toutesUnitesBudgetaires) && (
            <Alert severity="info" sx={{ mb: 2 }}>
              Rôle Direction des Budgets avec un périmètre restreint. Le preset « Tous* » est
              recommandé sauf besoin de limitation volontaire.
            </Alert>
          )}

          <Typography variant="body2" sx={{ mb: 1 }}>
            Départements
          </Typography>
          <RadioGroup
            row
            value={perimetre.tousDepartements ? 'tous' : 'sel'}
            onChange={(_, v) =>
              setPerimetre((p) => ({
                ...p,
                tousDepartements: v === 'tous',
                idDepartements: v === 'tous' ? [] : p.idDepartements,
              }))
            }
          >
            <FormControlLabel
              value="sel"
              control={<Radio size="small" />}
              label="Départements sélectionnés"
            />
            <FormControlLabel
              value="tous"
              control={<Radio size="small" />}
              label="Tous les départements"
            />
          </RadioGroup>
          {!perimetre.tousDepartements && (
            <FormGroup row sx={{ mb: 2, gap: 0.5 }}>
              {departements
                .filter((d) => d.actif)
                .map((d) => (
                  <FormControlLabel
                    key={d.idDepartement}
                    control={
                      <Checkbox
                        size="small"
                        checked={perimetre.idDepartements.includes(d.idDepartement)}
                        onChange={() => toggleDept(d.idDepartement)}
                      />
                    }
                    label={`${d.code} — ${d.libelle}`}
                  />
                ))}
            </FormGroup>
          )}

          <Typography variant="body2" sx={{ mb: 1 }}>
            Unités budgétaires
          </Typography>
          <RadioGroup
            row
            value={perimetre.toutesUnitesBudgetaires ? 'tous' : 'sel'}
            onChange={(_, v) =>
              setPerimetre((p) => ({
                ...p,
                toutesUnitesBudgetaires: v === 'tous',
                idUnitesBudgetaires: v === 'tous' ? [] : p.idUnitesBudgetaires,
              }))
            }
          >
            <FormControlLabel value="sel" control={<Radio size="small" />} label="UB sélectionnées" />
            <FormControlLabel value="tous" control={<Radio size="small" />} label="Toutes les UB" />
          </RadioGroup>
          {!perimetre.toutesUnitesBudgetaires && (
            <Stack spacing={1} sx={{ maxHeight: 240, overflow: 'auto' }}>
              {deptsForUb.length === 0 ? (
                <Typography variant="body2" color="text.secondary">
                  Sélectionnez d’abord des départements, ou passez en « Tous les départements ».
                </Typography>
              ) : (
                deptsForUb.map((d) => (
                  <Box key={d.idDepartement}>
                    <Typography variant="subtitle2">
                      {d.code} — {d.libelle}
                    </Typography>
                    <FormGroup row sx={{ gap: 0.5, pl: 1 }}>
                      {(ubsByDept.get(d.idDepartement) ?? []).map((ub) => (
                        <FormControlLabel
                          key={ub.idUB}
                          control={
                            <Checkbox
                              size="small"
                              checked={perimetre.idUnitesBudgetaires.includes(ub.idUB)}
                              onChange={() => toggleUb(ub.idUB)}
                            />
                          }
                          label={`${ub.codeUB}${ub.libelle ? ` — ${ub.libelle}` : ''}`}
                        />
                      ))}
                    </FormGroup>
                  </Box>
                ))
              )}
            </Stack>
          )}
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

      <Dialog open={resetOpen} onClose={() => !resetBusy && setResetOpen(false)} maxWidth="xs" fullWidth>
        <DialogTitle>Réinitialiser le mot de passe</DialogTitle>
        <DialogContent>
          <Typography variant="body2" sx={{ mb: 2 }}>
            {resetRow?.nomComplet} — {resetRow?.nomUtilisateur}
          </Typography>
          <TextField
            label="Nouveau mot de passe"
            type="password"
            value={resetPwd}
            onChange={(e) => setResetPwd(e.target.value)}
            fullWidth
            helperText="Minimum 8 caractères"
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setResetOpen(false)} disabled={resetBusy}>
            Annuler
          </Button>
          <Button
            variant="contained"
            onClick={() => void doResetPwd()}
            disabled={resetBusy || resetPwd.length < 8}
          >
            Réinitialiser
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
