import { Injectable, inject } from '@angular/core';
import { TicketService } from './ticket.service';
import { AuthService } from './auth.service';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { TicketListItemDto } from '../models';

/**
 * Employee ticket service.
 *
 * Uses GET /tickets/employee/{employeeId} — a dedicated backend endpoint
 * that returns all tickets created by a specific employee.
 *
 * This endpoint is [Authorize(Roles = "Admin,Agent")] on the backend,
 * BUT employees can call it with their own ID because the backend
 * enforces ownership checks. If your backend restricts this,
 * the AdminAgent-accessible endpoint still works because the employee
 * is the createdByUserId and results are filtered server-side.
 *
 * NOTE: We store ticket IDs locally only for the ticket-detail saveTicketId
 * call (so My Tickets stays in sync after creating tickets). The actual
 * listing now uses the API directly.
 */
@Injectable({ providedIn: 'root' })
export class EmployeeTicketService {
  private ts   = inject(TicketService);
  private auth = inject(AuthService);

  private get storageKey(): string {
    const uid = this.auth.currentUser()?.id ?? 0;
    return `emp_ticket_ids_${uid}`;
  }

  /** Save a ticket ID locally (used so ticket-detail page registers tickets) */
  saveTicketId(ticketId: number): void {
    const ids = this.getStoredIds();
    if (!ids.includes(ticketId)) {
      ids.unshift(ticketId);
      localStorage.setItem(this.storageKey, JSON.stringify(ids));
    }
  }

  getStoredIds(): number[] {
    try {
      const raw = localStorage.getItem(this.storageKey);
      return raw ? JSON.parse(raw) : [];
    } catch { return []; }
  }

  /**
   * Fetch all tickets created by the current employee.
   * Uses GET /tickets/employee/{employeeId} — single API call, no loops.
   * Returns tickets sorted newest first.
   */
  getMyTickets(): Observable<TicketListItemDto[]> {
    const myId = this.auth.currentUser()?.id;
    if (!myId) return new Observable(obs => { obs.next([]); obs.complete(); });

    return this.ts.getByEmployee(myId).pipe(
      map(result => result.items.sort((a, b) =>
        new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()
      ))
    );
  }

  clearStoredIds(): void {
    localStorage.removeItem(this.storageKey);
  }
}
