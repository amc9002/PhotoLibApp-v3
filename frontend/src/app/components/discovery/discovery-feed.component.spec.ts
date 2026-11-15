import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { BehaviorSubject, Observable, Subject, of } from 'rxjs';
import { DiscoveryFeedComponent } from './discovery-feed.component';
import { DiscoveryApiService } from '../../services/discovery-api.service';
import { ConnectivityService } from '../../core/offline/connectivity.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { DiscoveryPhotoDto } from '../../models/discovery-photo.dto';

/**
 * Stand-ins for the real child components, swapped in via
 * `overrideComponent` below - this keeps these tests from pulling in
 * `ImageCacheService`/IndexedDB (via the real `PhotoThumbnailComponent`) or
 * the much heavier real `PhotoViewerComponent` tree. Matching selectors and
 * bindings only - no behavior of their own.
 */
@Component({ selector: 'app-photo-thumbnail', standalone: true, template: '' })
class StubPhotoThumbnailComponent {
  @Input() photoId!: string;
  @Input() hasThumbnail = true;
}

@Component({ selector: 'app-photo-viewer', standalone: true, template: '' })
class StubPhotoViewerComponent {
  @Input() photos: unknown;
  @Input() activePhotoId: unknown;
  @Output() close = new EventEmitter<void>();
  @Output() photoSelected = new EventEmitter<string>();
}

/**
 * Fake with a controllable `BehaviorSubject` instead of the real
 * `ConnectivityService`, which would otherwise fire a genuine
 * `HttpClient.get('/api/Health')` probe (plus a 15s re-probe interval) from
 * its own constructor.
 */
class FakeConnectivityService {
  private readonly subject: BehaviorSubject<boolean>;
  readonly isOnline$: Observable<boolean>;

  constructor(initiallyOnline: boolean) {
    this.subject = new BehaviorSubject<boolean>(initiallyOnline);
    this.isOnline$ = this.subject.asObservable();
  }

  get isOnline(): boolean {
    return this.subject.value;
  }

  setOnline(value: boolean): void {
    this.subject.next(value);
  }
}

function samplePhoto(id: string): DiscoveryPhotoDto {
  return {
    id,
    title: `Photo ${id}`,
    hasThumbnail: true,
    updatedAtUtc: new Date().toISOString(),
    tags: [],
    ownerName: 'Me',
  };
}

describe('DiscoveryFeedComponent', () => {
  let fixture: ComponentFixture<DiscoveryFeedComponent>;
  let component: DiscoveryFeedComponent;
  let discoveryApiSpy: jasmine.SpyObj<DiscoveryApiService>;
  let fakeConnectivity: FakeConnectivityService;

  // `initialFeedResult` must be configured before `TestBed.createComponent`
  // runs below: the component's constructor subscribes to `isOnline$`,
  // which (being a BehaviorSubject) replays its current value immediately
  // and synchronously - so if `initiallyOnline` is true, the very first
  // `getFeed` call already happens during construction, not later during
  // `detectChanges()`.
  function setup(
    initiallyOnline: boolean,
    initialFeedResult: Observable<DiscoveryPhotoDto[]> = of([]),
  ): void {
    fakeConnectivity = new FakeConnectivityService(initiallyOnline);
    discoveryApiSpy = jasmine.createSpyObj<DiscoveryApiService>('DiscoveryApiService', [
      'getFeed',
      'recordView',
    ]);
    discoveryApiSpy.getFeed.and.returnValue(initialFeedResult);

    TestBed.configureTestingModule({
      imports: [DiscoveryFeedComponent],
      providers: [
        provideNoopAnimations(),
        { provide: DiscoveryApiService, useValue: discoveryApiSpy },
        { provide: ConnectivityService, useValue: fakeConnectivity },
      ],
    }).overrideComponent(DiscoveryFeedComponent, {
      set: {
        imports: [CommonModule, StubPhotoThumbnailComponent, StubPhotoViewerComponent, TranslatePipe],
      },
    });

    fixture = TestBed.createComponent(DiscoveryFeedComponent);
    component = fixture.componentInstance;
  }

  it('loads the feed on init when already online, calling getFeed exactly once', () => {
    setup(true, of([samplePhoto('1'), samplePhoto('2')]));

    fixture.detectChanges();

    // Regression guard: an earlier version called loadInitial() from both
    // the constructor's isOnline$ subscription (which replays immediately
    // for an already-online BehaviorSubject) and ngOnInit(), firing getFeed
    // twice on every cold start while online.
    expect(discoveryApiSpy.getFeed).toHaveBeenCalledTimes(1);
    expect(discoveryApiSpy.getFeed).toHaveBeenCalledWith(40, []);
    expect(component.photos.length).toBe(2);
  });

  it('shows the offline placeholder and never calls getFeed when offline at init', () => {
    setup(false);

    fixture.detectChanges();

    expect(discoveryApiSpy.getFeed).not.toHaveBeenCalled();
    const message: HTMLElement = fixture.nativeElement.querySelector('.discovery-feed-message');
    expect(message).toBeTruthy();
    expect(fixture.nativeElement.querySelector('.discovery-grid')).toBeFalsy();
  });

  it('loads the feed automatically once connectivity comes back online', () => {
    setup(false, of([samplePhoto('1')]));
    fixture.detectChanges();
    expect(discoveryApiSpy.getFeed).not.toHaveBeenCalled();

    fakeConnectivity.setOnline(true);

    expect(discoveryApiSpy.getFeed).toHaveBeenCalledTimes(1);
    expect(component.photos.length).toBe(1);
  });

  it('ignores a loadMore trigger while a previous one is still in flight', () => {
    setup(true, of([samplePhoto('1'), samplePhoto('2')]));
    fixture.detectChanges();
    expect(discoveryApiSpy.getFeed).toHaveBeenCalledTimes(1);

    const moreSubject = new Subject<DiscoveryPhotoDto[]>();
    discoveryApiSpy.getFeed.and.returnValue(moreSubject.asObservable());

    const scrollEvent = {
      target: { scrollHeight: 1000, scrollTop: 900, clientHeight: 500 },
    } as unknown as Event;

    component.onScroll(scrollEvent); // starts loadMore, awaiting moreSubject
    component.onScroll(scrollEvent); // must be ignored - a load is already in flight

    expect(discoveryApiSpy.getFeed).toHaveBeenCalledTimes(2); // 1 initial + exactly 1 loadMore

    moreSubject.next([samplePhoto('3')]);
    moreSubject.complete();
  });

  it('opens and closes the photo viewer, tracking the active photo id', () => {
    setup(true, of([samplePhoto('1')]));
    fixture.detectChanges();

    component.openViewer('1');
    expect(component.viewerOpen).toBeTrue();
    expect(component.viewerPhotoId).toBe('1');

    component.closeViewer();
    expect(component.viewerOpen).toBeFalse();
    expect(component.viewerPhotoId).toBeUndefined();
  });

  it('retries once per online signal (not in an uncontrolled loop) while the feed is genuinely empty', () => {
    // Distinct from the double-call fix above: an empty result leaves
    // photos.length === 0, so the same guard that correctly triggers a
    // reconnect-reload also legitimately re-fires on every later online
    // signal while the feed stays empty. This locks in that it's bounded
    // to exactly one getFeed call per explicit online signal - not a
    // synchronous/runaway loop - and documents the behavior rather than
    // changing it, since changing it would be a separate, larger decision.
    setup(true, of([]));
    fixture.detectChanges();
    expect(discoveryApiSpy.getFeed).toHaveBeenCalledTimes(1);

    fakeConnectivity.setOnline(false);
    fakeConnectivity.setOnline(true);
    fakeConnectivity.setOnline(false);
    fakeConnectivity.setOnline(true);

    expect(discoveryApiSpy.getFeed).toHaveBeenCalledTimes(3);
  });
});
