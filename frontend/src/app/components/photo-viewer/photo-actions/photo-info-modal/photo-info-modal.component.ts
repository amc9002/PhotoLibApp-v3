import {
  Component,
  EventEmitter,
  HostListener,
  Input,
  OnChanges,
  Output,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { ExifGroup, PhotoDto } from '../../../../models/photo.dto';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';

const MAP_SPAN = 0.004;

@Component({
  selector: 'app-photo-info-modal',
  standalone: true,
  imports: [CommonModule, TranslatePipe],
  templateUrl: './photo-info-modal.component.html',
  styleUrls: ['./photo-info-modal.component.css'],
})
export class PhotoInfoModalComponent implements OnChanges {
  @Input({ required: true }) photo!: PhotoDto;
  @Output() close = new EventEmitter<void>();

  exifGroups: ExifGroup[] = [];
  showFullExif = false;
  mapUrl: SafeResourceUrl | null = null;

  constructor(private sanitizer: DomSanitizer) {}

  @HostListener('keydown.escape', ['$event'])
  onEscape(event: KeyboardEvent) {
    event.stopPropagation();
    this.close.emit();
  }

  ngOnChanges() {
    this.exifGroups = this.parseExif();
    this.showFullExif = false;
    this.mapUrl = this.computeMapUrl();
  }

  private parseExif(): ExifGroup[] {
    if (!this.photo?.exifJson) return [];
    try {
      return JSON.parse(this.photo.exifJson);
    } catch {
      return [];
    }
  }

  private findTag(groupName: string, tagName: string): string | null {
    const group = this.exifGroups.find((g) => g.name === groupName);
    return group?.tags.find((t) => t.name === tagName)?.description ?? null;
  }

  get make() {
    return this.findTag('Exif IFD0', 'Make');
  }

  get model() {
    return this.findTag('Exif IFD0', 'Model');
  }

  get dateTaken() {
    return this.findTag('Exif SubIFD', 'Date/Time Original');
  }

  get exposureTime() {
    return this.findTag('Exif SubIFD', 'Exposure Time');
  }

  get hasFacts() {
    return !!(this.make || this.model || this.dateTaken || this.exposureTime);
  }

  get hasLocation() {
    return this.photo?.latitude != null && this.photo?.longitude != null;
  }

  private computeMapUrl(): SafeResourceUrl | null {
    if (!this.hasLocation) return null;

    const lat = this.photo.latitude!;
    const lon = this.photo.longitude!;
    const bbox = [
      lon - MAP_SPAN,
      lat - MAP_SPAN,
      lon + MAP_SPAN,
      lat + MAP_SPAN,
    ].join(',');
    const url = `https://www.openstreetmap.org/export/embed.html?bbox=${bbox}&marker=${lat},${lon}`;

    return this.sanitizer.bypassSecurityTrustResourceUrl(url);
  }

  toggleFullExif() {
    this.showFullExif = !this.showFullExif;
  }
}
