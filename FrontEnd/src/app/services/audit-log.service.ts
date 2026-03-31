import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuditLogQueryDto, AuditLogResponseDto, PagedResult } from '../models';

@Injectable({ providedIn: 'root' })
export class AuditLogService {
  private base = `${environment.apiUrl}/auditlogs`;
  constructor(private http: HttpClient) {}

  getRecent(take = 100): Observable<AuditLogResponseDto[]> {
    return this.http.get<AuditLogResponseDto[]>(`${this.base}/recent`, { params: { take: String(take) } });
  }

  query(dto: AuditLogQueryDto): Observable<PagedResult<AuditLogResponseDto>> {
    return this.http.post<PagedResult<AuditLogResponseDto>>(`${this.base}/query`, dto);
  }
}
