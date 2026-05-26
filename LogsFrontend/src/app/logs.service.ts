import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { LogEntry, PaginatedResult } from './log.model';

@Injectable({
  providedIn: 'root'
})
export class LogsService {
  private apiUrl = 'http://localhost:5200/api/logs';

  constructor(private http: HttpClient) { }

  getLogs(page: number, pageSize: number, filters: any): Observable<PaginatedResult<LogEntry>> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());

    if (filters.level) params = params.set('level', filters.level);
    if (filters.serviceName) params = params.set('serviceName', filters.serviceName);
    if (filters.search) params = params.set('search', filters.search);
    if (filters.startDate) params = params.set('startDate', filters.startDate);
    if (filters.endDate) params = params.set('endDate', filters.endDate);

    return this.http.get<PaginatedResult<LogEntry>>(this.apiUrl, { params });
  }
}
