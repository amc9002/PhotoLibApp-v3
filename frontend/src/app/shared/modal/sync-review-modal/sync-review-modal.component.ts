import { Component, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subscription } from 'rxjs';
import { SyncCoordinatorService } from '../../../core/offline/sync-coordinator.service';
import { SyncReviewItem } from '../../../core/offline/sync-review.model';
import { PhotoThumbnailComponent } from '../../ui/photo-thumbnail/photo-thumbnail.component';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';

/**
 * Shown once the app reconnects with unconfirmed offline changes queued.
 * Lists what changed while offline - added / deleted / changed, plus
 * conflicts where the server moved in the meantime - for the user to
 * confirm (individually or in bulk) before anything is pushed.
 */
@Component({
  selector: 'app-sync-review-modal',
  standalone: true,
  imports: [CommonModule, PhotoThumbnailComponent, TranslatePipe],
  templateUrl: './sync-review-modal.component.html',
  styleUrls: ['./sync-review-modal.component.css'],
})
export class SyncReviewModalComponent implements OnDestroy {
  items: SyncReviewItem[] = [];
  busy = false;

  private readonly sub: Subscription;

  constructor(private syncCoordinator: SyncCoordinatorService) {
    this.sub = this.syncCoordinator.reviewSummary$.subscribe((summary) => {
      this.items = summary?.items ?? [];
    });
  }

  ngOnDestroy(): void {
    this.sub.unsubscribe();
  }

  get added(): SyncReviewItem[] {
    return this.items.filter((i) => i.kind === 'added' && !i.conflict);
  }

  get deleted(): SyncReviewItem[] {
    return this.items.filter((i) => i.kind === 'deleted' && !i.conflict);
  }

  get changed(): SyncReviewItem[] {
    return this.items.filter((i) => i.kind === 'changed' && !i.conflict);
  }

  get conflicted(): SyncReviewItem[] {
    return this.items.filter((i) => i.conflict);
  }

  async confirm(item: SyncReviewItem): Promise<void> {
    if (this.busy) return;
    this.busy = true;
    try {
      await this.syncCoordinator.confirmItem(item);
      this.removeItem(item);
    } catch (err) {
      console.error('Failed to sync item', item, err);
    } finally {
      this.busy = false;
    }
  }

  async discard(item: SyncReviewItem): Promise<void> {
    if (this.busy) return;
    this.busy = true;
    try {
      await this.syncCoordinator.discardItem(item);
      this.removeItem(item);
    } catch (err) {
      console.error('Failed to discard item', item, err);
    } finally {
      this.busy = false;
    }
  }

  async confirmAll(): Promise<void> {
    if (this.busy) return;
    this.busy = true;
    try {
      for (const item of [...this.items]) {
        await this.syncCoordinator.confirmItem(item);
        this.removeItem(item);
      }
    } catch (err) {
      console.error('Failed to sync all items', err);
    } finally {
      this.busy = false;
    }
  }

  async discardAll(): Promise<void> {
    if (this.busy) return;
    this.busy = true;
    try {
      for (const item of [...this.items]) {
        await this.syncCoordinator.discardItem(item);
        this.removeItem(item);
      }
    } catch (err) {
      console.error('Failed to discard all items', err);
    } finally {
      this.busy = false;
    }
  }

  trackByOpId(index: number, item: SyncReviewItem): number {
    return item.outboxOpId;
  }

  private removeItem(item: SyncReviewItem): void {
    this.items = this.items.filter((i) => i !== item);
    if (this.items.length === 0) {
      void this.syncCoordinator.finishReview();
    }
  }
}
