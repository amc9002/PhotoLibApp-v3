import { Injectable } from '@angular/core';

export type ThemeId = 'navy' | 'charcoal' | 'black';

const STORAGE_KEY = 'photolib.theme';
const DEFAULT_THEME: ThemeId = 'navy';
export const THEME_IDS: ThemeId[] = ['navy', 'charcoal', 'black'];

/**
 * App background preset. Only the neutral canvas tokens change between
 * presets (see styles.css `[data-theme=...]` overrides) - the brand ink/
 * orange accent stays constant so the app still reads as PhotoLib under
 * any of them.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  theme: ThemeId = readStoredTheme();

  constructor() {
    this.apply(this.theme);
  }

  setTheme(theme: ThemeId) {
    this.theme = theme;
    localStorage.setItem(STORAGE_KEY, theme);
    this.apply(theme);
  }

  private apply(theme: ThemeId) {
    document.documentElement.setAttribute('data-theme', theme);
  }
}

function readStoredTheme(): ThemeId {
  const stored = localStorage.getItem(STORAGE_KEY);
  return (THEME_IDS as string[]).includes(stored ?? '') ? (stored as ThemeId) : DEFAULT_THEME;
}
