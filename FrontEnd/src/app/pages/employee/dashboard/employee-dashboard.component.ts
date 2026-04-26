import { Component, inject, signal, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NavbarComponent } from '../../../shared/components/navbar/navbar.component';
import { ReportService } from '../../../services/report.service';
import { AuthService } from '../../../services/auth.service';
import { EmployeeTicketService } from '../../../services/employee-ticket.service';
import { TicketSummaryDto } from '../../../models';

@Component({
  selector: 'app-employee-dashboard',
  standalone: true,
  imports: [RouterLink, NavbarComponent],
  templateUrl: './employee-dashboard.component.html',
  styleUrls: ['./employee-dashboard.component.css']
})
export class EmployeeDashboardComponent implements OnInit {
  auth   = inject(AuthService);
  rpt    = inject(ReportService);
  empTs  = inject(EmployeeTicketService);
  summary          = signal<TicketSummaryDto | null>(null);
  pendingFeedback  = signal(0);

  ngOnInit(): void {
    this.rpt.getTicketSummary().subscribe(s => this.summary.set(s));
    this.empTs.getMyTickets().subscribe(tickets => {
      this.pendingFeedback.set(tickets.filter(t => t.feedbackStatus === 'Pending').length);
    });
  }
}
