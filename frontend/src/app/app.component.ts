import { Component, ElementRef, OnInit, ViewChild } from '@angular/core';
import { EMPTY, catchError, forkJoin, from, mergeMap, switchMap, tap } from 'rxjs';
import { Gallery } from './models/gallery.model';
import { GalleryApiService } from './services/gallery-api.service';
import { CommonModule } from '@angular/common';
import { ToolbarComponent } from './components/toolbar/toolbar.component';
import { GalleryPropertiesComponent } from './components/gallery-properties/gallery-properties.component';
import { CreateGalleryComponent } from './components/create-gallery/create-gallery.component';
import { PhotoApiService } from './services/photo-api.service';
import { PhotoListItemDto } from './models/photoLisrItem.dto';
import { GalleryPageComponent } from './components/gallery-page/gallery-page.component';
import { ConfirmModalComponent } from './shared/modal/confirm-modal/confirm-modal.component';
import { EditMetadataModalComponent } from './shared/modal/edit-metadata-modal/edit-metadata-modal.component';
import { AddFromInternetModalComponent } from './shared/modal/add-from-internet-modal/add-from-internet-modal.component';
import { SyncReviewModalComponent } from './shared/modal/sync-review-modal/sync-review-modal.component';
import { PhotoSelectionService } from './services/photo-selection.service';
import { ThumbnailSizeService } from './services/thumbnail-size.service';
import { SyncCoordinatorService } from './core/offline/sync-coordinator.service';
import { ThemeService } from './core/theme/theme.service';
import { TranslatePipe } from './core/i18n/translate.pipe';
import { SettingsModalComponent } from './shared/modal/settings-modal/settings-modal.component';
import { LoginComponent } from './components/login/login.component';
import { AuthService } from './core/auth/auth.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    CommonModule,
    GalleryPageComponent,
    ToolbarComponent,
    GalleryPropertiesComponent,
    CreateGalleryComponent,
    ConfirmModalComponent,
    EditMetadataModalComponent,
    AddFromInternetModalComponent,
    SyncReviewModalComponent,
    SettingsModalComponent,
    LoginComponent,
    TranslatePipe,
  ],
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.css'],
})
export class AppComponent implements OnInit {
  galleries: Gallery[] = [];
  selectedGallery?: Gallery;
  isViewerOpen = false;
  viewerPhotoId?: string;
  photos: PhotoListItemDto[] = [];

  @ViewChild('fileInput') fileInput!: ElementRef<HTMLInputElement>;
  @ViewChild(GalleryPageComponent) galleryPage?: GalleryPageComponent;

  constructor(
    private galleryApi: GalleryApiService,
    private photoApi: PhotoApiService,
    public photoSelection: PhotoSelectionService,
    private thumbnailSize: ThumbnailSizeService,
    private syncCoordinator: SyncCoordinatorService,
    // Injected only so it constructs (and applies the stored theme
    // attribute) before first render - not read directly here.
    private themeService: ThemeService,
    public authService: AuthService,
  ) {}

  ngOnInit(): void {
    // AuthService.initialize() already ran (blocking, via
    // provideAppInitializer) before this component's first render, so
    // currentUser is settled by now - only load galleries if signed in.
    // A fresh sign-in instead calls onLoggedIn() below.
    if (this.authService.currentUser) {
      this.loadGalleries();
    }

    // Fires once a sync pass finishes, whether it ran in this tab or
    // another one - refresh whatever's on screen either way.
    this.syncCoordinator.syncCompleted$.subscribe(() => {
      this.loadGalleries();
      this.galleryPage?.refreshPhotos();
    });
  }

  onLoggedIn(): void {
    this.loadGalleries();
  }

  logout(): void {
    this.authService.logout().subscribe({
      error: (err) => console.error('Logout failed', err),
    });
  }

  private loadGalleries(): void {
    this.galleryApi.getAll().subscribe({
      next: (g) => (this.galleries = g),
      error: (e) => console.error(e),
    });
  }

  selectGallery(gallery: Gallery) {
    // прымусова "змяняем" значэнне
    this.selectedGallery = undefined;

    setTimeout(() => {
      this.selectedGallery = gallery;
      this.thumbnailSize.setGallery(gallery.id);
    });
  }

  showGalleryProperties = false;

  openFilePicker() {
    if (!this.selectedGallery) return;
    this.fileInput.nativeElement.click();
  }

  /**
   * Caps how many files a multi-select upload sends to the server at once.
   * Each upload makes the backend decode the full-resolution original into
   * memory to build a thumbnail, so firing all of them in parallel (as a
   * plain `forEach` once did) could hold dozens of decoded images in memory
   * simultaneously and exhaust it; capping concurrency bounds that to a
   * handful without meaningfully slowing a normal-sized batch down.
   */
  private static readonly MAX_CONCURRENT_UPLOADS = 3;

  onFilesSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    if (!input.files || !this.selectedGallery) return;

    const files = Array.from(input.files);
    const singleFile = files.length === 1;
    const galleryId = this.selectedGallery.id;

    from(files)
      .pipe(
        mergeMap(
          (file) => this.createAndUploadOne(file, galleryId, singleFile),
          AppComponent.MAX_CONCURRENT_UPLOADS,
        ),
      )
      .subscribe();

    input.value = '';
  }

  /**
   * Creates a photo's metadata, then uploads its file, then refreshes the
   * grid - the per-file unit of work `onFilesSelected` runs with bounded
   * concurrency via `mergeMap`. Errors are logged and swallowed here (rather
   * than propagated) so one failing file in a batch doesn't cancel the
   * `mergeMap` and abort the rest of the upload.
   */
  private createAndUploadOne(file: File, galleryId: string, singleFile: boolean) {
    return this.photoApi.create({ galleryId, title: file.name }).pipe(
      switchMap((photo) =>
        this.photoApi.upload(photo.id, file).pipe(
          tap(() => {
            this.galleryPage?.refreshPhotos(() => {
              // Only for a single upload - a batch would otherwise chain
              // one edit modal after another.
              if (singleFile) {
                this.galleryPage?.openViewer(photo.id, true);
              }
            });
          }),
          // withRetry (in PhotoApiService) already absorbs transient server
          // hiccups, and a real connectivity error is queued for background
          // sync rather than rejected here - so a rejection reaching this
          // point is a genuine, non-retryable failure worth logging rather
          // than failing silently.
          catchError((err) => {
            console.error('Failed to upload photo file', file.name, err);
            return EMPTY;
          }),
        ),
      ),
      catchError((err) => {
        console.error('Failed to create photo', file.name, err);
        return EMPTY;
      }),
    );
  }

  openGalleryProperties() {
    this.showGalleryProperties = true;
  }

  closeGalleryProperties() {
    this.showGalleryProperties = false;
  }

  showAddFromInternet = false;

  openAddFromInternet() {
    if (!this.selectedGallery) return;
    this.showAddFromInternet = true;
  }

  closeAddFromInternet() {
    this.showAddFromInternet = false;
  }

  showCreateGallery = false;

  openCreateGallery() {
    this.showCreateGallery = true;
  }

  closeCreateGallery() {
    this.showCreateGallery = false;
  }

  createGallery(title: string) {
    this.galleryApi.create({ title }).subscribe({
      next: (gallery) => {
        this.galleries = [...this.galleries, gallery];
        this.selectedGallery = gallery;
        this.thumbnailSize.setGallery(gallery.id);
        this.showCreateGallery = false;
      },
      error: (err) => {
        console.error('create gallery error:', err);
      },
    });
  }

  deleteGalleryConfirmOpen = false;
  isDeletingGallery = false;
  deleteGalleryTitle = 'app.deleteGalleryTitle';
  deleteGalleryMessage = 'app.deleteGalleryMessage';

  openDeleteGalleryConfirm() {
    if (!this.selectedGallery) return;
    this.deleteGalleryTitle = 'app.deleteGalleryTitle';
    this.deleteGalleryMessage = 'app.deleteGalleryMessage';
    this.deleteGalleryConfirmOpen = true;
  }

  closeDeleteGalleryConfirm() {
    this.deleteGalleryConfirmOpen = false;
  }

  onGalleryEmptied() {
    if (!this.selectedGallery) return;
    this.deleteGalleryTitle = 'app.emptyGalleryTitle';
    this.deleteGalleryMessage = 'app.emptyGalleryMessage';
    this.deleteGalleryConfirmOpen = true;
  }

  confirmDeleteGallery() {
    if (!this.selectedGallery) return;

    const galleryId = this.selectedGallery.id;
    this.isDeletingGallery = true;

    this.galleryApi.delete(galleryId).subscribe({
      next: () => {
        this.galleries = this.galleries.filter((g) => g.id !== galleryId);
        this.selectedGallery = undefined;
        this.thumbnailSize.setGallery(null);
        this.thumbnailSize.forgetGallery(galleryId);
        this.isDeletingGallery = false;
        this.deleteGalleryConfirmOpen = false;
      },
      error: (err) => {
        console.error('delete gallery error:', err);
        this.isDeletingGallery = false;
      },
    });
  }

  editGalleryOpen = false;
  isSavingGallery = false;
  gallerySaveError: string | null = null;

  openEditGallery() {
    if (!this.selectedGallery) return;
    this.gallerySaveError = null;
    this.editGalleryOpen = true;
  }

  closeEditGallery() {
    this.editGalleryOpen = false;
  }

  saveGalleryEdit(data: { title: string; description: string; tags: string[] }) {
    if (!this.selectedGallery) return;

    const galleryId = this.selectedGallery.id;
    this.isSavingGallery = true;
    this.gallerySaveError = null;

    forkJoin([
      this.galleryApi.update(galleryId, data),
      this.galleryApi.setTags(galleryId, data.tags),
    ]).subscribe({
      next: () => {
        const updated: Gallery = {
          ...this.selectedGallery!,
          title: data.title,
          description: data.description,
          tags: data.tags,
        };

        this.selectedGallery = updated;
        this.galleries = this.galleries.map((g) =>
          g.id === galleryId ? updated : g,
        );

        this.isSavingGallery = false;
        this.editGalleryOpen = false;
      },
      error: (err) => {
        console.error('update gallery error:', err);
        this.isSavingGallery = false;
        this.gallerySaveError = 'app.saveFailed';
      },
    });
  }

  onGalleryCreatedElsewhere(gallery: Gallery) {
    this.galleries = [...this.galleries, gallery];
  }

  onGalleriesReordered(galleries: Gallery[]) {
    this.galleryApi.reorder(galleries.map((g) => g.id)).subscribe({
      error: (err) => console.error('Failed to save gallery order', err),
    });
  }

  onCopySelected() {
    this.galleryPage?.requestCopyOrMove(this.photoSelection.ids, 'copy');
  }

  onMoveSelected() {
    this.galleryPage?.requestCopyOrMove(this.photoSelection.ids, 'move');
  }

  onDeleteSelected() {
    this.galleryPage?.requestBulkDelete(this.photoSelection.ids);
  }

  onSlideshow() {
    this.galleryPage?.openSlideshowSettings();
  }

  showSettings = false;

  openSettings() {
    this.showSettings = true;
  }

  closeSettings() {
    this.showSettings = false;
  }
}
