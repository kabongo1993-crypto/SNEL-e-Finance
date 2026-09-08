export function apiErrorMessage(err: unknown, fallback: string): string {
  if (typeof err === 'object' && err !== null && 'response' in err) {
    const data = (err as { response?: { data?: { message?: string; Message?: string } } }).response?.data;
    return data?.message ?? data?.Message ?? fallback;
  }
  return fallback;
}

export function libelleStatut(actif: boolean): 'ACTIF' | 'INACTIF' {
  return actif ? 'ACTIF' : 'INACTIF';
}
