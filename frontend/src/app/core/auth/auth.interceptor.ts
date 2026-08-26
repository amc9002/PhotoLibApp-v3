import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { AuthService } from './auth.service';

/**
 * Requests where a 401 is an expected, normal outcome (wrong credentials
 * against an as-yet-unauthenticated request) rather than a sign of an
 * expired/invalid session - the caller's own .subscribe({ error }) already
 * handles these, so the interceptor must not also treat them as a sign-out
 * signal. change-password is deliberately not here: it's [Authorize]-gated
 * and returns 403 (not 401) for "wrong current password", so any 401 it
 * does produce genuinely means the session is no longer valid.
 */
const AUTH_FLOW_URLS = ['/api/Auth/login'];

/**
 * Flips AuthService.currentUser back to null whenever the server reports
 * the session is no longer valid, so AppComponent's template reactively
 * swaps back to the login screen. Never swallows the error - existing
 * per-call .subscribe({ error }) handlers throughout the app must keep
 * seeing it exactly as before (a 401 has a real status code, so it isn't
 * already absorbed by ApiService's own status-0/offline handling).
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);

  return next(req).pipe(
    catchError((err) => {
      if (
        err instanceof HttpErrorResponse &&
        err.status === 401 &&
        !AUTH_FLOW_URLS.some((url) => req.url.includes(url))
      ) {
        authService.handleUnauthorized();
      }
      return throwError(() => err);
    }),
  );
};
