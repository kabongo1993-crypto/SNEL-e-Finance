import { Fragment, useEffect, useMemo, useState } from 'react';
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
import PrintOutlinedIcon from '@mui/icons-material/PrintOutlined';
import VisibilityOutlinedIcon from '@mui/icons-material/VisibilityOutlined';
import { FilterFields, PageHeader } from '../../../components';
import { useResponsive } from '../../../theme/useResponsive';
import {
  downloadRapportPrevisionsAePdf,
  fetchExercices,
  fetchRapportPrevisionsAe,
  fetchStructures,
  fetchUnitesBudgetaires,
  fetchVersionsBudgetaires,
  type Exercice,
  type RapportAeDetailMensuel,
  type RapportAeDto,
  type RapportAeNiveau,
  type Structure,
  type UniteBudgetaire,
  type VersionBudgetaire,
} from '../../../services/apiClient';
import {
  formatUsdAeCell,
  formatUsdFr,
  formatModeRapport,
  MOIS_FILTRE_OPTIONS,
  MOIS_LABELS,
  STATUTS_CONSULTATION,
} from './rapportAeUtils';
import {
  buildStructuresById,
  filterUbsSousStructure,
  findAncestor,
  structuresAccessiblesParType,
} from '../rapportPerimetre';

/**
 * Colonnes de détail (rubriques, mois) masquées sous `md` : sur téléphone on ne
 * garde que la désignation et le montant de synthèse. Le rapport retrouve la
 * totalité de ses colonnes dès `md`.
 */
const CELLULE_DETAIL = { display: { xs: 'none', md: 'table-cell' } } as const;

function apiErrorMessage(err: unknown, fallback: string): string {
  if (err && typeof err === 'object' && 'response' in err) {
    const data = (err as { response?: { data?: { message?: string } } }).response?.data;
    if (data?.message) return data.message;
  }
  return fallback;
}

function DetailTable({ detail }: { detail: RapportAeDetailMensuel }) {
  const { isMobile } = useResponsive();
  const nbRb = detail.colonnesRb.length;
  return (
    <Table size="small" sx={{ mb: 1 }}>
      <TableHead>
        <TableRow>
          <TableCell>N°</TableCell>
          <TableCell>DÉSIGNATION</TableCell>
          {detail.colonnesRb.map((c) => (
            <TableCell key={c.idRB} align="right" sx={CELLULE_DETAIL}>
              {c.codeRB}
            </TableCell>
          ))}
          <TableCell align="right">TOTAL</TableCell>
        </TableRow>
      </TableHead>
      <TableBody>
        {detail.lignes.map((l, idx) => {
          if (l.typeLigne === 'GROUPE') {
            return (
              <TableRow key={`g-${idx}`}>
                <TableCell
                  colSpan={isMobile ? 3 : nbRb + 3}
                  sx={{ bgcolor: 'primary.light', fontWeight: 700 }}
                >
                  {l.libelleGroupe}
                </TableCell>
              </TableRow>
            );
          }
          return (
            <TableRow key={`i-${l.libelleItemAE}-${idx}`}>
              <TableCell>{l.numero}</TableCell>
              <TableCell>{l.libelleItemAE}</TableCell>
              {l.montantsParRb.map((v, i) => (
                <TableCell key={i} align="right" sx={CELLULE_DETAIL}>
                  {formatUsdAeCell(v)}
                </TableCell>
              ))}
              <TableCell align="right">{formatUsdFr(l.total ?? 0)}</TableCell>
            </TableRow>
          );
        })}
        <TableRow>
          <TableCell colSpan={2} align="right" sx={{ fontWeight: 700 }}>
            TOTAL
          </TableCell>
          {detail.totauxParRb.map((v, i) => (
            <TableCell key={i} align="right" sx={{ ...CELLULE_DETAIL, fontWeight: 700 }}>
              {formatUsdFr(v)}
            </TableCell>
          ))}
          <TableCell align="right" sx={{ fontWeight: 700 }}>
            {formatUsdFr(detail.totalGeneral)}
          </TableCell>
        </TableRow>
      </TableBody>
    </Table>
  );
}

export function RapportPrevisionsAePage() {
  const { isMobile } = useResponsive();
  const [exercices, setExercices] = useState<Exercice[]>([]);
  const [versions, setVersions] = useState<VersionBudgetaire[]>([]);
  const [structures, setStructures] = useState<Structure[]>([]);
  const [ubs, setUbs] = useState<UniteBudgetaire[]>([]);

  const [idExercice, setIdExercice] = useState('');
  const [idVersion, setIdVersion] = useState('');
  const [niveau, setNiveau] = useState<RapportAeNiveau>('ENTITE');
  const [entite, setEntite] = useState<Structure | null>(null);
  const [departement, setDepartement] = useState<Structure | null>(null);
  const [division, setDivision] = useState<Structure | null>(null);
  const [ub, setUb] = useState<UniteBudgetaire | null>(null);
  const [statut, setStatut] = useState('VALIDEE');
  const [mois, setMois] = useState('');

  const [rapport, setRapport] = useState<RapportAeDto | null>(null);
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

  const departementsOptions = useMemo(() => {
    if (!entite) return [];
    return structuresAccessiblesParType(
      structuresById,
      ubs,
      'DEPARTEMENT',
      entite.idStructure,
    );
  }, [structuresById, ubs, entite]);

  const divisionsOptions = useMemo(() => {
    const root = departement ?? entite;
    if (!root) return [];
    return structuresAccessiblesParType(structuresById, ubs, 'DIVISION', root.idStructure);
  }, [structuresById, ubs, entite, departement]);

  const ubsOptions = useMemo(() => {
    let list = ubs;
    if (entite) {
      list = list.filter((u) => {
        const e = findAncestor(structuresById, u.idStructure, 'ENTITE');
        return e?.idStructure === entite.idStructure;
      });
    }
    if (departement) {
      list = filterUbsSousStructure(structuresById, list, departement.idStructure);
    }
    if (division) {
      list = filterUbsSousStructure(structuresById, list, division.idStructure);
    }
    return list;
  }, [ubs, entite, departement, division, structuresById]);

  const versionsFiltrees = useMemo(
    () => versions.filter((v) => !idExercice || String(v.idExercice) === idExercice),
    [versions, idExercice],
  );

  useEffect(() => {
    if (versionsFiltrees[0] && !versionsFiltrees.some((v) => String(v.idVersion) === idVersion)) {
      setIdVersion(String(versionsFiltrees[0].idVersion));
    }
  }, [versionsFiltrees, idVersion]);

  const canSubmit = useMemo(() => {
    if (!idVersion || !entite || !mois) return false;
    if (niveau === 'DEPARTEMENT' && !departement) return false;
    if (niveau === 'UB' && (!departement || !ub)) return false;
    return true;
  }, [idVersion, entite, departement, ub, niveau, mois]);

  const buildParams = () => {
    if (!idVersion || !entite) throw new Error('Version et Entité obligatoires.');
    if (!mois) throw new Error('Le mois est obligatoire.');
    return {
      idVersion: Number(idVersion),
      niveau,
      mois,
      idEntite: entite.idStructure,
      statutConsultation: statut,
      ...(departement ? { idDepartementStructure: departement.idStructure } : {}),
      ...(division ? { idDivision: division.idStructure } : {}),
      ...(ub ? { idUB: ub.idUB } : {}),
    };
  };

  const loadPreview = async () => {
    setLoading(true);
    setError(null);
    try {
      setRapport(await fetchRapportPrevisionsAe(buildParams()));
    } catch (err) {
      setRapport(null);
      setError(apiErrorMessage(err, 'Impossible de charger l’aperçu.'));
    } finally {
      setLoading(false);
    }
  };

  const openPdf = async (mode: 'download' | 'print') => {
    setLoading(true);
    setError(null);
    try {
      const blob = await downloadRapportPrevisionsAePdf(buildParams());
      const url = URL.createObjectURL(blob);
      if (mode === 'download') {
        const a = document.createElement('a');
        a.href = url;
        a.download = 'rapport-ae.pdf';
        a.click();
      } else {
        window.open(url, '_blank');
      }
      setTimeout(() => URL.revokeObjectURL(url), 60_000);
    } catch (err) {
      setError(apiErrorMessage(err, 'Impossible de générer le PDF.'));
    } finally {
      setLoading(false);
    }
  };

  return (
    <Box>
      <PageHeader
        title="Prévisions — Actions d'exploitation (AE)"
        subtitle="Budget détaillé des actions par item × rubriques — PDF SNEL"
      />

      {bootError && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {bootError}
        </Alert>
      )}
      {error && (
        <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError(null)}>
          {error}
        </Alert>
      )}

      <Paper sx={{ p: 2, mb: 2 }}>
        <FilterFields>
          <TextField select size="small"
            fullWidth
            label="Exercice"
            value={idExercice}
            onChange={(e) => {
              setIdExercice(e.target.value);
              setIdVersion('');
            }}
          >
            {exercices.map((e) => (
              <MenuItem key={e.idExercice} value={String(e.idExercice)}>
                {e.annee}
              </MenuItem>
            ))}
          </TextField>
          <TextField select size="small"
            fullWidth
            label="Version"
            value={idVersion}
            onChange={(e) => setIdVersion(e.target.value)}
          >
            {versionsFiltrees.map((v) => (
              <MenuItem key={v.idVersion} value={String(v.idVersion)}>
                V{String(v.numeroVersion).padStart(2, '0')} — {v.libelle}
              </MenuItem>
            ))}
          </TextField>
          <TextField select size="small"
            fullWidth
            label="Niveau"
            value={niveau}
            onChange={(e) => {
              setNiveau(e.target.value as RapportAeNiveau);
              setUb(null);
            }}
          >
            <MenuItem value="ENTITE">Entité</MenuItem>
            <MenuItem value="DEPARTEMENT">Département</MenuItem>
            <MenuItem value="UB">Unité budgétaire</MenuItem>
          </TextField>
          <Autocomplete
            size="small"
            fullWidth
            options={entites}
            getOptionLabel={(o) => `${o.code} — ${o.libelle}`}
            value={entite}
            onChange={(_, v) => {
              setEntite(v);
              setDepartement(null);
              setDivision(null);
              setUb(null);
            }}
            renderInput={(params) => <TextField {...params} label="Entité *" />}
          />
          <Autocomplete
            size="small"
            fullWidth
            options={departementsOptions}
            getOptionLabel={(o) => `${o.code} — ${o.libelle}`}
            value={departement}
            onChange={(_, v) => {
              setDepartement(v);
              setDivision(null);
              setUb(null);
            }}
            renderInput={(params) => (
              <TextField
                {...params}
                label={niveau === 'ENTITE' ? 'Département (facultatif)' : 'Département *'}
              />
            )}
          />
          <Autocomplete
            size="small"
            fullWidth
            options={divisionsOptions}
            getOptionLabel={(o) => `${o.code} — ${o.libelle}`}
            value={division}
            onChange={(_, v) => {
              setDivision(v);
              setUb(null);
            }}
            renderInput={(params) => <TextField {...params} label="Division (facultatif)" />}
          />
          <Autocomplete
            size="small"
            fullWidth
            options={ubsOptions}
            getOptionLabel={(o) => `${o.codeUB} — ${o.libelle}`}
            value={ub}
            onChange={(_, v) => setUb(v)}
            renderInput={(params) => (
              <TextField {...params} label={niveau === 'UB' ? 'UB *' : 'UB (vide = toutes)'} />
            )}
          />
          <TextField select size="small"
            fullWidth
            label="Statut consultation"
            value={statut}
            onChange={(e) => setStatut(e.target.value)}
          >
            {STATUTS_CONSULTATION.map((s) => (
              <MenuItem key={s} value={s}>
                {s}
              </MenuItem>
            ))}
          </TextField>
          <TextField select size="small"
            fullWidth
            label="Mois *"
            value={mois}
            onChange={(e) => setMois(e.target.value)}
            required
            error={!mois}
            helperText={!mois ? 'Obligatoire' : undefined}
          >
            {MOIS_FILTRE_OPTIONS.map((o) => (
              <MenuItem key={o.value} value={o.value}>
                {o.label}
              </MenuItem>
            ))}
          </TextField>
        </FilterFields>

        <Stack direction="row" spacing={1} useFlexGap sx={{ mt: 2.5, flexWrap: 'wrap' }}>
          <Button
            variant="contained"
            startIcon={<VisibilityOutlinedIcon />}
            disabled={loading || !canSubmit}
            onClick={() => void loadPreview()}
          >
            Aperçu
          </Button>
          <Button
            variant="outlined"
            startIcon={<PictureAsPdfOutlinedIcon />}
            disabled={loading || !canSubmit}
            onClick={() => void openPdf('download')}
          >
            Télécharger PDF
          </Button>
          <Button
            variant="outlined"
            startIcon={<PrintOutlinedIcon />}
            disabled={loading || !canSubmit}
            onClick={() => void openPdf('print')}
          >
            Imprimer
          </Button>
        </Stack>
      </Paper>

      {rapport && (
        <Paper sx={{ p: 2, overflow: 'auto' }}>
          <Typography variant="h6">{rapport.enTete.titre}</Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
            {rapport.enTete.codeEntite} — {rapport.enTete.libelleEntite}
            {' · '}
            Mois {rapport.enTete.libelleMois} · {rapport.enTete.nbUB} UB ·{' '}
            {formatUsdFr(rapport.enTete.montantTotalAe)} · {rapport.enTete.statutFiltre}
          </Typography>

          {rapport.blocsUb.length === 0 && (
            <Alert severity="info">Aucune prévision AE pour ces critères.</Alert>
          )}

          {rapport.blocsUb.map((bloc) => (
            <Box key={bloc.identite.idUB} sx={{ mb: 3 }}>
              <Typography
                variant="subtitle1"
                sx={{ bgcolor: 'primary.main', color: 'primary.contrastText', px: 1.5, py: 0.75 }}
              >
                UB {bloc.identite.codeUB} — {bloc.identite.libelleUB}
              </Typography>
              {bloc.detailsMensuels.map((detail) => (
                <Box key={`${bloc.identite.idUB}-${detail.mois}`} sx={{ mt: 1.5 }}>
                  {rapport.estTousLesMois && (
                    <Typography
                      variant="subtitle2"
                      sx={{ bgcolor: 'primary.dark', color: 'primary.contrastText', px: 1, py: 0.5, mb: 0.5 }}
                    >
                      {detail.libelleMois}
                    </Typography>
                  )}
                  <DetailTable detail={detail} />
                </Box>
              ))}
            </Box>
          ))}

          {rapport.syntheseItem && (
            <Box sx={{ mt: 4 }}>
              <Typography
                variant="subtitle1"
                sx={{ bgcolor: 'primary.dark', color: 'primary.contrastText', px: 1.5, py: 0.75 }}
              >
                {rapport.syntheseItem.titre}
              </Typography>
              <Table size="small" sx={{ mt: 1 }}>
                <TableHead>
                  <TableRow>
                    <TableCell>N°</TableCell>
                    <TableCell>DÉSIGNATION</TableCell>
                    {MOIS_LABELS.map((m) => (
                      <TableCell key={m} align="right" sx={CELLULE_DETAIL}>
                        {m}
                      </TableCell>
                    ))}
                    <TableCell align="right">TOTAL</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {rapport.syntheseItem.lignes.map((l, idx) => {
                    if (l.typeLigne === 'GROUPE') {
                      return (
                        <TableRow key={`si-g-${idx}`}>
                          <TableCell
                            colSpan={isMobile ? 3 : 15}
                            sx={{ bgcolor: 'primary.light', fontWeight: 700 }}
                          >
                            {l.libelleGroupe}
                          </TableCell>
                        </TableRow>
                      );
                    }
                    const moisVals = [
                      l.m01, l.m02, l.m03, l.m04, l.m05, l.m06, l.m07, l.m08, l.m09, l.m10, l.m11, l.m12,
                    ];
                    return (
                      <TableRow key={`si-i-${idx}`}>
                        <TableCell>{l.numero}</TableCell>
                        <TableCell>{l.libelleItemAE}</TableCell>
                        {moisVals.map((v, i) => (
                          <TableCell key={i} align="right" sx={CELLULE_DETAIL}>
                            {formatUsdAeCell(v)}
                          </TableCell>
                        ))}
                        <TableCell align="right">{formatUsdFr(l.total ?? 0)}</TableCell>
                      </TableRow>
                    );
                  })}
                  <TableRow>
                    <TableCell colSpan={isMobile ? 2 : 14} align="right" sx={{ fontWeight: 700 }}>
                      TOTAL
                    </TableCell>
                    <TableCell align="right" sx={{ fontWeight: 700 }}>
                      {formatUsdFr(rapport.syntheseItem.totalGeneral)}
                    </TableCell>
                  </TableRow>
                </TableBody>
              </Table>
            </Box>
          )}

          {rapport.syntheseRb && (
            <Box sx={{ mt: 4 }}>
              <Typography
                variant="subtitle1"
                sx={{ bgcolor: 'primary.dark', color: 'primary.contrastText', px: 1.5, py: 0.75 }}
              >
                {rapport.syntheseRb.titre}
              </Typography>
              <Table size="small" sx={{ mt: 1 }}>
                <TableHead>
                  <TableRow>
                    <TableCell>ITEM</TableCell>
                    <TableCell>DESIGNATION</TableCell>
                    <TableCell>MODE</TableCell>
                    <TableCell align="right">CUMUL</TableCell>
                    {MOIS_LABELS.map((m) => (
                      <TableCell key={m} align="right" sx={CELLULE_DETAIL}>
                        {m}
                      </TableCell>
                    ))}
                  </TableRow>
                </TableHead>
                <TableBody>
                  {rapport.syntheseRb.groupes.map((g) => (
                    <Fragment key={`rg-${g.codeGroupe}-${g.libelle}`}>
                      <TableRow>
                        <TableCell
                          colSpan={isMobile ? 4 : 16}
                          sx={{ bgcolor: 'primary.light', fontWeight: 700 }}
                        >
                          {g.codeGroupe} — {g.libelle}
                        </TableCell>
                      </TableRow>
                      {g.lignes.map((l) => {
                        const mensuel = l.codeMode.toUpperCase() === 'MENSUEL';
                        const moisVals = [
                          l.m01, l.m02, l.m03, l.m04, l.m05, l.m06, l.m07, l.m08, l.m09, l.m10, l.m11, l.m12,
                        ];
                        return (
                          <TableRow key={`${l.codeRB}-${l.codeMode}`}>
                            <TableCell>{l.codeRB}</TableCell>
                            <TableCell>{l.libelle}</TableCell>
                            <TableCell>{formatModeRapport(l.codeMode)}</TableCell>
                            <TableCell align="right">{formatUsdFr(l.montantCumul)}</TableCell>
                            {moisVals.map((v, i) => (
                              <TableCell key={i} align="right" sx={CELLULE_DETAIL}>
                                {mensuel ? formatUsdAeCell(v ?? 0) : '—'}
                              </TableCell>
                            ))}
                          </TableRow>
                        );
                      })}
                      <TableRow>
                        <TableCell colSpan={3} align="right" sx={{ fontWeight: 700 }}>
                          Sous-total groupe
                        </TableCell>
                        <TableCell align="right" sx={{ fontWeight: 700 }}>
                          {formatUsdFr(g.sousTotal)}
                        </TableCell>
                        <TableCell colSpan={12} sx={CELLULE_DETAIL} />
                      </TableRow>
                    </Fragment>
                  ))}
                  <TableRow>
                    <TableCell colSpan={3} align="right" sx={{ fontWeight: 800 }}>
                      TOTAL GÉNÉRAL AE
                    </TableCell>
                    <TableCell align="right" sx={{ fontWeight: 800 }}>
                      {formatUsdFr(rapport.syntheseRb.totalGeneral)}
                    </TableCell>
                    <TableCell colSpan={12} sx={CELLULE_DETAIL} />
                  </TableRow>
                </TableBody>
              </Table>
            </Box>
          )}
        </Paper>
      )}
    </Box>
  );
}
