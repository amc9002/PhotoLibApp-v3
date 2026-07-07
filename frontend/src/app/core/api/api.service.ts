import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, catchError, throwError } from 'rxjs';
import { ConnectivityService } from '../offline/connectivity.service';

@Injectable({ providedIn: 'root' })
export class ApiService {
  constructor(
    private http: HttpClient,
    private connectivity: ConnectivityService,
  ) {}

  get<T>(url: string) {
    return this.wrap(this.http.get<T>(`/api/${url}`));
  }

  post<T>(url: string, body: unknown) {
    return this.wrap(this.http.post<T>(`/api/${url}`, body));
  }

  put<T>(url: string, body: unknown) {
    return this.wrap(this.http.put<T>(`/api/${url}`, body));
  }

  delete<T>(url: string) {
    return this.wrap(this.http.delete<T>(`/api/${url}`));
  }

  /**
   * A `status === 0` response means the request never reached the server
   * (dropped connection, DNS failure, etc.) as opposed to a real 4xx/5xx
   * the server actually returned - only that case should flip connectivity,
   * everything else is a genuine error the caller should still see.
   */
  private wrap<T>(source: Observable<T>): Observable<T> {
    return source.pipe(
      catchError((err) => {
        if (err instanceof HttpErrorResponse && err.status === 0) {
          this.connectivity.markOffline();
        }
        return throwError(() => err);
      }),
    );
  }
}
