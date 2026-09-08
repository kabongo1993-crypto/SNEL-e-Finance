import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from './AuthContext';
import { canAccessPath } from '../../layout/navAccess';

/**
 * Bloque l’accès deep-link aux pages hors permissions.
 * À placer sous `RequireAuth`.
 */
export function RequirePermission() {
  const { user } = useAuth();
  const location = useLocation();

  if (!canAccessPath(location.pathname, user, location.search)) {
    return <Navigate to="/dashboard" replace state={{ from: location.pathname, denied: true }} />;
  }

  return <Outlet />;
}
