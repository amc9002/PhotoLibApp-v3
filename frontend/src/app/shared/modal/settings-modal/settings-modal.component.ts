import { Component, EventEmitter, HostListener, OnInit, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { I18nService } from '../../../core/i18n/i18n.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { Lang } from '../../../core/i18n/translations';
import { ThemeId, ThemeService, THEME_IDS } from '../../../core/theme/theme.service';
import { AuthService } from '../../../core/auth/auth.service';
import { authErrorKey } from '../../../core/auth/auth-error.util';
import { userInitials } from '../../utils/user-initials';

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
export class SettingsModalComponent implements OnInit {
  @Output() close = new EventEmitter<void>();
  @Output() logout = new EventEmitter<void>();

  readonly themeIds = THEME_IDS;

  showChangePassword = false;
  currentPassword = '';
  newPassword = '';
  isChangingPassword = false;
  changePasswordError: string | null = null;
  changePasswordSuccess = false;

  profileName = '';
  profileBio = '';
  private savedProfileName = '';
  private savedProfileBio = '';
  isSavingProfile = false;
  profileError: string | null = null;
  profileSaved = false;

  isUploadingAvatar = false;
  avatarError: string | null = null;

  constructor(
    public i18n: I18nService,
    public theme: ThemeService,
    public authService: AuthService,
  ) {}

  ngOnInit(): void {
    const user = this.authService.currentUser;
    this.profileName = user?.name ?? '';
    this.profileBio = user?.bio ?? '';
    this.savedProfileName = this.profileName;
    this.savedProfileBio = this.profileBio;
  }

  get avatarUrl(): string | null {
    return this.authService.avatarUrl();
  }

  get avatarInitials(): string {
    return userInitials(this.authService.currentUser?.name);
  }

  get isProfileDirty(): boolean {
    return this.profileName.trim() !== this.savedProfileName || this.profileBio !== this.savedProfileBio;
  }

  submitProfile(): void {
    const name = this.profileName.trim();
    if (this.isSavingProfile || !name || !this.isProfileDirty) return;

    this.isSavingProfile = true;
    this.profileError = null;
    this.profileSaved = false;

    const bio = this.profileBio.trim() || null;

    this.authService.updateProfile(name, bio).subscribe({
      next: () => {
        this.isSavingProfile = false;
        this.profileSaved = true;
        this.profileName = name;
        this.profileBio = bio ?? '';
        this.savedProfileName = this.profileName;
        this.savedProfileBio = this.profileBio;
      },
      error: (err) => {
        console.error('Update profile failed', err);
        this.isSavingProfile = false;
        this.profileError = authErrorKey(
          err,
          { 400: 'settings.nameRequired' },
          'settings.profileSaveFailed',
        );
      },
    });
  }

  onAvatarSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file || this.isUploadingAvatar) return;

    this.isUploadingAvatar = true;
    this.avatarError = null;

    this.authService.uploadAvatar(file).subscribe({
      next: () => {
        this.isUploadingAvatar = false;
      },
      error: (err) => {
        console.error('Avatar upload failed', err);
        this.isUploadingAvatar = false;
        this.avatarError = 'settings.avatarUploadFailed';
      },
    });
  }

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
