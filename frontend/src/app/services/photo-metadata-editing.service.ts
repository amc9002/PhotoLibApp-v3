import { Injectable } from '@angular/core';
import { forkJoin } from 'rxjs';
import { PhotoApiService } from './photo-api.service';
import { PhotoDto } from '../models/photo.dto';
import { PhotoListItemDto } from '../models/photoLisrItem.dto';

/** Fields editable through the edit-metadata modal. */
export interface PhotoMetadataEdit {
  title: string;
  description: string;
  tags: string[];
}

/**
 * Owns the edit-metadata and photo-info modal flows shared by
 * `GalleryPageComponent` (editing from the grid's context menu) and
 * `PhotoViewerComponent` (editing the currently displayed photo) - both
 * used to carry their own copy of this same open/save/show-info logic.
 *
 * Not `providedIn: 'root'` - each host component declares it in its own
 * `providers` array so it gets an independent instance (and independent
 * open/saving/error state), the same isolation the two components had
 * before this was pulled out.
 */
@Injectable()
export class PhotoMetadataEditingService {
  editOpen = false;
  isSaving = false;
  saveError: string | null = null;

  infoOpen = false;
  infoPhoto?: PhotoDto;

  /** Id of the photo currently targeted by the edit modal, if any. */
  editingPhotoId: string | null = null;

  constructor(private photoApi: PhotoApiService) {}

  openEditFor(photoId: string): void {
    this.editingPhotoId = photoId;
    this.saveError = null;
    this.editOpen = true;
  }

  closeEdit(): void {
    this.editOpen = false;
    this.editingPhotoId = null;
  }

  /**
   * Saves the currently-edited photo's title/description/tags, patching the
   * matching entry in `photos` in place on success so the caller doesn't
   * need to re-fetch. No-ops if nothing is being edited.
   */
  save(photos: PhotoListItemDto[], data: PhotoMetadataEdit): void {
    const id = this.editingPhotoId;
    if (!id) return;

    this.isSaving = true;
    this.saveError = null;

    forkJoin([this.photoApi.update(id, data), this.photoApi.setTags(id, data.tags)]).subscribe({
      next: () => {
        const photo = photos.find((p) => p.id === id);
        if (photo) {
          photo.title = data.title;
          photo.description = data.description;
          photo.tags = data.tags;
        }
        this.isSaving = false;
        this.closeEdit();
      },
      error: (err) => {
        console.error('Failed to update photo metadata', err);
        this.isSaving = false;
        this.saveError = 'photoViewer.saveFailed';
      },
    });
  }

  showInfoFor(photoId: string): void {
    this.photoApi.getById(photoId).subscribe({
      next: (photo) => {
        this.infoPhoto = photo;
        this.infoOpen = true;
      },
      error: (err) => console.error('Failed to load photo info', err),
    });
  }

  closeInfo(): void {
    this.infoOpen = false;
    this.infoPhoto = undefined;
  }
}
