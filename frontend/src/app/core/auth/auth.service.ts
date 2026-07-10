import { Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { tap } from 'rxjs/operators';
import { AuthApiService } from '../../services/auth-api.service';
import { User } from '../../models/user.model';

/**
 * Tracks the signed-in user for the whole app. Mirrors ThemeService's
 * plain-property pattern - no signals, since this codebase has none yet
 * and still runs zone.js change detection, so a plain property triggers
 * change detection the same way every other service here already does.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  currentUser: User | null = null;

  constructor(private authApi: AuthApiService) {}

  /** Checks for an existing session cookie on app startup. */
  async initialize(): Promise<void> {
    try {
      this.currentUser = await firstValueFrom(this.authApi.me());
    } catch {
      this.currentUser = null;
    }
  }

  login(email: string, password: string) {
    return this.authApi.login(email, password).pipe(tap((user) => (this.currentUser = user)));
  }

  logout() {
    return this.authApi.logout().pipe(tap(() => (this.currentUser = null)));
  }

  /** Called by the 401 interceptor when a session has expired or was revoked. */
  handleUnauthorized(): void {
    this.currentUser = null;
  }
}
