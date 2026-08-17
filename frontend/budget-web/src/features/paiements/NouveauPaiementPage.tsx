import CloudUploadIcon from '@mui/icons-material/CloudUpload';
import {
  Box,
  Button,
  Grid,
  MenuItem,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { useState } from 'react';
import { Link as RouterLink, useNavigate } from 'react-router-dom';
import { FormSection, PageHeader } from '../../components';
import { MOCK_DEPARTEMENTS, MOCK_UNITES } from '../../mocks/types';

export function NouveauPaiementPage() {
  const navigate = useNavigate();
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [form, setForm] = useState({
    date: '2026-08-16',
    montant: '',
    devise: 'CDF',
    departement: '',
    structure: '',
    uniteBudgetaire: '',
    beneficiaire: '',
    objet: '',
    commentaire: '',
  });

  const set = (key: keyof typeof form, value: string) => {
    setForm((prev) => ({ ...prev, [key]: value }));
    setErrors((prev) => ({ ...prev, [key]: '' }));
  };

  const validate = () => {
    const next: Record<string, string> = {};
    if (!form.montant) next.montant = 'Montant obligatoire';
    if (!form.departement) next.departement = 'Département obligatoire';
    if (!form.uniteBudgetaire) next.uniteBudgetaire = 'Unité budgétaire obligatoire';
    if (!form.beneficiaire) next.beneficiaire = 'Bénéficiaire obligatoire';
    if (!form.objet) next.objet = 'Objet obligatoire';
    setErrors(next);
    return Object.keys(next).length === 0;
  };

  return (
    <Box>
      <PageHeader
        title="Nouveau paiement"
        subtitle="Création d’une demande de paiement. Les listes département / UB seront branchées sur le référentiel organisationnel."
        breadcrumbs={[
          { label: 'e-Finance', to: '/dashboard' },
          { label: 'Paiements', to: '/paiements' },
          { label: 'Nouveau' },
        ]}
      />

      <FormSection
        title="Informations générales"
        description="Champs marqués * sont obligatoires."
      >
        <Grid container spacing={2}>
          <Grid size={{ xs: 12, sm: 6, md: 3 }}>
            <TextField
              fullWidth
              required
              type="date"
              label="Date"
              value={form.date}
              onChange={(e) => set('date', e.target.value)}
              slotProps={{ inputLabel: { shrink: true } }}
            />
          </Grid>
          <Grid size={{ xs: 12, sm: 6, md: 3 }}>
            <TextField
              fullWidth
              required
              label="Montant"
              value={form.montant}
              error={Boolean(errors.montant)}
              helperText={errors.montant}
              onChange={(e) => set('montant', e.target.value)}
            />
          </Grid>
          <Grid size={{ xs: 12, sm: 6, md: 3 }}>
            <TextField
              select
              fullWidth
              required
              label="Devise"
              value={form.devise}
              onChange={(e) => set('devise', e.target.value)}
            >
              <MenuItem value="CDF">CDF</MenuItem>
              <MenuItem value="USD">USD</MenuItem>
              <MenuItem value="EUR">EUR</MenuItem>
            </TextField>
          </Grid>
          <Grid size={{ xs: 12, sm: 6, md: 3 }}>
            <TextField
              select
              fullWidth
              required
              label="Département"
              value={form.departement}
              error={Boolean(errors.departement)}
              helperText={errors.departement || 'Prêt pour branchement API référentiel'}
              onChange={(e) => set('departement', e.target.value)}
            >
              {MOCK_DEPARTEMENTS.map((d) => (
                <MenuItem key={d.id} value={d.id}>
                  {d.id} — {d.label}
                </MenuItem>
              ))}
            </TextField>
          </Grid>
          <Grid size={{ xs: 12, sm: 6 }}>
            <TextField
              fullWidth
              label="Structure"
              value={form.structure}
              onChange={(e) => set('structure', e.target.value)}
              helperText="Sera alimenté par le référentiel organisationnel"
            />
          </Grid>
          <Grid size={{ xs: 12, sm: 6 }}>
            <TextField
              select
              fullWidth
              required
              label="Unité budgétaire"
              value={form.uniteBudgetaire}
              error={Boolean(errors.uniteBudgetaire)}
              helperText={errors.uniteBudgetaire}
              onChange={(e) => set('uniteBudgetaire', e.target.value)}
            >
              {MOCK_UNITES.map((u) => (
                <MenuItem key={u.id} value={u.id}>
                  {u.id} — {u.label}
                </MenuItem>
              ))}
            </TextField>
          </Grid>
          <Grid size={{ xs: 12 }}>
            <TextField
              fullWidth
              required
              label="Bénéficiaire"
              value={form.beneficiaire}
              error={Boolean(errors.beneficiaire)}
              helperText={errors.beneficiaire}
              onChange={(e) => set('beneficiaire', e.target.value)}
            />
          </Grid>
          <Grid size={{ xs: 12 }}>
            <TextField
              fullWidth
              required
              multiline
              minRows={2}
              label="Objet"
              value={form.objet}
              error={Boolean(errors.objet)}
              helperText={errors.objet}
              onChange={(e) => set('objet', e.target.value)}
            />
          </Grid>
          <Grid size={{ xs: 12 }}>
            <TextField
              fullWidth
              multiline
              minRows={2}
              label="Commentaire"
              value={form.commentaire}
              onChange={(e) => set('commentaire', e.target.value)}
            />
          </Grid>
        </Grid>
      </FormSection>

      <FormSection
        title="Pièces justificatives"
        description="Zone de dépôt — maquette UI."
        actions={
          <Stack direction="row" spacing={1}>
            <Button component={RouterLink} to="/paiements/demandes">
              Annuler
            </Button>
            <Button
              variant="outlined"
              onClick={() => {
                if (validate()) navigate('/paiements/demandes');
              }}
            >
              Enregistrer
            </Button>
            <Button
              variant="contained"
              onClick={() => {
                if (validate()) navigate('/paiements/demandes');
              }}
            >
              Soumettre
            </Button>
          </Stack>
        }
      >
        <Box
          sx={{
            border: '1px dashed',
            borderColor: 'divider',
            borderRadius: 2,
            py: 5,
            textAlign: 'center',
            bgcolor: 'action.hover',
          }}
        >
          <CloudUploadIcon color="action" sx={{ fontSize: 40, mb: 1 }} />
          <Typography sx={{ fontWeight: 600 }}>Déposer les pièces justificatives</Typography>
          <Typography variant="body2" color="text.secondary">
            PDF, images — max 10 Mo (maquette)
          </Typography>
          <Button sx={{ mt: 2 }} variant="outlined">
            Parcourir
          </Button>
        </Box>
      </FormSection>
    </Box>
  );
}
