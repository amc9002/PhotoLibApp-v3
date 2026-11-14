import { Component, EventEmitter, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/auth/auth.service';
import { authErrorKey } from '../../core/auth/auth-error.util';
import { translations } from '../../core/i18n/translations';

type LoginMode = 'login' | 'register';

/**
 * Full-screen sign-in/registration gate shown by AppComponent whenever
 * there's no signed-in user. Unlike the app's modals, this is not
 * dismissible - there's no backdrop-click or Escape handler, since there's
 * nothing to fall back to behind it.
 *
 * Deliberately English-only for now, independent of I18nService/the `t`
 * pipe used everywhere else in the app - this is the portfolio-facing
 * entry screen, and full multi-language support for the rest of the app is
 * a separate, later piece of work. Error messages still go through the
 * shared `translations` dictionary (via authErrorKey's keys) rather than
 * being duplicated here, so they stay in sync if the wording changes.
 */
@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule],
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
        this.error = this.errorMessage(err);
      },
    });
  }

  private errorMessage(err: unknown): string {
    const key =
      this.mode === 'login'
        ? authErrorKey(err, { 401: 'auth.invalidCredentials' }, 'auth.loginFailed')
        : authErrorKey(
            err,
            { 409: 'auth.emailTaken', 400: 'auth.passwordTooShort' },
            'auth.registerFailed',
          );
    return translations[key].en;
  }
}
