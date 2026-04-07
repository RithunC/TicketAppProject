import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../../../services/auth.service';
import { ThemeService } from '../../../services/theme.service';
import { TicketDraft } from '../../../pages/employee/create-ticket/create-ticket.component';

@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './navbar.component.html',
  styleUrls: ['./navbar.component.css']
})
export class NavbarComponent {
  auth  = inject(AuthService);
  theme = inject(ThemeService);

  initials(): string {
    const n = this.auth.currentUser()?.displayName ?? '';
    return n.split(' ').map(x => x[0]).slice(0,2).join('').toUpperCase() || 'U';
  }

  get drafts(): TicketDraft[] {
    try {
      const uid = this.auth.currentUser()?.id ?? 0;
      return JSON.parse(localStorage.getItem(`ticket_drafts_${uid}`) ?? '[]');
    } catch { return []; }
  }
}
