import { Component, inject, signal, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe, LowerCasePipe, SlicePipe } from '@angular/common';
import { Router } from '@angular/router';
import { NavbarComponent } from '../../shared/components/navbar/navbar.component';
import { UserService } from '../../services/user.service';
import { AuthService } from '../../services/auth.service';
import { ToastService } from '../../services/toast.service';
import { UserLiteDto } from '../../models';
import { TicketDraft } from '../employee/create-ticket/create-ticket.component';

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [FormsModule, NavbarComponent, LowerCasePipe, DatePipe, SlicePipe],
  templateUrl: './profile.component.html',
  styleUrls: ['./profile.component.css']
})
export class ProfileComponent implements OnInit {
  userSvc = inject(UserService);
  auth    = inject(AuthService);
  toast   = inject(ToastService);
  router  = inject(Router);

  private readonly PHONE_KEY = 'profile_phone';

  profile  = signal<UserLiteDto | null>(null);
  editing  = signal(false);
  saving   = signal(false);
  drafts   = signal<TicketDraft[]>([]);

  editForm = { displayName: '', phone: '' };

  get isEmployee(): boolean { return this.auth.role() === 'Employee'; }

  private get draftsKey(): string {
    const uid = this.auth.currentUser()?.id ?? 0;
    return `ticket_drafts_${uid}`;
  }

  initials(): string {
    const n = this.profile()?.displayName ?? '';
    return n.split(' ').map(x => x[0]).slice(0,2).join('').toUpperCase() || 'U';
  }

  ngOnInit(): void { this.loadProfile(); this.loadDrafts(); }

  loadDrafts(): void {
    try { this.drafts.set(JSON.parse(localStorage.getItem(this.draftsKey) ?? '[]')); }
    catch { this.drafts.set([]); }
  }

  continueDraft(d: TicketDraft): void {
    this.router.navigate(['/employee/create-ticket'], { state: { draftId: d.id } });
  }

  discardDraft(d: TicketDraft): void {
    const updated = this.drafts().filter(x => x.id !== d.id);
    localStorage.setItem(this.draftsKey, JSON.stringify(updated));
    this.drafts.set(updated);
    this.toast.success('Draft discarded.');
  }

  loadProfile(): void {
    this.userSvc.getMe().subscribe({
      next: u => {
        // Email: from backend response → JWT claim → registration storage → fallback
        const emailSources = [
          u.email,
          this.auth.currentUser()?.email,
          localStorage.getItem('registered_email') ?? undefined
        ];
        const email = emailSources.find(e => !!e);

        // Phone: from backend response → localStorage
        const phone = u.phone ?? localStorage.getItem(this.PHONE_KEY) ?? undefined;

        const merged: UserLiteDto = { ...u, email, phone };
        this.profile.set(merged);
        this.auth.currentUser.update(cur =>
          cur ? { ...cur, displayName: u.displayName, email: merged.email } : cur
        );
      },
      error: () => this.toast.error('Failed to load profile.')
    });
  }

  startEdit(): void {
    const p = this.profile();
    if (!p) return;
    this.editForm.displayName = p.displayName;
    this.editForm.phone       = p.phone ?? '';
    this.editing.set(true);
  }

  cancelEdit(): void { this.editing.set(false); }

  saveEdit(): void {
    if (this.saving()) return;
    if (!this.editForm.displayName.trim()) {
      this.toast.error('Display name cannot be empty.');
      return;
    }
    this.saving.set(true);
    const phoneVal = this.editForm.phone.trim();

    this.userSvc.updateMe({
      displayName: this.editForm.displayName.trim(),
      phone: phoneVal || undefined
    }).subscribe({
      next: updated => {
        if (phoneVal) localStorage.setItem(this.PHONE_KEY, phoneVal);
        else          localStorage.removeItem(this.PHONE_KEY);

        const merged: UserLiteDto = {
          ...updated,
          phone: phoneVal || undefined,
          email: this.profile()?.email // preserve email
        };
        this.profile.set(merged);
        this.auth.currentUser.update(cur =>
          cur ? { ...cur, displayName: updated.displayName } : cur
        );
        this.editing.set(false);
        this.saving.set(false);
        this.toast.success('Profile updated successfully!');
      },
      error: () => { this.saving.set(false); }
    });
  }
}
