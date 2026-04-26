import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { TicketSummaryDto, AgentWorkloadDto } from '../models';

@Injectable({ providedIn: 'root' })
export class ReportService {
  constructor(private http: HttpClient) {}

  getTicketSummary(): Observable<TicketSummaryDto> {
    return this.http.get<TicketSummaryDto>(`${environment.apiUrl}/reports/tickets/summary`);
  }

  getAgentWorkload(): Observable<AgentWorkloadDto[]> {
    return this.http.get<AgentWorkloadDto[]>(`${environment.apiUrl}/reports/agents/workload`);
  }
}
