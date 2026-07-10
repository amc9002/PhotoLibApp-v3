import { Component, EventEmitter, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/auth/auth.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

/**
 * Full-screen sign-in gate shown by AppComponent whenever there's no
 * signed-in user. Unlike the app's modals, this is not dismissible - there's
 * no backdrop-click or Escape handler, since there's nothing to fall back to
 * behind it.
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

  email = '';
  password = '';
  isSubmitting = false;
  error: string | null = null;

  constructor(private authService: AuthService) {}

  submit() {
    if (this.isSubmitting || !this.email || !this.password) return;

    this.isSubmitting = true;
    this.error = null;

    this.authService.login(this.email, this.password).subscribe({
      next: () => {
        this.isSubmitting = false;
        this.loggedIn.emit();
      },
      error: (err) => {
        console.error('Login failed', err);
        this.isSubmitting = false;
        this.error =
          err instanceof HttpErrorResponse && err.status === 401
            ? 'auth.invalidCredentials'
            : 'auth.loginFailed';
      },
    });
  }
}
