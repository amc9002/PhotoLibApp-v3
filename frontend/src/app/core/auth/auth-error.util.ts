import { HttpErrorResponse } from '@angular/common/http';

/** Maps an HTTP error's status code to an i18n key via `statusMap`, or `fallbackKey` if unmapped. */
export function authErrorKey(
  err: unknown,
  statusMap: Record<number, string>,
  fallbackKey: string,
): string {
  if (err instanceof HttpErrorResponse && err.status in statusMap) {
    return statusMap[err.status];
  }
  return fallbackKey;
}
