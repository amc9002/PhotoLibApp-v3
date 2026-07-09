import { Injectable } from '@angular/core';

export type ViewMode = 'grid' | 'feed';

const STORAGE_KEY = 'photolib.viewMode';
const DEFAULT_MODE: ViewMode = 'feed';
const VIEW_MODES: ViewMode[] = ['grid', 'feed'];

/**
 * Which gallery browsing surface is active: the thumbnail grid or the
 * vertical scroll feed. Plain mutable property (not a signal) to match
 * I18nService/ThemeService - this is a pure client-side UI preference and
 * never touches the offline-sync mirror/outbox, so there's nothing to
 * reconcile with the server.
 */
@Injectable({ providedIn: 'root' })
export class ViewModeService {
  mode: ViewMode = readStoredMode();

  setMode(mode: ViewMode) {
    this.mode = mode;
    localStorage.setItem(STORAGE_KEY, mode);
  }
}

function readStoredMode(): ViewMode {
  const stored = localStorage.getItem(STORAGE_KEY);
  return (VIEW_MODES as string[]).includes(stored ?? '') ? (stored as ViewMode) : DEFAULT_MODE;
}
