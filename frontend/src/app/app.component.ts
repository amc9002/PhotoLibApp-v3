import { Component, ElementRef, OnInit, ViewChild } from '@angular/core';
import { forkJoin } from 'rxjs';
import { Gallery } from './models/gallery.model';
import { GalleryApiService } from './services/gallery-api.service';
import { CommonModule } from '@angular/common';
import { ToolbarComponent } from './components/toolbar/toolbar.component';
import { GalleryPropertiesComponent } from './components/gallery-properties/gallery-properties.component';
import { CreateGalleryComponent } from './components/create-gallery/create-gallery.component';
import { PhotoUploadService } from './services/photo-upload.service';
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
import { userInitials } from './shared/utils/user-initials';
import { ModalState } from './shared/utils/modal-state';

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
    private photoUpload: PhotoUploadService,
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

  userInitials(name: string): string {
    return userInitials(name);
  }

  logout(): void {
    // Settings can trigger this from its own logout button while still
    // open - close it too, or a later sign-in reopens it unprompted since
    // settingsModal lives on this always-alive component, not reset just
    // because the *ngIf-gated children behind it were destroyed.
    this.closeSettings();
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
    // Force a clear-then-set so `*ngIf="selectedGallery"` tears down and
    // remounts app-gallery-page with fresh internal state, even if the
    // newly selected gallery happens to be the same one as before.
    this.selectedGallery = undefined;

    setTimeout(() => {
      this.selectedGallery = gallery;
      this.thumbnailSize.setGallery(gallery.id);
    });
  }

  galleryPropertiesModal = new ModalState();

  openFilePicker() {
    if (!this.selectedGallery) return;
    this.fileInput.nativeElement.click();
  }

  onFilesSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    if (!input.files || !this.selectedGallery) return;

    const files = Array.from(input.files);
    const galleryId = this.selectedGallery.id;

    this.photoUpload.uploadAll(files, galleryId, (photoId, singleFile) => {
      this.galleryPage?.refreshPhotos(() => {
        // Only for a single upload - a batch would otherwise chain one edit
        // modal after another.
        if (singleFile) {
          this.galleryPage?.openViewer(photoId, true);
        }
      });
    });

    input.value = '';
  }

  openGalleryProperties() {
    this.galleryPropertiesModal.open();
  }

  closeGalleryProperties() {
    this.galleryPropertiesModal.close();
  }

  addFromInternetModal = new ModalState();

  openAddFromInternet() {
    if (!this.selectedGallery) return;
    this.addFromInternetModal.open();
  }

  closeAddFromInternet() {
    this.addFromInternetModal.close();
  }

  createGalleryModal = new ModalState();

  openCreateGallery() {
    this.createGalleryModal.open();
  }

  closeCreateGallery() {
    this.createGalleryModal.close();
  }

  createGallery(title: string) {
    this.galleryApi.create({ title }).subscribe({
      next: (gallery) => {
        this.galleries = [...this.galleries, gallery];
        this.selectedGallery = gallery;
        this.thumbnailSize.setGallery(gallery.id);
        this.createGalleryModal.close();
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

  settingsModal = new ModalState();

  openSettings() {
    this.settingsModal.open();
  }

  closeSettings() {
    this.settingsModal.close();
  }
}
