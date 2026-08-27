import { HttpErrorResponse } from '@angular/common/http';

/**
 * True for a request that never reached the server (offline or the host is
 * unreachable), as opposed to a real HTTP error response. `PhotoApiService`,
 * `GalleryApiService` and `SyncCoordinatorService` all use this to tell
 * "fall back to the offline path" apart from "surface this error".
 */
export function isConnectivityError(err: unknown): boolean {
  return err instanceof HttpErrorResponse && err.status === 0;
}

/** Outcome of {@link attemptOnline}: either the online call's result, or a signal to fall back. */
export type OnlineAttempt<T> = { ok: true; value: T } | { ok: false };

/**
 * Runs `onlineCall` only when `isOnline`. A connectivity error is treated as
 * "couldn't reach the server" and reported as `{ ok: false }` so the caller
 * can fall through to its offline/mirror path; any other error is rethrown.
 *
 * Shared by every `PhotoApiService`/`GalleryApiService` method that tries
 * the network first and mirrors the result into IndexedDB on success - it
 * doesn't own that mirroring step, since what gets mirrored (and the
 * offline fallback) differs per method.
 */
export async function attemptOnline<T>(
  isOnline: boolean,
  onlineCall: () => Promise<T>,
): Promise<OnlineAttempt<T>> {
  if (!isOnline) return { ok: false };

  try {
    return { ok: true, value: await onlineCall() };
  } catch (err) {
    if (!isConnectivityError(err)) throw err;
    return { ok: false };
  }
}
