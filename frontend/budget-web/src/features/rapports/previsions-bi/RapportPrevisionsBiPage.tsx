import { useEffect, useMemo, useState } from 'react';
import {
  Alert,
  Autocomplete,
  Box,
  Button,
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
import PictureAsPdfOutlinedIcon from '@mui/icons-material/PictureAsPdfOutlined';
import VisibilityOutlinedIcon from '@mui/icons-material/VisibilityOutlined';
import { FilterFields, PageHeader } from '../../../components';
import { useResponsive } from '../../../theme/useResponsive';
import {
  downloadRapportPrevisionsBiPdf,
  fetchExercices,
  fetchRapportPrevisionsBi,
  fetchStructures,
  fetchUnitesBudgetaires,
  fetchVersionsBudgetaires,
  type Exercice,
  type RapportBiDto,
  type RapportBiNiveau,
  type Structure,
  type UniteBudgetaire,
  type VersionBudgetaire,
} from '../../../services/apiClient';
import {
  formatModeRapport,
  formatUsdFr,
  MOIS_LABELS,
  STATUTS_CONSULTATION,
} from '../previsions-dc/rapportDcUtils';
import {
  buildStructuresById,
  filterUbsSousStructure,
  structuresAccessiblesParType,
} from '../rapportPerimetre';

/**
 * Colonnes de détail (mois, départements) masquées sous `md` : sur téléphone on
 * conserve la désignation et le cumul. Le rapport reprend toutes ses colonnes
 * dès `md`.
 */
const CELLULE_DETAIL = { display: { xs: 'none', md: 'table-cell' } } as const;

function apiErrorMessage(err: unknown, fallback: string): string {
  if (err && typeof err === 'object' && 'response' in err) {
    const data = (err as { response?: { data?: { message?: string } } }).response?.data;
    if (data?.message) return data.message;
  }
  return fallback;
}

function formatMoisCell(v: number | null | undefined, mensuelLine: boolean, agregeHasMensuel: boolean): string {
  if (!mensuelLine && !agregeHasMensuel) return '—';
  if (v == null) return '—';
  return new Intl.NumberFormat('fr-FR', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(v);
}

export function RapportPrevisionsBiPage() {
  const { isMobile } = useResponsive();
  const [exercices, setExercices] = useState<Exercice[]>([]);
  const [versions, setVersions] = useState<VersionBudgetaire[]>([]);
  const [structures, setStructures] = useState<Structure[]>([]);
  const [ubs, setUbs] = useState<UniteBudgetaire[]>([]);

  const [idExercice, setIdExercice] = useState('');
  const [idVersion, setIdVersion] = useState('');
  const [niveau, setNiveau] = useState<RapportBiNiveau>('ENTITE');
  const [entite, setEntite] = useState<Structure | null>(null);
  const [departement, setDepartement] = useState<Structure | null>(null);
  const [division, setDivision] = useState<Structure | null>(null);
  const [ub, setUb] = useState<UniteBudgetaire | null>(null);
  const [statut, setStatut] = useState('VALIDEE');

  const [rapport, setRapport] = useState<RapportBiDto | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [bootError, setBootError] = useState<string | null>(null);

  useEffect(() => {
    void (async () => {
      try {
        const [ex, ver, st, ubList] = await Promise.all([
          fetchExercices(),
          fetchVersionsBudgetaires(),
          fetchStructures(),
          fetchUnitesBudgetaires({ accessibles: true }),
        ]);
        setExercices(ex);
        setVersions(ver);
        setStructures(st.filter((s) => s.actif));
        setUbs(ubList.filter((u) => u.actif));
        if (ex[0]) setIdExercice(String(ex[0].idExercice));
      } catch (err) {
        setBootError(apiErrorMessage(err, 'Impossible de charger les référentiels.'));
      }
    })();
  }, []);

  const structuresById = useMemo(() => buildStructuresById(structures), [structures]);

  const entites = useMemo(
    () => structuresAccessiblesParType(structuresById, ubs, 'ENTITE'),
    [structuresById, ubs],
  );

  const versionsFiltrees = useMemo(() => {
    const idEx = Number(idExercice);
    return versions.filter((v) => !idExercice || v.idExercice === idEx);
  }, [versions, idExercice]);

  const departements = useMemo(() => {
    if (!entite) return [];
    return structuresAccessiblesParType(
      structuresById,
      ubs,
      'DEPARTEMENT',
      entite.idStructure,
    );
  }, [structuresById, ubs, entite]);

  const divisions = useMemo(() => {
    if (!departement) return [];
    return structuresAccessiblesParType(
      structuresById,
      ubs,
      'DIVISION',
      departement.idStructure,
    );
  }, [structuresById, ubs, departement]);

  const ubsFiltrees = useMemo(() => {
    if (!departement) return [];
    return filterUbsSousStructure(structuresById, ubs, departement.idStructure);
  }, [ubs, departement, structuresById]);

  useEffect(() => {
    setDepartement(null);
    setDivision(null);
    setUb(null);
  }, [entite?.idStructure]);

  useEffect(() => {
    setDivision(null);
    setUb(null);
  }, [departement?.idStructure]);

  const showMonths =
    rapport?.layoutColonnes === 'MENSUEL' || rapport?.layoutColonnes === 'MIXTE';

  async function charger() {
    setError(null);
    if (!idVersion || !entite) {
      setError('Version et Entité sont obligatoires.');
      return;
    }
    if ((niveau === 'DEPARTEMENT' || niveau === 'UB') && !departement) {
      setError('Département obligatoire pour ce niveau.');
      return;
    }
    if (niveau === 'UB' && !ub) {
      setError('UB obligatoire pour le niveau UB.');
      return;
    }
    setLoading(true);
    try {
      const data = await fetchRapportPrevisionsBi({
        idVersion: Number(idVersion),
        niveau,
        idEntite: entite.idStructure,
        idDepartementStructure: departement?.idStructure,
        idDivision: division?.idStructure,
        idUB: ub?.idUB,
        statutConsultation: statut || undefined,
      });
      setRapport(data);
    } catch (err) {
      setRapport(null);
      setError(apiErrorMessage(err, 'Échec du chargement du rapport BI.'));
    } finally {
      setLoading(false);
    }
  }

  async function telechargerPdf() {
    if (!idVersion || !entite) return;
    try {
      const blob = await downloadRapportPrevisionsBiPdf({
        idVersion: Number(idVersion),
        niveau,
        idEntite: entite.idStructure,
        idDepartementStructure: departement?.idStructure,
        idDivision: division?.idStructure,
        idUB: ub?.idUB,
        statutConsultation: statut || undefined,
      });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `rapport-bi.pdf`;
      a.click();
      URL.revokeObjectURL(url);
    } catch (err) {
      setError(apiErrorMessage(err, 'Échec du téléchargement PDF.'));
    }
  }

  return (
    <Box>
      <PageHeader
        title="Prévisions BI — Investissements"
        subtitle="Budget détaillé des investissements (Entité / Département / UB)"
      />
      {bootError && <Alert severity="error">{bootError}</Alert>}
      <Paper sx={{ p: 2, mb: 2 }}>
        <FilterFields>
          <TextField select label="Exercice"
            size="small"
            fullWidth
            value={idExercice}
            onChange={(e) => {
              setIdExercice(e.target.value);
              setIdVersion('');
            }}
          >
            {exercices.map((ex) => (
              <MenuItem key={ex.idExercice} value={String(ex.idExercice)}>
                {ex.annee}
              </MenuItem>
            ))}
          </TextField>
          <TextField select label="Version"
            size="small"
            fullWidth
            value={idVersion}
            onChange={(e) => setIdVersion(e.target.value)}
          >
            {versionsFiltrees.map((v) => (
              <MenuItem key={v.idVersion} value={String(v.idVersion)}>
                V{v.numeroVersion} — {v.libelle ?? ''}
              </MenuItem>
            ))}
          </TextField>
          <TextField select label="Niveau"
            size="small"
            fullWidth
            value={niveau}
            onChange={(e) => setNiveau(e.target.value as RapportBiNiveau)}
          >
            <MenuItem value="ENTITE">ENTITE</MenuItem>
            <MenuItem value="DEPARTEMENT">DEPARTEMENT</MenuItem>
            <MenuItem value="UB">UB</MenuItem>
          </TextField>
          <Autocomplete
            size="small"
            fullWidth
            options={entites}
            getOptionLabel={(o) => `${o.code} — ${o.libelle}`}
            value={entite}
            onChange={(_, v) => setEntite(v)}
            renderInput={(params) => <TextField {...params} label="Entité" />}
          />
          {(niveau === 'DEPARTEMENT' || niveau === 'UB') && (
            <Autocomplete
              size="small"
              fullWidth
              options={departements}
              getOptionLabel={(o) => `${o.code} — ${o.libelle}`}
              value={departement}
              onChange={(_, v) => setDepartement(v)}
              renderInput={(params) => <TextField {...params} label="Département" />}
            />
          )}
          {niveau === 'UB' && (
            <>
              <Autocomplete
                size="small"
                fullWidth
                options={divisions}
                getOptionLabel={(o) => `${o.code} — ${o.libelle}`}
                value={division}
                onChange={(_, v) => setDivision(v)}
                renderInput={(params) => <TextField {...params} label="Division (opt.)" />}
              />
              <Autocomplete
                size="small"
                fullWidth
                options={ubsFiltrees}
                getOptionLabel={(o) => `${o.codeUB} — ${o.libelle}`}
                value={ub}
                onChange={(_, v) => setUb(v)}
                renderInput={(params) => <TextField {...params} label="UB" />}
              />
            </>
          )}
          <TextField select label="Statut"
            size="small"
            fullWidth
            value={statut}
            onChange={(e) => setStatut(e.target.value)}
          >
            {STATUTS_CONSULTATION.map((s) => (
              <MenuItem key={s} value={s}>
                {s}
              </MenuItem>
            ))}
          </TextField>
        </FilterFields>

        <Stack direction="row" spacing={1} useFlexGap sx={{ mt: 2.5, flexWrap: 'wrap' }}>
          <Button variant="contained" startIcon={<VisibilityOutlinedIcon />} disabled={loading} onClick={() => void charger()}>
            Aperçu
          </Button>
          <Button
            variant="outlined"
            startIcon={<PictureAsPdfOutlinedIcon />}
            disabled={!rapport || loading}
            onClick={() => void telechargerPdf()}
          >
            PDF
          </Button>
        </Stack>
      </Paper>

      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      {rapport && (
        <Paper sx={{ p: 2, overflow: 'auto' }}>
          <Typography variant="h6">{rapport.enTete.titre}</Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
            {rapport.enTete.codeEntite} — {rapport.enTete.libelleEntite} · {rapport.enTete.nbDepartements} dép. ·{' '}
            {rapport.enTete.nbUB} UB · {formatUsdFr(rapport.enTete.montantTotalBi)} · {rapport.enTete.statutFiltre}
          </Typography>

          {rapport.departements.length === 0 && (
            <Alert severity="info">Aucune prévision BI pour ce périmètre / statut.</Alert>
          )}

          {rapport.departements.map((dept) => (
            <Box key={`d-${dept.idDepartementStructure ?? dept.codeDepartement}`} sx={{ mb: 3 }}>
              <Typography
                variant="subtitle1"
                sx={{ bgcolor: 'primary.dark', color: 'primary.contrastText', px: 1.5, py: 0.75, mb: 1 }}
              >
                DÉPARTEMENT : {dept.codeDepartement} — {dept.libelleDepartement}
              </Typography>

              {dept.ubs.map((bloc) => {
                const showM =
                  showMonths ||
                  bloc.layoutColonnes === 'MENSUEL' ||
                  bloc.layoutColonnes === 'MIXTE';
                return (
                  <Box key={bloc.identite.idUB} sx={{ mb: 2, pl: 1 }}>
                    <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 0.5 }}>
                      UB {bloc.identite.codeUB} — {bloc.identite.libelleUB}
                    </Typography>
                    <Table size="small">
                      <TableHead>
                        <TableRow>
                          <TableCell>ITEM</TableCell>
                          <TableCell>DÉSIGNATION</TableCell>
                          <TableCell>MODE</TableCell>
                          <TableCell align="right">CUMUL</TableCell>
                          {showM &&
                            MOIS_LABELS.map((m) => (
                              <TableCell key={m} align="right" sx={CELLULE_DETAIL}>
                                {m}
                              </TableCell>
                            ))}
                        </TableRow>
                      </TableHead>
                      <TableBody>
                        {bloc.lignes.map((l, idx) => {
                          const mois = [
                            l.m01, l.m02, l.m03, l.m04, l.m05, l.m06, l.m07, l.m08, l.m09, l.m10, l.m11, l.m12,
                          ];
                          const mensuelDetail =
                            l.typeLigne === 'DETAIL' &&
                            (l.codeMode?.toUpperCase() === 'MENSUEL' || l.codeMode?.toUpperCase() === 'MENS');
                          const agregeMensuel = l.estAgrege && mois.some((v) => v != null);
                          if (l.typeLigne === 'CATEGORIE') {
                            return (
                              <TableRow key={`c-${idx}`}>
                                <TableCell
                                  colSpan={showM && !isMobile ? 16 : 4}
                                  sx={{ bgcolor: 'primary.light', fontWeight: 700 }}
                                >
                                  {l.codeAffichage ? `${l.codeAffichage} ` : ''}
                                  {l.libelle}
                                </TableCell>
                              </TableRow>
                            );
                          }
                          return (
                            <TableRow key={`l-${idx}`}>
                              <TableCell sx={{ fontWeight: l.typeLigne === 'ITEM' || l.typeLigne === 'TOTAL_UB' ? 700 : 400, pl: l.typeLigne === 'DETAIL' ? 3 : 1 }}>
                                {l.typeLigne === 'DETAIL' ? l.codeAffichage ?? '' : l.codeAffichage ?? l.codeItem ?? ''}
                              </TableCell>
                              <TableCell sx={{ fontWeight: l.typeLigne === 'ITEM' || l.typeLigne === 'TOTAL_UB' ? 700 : 400 }}>
                                {l.libelle}
                              </TableCell>
                              <TableCell>
                                {l.typeLigne === 'DETAIL' ? formatModeRapport(l.codeMode) : ''}
                              </TableCell>
                              <TableCell align="right" sx={{ fontWeight: l.typeLigne === 'TOTAL_UB' ? 800 : 400 }}>
                                {formatUsdFr(l.montantCumul)}
                              </TableCell>
                              {showM &&
                                mois.map((v, i) => (
                                  <TableCell key={i} align="right" sx={CELLULE_DETAIL}>
                                    {formatMoisCell(v, !!mensuelDetail, agregeMensuel)}
                                  </TableCell>
                                ))}
                            </TableRow>
                          );
                        })}
                      </TableBody>
                    </Table>
                  </Box>
                );
              })}

              <Typography align="right" sx={{ fontWeight: 800, mt: 1 }}>
                TOTAL DÉPARTEMENT : {formatUsdFr(dept.totalDepartement)}
              </Typography>
            </Box>
          ))}

          {rapport.syntheseEntite && (
            <Box sx={{ mt: 4 }}>
              <Typography
                variant="subtitle1"
                sx={{ bgcolor: 'primary.dark', color: 'primary.contrastText', px: 1.5, py: 0.75 }}
              >
                {rapport.syntheseEntite.titre}
              </Typography>
              <Table size="small" sx={{ mt: 1 }}>
                <TableHead>
                  <TableRow>
                    <TableCell>ITEM / DÉSIGNATION</TableCell>
                    {rapport.syntheseEntite.colonnesDepartement.map((c) => (
                      <TableCell key={c.codeDepartement} align="right" sx={CELLULE_DETAIL}>
                        {c.codeDepartement}
                      </TableCell>
                    ))}
                    <TableCell align="right">TOTAL</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {rapport.syntheseEntite.lignes.map((l, idx) => (
                    <TableRow key={`s-${idx}`}>
                      <TableCell sx={{ fontWeight: l.typeLigne === 'CATEGORIE' || l.typeLigne === 'TOTAL' ? 700 : 400 }}>
                        {l.codeAffichage ? `${l.codeAffichage} ` : ''}
                        {l.libelle}
                      </TableCell>
                      {l.montantsParDepartement.map((v, i) => (
                        <TableCell key={i} align="right" sx={CELLULE_DETAIL}>
                          {formatUsdFr(v)}
                        </TableCell>
                      ))}
                      <TableCell align="right" sx={{ fontWeight: 700 }}>
                        {formatUsdFr(l.totalLigne)}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </Box>
          )}
        </Paper>
      )}
    </Box>
  );
}
