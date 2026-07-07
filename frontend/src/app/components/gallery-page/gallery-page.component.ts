import {
  Component,
  EventEmitter,
  Input,
  Output,
  ViewChild,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { Observable, forkJoin } from 'rxjs';
import { Gallery } from '../../models/gallery.model';
import { PhotoListItemDto } from '../../models/photoLisrItem.dto';
import { GalleryViewComponent } from '../gallery-view/gallery-view.component';
import { PhotoViewerComponent } from '../photo-viewer/photo-viewer.component';
import { ConfirmModalComponent } from '../../shared/modal/confirm-modal/confirm-modal.component';
import { GallerySelectModalComponent } from '../gallery-select-modal/gallery-select-modal.component';
import { PhotoApiService } from '../../services/photo-api.service';
import { GalleryApiService } from '../../services/gallery-api.service';
import { PhotoSelectionService } from '../../services/photo-selection.service';

@Component({
  selector: 'app-gallery-page',
  standalone: true,
  imports: [
    CommonModule,
    GalleryViewComponent,
    PhotoViewerComponent,
    ConfirmModalComponent,
    GallerySelectModalComponent,
  ],
  templateUrl: './gallery-page.component.html',
})
export class GalleryPageComponent {
  @Input({ required: true }) gallery!: Gallery;
  @Input() allGalleries: Gallery[] = [];
  @Output() galleryEmptied = new EventEmitter<void>();
  @Output() galleryCreated = new EventEmitter<Gallery>();

  @ViewChild(GalleryViewComponent) galleryView!: GalleryViewComponent;

  photos: PhotoListItemDto[] = [];

  viewerOpen = false;
  viewerPhotoId?: string;
  viewerAutoEdit = false;

  // delete (single photo from the viewer, or a bulk selection from the grid)
  deleteConfirmOpen = false;
  photoIdsToDelete: string[] = [];
  isDeletingPhoto = false;

  // copy / move to another gallery
  gallerySelectOpen = false;
  gallerySelectMode: 'copy' | 'move' = 'copy';
  photoIdsToMoveOrCopy: string[] = [];
  isMovingOrCopying = false;

  constructor(
    private photoApi: PhotoApiService,
    private galleryApi: GalleryApiService,
    public photoSelection: PhotoSelectionService,
  ) {}

  openViewer(photoId: string, autoEdit = false) {
    this.viewerPhotoId = photoId;
    this.viewerOpen = true;
    this.viewerAutoEdit = autoEdit;
  }

  closeViewer() {
    this.viewerOpen = false;
    this.viewerPhotoId = undefined;
    this.viewerAutoEdit = false;
  }

  /** Re-fetches this gallery's photos without a full page/view remount. */
  refreshPhotos(onLoaded?: () => void) {
    this.galleryView.refreshPhotos(() => onLoaded?.());
  }

  onPhotosLoaded(photos: PhotoListItemDto[]) {
    this.photos = photos;
  }

  private removePhotosLocally(ids: string[]) {
    const idSet = new Set(ids);
    this.photos = this.photos.filter((p) => !idSet.has(p.id));

    ids.forEach((id) => {
      this.galleryView.removePhoto(id);
      if (this.viewerPhotoId === id) {
        this.closeViewer();
      }
    });
  }

  /**
   * Runs N API calls in parallel, toggling a busy flag and forwarding
   * errors to the console — the shape shared by every bulk action here.
   */
  private runBulk(
    calls: Observable<unknown>[],
    setBusy: (busy: boolean) => void,
    onSuccess: () => void,
    errorLabel: string,
  ) {
    setBusy(true);

    forkJoin(calls).subscribe({
      next: () => {
        setBusy(false);
        onSuccess();
      },
      error: (err) => {
        console.error(errorLabel, err);
        setBusy(false);
      },
    });
  }

  // ---------------- delete (single or bulk) ----------------

  openDeleteConfirm(photoId: string) {
    this.photoIdsToDelete = [photoId];
    this.deleteConfirmOpen = true;
  }

  requestBulkDelete(photoIds: string[]) {
    if (!photoIds.length) return;
    this.photoIdsToDelete = photoIds;
    this.deleteConfirmOpen = true;
  }

  closeDeleteConfirm() {
    this.deleteConfirmOpen = false;
    this.photoIdsToDelete = [];
  }

  get deleteConfirmMessage() {
    return this.photoIdsToDelete.length > 1
      ? `${this.photoIdsToDelete.length} photos will be removed from the gallery.`
      : 'This photo will be removed from the gallery.';
  }

  confirmDelete() {
    if (!this.photoIdsToDelete.length) return;

    const ids = [...this.photoIdsToDelete];

    this.runBulk(
      ids.map((id) => this.photoApi.delete(id)),
      (busy) => (this.isDeletingPhoto = busy),
      () => {
        this.removePhotosLocally(ids);
        this.photoSelection.clear();
        this.closeDeleteConfirm();

        if (this.photos.length === 0) {
          this.galleryEmptied.emit();
        }
      },
      'Failed to delete photo(s)',
    );
  }

  // ---------------- copy / move (single or bulk) ----------------

  get otherGalleries() {
    return this.allGalleries.filter((g) => g.id !== this.gallery.id);
  }

  requestCopyOrMove(photoIds: string[], mode: 'copy' | 'move') {
    if (!photoIds.length) return;
    this.photoIdsToMoveOrCopy = photoIds;
    this.gallerySelectMode = mode;
    this.gallerySelectOpen = true;
  }

  closeGallerySelect() {
    this.gallerySelectOpen = false;
    this.photoIdsToMoveOrCopy = [];
  }

  onGallerySelected(targetGalleryId: string) {
    this.runMoveOrCopy(targetGalleryId);
  }

  onCreateGalleryForMoveOrCopy(title: string) {
    this.isMovingOrCopying = true;

    this.galleryApi.create({ title }).subscribe({
      next: (newGallery) => {
        this.galleryCreated.emit(newGallery);
        this.runMoveOrCopy(newGallery.id);
      },
      error: (err) => {
        console.error('Failed to create gallery', err);
        this.isMovingOrCopying = false;
      },
    });
  }

  private runMoveOrCopy(targetGalleryId: string) {
    const ids = [...this.photoIdsToMoveOrCopy];
    const mode = this.gallerySelectMode;

    const calls = ids.map((id) =>
      mode === 'copy'
        ? this.photoApi.copy(id, targetGalleryId)
        : this.photoApi.move(id, targetGalleryId),
    );

    this.runBulk(
      calls,
      (busy) => (this.isMovingOrCopying = busy),
      () => {
        if (mode === 'move') {
          this.removePhotosLocally(ids);
        }

        this.photoSelection.clear();
        this.closeGallerySelect();

        if (mode === 'move' && this.photos.length === 0) {
          this.galleryEmptied.emit();
        }
      },
      `Failed to ${mode} photo(s)`,
    );
  }
}
