import { Injectable, OnDestroy } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Subscription, catchError, filter, interval, of, timeout } from 'rxjs';

const PROBE_TIMEOUT_MS = 3000;
const REPROBE_INTERVAL_MS = 15000;

/**
 * Tracks whether the backend is actually reachable - not just whether the
 * OS network interface is up. `navigator.onLine`/the `online`/`offline`
 * events are a fast first signal but don't reflect this specific server
 * being down, so every signal is confirmed against a real `GET /api/Health`
 * probe before flipping to "online".
 */
@Injectable({ providedIn: 'root' })
export class ConnectivityService implements OnDestroy {
  private readonly online$ = new BehaviorSubject<boolean>(navigator.onLine);
  readonly isOnline$ = this.online$.asObservable();

  private readonly reprobeSub: Subscription;
  private readonly onOnline = () => this.probe();
  private readonly onOffline = () => this.online$.next(false);

  get isOnline(): boolean {
    return this.online$.value;
  }

  constructor(private http: HttpClient) {
    window.addEventListener('online', this.onOnline);
    window.addEventListener('offline', this.onOffline);

    this.probe();

    // navigator.onLine can flip true (wifi reconnected) while this specific
    // backend is still down, so keep re-checking until a probe succeeds.
    this.reprobeSub = interval(REPROBE_INTERVAL_MS)
      .pipe(filter(() => !this.online$.value))
      .subscribe(() => this.probe());
  }

  ngOnDestroy(): void {
    window.removeEventListener('online', this.onOnline);
    window.removeEventListener('offline', this.onOffline);
    this.reprobeSub.unsubscribe();
  }

  /** Called by ApiService the moment a real request fails to even reach the server. */
  markOffline(): void {
    this.online$.next(false);
  }

  private probe(): void {
    this.http
      .get('/api/Health', { responseType: 'text' })
      .pipe(
        timeout(PROBE_TIMEOUT_MS),
        catchError(() => of(null)),
      )
      .subscribe((result) => this.online$.next(result !== null));
  }
}
