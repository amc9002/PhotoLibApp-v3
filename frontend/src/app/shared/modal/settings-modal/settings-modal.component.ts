import { Component, EventEmitter, HostListener, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { I18nService } from '../../../core/i18n/i18n.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { Lang } from '../../../core/i18n/translations';
import { ThemeId, ThemeService, THEME_IDS } from '../../../core/theme/theme.service';

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
  imports: [CommonModule, TranslatePipe],
  templateUrl: './settings-modal.component.html',
  styleUrls: ['./settings-modal.component.css'],
})
export class SettingsModalComponent {
  @Output() close = new EventEmitter<void>();

  readonly themeIds = THEME_IDS;

  constructor(
    public i18n: I18nService,
    public theme: ThemeService,
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
    this.close.emit();
  }
}
