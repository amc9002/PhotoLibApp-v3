import { Lang } from './translations';

/**
 * The handful of UI strings whose wording depends on a count. Kept as
 * small dedicated functions rather than a generic plural-rules engine -
 * Belarusian "фота" is grammatically invariant (doesn't decline by
 * number), so most of these only need to branch the verb/participle, not
 * build a full CLDR plural system for three call sites.
 */

export function photosSelectedText(n: number, lang: Lang): string {
  if (lang === 'en') return `${n} photo${n === 1 ? '' : 's'} selected`;
  return `Абрана ${n} фота`;
}

export function photoRemoveMessage(n: number, lang: Lang): string {
  if (lang === 'en') {
    return n === 1
      ? 'This photo will be removed from the gallery.'
      : `${n} photos will be removed from the gallery.`;
  }
  return n === 1
    ? 'Гэтае фота будзе выдалена з галерэі.'
    : `${n} фота будуць выдаленыя з галерэі.`;
}

export function copyMoveToGalleryTitle(action: 'copy' | 'move', n: number, lang: Lang): string {
  if (lang === 'en') {
    const verb = action === 'copy' ? 'Copy' : 'Move';
    const subject = n === 1 ? 'photo' : `${n} photos`;
    return `${verb} ${subject} to gallery`;
  }
  const verb = action === 'copy' ? 'Капіяваць' : 'Перамясціць';
  const subject = n === 1 ? 'фота' : `${n} фота`;
  return `${verb} ${subject} у галерэю`;
}
