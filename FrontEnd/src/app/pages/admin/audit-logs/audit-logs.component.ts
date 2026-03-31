import { Component, inject, signal, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { NavbarComponent } from '../../../shared/components/navbar/navbar.component';
import { AuditLogService } from '../../../services/audit-log.service';
import { AuditLogResponseDto, AuditLogQueryDto, PagedResult } from '../../../models';

// Raw backend action → human-readable label (used in both Action column AND dropdown)
const ACTION_LABELS: Record<string, string> = {
  LOGIN:           'Logged In',
  LOGOUT:          'Logged Out',
  TICKET_CREATED:  'Ticket Created',
  TICKET_UPDATED:  'Ticket Updated',
  TICKET_CLOSED:   'Ticket Closed',
  TICKET_DELETED:  'Ticket Deleted',
  COMMENT_ADDED:   'Comment Added',
  USER_REGISTERED: 'User Registered',
  PROFILE_UPDATED: 'Profile Updated',
  GET:             'Viewed Record',
  QUERY:           'Searched Records',
  ASSIGN:          'Ticket Assigned',
  AUTOASSIGN:      'Auto-Assigned Ticket',
  UPDATESTATUS:    'Status Changed',
  GETHISTORY:      'Viewed History',
  GETALLUSERS:     'Viewed All Users',
  GETME:           'Viewed Own Profile',
  GETAGENTS:       'Viewed Agents List',
};

const ACTION_CLASSES: Record<string, string> = {
  LOGIN:           'act-login',
  LOGOUT:          'act-logout',
  TICKET_CREATED:  'act-add',
  TICKET_UPDATED:  'act-upd',
  TICKET_CLOSED:   'act-close',
  TICKET_DELETED:  'act-del',
  COMMENT_ADDED:   'act-comment',
  USER_REGISTERED: 'act-add',
  PROFILE_UPDATED: 'act-upd',
  GET:             'act-get',
  QUERY:           'act-query',
  ASSIGN:          'act-upd',
  AUTOASSIGN:      'act-upd',
  UPDATESTATUS:    'act-upd',
};

@Component({
  selector: 'app-audit-logs',
  standalone: true,
  imports: [FormsModule, DatePipe, NavbarComponent],
  templateUrl: './audit-logs.component.html',
  styleUrls: ['./audit-logs.component.css']
})
export class AuditLogsComponent implements OnInit {
  svc      = inject(AuditLogService);
  result   = signal<PagedResult<AuditLogResponseDto> | null>(null);
  loading  = signal(false);
  page     = signal(1);
  pageSize = 50;

  filters = {
    action:        '',
    actorUserName: '',
    role:          '',
    fromUtc:       '',
    toUtc:         '',
    status:        '' as '' | 'success' | 'failed'
  };

  /**
   * Dropdown options: value = raw backend action string (sent to API & matched against log.action),
   * label = same human-readable text shown in the Action column badge.
   */
  readonly actionOptions = [
    { value: '',                label: 'All Actions'         },
    { value: 'LOGIN',           label: ACTION_LABELS['LOGIN']           },
    { value: 'LOGOUT',          label: ACTION_LABELS['LOGOUT']          },
    { value: 'TICKET_CREATED',  label: ACTION_LABELS['TICKET_CREATED']  },
    { value: 'TICKET_UPDATED',  label: ACTION_LABELS['TICKET_UPDATED']  },
    { value: 'TICKET_CLOSED',   label: ACTION_LABELS['TICKET_CLOSED']   },
    { value: 'TICKET_DELETED',  label: ACTION_LABELS['TICKET_DELETED']  },
    { value: 'COMMENT_ADDED',   label: ACTION_LABELS['COMMENT_ADDED']   },
    { value: 'USER_REGISTERED', label: ACTION_LABELS['USER_REGISTERED'] },
    { value: 'PROFILE_UPDATED', label: ACTION_LABELS['PROFILE_UPDATED'] },
  ];

  readonly roleOptions = [
    { value: '',         label: 'All Roles' },
    { value: 'Admin',    label: 'Admin'     },
    { value: 'Agent',    label: 'Agent'     },
    { value: 'Employee', label: 'Employee'  },
  ];

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading.set(true);
    const dto: AuditLogQueryDto = {
      page:     this.page(),
      pageSize: this.pageSize,
      // Send raw action value — backend now filters by it
      action:   this.filters.action  || undefined,
      fromUtc:  this.filters.fromUtc || undefined,
      toUtc:    this.filters.toUtc   || undefined,
    };
    this.svc.query(dto).subscribe({
      next: r => { this.result.set(r); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }

  applyFilter(): void { this.page.set(1); this.load(); }

  clearFilters(): void {
    this.filters = { action: '', actorUserName: '', role: '', fromUtc: '', toUtc: '', status: '' };
    this.page.set(1);
    this.load();
  }

  refresh(): void { this.page.set(1); this.load(); }
  prevPage(): void { if (this.page() > 1) { this.page.update(p => p - 1); this.load(); } }
  nextPage(): void { if (!this.isLastPage()) { this.page.update(p => p + 1); this.load(); } }
  goToPage(p: number): void { this.page.set(p); this.load(); }

  get totalPages(): number { return Math.ceil((this.result()?.totalCount ?? 0) / this.pageSize); }
  isLastPage(): boolean { return this.page() >= this.totalPages; }

  pageNumbers(): number[] {
    const total = this.totalPages; const cur = this.page();
    if (total <= 7) return Array.from({ length: total }, (_, i) => i + 1);
    const pages: number[] = [1];
    if (cur > 3) pages.push(-1);
    for (let i = Math.max(2, cur - 1); i <= Math.min(total - 1, cur + 1); i++) pages.push(i);
    if (cur < total - 2) pages.push(-1);
    pages.push(total);
    return pages;
  }

  min(a: number, b: number): number { return Math.min(a, b); }

  /** Human-readable label for the Action column badge — same source as dropdown labels */
  actionLabel(raw: string): string {
    return ACTION_LABELS[raw?.toUpperCase()] ?? this.titleCase(raw);
  }

  actionClass(raw: string): string {
    return ACTION_CLASSES[raw?.toUpperCase()] ?? 'act-default';
  }

  /** Full-sentence description of what the user actually did */
  getDescription(log: AuditLogResponseDto): string {
    const user = log.actorUserName ?? 'System';
    const raw  = (log.action ?? '').toUpperCase();

    if (log.description?.trim()) {
      return log.description.replace(/^User\b/, user);
    }

    switch (raw) {
      case 'LOGIN':           return `${user} logged in to the system`;
      case 'LOGOUT':          return `${user} logged out of the system`;
      case 'TICKET_CREATED':  return `${user} submitted a new support ticket`;
      case 'TICKET_UPDATED':  return `${user} updated the details of a ticket`;
      case 'TICKET_CLOSED':   return `${user} closed a support ticket`;
      case 'TICKET_DELETED':  return `${user} deleted a support ticket`;
      case 'COMMENT_ADDED':   return `${user} added a comment to a ticket`;
      case 'USER_REGISTERED': return `A new user account was registered`;
      case 'PROFILE_UPDATED': return `${user} updated their profile information`;
      case 'ASSIGN':          return `${user} manually assigned a ticket to an agent`;
      case 'AUTOASSIGN':      return `${user} triggered auto-assignment for a ticket`;
      case 'UPDATESTATUS':    return `${user} changed the status of a ticket`;
      case 'GETHISTORY':      return `${user} viewed the status history of a ticket`;
      case 'GETALLUSERS':     return `${user} viewed the full list of users`;
      case 'GETAGENTS':       return `${user} viewed the list of available agents`;
      case 'GETME':           return `${user} viewed their own profile`;
      case 'GET':             return `${user} viewed a record`;
      case 'QUERY':           return `${user} searched or filtered records`;
      default:                return `${user} performed ${this.titleCase(raw)}`;
    }
  }

  /**
   * Client-side filter for user, role, status.
   * Action is handled server-side but also applied here as a safety net.
   */
  get filteredItems(): AuditLogResponseDto[] {
    return (this.result()?.items ?? []).filter(log => {
      if (this.filters.action &&
          log.action?.toUpperCase() !== this.filters.action.toUpperCase()) return false;
      if (this.filters.actorUserName &&
          !log.actorUserName?.toLowerCase().includes(this.filters.actorUserName.toLowerCase())) return false;
      if (this.filters.role &&
          log.actorRole?.toLowerCase() !== this.filters.role.toLowerCase()) return false;
      if (this.filters.status === 'success' && !log.success) return false;
      if (this.filters.status === 'failed'  &&  log.success) return false;
      return true;
    });
  }

  statusClass(code: number): string {
    if (code >= 200 && code < 300) return 'code-ok';
    if (code >= 400 && code < 500) return 'code-warn';
    return 'code-err';
  }

  private titleCase(s: string): string {
    return s.replace(/_/g, ' ').toLowerCase().replace(/\b\w/g, c => c.toUpperCase());
  }
}
