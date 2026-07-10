import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { AuthService } from './auth.service';

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
      // A failed login attempt is an expected 401, not a session expiry -
      // don't treat it as a sign-out signal.
      if (
        err instanceof HttpErrorResponse &&
        err.status === 401 &&
        !req.url.includes('/api/Auth/login')
      ) {
        authService.handleUnauthorized();
      }
      return throwError(() => err);
    }),
  );
};
