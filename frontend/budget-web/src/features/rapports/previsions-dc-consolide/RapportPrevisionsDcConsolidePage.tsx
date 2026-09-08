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
  downloadRapportPrevisionsDcConsolidePdf,
  fetchExercices,
  fetchRapportPrevisionsDcConsolide,
  fetchStructures,
  fetchUnitesBudgetaires,
  fetchVersionsBudgetaires,
  type Exercice,
  type RapportDcConsolideDto,
  type Structure,
  type UniteBudgetaire,
  type VersionBudgetaire,
} from '../../../services/apiClient';
import { formatUsdCell, formatUsdFr, formatModeRapport, MOIS_LABELS, STATUTS_CONSULTATION } from '../previsions-dc/rapportDcUtils';
import {
  buildStructuresById,
  structuresAccessiblesParType,
} from '../rapportPerimetre';

/**
 * Colonnes mensuelles masquées sous `md` : sur téléphone on garde l'item, la
 * désignation, le mode et le cumul. Les douze mois reviennent dès `md`.
 */
const CELLULE_DETAIL = { display: { xs: 'none', md: 'table-cell' } } as const;

function apiErrorMessage(err: unknown, fallback: string): string {
  if (err && typeof err === 'object' && 'response' in err) {
    const data = (err as { response?: { data?: { message?: string } } }).response?.data;
    if (data?.message) return data.message;
  }
  return fallback;
}

export function RapportPrevisionsDcConsolidePage() {
  const { isMobile } = useResponsive();
  const [exercices, setExercices] = useState<Exercice[]>([]);
  const [versions, setVersions] = useState<VersionBudgetaire[]>([]);
  const [structures, setStructures] = useState<Structure[]>([]);
  const [ubs, setUbs] = useState<UniteBudgetaire[]>([]);
  const [idExercice, setIdExercice] = useState('');
  const [idVersion, setIdVersion] = useState('');
  const [entite, setEntite] = useState<Structure | null>(null);
  const [departement, setDepartement] = useState<Structure | null>(null);
  const [division, setDivision] = useState<Structure | null>(null);
  const [statut, setStatut] = useState('VALIDEE');
  const [rapport, setRapport] = useState<RapportDcConsolideDto | null>(null);
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
    if (!entite) {
      return structuresAccessiblesParType(structuresById, ubs, 'DEPARTEMENT');
    }
    return structuresAccessiblesParType(
      structuresById,
      ubs,
      'DEPARTEMENT',
      entite.idStructure,
    );
  }, [structuresById, ubs, entite]);

  const divisionsOptions = useMemo(() => {
    const root = departement ?? entite;
    if (!root) {
      return structuresAccessiblesParType(structuresById, ubs, 'DIVISION');
    }
    return structuresAccessiblesParType(structuresById, ubs, 'DIVISION', root.idStructure);
  }, [structuresById, ubs, entite, departement]);

  const versionsFiltrees = useMemo(
    () => versions.filter((v) => !idExercice || String(v.idExercice) === idExercice),
    [versions, idExercice],
  );

  useEffect(() => {
    if (versionsFiltrees[0] && !versionsFiltrees.some((v) => String(v.idVersion) === idVersion)) {
      setIdVersion(String(versionsFiltrees[0].idVersion));
    }
  }, [versionsFiltrees, idVersion]);

  const buildParams = () => {
    if (!idVersion) throw new Error('Version obligatoire.');
    return {
      idVersion: Number(idVersion),
      statutConsultation: statut,
      ...(entite ? { idEntite: entite.idStructure } : {}),
      ...(departement ? { idDepartementStructure: departement.idStructure } : {}),
      ...(division ? { idDivision: division.idStructure } : {}),
    };
  };

  const loadPreview = async () => {
    setLoading(true);
    setError(null);
    try {
      setRapport(await fetchRapportPrevisionsDcConsolide(buildParams()));
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
      const blob = await downloadRapportPrevisionsDcConsolidePdf(buildParams());
      const url = URL.createObjectURL(blob);
      if (mode === 'download') {
        const a = document.createElement('a');
        a.href = url;
        a.download = 'rapport-dc-consolide.pdf';
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
        title="Prévisions budgétaires mensualisées des dépenses courantes"
        subtitle="Synthèse consolidée DC — toutes les UB du périmètre (USD)"
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
            }}
            renderInput={(params) => <TextField {...params} label="Entité (Toutes)" />}
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
            }}
            renderInput={(params) => <TextField {...params} label="Département (Tous)" />}
          />
          <Autocomplete
            size="small"
            fullWidth
            options={divisionsOptions}
            getOptionLabel={(o) => `${o.code} — ${o.libelle}`}
            value={division}
            onChange={(_, v) => setDivision(v)}
            renderInput={(params) => <TextField {...params} label="Division (Toutes)" />}
          />
          <TextField select size="small"
            fullWidth
            label="Statut"
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
          <Button
            variant="contained"
            startIcon={<VisibilityOutlinedIcon />}
            disabled={loading || !idVersion}
            onClick={() => void loadPreview()}
          >
            Aperçu
          </Button>
          <Button
            variant="outlined"
            startIcon={<PictureAsPdfOutlinedIcon />}
            disabled={loading || !idVersion}
            onClick={() => void openPdf('download')}
          >
            Télécharger PDF
          </Button>
          <Button
            variant="outlined"
            startIcon={<PrintOutlinedIcon />}
            disabled={loading || !idVersion}
            onClick={() => void openPdf('print')}
          >
            Imprimer
          </Button>
        </Stack>
      </Paper>

      {rapport && (
        <Paper sx={{ p: 2, overflow: 'auto' }}>
          <Typography variant="h6">{rapport.enTete.titre}</Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
            {rapport.enTete.nbUB} UB · {formatUsdFr(rapport.enTete.montantTotalDc)} ·{' '}
            {rapport.enTete.statutFiltre}
            {rapport.enTete.codeEntite ? ` · ${rapport.enTete.codeEntite}` : ' · TOUTES LES UB'}
          </Typography>

          {rapport.groupes.length === 0 && (
            <Alert severity="info">Aucune prévision DC consolidée pour ces critères.</Alert>
          )}

          {rapport.groupes.length > 0 && (
            <Table size="small">
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
                {rapport.groupes.map((g) => (
                  <Fragment key={`g-${g.idGroupeRB ?? 'x'}-${g.codeGroupe}-${g.libelle}`}>
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
                      const mois = [
                        l.m01, l.m02, l.m03, l.m04, l.m05, l.m06, l.m07, l.m08, l.m09, l.m10, l.m11, l.m12,
                      ];
                      return (
                        <TableRow key={`${l.idRB}-${l.codeMode}`}>
                          <TableCell>{l.codeRB}</TableCell>
                          <TableCell>{l.libelle}</TableCell>
                          <TableCell>{formatModeRapport(l.codeMode)}</TableCell>
                          <TableCell align="right">{formatUsdFr(l.montantCumul)}</TableCell>
                          {mois.map((v, i) => (
                            <TableCell key={i} align="right" sx={CELLULE_DETAIL}>
                              {formatUsdCell(v, mensuel)}
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
                        {formatUsdFr(g.sousTotalDc)}
                      </TableCell>
                      <TableCell colSpan={12} sx={CELLULE_DETAIL} />
                    </TableRow>
                  </Fragment>
                ))}
                <TableRow>
                  <TableCell colSpan={3} align="right" sx={{ fontWeight: 800 }}>
                    TOTAL GÉNÉRAL DC
                  </TableCell>
                  <TableCell align="right" sx={{ fontWeight: 800 }}>
                    {formatUsdFr(rapport.enTete.montantTotalDc)}
                  </TableCell>
                  <TableCell colSpan={12} sx={CELLULE_DETAIL} />
                </TableRow>
              </TableBody>
            </Table>
          )}
        </Paper>
      )}
    </Box>
  );
}
