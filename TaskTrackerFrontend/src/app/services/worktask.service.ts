import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { CreateUpdateWorkTaskDto, Status, WorkTaskDto, WorkTaskWithIdDto } from '../models';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class WorkTaskService {
  private http = inject(HttpClient);
  private baseUrl = `${environment.apiUrl}/api/worktask`;

  getAll(): Observable<WorkTaskDto[]> {
    return this.http.get<WorkTaskDto[]>(this.baseUrl);
  }

  getAllWithId(): Observable<WorkTaskWithIdDto[]> {
    return this.http.get<WorkTaskWithIdDto[]>(`${this.baseUrl}/WithId`);
  }

  getById(id: string): Observable<WorkTaskDto> {
    return this.http.get<WorkTaskDto>(`${this.baseUrl}/${id}`);
  }

  create(dto: CreateUpdateWorkTaskDto): Observable<WorkTaskDto> {
    return this.http.post<WorkTaskDto>(this.baseUrl, dto);
  }

  update(id: string, dto: CreateUpdateWorkTaskDto): Observable<WorkTaskDto> {
    return this.http.put<WorkTaskDto>(`${this.baseUrl}/${id}`, dto);
  }

  updateStatus(id: string, status: Status): Observable<WorkTaskDto> {
    return this.http.patch<WorkTaskDto>(`${this.baseUrl}/${id}/status`, { status });
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  addComment(id: string, content: string): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/${id}/comments`, `"${content}"`, {
      headers: { 'Content-Type': 'application/json' }
    });
  }
}
