import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { CreateUpdateUserDto, UserDto, UserWithIdDto } from '../models';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class UserService {
  private http = inject(HttpClient);
  private baseUrl = `${environment.apiUrl}/api/user`;

  getAll(): Observable<UserDto[]> {
    return this.http.get<UserDto[]>(this.baseUrl);
  }

  getAllWithId(): Observable<UserWithIdDto[]> {
    return this.http.get<UserWithIdDto[]>(`${this.baseUrl}/withId`);
  }

  getById(id: string): Observable<UserDto> {
    return this.http.get<UserDto>(`${this.baseUrl}/${id}`);
  }

  create(dto: CreateUpdateUserDto): Observable<UserDto> {
    return this.http.post<UserDto>(this.baseUrl, dto);
  }

  update(id: string, dto: CreateUpdateUserDto): Observable<UserDto> {
    return this.http.put<UserDto>(`${this.baseUrl}/${id}`, dto);
  }

  updatePartly(id: string, dto: Partial<CreateUpdateUserDto>): Observable<UserDto> {
    return this.http.patch<UserDto>(`${this.baseUrl}/${id}`, dto);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  updateRole(id: string, dto: { role: string }): Observable<UserDto> {
    return this.http.patch<UserDto>(`${this.baseUrl}/${id}/role`, dto);
  }
}
