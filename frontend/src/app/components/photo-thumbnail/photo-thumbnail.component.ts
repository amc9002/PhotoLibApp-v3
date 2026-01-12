import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-photo-thumbnail',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './photo-thumbnail.component.html',
  styleUrls: ['./photo-thumbnail.component.css'],
})
export class PhotoThumbnailComponent {
  @Input({ required: true }) photoId!: string;
  @Input() title = '';
  @Input() hasThumbnail = true;
  @Input() alt = 'Photo thumbnail';
  @Input() loading: 'lazy' | 'eager' = 'lazy';

  getThumbnailUrl(photoId: string): string {
    return `/api/Photo/${photoId}/thumbnail`;
  }
}
