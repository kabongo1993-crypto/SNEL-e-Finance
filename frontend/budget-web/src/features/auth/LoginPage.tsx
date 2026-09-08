import BoltOutlinedIcon from '@mui/icons-material/BoltOutlined';
import DescriptionOutlinedIcon from '@mui/icons-material/DescriptionOutlined';
import HeadsetMicOutlinedIcon from '@mui/icons-material/HeadsetMicOutlined';
import InfoOutlinedIcon from '@mui/icons-material/InfoOutlined';
import LanguageOutlinedIcon from '@mui/icons-material/LanguageOutlined';
import LockOutlinedIcon from '@mui/icons-material/LockOutlined';
import LoginOutlinedIcon from '@mui/icons-material/LoginOutlined';
import PersonOutlineOutlinedIcon from '@mui/icons-material/PersonOutlineOutlined';
import SecurityOutlinedIcon from '@mui/icons-material/SecurityOutlined';
import VisibilityOffOutlinedIcon from '@mui/icons-material/VisibilityOffOutlined';
import VisibilityOutlinedIcon from '@mui/icons-material/VisibilityOutlined';
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  IconButton,
  InputAdornment,
  Link,
  MenuItem,
  Paper,
  Stack,
  TextField,
  Tooltip,
  Typography,
} from '@mui/material';
import { useEffect, useState, type FormEvent } from 'react';
import { Navigate, useLocation, useNavigate, useSearchParams } from 'react-router-dom';
import { SNEL_LOGO_SRC, efLightColors, useThemeMode } from '../../theme';
import { apiErrorMessage } from '../rubriques-budgetaires/rubriqueUtils';
import { fetchAuthStatus } from '../../services/apiClient';
import { useAuth } from './AuthContext';
import {
  completePostLoginSession,
  getLastLoggedInUserId,
  isPostLogoutLogin,
  resolvePostLoginRedirect,
} from './authSession';

/** Aligné sur la charte centrale (tokens). */
const SNEL_BLUE = efLightColors.primary;
const SNEL_BLUE_HOVER = efLightColors.primaryDark;
const SNEL_BLUE_MID = efLightColors.primaryHover;
const SNEL_YELLOW = efLightColors.accent;
const LOGIN_BG = '/branding/snel-login-bg.jpg';
const SUBTITLE = 'text.secondary';

const LANG_KEY = 'budgetweb.ui-lang';

const fieldSx = {
  '& .MuiInputLabel-root': {
    color: 'text.secondary',
    fontWeight: 600,
  },
  '& .MuiOutlinedInput-root': {
    bgcolor: 'background.paper',
    '& fieldset': { borderColor: 'divider' },
    '&:hover fieldset': { borderColor: SNEL_BLUE_MID },
    '&.Mui-focused fieldset': { borderColor: SNEL_BLUE },
  },
  '& .MuiInputBase-input::placeholder': {
    color: 'text.secondary',
    opacity: 0.7,
  },
  /* Masquer les icônes natives du navigateur (révélation / autofill). */
  '& input::-ms-reveal': { display: 'none' },
  '& input::-ms-clear': { display: 'none' },
  '& input::-webkit-credentials-auto-fill-button': {
    visibility: 'hidden',
    display: 'none',
    pointerEvents: 'none',
  },
} as const;

function resolveLoginDestination(
  fromPath: string | undefined,
  newUserId: number,
  searchParams: { get: (key: string) => string | null },
): string {
  return resolvePostLoginRedirect({
    fromPath,
    newUserId,
    isPostLogout: isPostLogoutLogin(searchParams),
    previousUserId: getLastLoggedInUserId(),
  });
}

export function LoginPage() {
  const { login, bootstrapAdmin, isAuthenticated, isBootstrapping, user } = useAuth();
  const { resolved } = useThemeMode();
  const isDark = resolved === 'dark';
  const navigate = useNavigate();
  const location = useLocation();
  const [searchParams] = useSearchParams();

  const [nomUtilisateur, setNomUtilisateur] = useState('');
  const [motDePasse, setMotDePasse] = useState('');
  const [nom, setNom] = useState('');
  const [prenom, setPrenom] = useState('');
  const [email, setEmail] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [lang, setLang] = useState(() => localStorage.getItem(LANG_KEY) ?? 'fr');
  const [needsBootstrap, setNeedsBootstrap] = useState(false);
  const [statusLoaded, setStatusLoaded] = useState(false);
  const sessionExpired = searchParams.get('reason') === 'expired';

  useEffect(() => {
    localStorage.setItem(LANG_KEY, lang);
  }, [lang]);

  useEffect(() => {
    let cancelled = false;
    async function loadStatus() {
      try {
        const status = await fetchAuthStatus();
        if (!cancelled) setNeedsBootstrap(status.needsBootstrap);
      } catch {
        if (!cancelled) setNeedsBootstrap(false);
      } finally {
        if (!cancelled) setStatusLoaded(true);
      }
    }
    void loadStatus();
    return () => {
      cancelled = true;
    };
  }, []);

  if (isBootstrapping || !statusLoaded) {
    return (
      <Box sx={{ minHeight: '100vh', display: 'grid', placeItems: 'center', bgcolor: 'background.default' }}>
        <CircularProgress sx={{ color: SNEL_BLUE }} />
      </Box>
    );
  }

  if (isAuthenticated && user) {
    const from = (location.state as { from?: string } | null)?.from;
    const target = resolveLoginDestination(from, user.idUtilisateur, searchParams);
    completePostLoginSession(user.idUtilisateur);
    return <Navigate to={target} replace />;
  }

  const formValid = needsBootstrap
    ? nomUtilisateur.trim().length >= 3 && motDePasse.length >= 8 && nom.trim().length > 0
    : nomUtilisateur.trim().length > 0 && motDePasse.length > 0;

  const onSubmit = async (e: FormEvent) => {
    e.preventDefault();
    if (submitting || !formValid) return;
    setError(null);
    setSubmitting(true);
    try {
      let response;
      if (needsBootstrap) {
        response = await bootstrapAdmin({
          nomUtilisateur: nomUtilisateur.trim(),
          motDePasse,
          nom: nom.trim(),
          prenom: prenom.trim() || undefined,
          email: email.trim() || undefined,
        });
      } else {
        response = await login(nomUtilisateur.trim(), motDePasse);
      }
      const from = (location.state as { from?: string } | null)?.from;
      const target = resolveLoginDestination(from, response.utilisateur.idUtilisateur, searchParams);
      completePostLoginSession(response.utilisateur.idUtilisateur);
      navigate(target, { replace: true });
    } catch (err) {
      const axiosErr = err as { response?: { status?: number; data?: { message?: string } }; code?: string; message?: string };
      if (!axiosErr.response) {
        setError(
          "Impossible de joindre l'API. Vérifiez que le serveur est accessible, puis réessayez.",
        );
      } else {
        setError(
          apiErrorMessage(
            err,
            needsBootstrap
              ? "Impossible d'initialiser le compte administrateur."
              : "Nom d'utilisateur ou mot de passe incorrect.",
          ),
        );
      }
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Box
      sx={{
        minHeight: '100vh',
        display: 'flex',
        flexDirection: 'column',
        bgcolor: 'background.default',
        color: 'text.primary',
      }}
    >
        <Box
          component="header"
          sx={{
            bgcolor: SNEL_BLUE,
            color: '#fff',
            px: { xs: 2, md: 3 },
            py: 1.25,
          }}
        >
          <Stack
            direction="row"
            spacing={2}
            sx={{ alignItems: 'center', justifyContent: 'space-between' }}
          >
            <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center', minWidth: 0 }}>
              <Box
                component="img"
                src={SNEL_LOGO_SRC}
                alt="Logo SNEL"
                sx={{ height: { xs: 40, sm: 48 }, width: 'auto' }}
              />
              <Box sx={{ minWidth: 0, display: { xs: 'none', sm: 'block' } }}>
                <Typography
                  sx={{
                    fontWeight: 800,
                    fontSize: { sm: '0.85rem', md: '0.95rem' },
                    letterSpacing: 0.4,
                    lineHeight: 1.2,
                    textTransform: 'uppercase',
                  }}
                >
                  Société Nationale d&apos;Électricité - S.A.
                </Typography>
                <Typography sx={{ color: SNEL_YELLOW, fontStyle: 'italic', fontSize: '0.8rem', mt: 0.25 }}>
                  L&apos;énergie au service du développement
                </Typography>
              </Box>
            </Stack>

            <TextField select size="small"
              value={lang}
              onChange={(e) => setLang(e.target.value)}
              aria-label="Langue"
              sx={{
                minWidth: 140,
                '& .MuiOutlinedInput-root': {
                  color: '#fff',
                  bgcolor: 'rgba(255,255,255,0.08)',
                  '& fieldset': { borderColor: 'rgba(255,255,255,0.25)' },
                  '&:hover fieldset': { borderColor: 'rgba(255,255,255,0.45)' },
                },
                '& .MuiSelect-icon': { color: '#fff' },
              }}
              slotProps={{
                input: {
                  startAdornment: (
                    <InputAdornment position="start">
                      <LanguageOutlinedIcon sx={{ color: '#fff', fontSize: 18 }} />
                    </InputAdornment>
                  ),
                },
              }}
            >
              <MenuItem value="fr">Français</MenuItem>
              <MenuItem value="en">English</MenuItem>
              <MenuItem value="zh">中文</MenuItem>
            </TextField>
          </Stack>
        </Box>

        <Box sx={{ height: 4, bgcolor: SNEL_YELLOW, flexShrink: 0 }} />

        <Box
          sx={{
            flex: 1,
            position: 'relative',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            px: 2,
            py: { xs: 2.5, md: 4 },
            backgroundImage: isDark
              ? `linear-gradient(180deg, rgba(11,20,36,0.88) 0%, rgba(8,24,39,0.7) 100%), url(${LOGIN_BG})`
              : `linear-gradient(180deg, rgba(232,240,250,0.78) 0%, rgba(11,85,197,0.16) 100%), url(${LOGIN_BG})`,
            backgroundSize: 'cover',
            backgroundPosition: 'center',
          }}
        >
          <Paper
            elevation={0}
            component="form"
            onSubmit={onSubmit}
            sx={{
              width: '100%',
              maxWidth: 520,
              px: { xs: 2.5, sm: 3.5 },
              py: { xs: 2.5, sm: 3 },
              borderRadius: 'var(--ef-radius-lg)',
              position: 'relative',
              zIndex: 1,
              bgcolor: isDark ? 'rgba(17, 29, 46, 0.94)' : 'rgba(255,255,255,0.97)',
              color: 'text.primary',
              border: '1px solid',
              borderColor: 'divider',
              boxShadow: isDark
                ? '0 8px 28px rgba(0, 0, 0, 0.45)'
                : '0 8px 28px rgba(15, 39, 68, 0.12)',
            }}
          >
            <Typography
              component="h1"
              align="center"
              sx={{ fontWeight: 800, color: isDark ? '#4A96E3' : SNEL_BLUE, fontSize: { xs: '1.3rem', sm: '1.5rem' } }}
            >
              {needsBootstrap ? 'Initialisation SNEL Online' : 'Bienvenue sur SNEL Online'}
            </Typography>

            <Stack
              direction="row"
              spacing={1.25}
              sx={{ alignItems: 'center', justifyContent: 'center', my: 1.25 }}
            >
              <Box sx={{ flex: 1, maxWidth: 64, height: 2, bgcolor: SNEL_YELLOW }} />
              <BoltOutlinedIcon sx={{ color: SNEL_BLUE_MID, fontSize: 20 }} />
              <Box sx={{ flex: 1, maxWidth: 64, height: 2, bgcolor: SNEL_YELLOW }} />
            </Stack>

            <Typography align="center" sx={{ mb: 2, color: SUBTITLE, fontSize: '0.95rem' }}>
              {needsBootstrap
                ? 'Créez le premier compte User Admin Full (aucun utilisateur en base).'
                : 'Connectez-vous à votre espace personnel'}
            </Typography>

            {needsBootstrap && (
              <Alert severity="info" sx={{ mb: 1.5 }}>
                Le rôle <strong>User Admin Full</strong> sera attribué via le matricule{' '}
                <code>ADMIN-FULL</code>. Aucun mot de passe n&apos;est stocké en clair.
              </Alert>
            )}

            {sessionExpired && (
              <Alert severity="warning" sx={{ mb: 1.5 }}>
                Votre session a expiré. Veuillez vous reconnecter.
              </Alert>
            )}

            {error && (
              <Alert severity="error" sx={{ mb: 1.5 }} onClose={() => setError(null)}>
                {error}
              </Alert>
            )}

            {needsBootstrap && (
              <>
                <TextField
                  fullWidth
                  required
                  label="Nom"
                  placeholder="Nom de famille"
                  value={nom}
                  onChange={(e) => setNom(e.target.value)}
                  margin="dense"
                  sx={{ ...fieldSx, mt: 0.5 }}
                />
                <TextField
                  fullWidth
                  label="Prénom"
                  placeholder="Prénom (facultatif)"
                  value={prenom}
                  onChange={(e) => setPrenom(e.target.value)}
                  margin="dense"
                  sx={{ ...fieldSx, mt: 1.5 }}
                />
                <TextField
                  fullWidth
                  label="E-mail"
                  placeholder="email@snel.cd (facultatif)"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  margin="dense"
                  sx={{ ...fieldSx, mt: 1.5 }}
                />
              </>
            )}

            <TextField
              fullWidth
              required
              label="Nom d'utilisateur"
              placeholder="Saisissez votre nom d'utilisateur"
              value={nomUtilisateur}
              onChange={(e) => setNomUtilisateur(e.target.value)}
              autoComplete="username"
              margin="dense"
              sx={{ ...fieldSx, mt: needsBootstrap ? 1.5 : 0.5 }}
              slotProps={{
                input: {
                  startAdornment: (
                    <InputAdornment position="start">
                      <PersonOutlineOutlinedIcon sx={{ color: '#7A8A9A', fontSize: 22 }} />
                    </InputAdornment>
                  ),
                },
              }}
            />

            <TextField
              fullWidth
              required
              type={showPassword ? 'text' : 'password'}
              label="Mot de passe"
              placeholder={
                needsBootstrap
                  ? 'Mot de passe initial (8 caractères minimum)'
                  : 'Saisissez votre mot de passe'
              }
              value={motDePasse}
              onChange={(e) => setMotDePasse(e.target.value)}
              autoComplete={needsBootstrap ? 'new-password' : 'current-password'}
              margin="dense"
              sx={{ ...fieldSx, mt: 1.5 }}
              slotProps={{
                input: {
                  startAdornment: (
                    <InputAdornment position="start">
                      <LockOutlinedIcon sx={{ color: '#7A8A9A', fontSize: 22 }} />
                    </InputAdornment>
                  ),
                  endAdornment: (
                    <InputAdornment position="end">
                      <IconButton
                        aria-label={showPassword ? 'Masquer le mot de passe' : 'Afficher le mot de passe'}
                        onClick={() => setShowPassword((v) => !v)}
                        edge="end"
                        size="small"
                        sx={{ color: '#7A8A9A' }}
                      >
                        {showPassword ? (
                          <VisibilityOffOutlinedIcon fontSize="small" />
                        ) : (
                          <VisibilityOutlinedIcon fontSize="small" />
                        )}
                      </IconButton>
                    </InputAdornment>
                  ),
                },
              }}
            />

            {!needsBootstrap && (
              <Box sx={{ display: 'flex', justifyContent: 'flex-end', mt: 0.75, mb: 1.75 }}>
                <Tooltip title="Récupération de mot de passe non disponible pour le moment">
                  <Link
                    component="button"
                    type="button"
                    underline="hover"
                    onClick={(e) => e.preventDefault()}
                    sx={{ color: SNEL_BLUE_MID, fontSize: '0.875rem', cursor: 'default', fontWeight: 500 }}
                  >
                    Mot de passe oublié ?
                  </Link>
                </Tooltip>
              </Box>
            )}

            <Button
              type="submit"
              fullWidth
              variant="contained"
              size="large"
              disableElevation
              disabled={submitting || !formValid}
              startIcon={
                submitting ? <CircularProgress size={18} color="inherit" /> : <LoginOutlinedIcon />
              }
              sx={{
                bgcolor: SNEL_BLUE,
                color: '#FFFFFF',
                py: 1.15,
                mt: needsBootstrap ? 2 : 0,
                fontWeight: 700,
                textTransform: 'none',
                fontSize: '1rem',
                boxShadow: 'none',
                '&:hover': {
                  bgcolor: SNEL_BLUE_HOVER,
                  boxShadow: 'none',
                },
                '&.Mui-disabled': {
                  bgcolor: '#B8C2CE',
                  color: '#FFFFFF',
                },
              }}
            >
              {submitting
                ? needsBootstrap
                  ? 'Création…'
                  : 'Connexion…'
                : needsBootstrap
                  ? 'Créer User Admin Full'
                  : 'Se connecter'}
            </Button>
          </Paper>
        </Box>

        <Box component="footer" sx={{ bgcolor: SNEL_BLUE, color: '#fff' }}>
          <Stack
            direction={{ xs: 'column', md: 'row' }}
            spacing={1.5}
            sx={{
              alignItems: { xs: 'flex-start', md: 'center' },
              justifyContent: 'space-between',
              px: { xs: 2, md: 3 },
              py: 1.25,
            }}
          >
            <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
              <Box component="img" src={SNEL_LOGO_SRC} alt="" sx={{ height: 26 }} />
              <Typography variant="body2" sx={{ opacity: 0.95, fontSize: '0.85rem' }}>
                SNEL S.A. — L&apos;énergie au service du développement
              </Typography>
            </Stack>
            <Stack direction="row" spacing={2} useFlexGap sx={{ flexWrap: 'wrap' }}>
              {[
                { icon: HeadsetMicOutlinedIcon, label: 'Support' },
                { icon: DescriptionOutlinedIcon, label: 'Documentation' },
                { icon: SecurityOutlinedIcon, label: 'Sécurité' },
                { icon: InfoOutlinedIcon, label: 'À propos' },
              ].map((item) => (
                <Stack
                  key={item.label}
                  direction="row"
                  spacing={0.75}
                  sx={{ alignItems: 'center', opacity: 0.9 }}
                >
                  <item.icon sx={{ fontSize: 17 }} />
                  <Typography variant="body2" sx={{ fontSize: '0.85rem' }}>
                    {item.label}
                  </Typography>
                </Stack>
              ))}
            </Stack>
          </Stack>
          <Box sx={{ bgcolor: 'rgba(0,0,0,0.18)', py: 0.75, textAlign: 'center' }}>
            <Typography variant="caption">
              © {new Date().getFullYear()} Société Nationale d&apos;Électricité - S.A. Tous droits
              réservés.
            </Typography>
          </Box>
        </Box>
      </Box>
  );
}
