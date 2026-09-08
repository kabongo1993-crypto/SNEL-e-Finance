import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { AppErrorBoundary } from './app/AppErrorBoundary';
import { AppRouter } from './app/AppRouter';
import { DOCUMENT_TITLE } from './theme';

document.title = DOCUMENT_TITLE;

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <AppErrorBoundary>
      <AppRouter />
    </AppErrorBoundary>
  </StrictMode>,
);
