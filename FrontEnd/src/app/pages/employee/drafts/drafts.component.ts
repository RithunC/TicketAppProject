import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { DatePipe, SlicePipe } from '@angular/common';
import { NavbarComponent } from '../../../shared/components/navbar/navbar.component';
import { AuthService } from '../../../services/auth.service';
import { ToastService } from '../../../services/toast.service';
import { TicketDraft } from '../create-ticket/create-ticket.component';

@Component({
  selector: 'app-drafts',
  standalone: true,
  imports: [RouterLink, NavbarComponent, DatePipe, SlicePipe],
  templateUrl: './drafts.component.html',
  styleUrls: ['./drafts.component.css']
})
export class DraftsComponent {
  auth   = inject(AuthService);
  router = inject(Router);
  toast  = inject(ToastService);

  drafts = signal<TicketDraft[]>(this.load());

  private get key(): string {
    return `ticket_drafts_${this.auth.currentUser()?.id ?? 0}`;
  }

  private load(): TicketDraft[] {
    try { return JSON.parse(localStorage.getItem(`ticket_drafts_${this.auth.currentUser()?.id ?? 0}`) ?? '[]'); }
    catch { return []; }
  }

  private save(drafts: TicketDraft[]): void {
    localStorage.setItem(this.key, JSON.stringify(drafts));
    this.drafts.set(drafts);
  }

  continue(d: TicketDraft): void {
    this.router.navigate(['/employee/create-ticket'], { state: { draftId: d.id } });
  }

  discard(d: TicketDraft): void {
    this.save(this.drafts().filter(x => x.id !== d.id));
    this.toast.success('Draft discarded.');
  }

  fmtSize(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1048576) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / 1048576).toFixed(1)} MB`;
  }
}
