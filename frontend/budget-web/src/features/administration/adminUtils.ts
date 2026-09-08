import { hasPerm } from '../auth/permissions';

export function apiErrorMessage(err: unknown, fallback: string): string {
  if (typeof err === 'object' && err !== null && 'response' in err) {
    const data = (err as { response?: { data?: { message?: string; Message?: string; title?: string } } })
      .response?.data;
    return data?.message ?? data?.Message ?? data?.title ?? fallback;
  }
  if (err instanceof Error && err.message) return err.message;
  return fallback;
}

export function formatDateTimeFr(value: string | null | undefined): string {
  if (!value) return '—';
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return value;
  return d.toLocaleString('fr-FR');
}

export function hasAdminUtilisateurs(user: { permissions?: string[]; roles?: string[] } | null): boolean {
  return hasPerm(user, 'admin.utilisateurs');
}

export function hasAdminProfils(user: { permissions?: string[]; roles?: string[] } | null): boolean {
  return hasPerm(user, 'admin.profils') || hasPerm(user, 'admin.utilisateurs');
}
