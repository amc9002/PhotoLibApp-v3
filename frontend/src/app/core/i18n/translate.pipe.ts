import { Pipe, PipeTransform } from '@angular/core';
import { I18nService } from './i18n.service';

/**
 * Impure so it re-evaluates on every change-detection pass - the app has
 * no rebuild-per-locale step, so a language switch has to be picked up
 * immediately by every `{{ 'key' | t }}` already on screen.
 */
@Pipe({ name: 't', standalone: true, pure: false })
export class TranslatePipe implements PipeTransform {
  constructor(private i18n: I18nService) {}

  transform(key: string): string {
    return this.i18n.translate(key);
  }
}
