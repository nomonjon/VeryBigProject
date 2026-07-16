import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { CreateUpdateProductRuleDto, ProductRuleDto } from '../models';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class RuleService {
  private http = inject(HttpClient);
  private baseUrl = `${environment.apiUrl}/api/productrule`;

  getAll(): Observable<ProductRuleDto[]> {
    return this.http.get<ProductRuleDto[]>(this.baseUrl);
  }

  create(dto: CreateUpdateProductRuleDto): Observable<ProductRuleDto> {
    return this.http.post<ProductRuleDto>(this.baseUrl, dto);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
