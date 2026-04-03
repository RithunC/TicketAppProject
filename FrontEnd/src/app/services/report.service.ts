import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { TicketSummaryDto } from '../models';

@Injectable({ providedIn: 'root' }) //make the service globally available
export class ReportService {
  constructor(private http: HttpClient) {}
  getTicketSummary(): Observable<TicketSummaryDto> {
    return this.http.get<TicketSummaryDto>(`${environment.apiUrl}/reports/tickets/summary`);
  }
}
