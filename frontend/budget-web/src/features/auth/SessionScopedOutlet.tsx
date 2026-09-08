import { Outlet } from 'react-router-dom';
import { useAuth } from './AuthContext';

/**
 * Remonte l'arbre des routes protégées lors d'un changement d'utilisateur authentifié,
 * afin d'éviter la reprise d'état React (formulaires, providers) entre sessions.
 */
export function SessionScopedOutlet() {
  const { user } = useAuth();
  const sessionKey = user?.idUtilisateur ?? 'anon';

  return (
    <div key={sessionKey} style={{ display: 'contents' }}>
      <Outlet />
    </div>
  );
}
