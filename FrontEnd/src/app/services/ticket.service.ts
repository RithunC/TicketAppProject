import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  TicketCreateDto, TicketUpdateDto, TicketQueryDto,
  TicketResponseDto, TicketListItemDto, PagedResult,
  TicketStatusUpdateDto, TicketAssignRequestDto,
  TicketAutoAssignRequestDto, TicketAssignmentResponseDto,
  TicketStatusHistoryDto
} from '../models';

@Injectable({ providedIn: 'root' })
export class TicketService {
  private base = `${environment.apiUrl}/tickets`;
  constructor(private http: HttpClient) {}

  create(dto: TicketCreateDto): Observable<TicketResponseDto> {
    return this.http.post<TicketResponseDto>(this.base, dto);
  }

  get(id: number): Observable<TicketResponseDto> {
    return this.http.get<TicketResponseDto>(`${this.base}/${id}`);
  }

  query(dto: TicketQueryDto): Observable<PagedResult<TicketListItemDto>> {
    return this.http.post<PagedResult<TicketListItemDto>>(`${this.base}/query`, dto);
  }

  update(id: number, dto: TicketUpdateDto): Observable<TicketResponseDto> {
    return this.http.patch<TicketResponseDto>(`${this.base}/${id}`, dto);
  }

  updateStatus(id: number, dto: TicketStatusUpdateDto): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/status`, dto);
  }

  assign(id: number, dto: TicketAssignRequestDto): Observable<TicketAssignmentResponseDto> {
    return this.http.post<TicketAssignmentResponseDto>(`${this.base}/${id}/assign`, dto);
  }

  autoAssign(id: number, dto: TicketAutoAssignRequestDto): Observable<TicketAssignmentResponseDto> {
    return this.http.post<TicketAssignmentResponseDto>(`${this.base}/${id}/auto-assign`, dto);
  }

  getHistory(id: number): Observable<TicketStatusHistoryDto[]> {
    return this.http.get<TicketStatusHistoryDto[]>(`${this.base}/${id}/history`);
  }

  requestFeedback(id: number, pendingStatusId: number): Observable<TicketResponseDto> {
    return this.http.post<TicketResponseDto>(`${this.base}/${id}/request-feedback`, { pendingStatusId });
  }

  respondFeedback(id: number, approved: boolean): Observable<TicketResponseDto> {
    return this.http.post<TicketResponseDto>(`${this.base}/${id}/feedback-response`, { approved });
  }

  /** GET /tickets/employee/{employeeId} — All tickets created by an employee (Admin/Agent only) */
  getByEmployee(employeeId: number): Observable<PagedResult<TicketListItemDto>> {
    return this.http.get<PagedResult<TicketListItemDto>>(`${this.base}/employee/${employeeId}`);
  }

  /** GET /tickets/agent/{agentId} — All tickets assigned to an agent (Admin/Agent only) */
  getByAgent(agentId: number): Observable<PagedResult<TicketListItemDto>> {
    return this.http.get<PagedResult<TicketListItemDto>>(`${this.base}/agent/${agentId}`);
  }
}
