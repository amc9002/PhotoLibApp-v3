import { Component, Input, OnChanges, OnDestroy, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ImageCacheService } from '../../../core/offline/image-cache.service';

@Component({
  selector: 'app-photo-thumbnail',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './photo-thumbnail.component.html',
  styleUrls: ['./photo-thumbnail.component.css'],
})
export class PhotoThumbnailComponent implements OnChanges, OnDestroy {
  @Input({ required: true }) photoId!: string;
  @Input() title = '';
  @Input() hasThumbnail = true;
  @Input() alt = 'Photo thumbnail';
  @Input() loading: 'lazy' | 'eager' = 'lazy';

  objectUrl: string | null = null;

  constructor(private imageCache: ImageCacheService) {}

  ngOnChanges(changes: SimpleChanges) {
    if (changes['photoId'] || changes['hasThumbnail']) {
      this.load();
    }
  }

  ngOnDestroy() {
    this.releaseUrl();
  }

  private load() {
    this.releaseUrl();

    if (!this.hasThumbnail || !this.photoId) return;

    this.imageCache.getThumbnailUrl$(this.photoId).subscribe({
      next: (url) => (this.objectUrl = url),
      error: (err) => console.error('Failed to load thumbnail', this.photoId, err),
    });
  }

  private releaseUrl() {
    if (this.objectUrl) {
      URL.revokeObjectURL(this.objectUrl);
      this.objectUrl = null;
    }
  }
}
