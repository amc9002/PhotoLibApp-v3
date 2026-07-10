import { ApplicationConfig, inject, provideAppInitializer, provideZoneChangeDetection } from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { authInterceptor } from './core/auth/auth.interceptor';
import { AuthService } from './core/auth/auth.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideHttpClient(withInterceptors([authInterceptor])),
    // Blocks first render until the session check resolves, so the app
    // never flashes the gallery shell (or fires a doomed API call) before
    // knowing whether the user is signed in.
    provideAppInitializer(() => inject(AuthService).initialize()),
  ],
};
