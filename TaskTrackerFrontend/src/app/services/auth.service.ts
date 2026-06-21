import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { BehaviorSubject, Observable, tap } from 'rxjs';
import { AuthResponseDto, LoginDto, RegisterDto } from '../models';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);
  private router = inject(Router);

  private readonly TOKEN_KEY = 'tt_token';
  private readonly USER_KEY = 'tt_user';

  private _currentUser$ = new BehaviorSubject<AuthResponseDto | null>(this.loadUser());
  currentUser$ = this._currentUser$.asObservable();

  private baseUrl = `${environment.apiUrl}/api/auth`;

  register(dto: RegisterDto): Observable<AuthResponseDto> {
    return this.http.post<AuthResponseDto>(`${this.baseUrl}/register`, dto).pipe(
      tap(res => this.saveSession(res))
    );
  }

  login(dto: LoginDto): Observable<AuthResponseDto> {
    return this.http.post<AuthResponseDto>(`${this.baseUrl}/login`, dto).pipe(
      tap(res => this.saveSession(res))
    );
  }

  logout(): void {
    localStorage.removeItem(this.TOKEN_KEY);
    localStorage.removeItem(this.USER_KEY);
    this._currentUser$.next(null);
    this.router.navigate(['/login']);
  }

  getToken(): string | null {
    return localStorage.getItem(this.TOKEN_KEY);
  }

  isLoggedIn(): boolean {
    return !!this.getToken();
  }

  get currentUser(): AuthResponseDto | null {
    return this._currentUser$.value;
  }

  isAdmin(): boolean {
    return this.currentUser?.role === 'Admin';
  }

  get currentUserId(): string | null {
    return this.currentUser?.id ?? null;
  }

  private saveSession(res: AuthResponseDto): void {
    localStorage.setItem(this.TOKEN_KEY, res.token);
    localStorage.setItem(this.USER_KEY, JSON.stringify(res));
    this._currentUser$.next(res);
  }

  private loadUser(): AuthResponseDto | null {
    const raw = localStorage.getItem(this.USER_KEY);
    return raw ? JSON.parse(raw) : null;
  }
}
