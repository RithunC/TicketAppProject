import { Component, inject, signal, OnInit } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { DatePipe, LowerCasePipe } from '@angular/common';
import { NavbarComponent } from '../../../shared/components/navbar/navbar.component';
import { ReplacePipe } from '../../../shared/pipes/replace.pipe';
import { TicketService } from '../../../services/ticket.service';
import { CommentService } from '../../../services/comment.service';
import { AttachmentService } from '../../../services/attachment.service';
import { AttachmentPreviewComponent } from '../../../shared/components/attachment-preview/attachment-preview.component';
import { LookupService } from '../../../services/lookup.service';
import { UserService } from '../../../services/user.service';
import { ToastService } from '../../../services/toast.service';
import { AuthService } from '../../../services/auth.service';
import {
  TicketResponseDto, CommentResponseDto, AttachmentResponseDto,
  TicketStatusHistoryDto, StatusDto, UserLiteDto
} from '../../../models';

@Component({
  selector: 'app-admin-ticket-detail', standalone: true,
  imports: [RouterLink, FormsModule, NavbarComponent, DatePipe, LowerCasePipe, ReplacePipe, AttachmentPreviewComponent],
  templateUrl: './admin-ticket-detail.component.html',
  styleUrls: ['./admin-ticket-detail.component.css']
})
export class AdminTicketDetailComponent implements OnInit {
  route      = inject(ActivatedRoute);
  ts         = inject(TicketService);
  cs         = inject(CommentService);
  attachSvc  = inject(AttachmentService);
  ls         = inject(LookupService);
  us         = inject(UserService);
  toast      = inject(ToastService);
  auth       = inject(AuthService);

  get myId(): number { return this.auth.currentUser()?.id ?? 0; }

  ticket      = signal<TicketResponseDto | null>(null);
  comments    = signal<CommentResponseDto[]>([]);
  attachments = signal<AttachmentResponseDto[]>([]);
  history     = signal<TicketStatusHistoryDto[]>([]);
  statuses    = signal<StatusDto[]>([]);
  agents      = signal<UserLiteDto[]>([]);
  deletingAttachId = signal<number | null>(null);
  previewAttachment = signal<AttachmentResponseDto | null>(null);

  openPreview(a: AttachmentResponseDto): void { this.previewAttachment.set(a); }
  closePreview(): void { this.previewAttachment.set(null); }

  newStatusId = 0; statusNote = ''; assignUserId = 0;
  newComment = ''; isInternal = false;
  isDraft = false;
  deletingCommentId = signal<number | null>(null);
  showFeedbackModal = signal(false);
  pendingStatusId   = 0;

  get tid(): number { return +this.route.snapshot.paramMap.get('id')!; }
  private get draftKey(): string { return `comment_draft_${this.tid}`; }

  private isClosingStatus(id: number): boolean {
    const s = this.statuses().find(x => x.id === id);
    return !!s?.isClosedState;
  }

  get isClosed(): boolean { return this.ticket()?.isClosedState ?? false; }
  get feedbackStatus(): string { return this.ticket()?.feedbackStatus ?? 'None'; }
  get feedbackPending(): boolean { return this.feedbackStatus === 'Pending'; }
  get feedbackDeclined(): boolean { return this.feedbackStatus === 'Declined'; }

  // Exclude current status and current assignee from dropdowns
  get availableStatuses(): StatusDto[] {
    const currentId = this.ticket()?.statusId;
    return this.statuses().filter(s => s.id !== currentId);
  }
  get availableAgents(): UserLiteDto[] {
    const currentAssigneeId = this.ticket()?.currentAssigneeUserId;
    return this.agents().filter(a => a.id !== currentAssigneeId);
  }

  ngOnInit(): void {
    this.ls.getStatuses().subscribe(s => this.statuses.set(s));
    this.us.getAgents().subscribe(a => this.agents.set(a));
    this.newComment = localStorage.getItem(this.draftKey) ?? '';
    this.loadAll();
  }

  loadAll(): void {
    this.ts.get(this.tid).subscribe(t => this.ticket.set(t));
    this.cs.getByTicket(this.tid).subscribe(c => this.comments.set(c));
    this.ts.getHistory(this.tid).subscribe(h => this.history.set(h));
    this.attachSvc.getByTicket(this.tid).subscribe(a => this.attachments.set(a));
  }

  updateStatus(): void {
    if (!this.newStatusId) return;
    if (this.isClosingStatus(this.newStatusId)) {
      this.ts.requestFeedback(this.tid, this.newStatusId).subscribe({
        next: t => {
          this.ticket.set(t);
          this.newStatusId = 0;
          this.toast.success('Feedback request sent to employee. Status will update once they confirm.');
        }
      });
      return;
    }
    this.doUpdateStatus();
  }

  private doUpdateStatus(): void {
    this.ts.updateStatus(this.tid, { newStatusId: this.newStatusId, note: this.statusNote || undefined })
      .subscribe({
        next: () => { this.toast.success('Status updated!'); this.newStatusId = 0; this.statusNote = ''; this.loadAll(); },
        error: (err) => {
          const code = err.error?.error;
          if (code === 'FEEDBACK_REQUIRED') this.toast.warning('Employee feedback is required before closing this ticket.');
          else if (code === 'FEEDBACK_PENDING') this.toast.warning('Waiting for employee response. Cannot update status yet.');
          else if (code === 'FEEDBACK_DECLINED') this.toast.error('Employee declined resolution. Cannot close this ticket.');
          else this.toast.error('Failed to update status.');
        }
      });
  }

  assignAgent(): void {
    if (!this.assignUserId) return;
    this.ts.assign(this.tid, { assignedToUserId: this.assignUserId })
      .subscribe({ next: () => { this.toast.success('Agent assigned!'); this.assignUserId = 0; this.loadAll(); } });
  }

  addComment(): void {
    if (!this.newComment.trim()) return;
    this.isDraft = false;
    this.cs.add({ ticketId: this.tid, body: this.newComment.trim(), isInternal: this.isInternal }).subscribe({
      next: () => { this.toast.success('Comment posted!'); this.newComment = ''; this.isInternal = false; localStorage.removeItem(this.draftKey); this.cs.getByTicket(this.tid).subscribe(c => this.comments.set(c)); }
    });
  }

  deleteComment(c: CommentResponseDto): void {
    if (this.deletingCommentId() !== null) return;
    this.deletingCommentId.set(c.id);
    this.cs.delete(c.id).subscribe({
      next: () => { this.comments.update(list => list.filter(x => x.id !== c.id)); this.deletingCommentId.set(null); },
      error: () => this.deletingCommentId.set(null)
    });
  }

  clearDraft(): void { this.newComment = ''; this.isDraft = false; localStorage.removeItem(this.draftKey); }

  onDraftChange(): void { localStorage.setItem(this.draftKey, this.newComment); }

  onCommentKeydown(e: KeyboardEvent): void {
    if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); this.addComment(); }
  }

  onFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file  = input.files?.[0];
    input.value = '';
    if (!file) return;
    this.attachSvc.upload(this.tid, file).subscribe({
      next: () => { this.toast.success(`"${file.name}" uploaded!`); this.attachSvc.getByTicket(this.tid).subscribe(a => this.attachments.set(a)); }
    });
  }

  deleteAttachment(a: AttachmentResponseDto): void {
    if (this.deletingAttachId() !== null) return;
    this.deletingAttachId.set(a.id);
    this.attachSvc.delete(a.id).subscribe({
      next: () => { this.toast.success(`"${a.fileName}" deleted.`); this.attachments.update(list => list.filter(x => x.id !== a.id)); this.deletingAttachId.set(null); },
      error: () => this.deletingAttachId.set(null)
    });
  }

  download(a: AttachmentResponseDto): void {
    this.attachSvc.download(a.id).subscribe(blob => {
      const url = URL.createObjectURL(blob); const link = document.createElement('a');
      link.href = url; link.download = a.fileName; link.click(); URL.revokeObjectURL(url);
    });
  }

  fmtSize(b: number): string {
    if (b < 1024) return `${b} B`;
    if (b < 1048576) return `${(b / 1024).toFixed(1)} KB`;
    return `${(b / 1048576).toFixed(1)} MB`;
  }

  prioBg(p: string): string    { return ({ High: '#dc2626', Urgent: '#7c3aed', Medium: '#d97706', Low: '#059669' } as Record<string,string>)[p] ?? '#475569'; }
  isOverdue(d: string): boolean { return !this.ticket()?.isClosedState && new Date(d) < new Date(); }
}
