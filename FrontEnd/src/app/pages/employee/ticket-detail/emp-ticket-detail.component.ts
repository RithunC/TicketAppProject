import { Component, inject, signal, OnInit } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { DatePipe, LowerCasePipe } from '@angular/common';
import { NavbarComponent } from '../../../shared/components/navbar/navbar.component';
import { ReplacePipe } from '../../../shared/pipes/replace.pipe';
import { AttachmentPreviewComponent } from '../../../shared/components/attachment-preview/attachment-preview.component';
import { TicketService } from '../../../services/ticket.service';
import { CommentService } from '../../../services/comment.service';
import { AttachmentService } from '../../../services/attachment.service';
import { EmployeeTicketService } from '../../../services/employee-ticket.service';
import { AuthService } from '../../../services/auth.service';
import { ToastService } from '../../../services/toast.service';
import {
  TicketResponseDto, CommentResponseDto, AttachmentResponseDto,
  TicketStatusHistoryDto
} from '../../../models';

@Component({
  selector: 'app-emp-ticket-detail',
  standalone: true,
  imports: [RouterLink, FormsModule, NavbarComponent, DatePipe, LowerCasePipe, ReplacePipe, AttachmentPreviewComponent],
  templateUrl: './emp-ticket-detail.component.html',
  styleUrls: ['./emp-ticket-detail.component.css']
})
export class EmpTicketDetailComponent implements OnInit {
  route      = inject(ActivatedRoute);
  ts         = inject(TicketService);
  cs         = inject(CommentService);
  attachSvc  = inject(AttachmentService);
  empTs      = inject(EmployeeTicketService);
  auth       = inject(AuthService);
  toast      = inject(ToastService);

  ticket      = signal<TicketResponseDto | null>(null);
  comments    = signal<CommentResponseDto[]>([]);
  attachments = signal<AttachmentResponseDto[]>([]);
  history     = signal<TicketStatusHistoryDto[]>([]);
  newComment  = '';
  isDraft     = false;
  deletingCommentId = signal<number | null>(null);
  deletingAttachId  = signal<number | null>(null);

  private get draftKey(): string { return `comment_draft_${this.tid}`; }
  previewAttachment = signal<AttachmentResponseDto | null>(null);

  get tid(): number { return +this.route.snapshot.paramMap.get('id')!; }
  get myId(): number { return this.auth.currentUser()?.id ?? 0; }
  get isClosed(): boolean {
    const s = this.ticket()?.statusName?.toLowerCase() ?? '';
    return s === 'closed' || s === 'resolved';
  }

  ngOnInit(): void {
    this.newComment = localStorage.getItem(this.draftKey) ?? '';
    this.loadAll();
  }

  loadAll(): void {
    this.ts.get(this.tid).subscribe({
      next: t => { this.ticket.set(t); if (t) this.empTs.saveTicketId(t.id); },
      error: () => this.toast.error('Failed to load ticket.')
    });
    this.cs.getByTicket(this.tid).subscribe(c => this.comments.set(c));
    this.attachSvc.getByTicket(this.tid).subscribe(a => this.attachments.set(a));
    this.ts.getHistory(this.tid).subscribe({
      next: h => this.history.set(h),
      error: () => this.history.set([])
    });
  }

  addComment(): void {
    const body = this.newComment.trim();
    if (!body) return;
    this.isDraft = false;
    this.cs.add({ ticketId: this.tid, body, isInternal: false }).subscribe({
      next: () => {
        this.toast.success('Comment posted!');
        this.newComment = '';
        localStorage.removeItem(this.draftKey);
        this.cs.getByTicket(this.tid).subscribe(c => this.comments.set(c));
      },
      error: (err) => {
        const msg = err.error?.message ?? err.error ?? '';
        if (typeof msg === 'string' && msg) this.toast.error(msg);
      }
    });
  }

  deleteComment(c: CommentResponseDto): void {
    if (this.deletingCommentId() !== null) return;
    this.deletingCommentId.set(c.id);
    this.cs.delete(c.id).subscribe({
      next: () => {
        this.comments.update(list => list.filter(x => x.id !== c.id));
        this.deletingCommentId.set(null);
      },
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
    input.value = ''; // reset so same file can be re-selected
    if (!file) return;
    if (this.isClosed) {
      this.toast.warning('Uploading attachments is disabled when a ticket is in a closed state.');
      return;
    }
    this.attachSvc.upload(this.tid, file).subscribe({
      next: () => {
        this.toast.success(`"${file.name}" uploaded!`);
        this.attachSvc.getByTicket(this.tid).subscribe(a => this.attachments.set(a));
      }
    });
  }

  deleteAttachment(a: AttachmentResponseDto): void {
    if (this.deletingAttachId() !== null) return;
    this.deletingAttachId.set(a.id);
    this.attachSvc.delete(a.id).subscribe({
      next: () => {
        this.toast.success(`"${a.fileName}" deleted.`);
        this.attachments.update(list => list.filter(x => x.id !== a.id));
        this.deletingAttachId.set(null);
      },
      error: () => this.deletingAttachId.set(null)
    });
  }

  openPreview(a: AttachmentResponseDto): void { this.previewAttachment.set(a); }
  closePreview(): void { this.previewAttachment.set(null); }

  download(a: AttachmentResponseDto): void {
    this.attachSvc.download(a.id).subscribe(blob => {
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url; link.download = a.fileName; link.click();
      URL.revokeObjectURL(url);
    });
  }

  fmtSize(b: number): string {
    if (b < 1024) return `${b} B`;
    if (b < 1048576) return `${(b / 1024).toFixed(1)} KB`;
    return `${(b / 1048576).toFixed(1)} MB`;
  }

  prioBg(p: string): string    { return ({ High: '#dc2626', Urgent: '#7c3aed', Medium: '#d97706', Low: '#059669' } as Record<string,string>)[p] ?? '#475569'; }
  isOverdue(d: string): boolean { return new Date(d) < new Date(); }
}
