import { Component, EventEmitter, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/auth/auth.service';
import { authErrorKey } from '../../core/auth/auth-error.util';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

type LoginMode = 'login' | 'register';

/**
 * Full-screen sign-in/registration gate shown by AppComponent whenever
 * there's no signed-in user. Unlike the app's modals, this is not
 * dismissible - there's no backdrop-click or Escape handler, since there's
 * nothing to fall back to behind it.
 */
@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslatePipe],
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.css'],
})
export class LoginComponent {
  @Output() loggedIn = new EventEmitter<void>();

  mode: LoginMode = 'login';
  email = '';
  password = '';
  name = '';
  showPassword = false;
  isSubmitting = false;
  error: string | null = null;

  constructor(private authService: AuthService) {}

  switchMode(mode: LoginMode) {
    this.mode = mode;
    this.error = null;
  }

  togglePasswordVisibility() {
    this.showPassword = !this.showPassword;
  }

  submit() {
    if (this.isSubmitting || !this.email || !this.password) return;
    if (this.mode === 'register' && !this.name.trim()) return;

    this.isSubmitting = true;
    this.error = null;

    const request =
      this.mode === 'login'
        ? this.authService.login(this.email, this.password)
        : this.authService.register(this.email, this.password, this.name.trim());

    request.subscribe({
      next: () => {
        this.isSubmitting = false;
        this.loggedIn.emit();
      },
      error: (err) => {
        console.error(`${this.mode} failed`, err);
        this.isSubmitting = false;
        this.error = this.errorKey(err);
      },
    });
  }

  private errorKey(err: unknown): string {
    return this.mode === 'login'
      ? authErrorKey(err, { 401: 'auth.invalidCredentials' }, 'auth.loginFailed')
      : authErrorKey(
          err,
          { 409: 'auth.emailTaken', 400: 'auth.passwordTooShort' },
          'auth.registerFailed',
        );
  }
}
