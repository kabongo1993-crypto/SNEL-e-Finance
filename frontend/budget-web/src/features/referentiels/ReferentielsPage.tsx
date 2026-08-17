import { useEffect, useState } from 'react';
import {
  Alert,
  Box,
  CircularProgress,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Typography,
} from '@mui/material';
import { PageHeader } from '../../components';
import { fetchHealth, fetchModesPrevision, fetchTypesBudget } from '../../services/apiClient';
import type { ModePrevision, TypeBudget } from '../../services/apiClient';

export function ReferentielsPage() {
  const [typesBudget, setTypesBudget] = useState<TypeBudget[]>([]);
  const [modesPrevision, setModesPrevision] = useState<ModePrevision[]>([]);
  const [health, setHealth] = useState<{ status: string; database: boolean } | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    async function load() {
      try {
        const [types, modes, healthStatus] = await Promise.all([
          fetchTypesBudget(),
          fetchModesPrevision(),
          fetchHealth(),
        ]);
        setTypesBudget(types);
        setModesPrevision(modes);
        setHealth(healthStatus);
      } catch {
        setError("Impossible de contacter l'API backend. Vérifiez que l'API est démarrée.");
      } finally {
        setLoading(false);
      }
    }

    void load();
  }, []);

  if (loading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', py: 4 }}>
        <CircularProgress />
      </Box>
    );
  }

  if (error) {
    return <Alert severity="warning">{error}</Alert>;
  }

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 3 }}>
      <PageHeader
        title="Types & modes budgétaires"
        subtitle="Référentiels techniques connectés à l’API (types de budget et modes de prévision)."
        breadcrumbs={[
          { label: 'e-Finance', to: '/dashboard' },
          { label: 'Référentiels', to: '/referentiels/organisationnel' },
          { label: 'Types & modes' },
        ]}
      />

      {health && (
        <Alert severity={health.database ? 'success' : 'error'}>
          API : {health.status} — Base de données : {health.database ? 'connectée' : 'indisponible'}
        </Alert>
      )}

      <Paper sx={{ p: 2 }}>
        <Typography variant="h6" gutterBottom>
          Types de budget
        </Typography>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Code</TableCell>
              <TableCell>Libellé</TableCell>
              <TableCell>Ordre</TableCell>
              <TableCell>Actif</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {typesBudget.map((type) => (
              <TableRow key={type.idTypeBudget}>
                <TableCell>{type.codeType}</TableCell>
                <TableCell>{type.libelle}</TableCell>
                <TableCell>{type.ordreAffichage}</TableCell>
                <TableCell>{type.actif ? 'Oui' : 'Non'}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Paper>

      <Paper sx={{ p: 2 }}>
        <Typography variant="h6" gutterBottom>
          Modes de prévision
        </Typography>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Code</TableCell>
              <TableCell>Libellé</TableCell>
              <TableCell>Actif</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {modesPrevision.map((mode) => (
              <TableRow key={mode.idModePrevision}>
                <TableCell>{mode.codeMode}</TableCell>
                <TableCell>{mode.libelle}</TableCell>
                <TableCell>{mode.actif ? 'Oui' : 'Non'}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Paper>
    </Box>
  );
}
