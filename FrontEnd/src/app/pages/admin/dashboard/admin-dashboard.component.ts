import { Component, inject, signal, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NavbarComponent } from '../../../shared/components/navbar/navbar.component';
import { ReportService } from '../../../services/report.service';
import { TicketSummaryDto, AgentWorkloadDto } from '../../../models';

@Component({ selector:'app-admin-dashboard', standalone:true, imports:[RouterLink,NavbarComponent], templateUrl:'./admin-dashboard.component.html', styleUrls:['./admin-dashboard.component.css'] })
export class AdminDashboardComponent implements OnInit {
  rpt      = inject(ReportService);
  summary  = signal<TicketSummaryDto | null>(null);
  workload = signal<AgentWorkloadDto[]>([]);

  ngOnInit(): void {
    this.rpt.getTicketSummary().subscribe(s => this.summary.set(s));
    this.rpt.getAgentWorkload().subscribe(w => this.workload.set(w));
  }

  pct(v: number, t: number): number { return t > 0 ? Math.round((v/t)*100) : 0; }
}
