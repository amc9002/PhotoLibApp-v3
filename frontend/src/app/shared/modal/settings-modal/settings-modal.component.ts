import { Component, EventEmitter, HostListener, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { I18nService } from '../../../core/i18n/i18n.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { Lang } from '../../../core/i18n/translations';
import { ThemeId, ThemeService, THEME_IDS } from '../../../core/theme/theme.service';
import { AuthService } from '../../../core/auth/auth.service';
import { authErrorKey } from '../../../core/auth/auth-error.util';

const THEME_LABEL_KEYS: Record<ThemeId, string> = {
  navy: 'settings.themeNavy',
  charcoal: 'settings.themeCharcoal',
  black: 'settings.themeBlack',
};

/** Swatch preview colours, independent of the live `[data-theme]` tokens - lets all 3 be shown side by side without switching the document. */
const THEME_PREVIEW: Record<ThemeId, { canvas: string; elevated: string }> = {
  navy: { canvas: '#14171f', elevated: '#1e222c' },
  charcoal: { canvas: '#1a1817', elevated: '#242120' },
  black: { canvas: '#000000', elevated: '#141414' },
};

@Component({
  selector: 'app-settings-modal',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslatePipe],
  templateUrl: './settings-modal.component.html',
  styleUrls: ['./settings-modal.component.css'],
})
export class SettingsModalComponent {
  @Output() close = new EventEmitter<void>();
  @Output() logout = new EventEmitter<void>();

  readonly themeIds = THEME_IDS;

  showChangePassword = false;
  currentPassword = '';
  newPassword = '';
  isChangingPassword = false;
  changePasswordError: string | null = null;
  changePasswordSuccess = false;

  constructor(
    public i18n: I18nService,
    public theme: ThemeService,
    public authService: AuthService,
  ) {}

  setLang(lang: Lang) {
    this.i18n.setLang(lang);
  }

  setTheme(theme: ThemeId) {
    this.theme.setTheme(theme);
  }

  trackByThemeId(index: number, themeId: ThemeId): ThemeId {
    return themeId;
  }

  themeLabelKey(theme: ThemeId): string {
    return THEME_LABEL_KEYS[theme];
  }

  previewCanvas(theme: ThemeId): string {
    return THEME_PREVIEW[theme].canvas;
  }

  previewElevated(theme: ThemeId): string {
    return THEME_PREVIEW[theme].elevated;
  }

  @HostListener('keydown.escape', ['$event'])
  onEscape(event: KeyboardEvent) {
    event.stopPropagation();
    // Let Escape close the (unsaved) password sub-form first rather than
    // the whole Settings modal, so a mid-edit Escape doesn't lose more
    // than the user meant to discard.
    if (this.showChangePassword) {
      this.toggleChangePassword();
      return;
    }
    this.close.emit();
  }

  toggleChangePassword() {
    this.showChangePassword = !this.showChangePassword;
    this.currentPassword = '';
    this.newPassword = '';
    this.changePasswordError = null;
    this.changePasswordSuccess = false;
  }

  submitChangePassword() {
    if (this.isChangingPassword || !this.currentPassword || !this.newPassword) return;

    this.isChangingPassword = true;
    this.changePasswordError = null;
    this.changePasswordSuccess = false;

    this.authService.changePassword(this.currentPassword, this.newPassword).subscribe({
      next: () => {
        this.isChangingPassword = false;
        this.changePasswordSuccess = true;
        this.currentPassword = '';
        this.newPassword = '';
      },
      error: (err) => {
        console.error('Change password failed', err);
        this.isChangingPassword = false;
        this.changePasswordError = authErrorKey(
          err,
          { 403: 'auth.currentPasswordWrong', 400: 'auth.passwordTooShort' },
          'auth.changePasswordFailed',
        );
      },
    });
  }
}
