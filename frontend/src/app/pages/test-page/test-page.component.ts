import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Observable } from 'rxjs';
import { PhotoApiService } from '../../services/photo-api.service';
import { PhotoDto } from '../../models/photo.dto';

@Component({
  standalone: true,
  selector: 'app-test-page',
  imports: [CommonModule],
  template: `
    <h2>Photo API Test</h2>

    <div style="margin-bottom: 1rem;">
      <button (click)="load()">Load from API</button>
      <button (click)="create()">Create photo</button>
    </div>

    <div *ngIf="loading">Loading...</div>
    <div *ngIf="error" style="color:red">{{ error }}</div>

    <ul>
      <li *ngFor="let photo of photos">
        <strong>{{ photo.title }}</strong>
        <small>({{ photo.id }})</small>
        <button (click)="remove(photo.id)">✖</button>
      </li>
    </ul>
  `,
})
export class TestPageComponent implements OnInit {
  photos: PhotoDto[] = [];
  loading = false;
  error: string | null = null;

  private counter = 1;

  constructor(private api: PhotoApiService) {}

  ngOnInit(): void {
    this.load();
  }

  load() {
    this.loading = true;
    this.error = null;

    this.api.getAll().subscribe({
      next: (data) => {
        this.photos = data;
        this.loading = false;
      },
      error: (err) => {
        this.error = 'Failed to load photos';
        this.loading = false;
        console.error(err);
      },
    });
  }

  create() {
    const dto: Partial<PhotoDto> = {
      title: 'API Photo ' + this.counter++,
    };

    this.api.create(dto).subscribe({
      next: () => this.load(),
      error: (err) => {
        this.error = 'Failed to create photo';
        console.error(err);
      },
    });
  }

  remove(id: string) {
    this.api.delete(id).subscribe({
      next: () => this.load(),
      error: (err) => {
        this.error = 'Failed to delete photo';
        console.error(err);
      },
    });
  }
}
