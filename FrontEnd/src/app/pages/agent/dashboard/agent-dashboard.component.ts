import { Component, inject, signal, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NavbarComponent } from '../../../shared/components/navbar/navbar.component';
import { ReportService } from '../../../services/report.service';
import { TicketService } from '../../../services/ticket.service';
import { AuthService } from '../../../services/auth.service';
import { TicketSummaryDto } from '../../../models';

interface AgentStats {
  assignedToMe: number;
  open: number;
  inProgress: number;
  resolved: number;
  overdue: number;
}

@Component({
  selector: 'app-agent-dashboard',
  standalone: true,
  imports: [RouterLink, NavbarComponent],
  templateUrl: './agent-dashboard.component.html',
  styleUrls: ['./agent-dashboard.component.css']
})
export class AgentDashboardComponent implements OnInit {
  auth = inject(AuthService);
  rpt  = inject(ReportService);
  ts   = inject(TicketService);

  summary    = signal<TicketSummaryDto | null>(null);
  agentStats = signal<AgentStats | null>(null);

  ngOnInit(): void {
    const myId = this.auth.currentUser()?.id;
    if (!myId) return;

    // Global summary (still useful for reference)
    this.rpt.getTicketSummary().subscribe(s => this.summary.set(s));

    // Use GET /tickets/agent/{agentId} — dedicated endpoint, single call, no loop
    this.ts.getByAgent(myId).subscribe(result => {
      const items = result.items;
      const now   = new Date();
      this.agentStats.set({
        assignedToMe: result.totalCount,
        open:         items.filter(t =>
                        t.status.toLowerCase() === 'new' ||
                        t.status.toLowerCase() === 'open').length,
        inProgress:   items.filter(t => t.status.toLowerCase() === 'in progress').length,
        resolved:     items.filter(t => t.status.toLowerCase() === 'resolved').length,
        overdue:      items.filter(t =>
                        !!t.dueAt && new Date(t.dueAt) < now &&
                        t.status.toLowerCase() !== 'closed' &&
                        t.status.toLowerCase() !== 'resolved').length,
      });
    });
  }
}
