import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { CreateUpdateProjectDto, ProjectDto, ProjectWithIdDto } from '../models';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class ProjectService {
  private http = inject(HttpClient);
  private baseUrl = `${environment.apiUrl}/api/project`;

  getAll(): Observable<ProjectDto[]> {
    return this.http.get<ProjectDto[]>(this.baseUrl);
  }

  getAllWithId(): Observable<ProjectWithIdDto[]> {
    return this.http.get<ProjectWithIdDto[]>(`${this.baseUrl}/withId`);
  }

  getById(id: string): Observable<ProjectDto> {
    return this.http.get<ProjectDto>(`${this.baseUrl}/${id}`);
  }

  getByIdWithTasks(id: string): Observable<ProjectDto> {
    return this.http.get<ProjectDto>(`${this.baseUrl}/WithTasks/${id}`);
  }

  create(dto: CreateUpdateProjectDto): Observable<ProjectDto> {
    return this.http.post<ProjectDto>(this.baseUrl, dto);
  }

  update(id: string, dto: CreateUpdateProjectDto): Observable<ProjectDto> {
    return this.http.put<ProjectDto>(`${this.baseUrl}/${id}`, dto);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  addUser(projectId: string, userId: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${projectId}/users/${userId}`, {});
  }

  removeUser(projectId: string, userId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${projectId}/users/${userId}`);
  }
}
