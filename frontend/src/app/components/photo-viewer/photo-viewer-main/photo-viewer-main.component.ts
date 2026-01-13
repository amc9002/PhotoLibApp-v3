import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-photo-viewer-main',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './photo-viewer-main.component.html',
  styleUrls: ['./photo-viewer-main.component.css'],
})
export class PhotoViewerMainComponent {
  @Input({ required: true }) photoId!: string;
  @Input() open = false;

  getPhotoUrl(photoId: string): string {
    return `/api/Photo/${photoId}/file`;
  }
}
