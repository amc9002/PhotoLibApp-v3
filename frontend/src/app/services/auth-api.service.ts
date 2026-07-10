import { Injectable } from '@angular/core';
import { ApiService } from '../core/api/api.service';
import { User } from '../models/user.model';

/**
 * Thin wrapper over ApiService for the Auth endpoints. Unlike
 * PhotoApiService/GalleryApiService, there's no offline fallback here -
 * signing in/out and checking the session are meaningless without a server.
 */
@Injectable({ providedIn: 'root' })
export class AuthApiService {
  constructor(private api: ApiService) {}

  login(email: string, password: string) {
    return this.api.post<User>('Auth/login', { email, password });
  }

  logout() {
    return this.api.post<void>('Auth/logout', {});
  }

  me() {
    return this.api.get<User>('Auth/me');
  }
}
