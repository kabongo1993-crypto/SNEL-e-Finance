import { apiClient } from '../../services/apiClient';

export interface DocumentViewerFetchTimings {
  apiRequestMs: number;
  firstByteMs?: number;
  downloadMs: number;
  blobSizeBytes: number;
}

/** Charge un blob via GET avec mesures réseau (instrumentation temporaire). */
export async function fetchBlobWithPerf(
  url: string,
  params?: Record<string, unknown>,
): Promise<{ blob: Blob; timings: DocumentViewerFetchTimings }> {
  const requestStart = performance.now();
  let firstByteAt: number | undefined;

  const response = await apiClient.get<Blob>(url, {
    responseType: 'blob',
    params,
    onDownloadProgress: (event) => {
      if (firstByteAt === undefined && event.loaded > 0) {
        firstByteAt = performance.now();
      }
    },
  });

  const downloadEnd = performance.now();
  const blob = response.data;
  const blobSizeBytes = blob.size;

  return {
    blob,
    timings: {
      apiRequestMs: Math.round(downloadEnd - requestStart),
      firstByteMs:
        firstByteAt !== undefined ? Math.round(firstByteAt - requestStart) : undefined,
      downloadMs: Math.round(downloadEnd - requestStart),
      blobSizeBytes,
    },
  };
}

export function logDocumentViewerPerf(
  perfLabel: string,
  phases: {
    clickMs: number;
    apiRequestMs?: number;
    firstByteMs?: number;
    downloadMs?: number;
    blobProcessingMs?: number;
    createObjectUrlMs?: number;
    iframeLoadMs?: number;
    blobSizeBytes?: number;
  },
): void {
  const rel = (value?: number) =>
    value !== undefined ? Math.round(value - phases.clickMs) : undefined;

  console.info(
    [
      `[PERF][FE-${perfLabel}]`,
      `Click=0ms`,
      phases.apiRequestMs !== undefined
        ? `ApiRequest=${rel(phases.clickMs + phases.apiRequestMs)}ms (${phases.apiRequestMs}ms wall)`
        : null,
      phases.firstByteMs !== undefined ? `FirstByte=+${phases.firstByteMs}ms` : null,
      phases.downloadMs !== undefined ? `BlobReceived=+${phases.downloadMs}ms` : null,
      phases.blobProcessingMs !== undefined
        ? `BlobProcessing=${phases.blobProcessingMs}ms`
        : null,
      phases.createObjectUrlMs !== undefined
        ? `CreateObjectURL=+${rel(phases.createObjectUrlMs)}ms`
        : null,
      phases.iframeLoadMs !== undefined ? `IframeLoaded=+${rel(phases.iframeLoadMs)}ms` : null,
      phases.blobSizeBytes !== undefined
        ? `BlobSize=${Math.round(phases.blobSizeBytes / 1024)}KB`
        : null,
      `Total=${phases.iframeLoadMs !== undefined ? rel(phases.iframeLoadMs) : rel(phases.downloadMs !== undefined ? phases.clickMs + phases.downloadMs : undefined)}ms`,
    ]
      .filter(Boolean)
      .join(' '),
  );

  if (phases.apiRequestMs !== undefined) {
    console.info(
      `[PERF][FE-${perfLabel}-SUMMARY] ` +
        `API response time=${phases.apiRequestMs}ms | ` +
        `Download time=${phases.downloadMs ?? phases.apiRequestMs}ms | ` +
        `Blob processing time=${phases.blobProcessingMs ?? 0}ms | ` +
        `Iframe load time=${phases.iframeLoadMs !== undefined && phases.downloadMs !== undefined ? phases.iframeLoadMs - phases.downloadMs : 'n/a'}ms`,
    );
  }
}

/** Enveloppe un loader existant avec marquage perf (sans refetch). */
export function wrapDocumentLoadWithPerf(
  perfLabel: string,
  loader: () => Promise<Blob>,
): () => Promise<Blob> {
  return async () => {
    const clickMs = performance.now();
    const apiStart = performance.now();
    const blob = await loader();
    const downloadMs = Math.round(performance.now() - clickMs);
    const apiRequestMs = Math.round(performance.now() - apiStart);
    logDocumentViewerPerf(perfLabel, {
      clickMs,
      apiRequestMs,
      downloadMs,
      blobSizeBytes: blob.size,
    });
    return blob;
  };
}
