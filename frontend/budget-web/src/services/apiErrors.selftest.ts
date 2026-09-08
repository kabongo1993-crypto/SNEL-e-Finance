/**
 * Extraction messages erreur API.
 * npx --yes tsx src/services/apiErrors.selftest.ts
 */
import { extractApiErrorMessage } from './apiErrors.ts';

function assert(cond: unknown, msg: string): void {
  if (!cond) throw new Error(msg);
}

assert(
  extractApiErrorMessage(
    { response: { status: 400, data: { message: 'Extension non autorisée (.exe).' } } },
    'fallback',
  ) === 'Extension non autorisée (.exe).',
  'message camelCase',
);

assert(
  extractApiErrorMessage(
    { response: { status: 400, data: { Message: 'Le fichier est vide.' } } },
    'fallback',
  ) === 'Le fichier est vide.',
  'Message PascalCase',
);

assert(
  extractApiErrorMessage({ response: { status: 415, data: '' } }, 'fallback').includes(
    'format d’envoi',
  ),
  '415 fallback',
);

assert(
  extractApiErrorMessage(
    { response: { status: 400, data: '{"message":"Taille max"}' } },
    'fallback',
  ) === 'Taille max',
  'json string body',
);

console.log('apiErrors.selftest OK');
