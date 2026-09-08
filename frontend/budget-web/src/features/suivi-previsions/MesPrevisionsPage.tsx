import {
  Alert,
  Box,
  Chip,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from '@mui/material';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  ErrorState,
  FilterZone,
  KpiRow,
  LoadingState,
  PageHeader,
  PrimaryButton,
  SearchableSelect,
  SecondaryButton,
  StatCard,
} from '../../components';
import {
  fetchDepartements,
  fetchExercices,
  fetchMesPrevisions,
  fetchVersionsBudgetaires,
  type Departement,
  type Exercice,
  type SuiviPrevisionListe,
  type SuiviPrevisionUbResume,
  type VersionBudgetaire,
} from '../../services/apiClient';
import { BRAND_NAME } from '../../theme';
import { useResponsive } from '../../theme/useResponsive';
import { useAuth } from '../auth';
import { apiErrorMessage } from '../rubriques-budgetaires/rubriqueUtils';
import {
  buildPrevisionsContextUrl,
  formatDateSuivi,
  formatMontantSuivi,
  normalizeStatut,
  ouvrirViaGrillePrevisions,
  statutChipSx,
  versionLabel,
} from './suiviUtils';

/**
 * Colonnes secondaires masquées sous `md` : sur téléphone on garde l'UB, le
 * total, le statut et l'action. Le détail par type revient dès `md`.
 */
const CELLULE_DETAIL = { display: { xs: 'none', md: 'table-cell' } } as const;

type FiltreRapide = 'TOUTES' | 'BROUILLON' | 'SOUMISE' | 'CONTROLEE' | 'VALIDEE' | 'REJETEE';

export function MesPrevisionsPage() {
  const navigate = useNavigate();
  const { user } = useAuth();
  const { isMobile } = useResponsive();
  const [exercices, setExercices] = useState<Exercice[]>([]);
  const [versions, setVersions] = useState<VersionBudgetaire[]>([]);
  const [departements, setDepartements] = useState<Departement[]>([]);
  const [idExercice, setIdExercice] = useState('');
  const [idVersion, setIdVersion] = useState('');
  const [idDepartement, setIdDepartement] = useState('');
  const [searchUb, setSearchUb] = useState('');
  const [filtreRapide, setFiltreRapide] = useState<FiltreRapide>('TOUTES');
  const [data, setData] = useState<SuiviPrevisionListe | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const versionsFiltrees = useMemo(
    () => versions.filter((v) => !idExercice || String(v.idExercice) === idExercice),
    [versions, idExercice],
  );

  useEffect(() => {
    void (async () => {
      try {
        const [ex, ver, dep] = await Promise.all([
          fetchExercices(),
          fetchVersionsBudgetaires(),
          fetchDepartements(),
        ]);
        setExercices(ex);
        setVersions(ver);
        setDepartements(dep.filter((d) => d.actif));
        if (ex[0]) setIdExercice(String(ex[0].idExercice));
      } catch (err) {
        setError(apiErrorMessage(err, 'Impossible de charger les référentiels.'));
      }
    })();
  }, []);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await fetchMesPrevisions({
        idExercice: idExercice ? Number(idExercice) : undefined,
        idVersion: idVersion ? Number(idVersion) : undefined,
        idDepartement: idDepartement ? Number(idDepartement) : undefined,
        statut: filtreRapide === 'TOUTES' ? undefined : filtreRapide,
        searchUb: searchUb.trim() || undefined,
      });
      setData(result);
    } catch (err) {
      setError(apiErrorMessage(err, 'Impossible de charger vos prévisions.'));
      setData(null);
    } finally {
      setLoading(false);
    }
  }, [idExercice, idVersion, idDepartement, filtreRapide, searchUb]);

  useEffect(() => {
    void load();
  }, [load]);

  const actionLabel = (statut: string) => {
    const s = normalizeStatut(statut);
    if (s === 'BROUILLON') return 'Continuer';
    if (s === 'REJETEE') return 'Corriger';
    return 'Consulter';
  };

  const openRow = (row: SuiviPrevisionUbResume) => {
    if (ouvrirViaGrillePrevisions(user)) {
      navigate(
        buildPrevisionsContextUrl({
          idExercice: row.idExercice,
          idVersion: row.idVersion,
          idUB: row.idUB,
        }),
      );
      return;
    }
    navigate(`/budget/soumissions/${row.idVersion}/${row.idUB}?from=mes`);
  };

  const c = data?.compteurs;

  return (
    <Box>
      <PageHeader
        title="Suivi de mes prévisions"
        subtitle="Prévisions que vous avez préparées — brouillons, soumissions, contrôles et rejets."
        breadcrumbs={[
          { label: BRAND_NAME, to: '/dashboard' },
          { label: 'Budget' },
          { label: 'Suivi de mes prévisions' },
        ]}
      />

      <KpiRow columns={5}>
        {(
          [
            ['BROUILLONS', c?.brouillons ?? 0],
            ['SOUMISES', c?.soumises ?? 0],
            ['CONTRÔLÉES', c?.controlees ?? 0],
            ['VALIDÉES', c?.validees ?? 0],
            ['REJETÉES', c?.rejetees ?? 0],
          ] as const
        ).map(([label, value]) => (
          <StatCard key={label} title={label} value={String(value)} />
        ))}
      </KpiRow>

      <Paper sx={{ p: 1.5, mb: 2 }}>
        <FilterZone
          columns={{ xs: 1, sm: 2, md: 3, lg: 3, xl: 3 }}
          search={
            <TextField
              size="small"
              fullWidth
              label="Recherche UB"
              value={searchUb}
              onChange={(e) => setSearchUb(e.target.value)}
            />
          }
        >
          <SearchableSelect
            label="Exercice"
            value={idExercice}
            onChange={(v) => {
              setIdExercice(v);
              setIdVersion('');
            }}
            fullWidth
            options={exercices.map((ex) => ({
              value: String(ex.idExercice),
              label: String(ex.annee),
            }))}
          />
          <SearchableSelect
            label="Version"
            value={idVersion}
            onChange={setIdVersion}
            allowEmpty
            fullWidth
            options={[
              { value: '', label: 'Toutes' },
              ...versionsFiltrees.map((v) => ({
                value: String(v.idVersion),
                label: versionLabel(v.numeroVersion, v.libelle),
              })),
            ]}
          />
          <SearchableSelect
            label="Département"
            value={idDepartement}
            onChange={setIdDepartement}
            allowEmpty
            fullWidth
            options={[
              { value: '', label: 'Tous' },
              ...departements.map((d) => ({
                value: String(d.idDepartement),
                label: `${d.code} — ${d.libelle}`,
              })),
            ]}
          />
        </FilterZone>
        <Stack direction="row" spacing={0.75} useFlexGap sx={{ flexWrap: 'wrap', mt: 1.25 }}>
          {(
            [
              ['TOUTES', 'Toutes'],
              ['BROUILLON', 'Brouillons'],
              ['SOUMISE', 'Soumises'],
              ['CONTROLEE', 'Contrôlées'],
              ['VALIDEE', 'Validées'],
              ['REJETEE', 'Rejetées'],
            ] as const
          ).map(([key, label]) => (
            <Chip
              key={key}
              size="small"
              label={label}
              clickable
              color={filtreRapide === key ? 'primary' : 'default'}
              variant={filtreRapide === key ? 'filled' : 'outlined'}
              onClick={() => setFiltreRapide(key)}
            />
          ))}
        </Stack>
      </Paper>

      {error && <ErrorState title="Erreur" message={error} onRetry={() => void load()} />}
      {loading && <LoadingState label="Chargement de vos prévisions…" />}

      {!loading && data && (
        <TableContainer component={Paper}>
          <Table size="small" stickyHeader>
            <TableHead>
              <TableRow>
                <TableCell sx={CELLULE_DETAIL}>Département</TableCell>
                <TableCell>UB</TableCell>
                <TableCell sx={CELLULE_DETAIL}>Exercice</TableCell>
                <TableCell sx={CELLULE_DETAIL}>Version</TableCell>
                <TableCell align="right" sx={CELLULE_DETAIL}>
                  DC
                </TableCell>
                <TableCell align="right" sx={CELLULE_DETAIL}>
                  AE
                </TableCell>
                <TableCell align="right" sx={CELLULE_DETAIL}>
                  BI
                </TableCell>
                <TableCell align="right">Total</TableCell>
                <TableCell>Statut</TableCell>
                <TableCell sx={CELLULE_DETAIL}>Dernière modif.</TableCell>
                <TableCell align="right">Action</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {data.lignes.length === 0 && (
                <TableRow>
                  <TableCell colSpan={isMobile ? 4 : 11}>
                    <Typography color="text.secondary" sx={{ py: 2, textAlign: 'center' }}>
                      Aucune prévision pour ces filtres.
                    </Typography>
                  </TableCell>
                </TableRow>
              )}
              {data.lignes.map((row) => (
                <TableRow key={`${row.idVersion}-${row.idUB}`} hover>
                  <TableCell sx={CELLULE_DETAIL}>{row.libelleDepartement}</TableCell>
                  <TableCell>
                    <Typography variant="body2" sx={{ fontWeight: 650 }}>
                      {row.codeUB}
                    </Typography>
                    <Typography variant="caption" color="text.secondary">
                      {row.libelleUB}
                    </Typography>
                  </TableCell>
                  <TableCell sx={CELLULE_DETAIL}>{row.annee}</TableCell>
                  <TableCell sx={CELLULE_DETAIL}>{versionLabel(row.numeroVersion)}</TableCell>
                  <TableCell align="right" sx={CELLULE_DETAIL}>
                    {formatMontantSuivi(row.montantDC)}
                  </TableCell>
                  <TableCell align="right" sx={CELLULE_DETAIL}>
                    {formatMontantSuivi(row.montantAE)}
                  </TableCell>
                  <TableCell align="right" sx={CELLULE_DETAIL}>
                    {formatMontantSuivi(row.montantBI)}
                  </TableCell>
                  <TableCell align="right" sx={{ fontWeight: 700 }}>
                    {formatMontantSuivi(row.montantTotal)}
                  </TableCell>
                  <TableCell>
                    <Chip
                      size="small"
                      label={normalizeStatut(row.statut)}
                      variant="outlined"
                      sx={{ height: 22, fontWeight: 700, ...statutChipSx(row.statut) }}
                    />
                    {normalizeStatut(row.statut) === 'VALIDEE' && (
                      <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 0.5 }}>
                        Lecture seule
                      </Typography>
                    )}
                    {normalizeStatut(row.statut) === 'REJETEE' && row.motifRejet && (
                      <Alert severity="warning" sx={{ mt: 0.75, py: 0, '& .MuiAlert-message': { py: 0.5 } }}>
                        <Typography variant="caption" sx={{ display: 'block', fontWeight: 700 }}>
                          Motif : {row.motifRejet}
                        </Typography>
                        <Typography variant="caption" color="text.secondary">
                          {row.nomUtilisateurRejet ?? '—'} — {formatDateSuivi(row.dateRejet)}
                        </Typography>
                      </Alert>
                    )}
                  </TableCell>
                  <TableCell sx={CELLULE_DETAIL}>
                    {formatDateSuivi(row.dateDerniereModification)}
                  </TableCell>
                  <TableCell align="right">
                    {normalizeStatut(row.statut) === 'BROUILLON' || normalizeStatut(row.statut) === 'REJETEE' ? (
                      <PrimaryButton size="small" onClick={() => openRow(row)}>
                        {actionLabel(row.statut)}
                      </PrimaryButton>
                    ) : (
                      <SecondaryButton size="small" onClick={() => openRow(row)}>
                        Consulter
                      </SecondaryButton>
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}
    </Box>
  );
}
