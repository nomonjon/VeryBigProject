import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface LogEntry {
  id: string;
  serviceName: string;
  level: string;
  message: string;
  createdAt: string;
}

export interface PagedResult {
  items: LogEntry[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface LogQuery {
  page?: number;
  pageSize?: number;
  level?: string;
  serviceName?: string;
  search?: string;
  from?: string;
  to?: string;
  sortBy?: string;
  sortOrder?: string;
}

@Injectable({ providedIn: 'root' })
export class LogsService {
  private apiUrl = 'http://localhost:5003/api/logs';

  constructor(private http: HttpClient) {}

  getLogs(query: LogQuery): Observable<PagedResult> {
    let params = new HttpParams();
    if (query.page) params = params.set('page', query.page.toString());
    if (query.pageSize) params = params.set('pageSize', query.pageSize.toString());
    if (query.level) params = params.set('level', query.level);
    if (query.serviceName) params = params.set('serviceName', query.serviceName);
    if (query.search) params = params.set('search', query.search);
    if (query.from) params = params.set('from', query.from);
    if (query.to) params = params.set('to', query.to);
    if (query.sortBy) params = params.set('sortBy', query.sortBy);
    if (query.sortOrder) params = params.set('sortOrder', query.sortOrder);
    return this.http.get<PagedResult>(this.apiUrl, { params });
  }

  getServices(): Observable<string[]> {
    return this.http.get<string[]>(`${this.apiUrl}/services`);
  }

  getLevels(): Observable<string[]> {
    return this.http.get<string[]>(`${this.apiUrl}/levels`);
  }
}
