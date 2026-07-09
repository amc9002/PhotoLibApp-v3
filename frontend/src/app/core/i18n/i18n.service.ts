import { Injectable } from '@angular/core';
import { Lang, translations } from './translations';

const STORAGE_KEY = 'photolib.lang';
const DEFAULT_LANG: Lang = 'be';

/**
 * App-wide UI language. Plain mutable property (not a signal) to match the
 * rest of this codebase's service style - components re-read `t.lang` /
 * call `translate()` on every change-detection pass via the impure `t`
 * pipe, so no explicit reactivity plumbing is needed here.
 */
@Injectable({ providedIn: 'root' })
export class I18nService {
  lang: Lang = readStoredLang();

  setLang(lang: Lang) {
    this.lang = lang;
    localStorage.setItem(STORAGE_KEY, lang);
  }

  translate(key: string): string {
    const entry = translations[key];
    if (!entry) {
      console.warn(`Missing translation key: ${key}`);
      return key;
    }
    return entry[this.lang];
  }
}

function readStoredLang(): Lang {
  const stored = localStorage.getItem(STORAGE_KEY);
  return stored === 'be' || stored === 'en' ? stored : DEFAULT_LANG;
}
