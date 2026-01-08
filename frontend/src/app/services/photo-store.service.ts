import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { Photo } from '../models/photo.model';

@Injectable({
  providedIn: 'root',
})
export class PhotoStoreService {
  private readonly photos$ = new BehaviorSubject<Photo[]>([]);

  getPhotos() {
    return this.photos$.asObservable();
  }

  add(photo: Photo) {
    this.photos$.next([...this.photos$.value, photo]);
  }

  remove(localId: number) {
    this.photos$.next(this.photos$.value.filter((p) => p.localId !== localId));
  }

  update(photo: Photo) {
    this.photos$.next(
      this.photos$.value.map((p) => (p.localId === photo.localId ? photo : p)),
    );
  }
}
