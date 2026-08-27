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

  register(email: string, password: string, name: string) {
    return this.api.post<User>('Auth/register', { email, password, name });
  }

  logout() {
    return this.api.post<void>('Auth/logout', {});
  }

  changePassword(currentPassword: string, newPassword: string) {
    return this.api.post<void>('Auth/change-password', { currentPassword, newPassword });
  }

  me() {
    return this.api.get<User>('Auth/me');
  }

  updateProfile(name: string, bio: string | null) {
    return this.api.put<User>('Auth/me', { name, bio });
  }

  uploadAvatar(file: File) {
    const formData = new FormData();
    formData.append('file', file);
    return this.api.post<User>('Avatar/me', formData);
  }
}
