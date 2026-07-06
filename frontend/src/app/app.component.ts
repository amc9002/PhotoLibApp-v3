import { Component, ElementRef, OnInit, ViewChild } from '@angular/core';
import { forkJoin } from 'rxjs';
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
import { PhotoSelectionService } from './services/photo-selection.service';

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
  ) {}

  ngOnInit(): void {
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
    });
  }

  showGalleryProperties = false;

  openFilePicker() {
    if (!this.selectedGallery) return;
    this.fileInput.nativeElement.click();
  }

  onFilesSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    if (!input.files || !this.selectedGallery) return;

    const files = Array.from(input.files);

    files.forEach((file) => {
      this.photoApi
        .create({
          galleryId: this.selectedGallery!.id,
          title: file.name,
        })
        .subscribe((photo) => {
          console.log('Photo metadata created:', photo);

          this.photoApi.upload(photo.id, file).subscribe(() => {
            console.log('File uploaded for photo:', photo.id);

            // абнаўляем галерэю
            this.selectGallery(this.selectedGallery!);
          });
        });
    });

    input.value = '';
  }

  openGalleryProperties() {
    this.showGalleryProperties = true;
  }

  closeGalleryProperties() {
    this.showGalleryProperties = false;
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
        this.showCreateGallery = false;
      },
      error: (err) => {
        console.error('create gallery error:', err);
      },
    });
  }

  deleteGalleryConfirmOpen = false;
  isDeletingGallery = false;
  deleteGalleryTitle = 'Delete gallery?';
  deleteGalleryMessage = 'This gallery and all its photos will be removed.';

  openDeleteGalleryConfirm() {
    if (!this.selectedGallery) return;
    this.deleteGalleryTitle = 'Delete gallery?';
    this.deleteGalleryMessage =
      'This gallery and all its photos will be removed.';
    this.deleteGalleryConfirmOpen = true;
  }

  closeDeleteGalleryConfirm() {
    this.deleteGalleryConfirmOpen = false;
  }

  onGalleryEmptied() {
    if (!this.selectedGallery) return;
    this.deleteGalleryTitle = 'Gallery is empty';
    this.deleteGalleryMessage =
      'This gallery has no photos left. Delete it as well?';
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

  openEditGallery() {
    if (!this.selectedGallery) return;
    this.editGalleryOpen = true;
  }

  closeEditGallery() {
    this.editGalleryOpen = false;
  }

  saveGalleryEdit(data: { title: string; description: string; tags: string[] }) {
    if (!this.selectedGallery) return;

    const galleryId = this.selectedGallery.id;
    this.isSavingGallery = true;

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
      },
    });
  }

  onGalleryCreatedElsewhere(gallery: Gallery) {
    this.galleries = [...this.galleries, gallery];
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
}
