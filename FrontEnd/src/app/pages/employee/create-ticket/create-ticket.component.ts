import { Component, inject, signal, OnInit } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { NavbarComponent } from '../../../shared/components/navbar/navbar.component';
import { TicketService } from '../../../services/ticket.service';
import { AttachmentService } from '../../../services/attachment.service';
import { LookupService } from '../../../services/lookup.service';
import { ToastService } from '../../../services/toast.service';
import { EmployeeTicketService } from '../../../services/employee-ticket.service';
import { AuthService } from '../../../services/auth.service';
import { DraftFilesService } from '../../../services/draft-files.service';
import { DepartmentDto, CategoryDto, PriorityDto } from '../../../models';

export interface TicketDraftFile {
  name: string;
  size: number;
  type: string;
}

export interface TicketDraft {
  id: string;
  savedAt: string;
  title: string;
  description: string;
  departmentId?: number;
  categoryId?: number;
  priorityId: number;
  dueAt: string;
  files?: TicketDraftFile[];   // metadata only — actual File objects live in DraftFilesService
}

@Component({
  selector: 'app-create-ticket',
  standalone: true,
  imports: [RouterLink, FormsModule, NavbarComponent],
  templateUrl: './create-ticket.component.html',
  styleUrls: ['./create-ticket.component.css']
})
export class CreateTicketComponent implements OnInit {
  router      = inject(Router);
  ts          = inject(TicketService);
  attachSvc   = inject(AttachmentService);
  ls          = inject(LookupService);
  toast       = inject(ToastService);
  empTs       = inject(EmployeeTicketService);
  auth        = inject(AuthService);
  draftFiles  = inject(DraftFilesService);

  loading        = signal(false);
  uploadingFiles = signal(false);
  depts          = signal<DepartmentDto[]>([]);
  cats           = signal<CategoryDto[]>([]);
  prios          = signal<PriorityDto[]>([]);
  selectedFiles  = signal<File[]>([]);
  minDateTime    = '';

  /** ID of the draft being edited (null = new ticket) */
  activeDraftId: string | null = null;

  form = {
    title:        '',
    description:  '',
    departmentId: undefined as number | undefined,
    categoryId:   undefined as number | undefined,
    priorityId:   0,
    dueAt:        ''
  };

  private get draftsKey(): string {
    const uid = this.auth.currentUser()?.id ?? 0;
    return `ticket_drafts_${uid}`;
  }

  get titleLen(): number { return this.form.title.length; }
  get hasDraftContent(): boolean { return !!this.form.title.trim(); }

  get totalSize(): string {
    const bytes = this.selectedFiles().reduce((s, f) => s + f.size, 0);
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1048576) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / 1048576).toFixed(1)} MB`;
  }

  ngOnInit(): void {
    this.ls.getDepartments().subscribe(d => this.depts.set(d));
    this.ls.getCategories().subscribe(c => this.cats.set(c));
    this.ls.getPriorities().subscribe(p => this.prios.set(p));
    const now = new Date();
    const local = new Date(now.getTime() - now.getTimezoneOffset() * 60000);
    this.minDateTime = local.toISOString().slice(0, 16);

    // Only restore a draft when explicitly navigated here with a draftId (from profile page)
    const nav = this.router.getCurrentNavigation();
    const draftId = (nav?.extras?.state?.['draftId'] ?? history.state?.['draftId']) as string | undefined;
    if (draftId) {
      this.loadDraft(draftId);
    }
  }

  // ── Draft helpers ──────────────────────────────────────────────────

  getDrafts(): TicketDraft[] {
    try { return JSON.parse(localStorage.getItem(this.draftsKey) ?? '[]'); }
    catch { return []; }
  }

  private saveDrafts(drafts: TicketDraft[]): void {
    localStorage.setItem(this.draftsKey, JSON.stringify(drafts));
  }

  private loadDraft(id: string): void {
    const draft = this.getDrafts().find(d => d.id === id);
    if (draft) { this.activeDraftId = draft.id; this.applyDraft(draft); }
  }

  private applyDraft(d: TicketDraft): void {
    this.form.title        = d.title;
    this.form.description  = d.description;
    this.form.departmentId = d.departmentId;
    this.form.categoryId   = d.categoryId;
    this.form.priorityId   = d.priorityId;
    this.form.dueAt        = d.dueAt;
    // Restore actual File objects from the in-memory service
    this.selectedFiles.set(this.draftFiles.get(d.id));
  }

  saveAsDraft(): void {
    if (!this.form.title.trim()) { this.toast.error('Add a title before saving as draft.'); return; }
    const id = this.activeDraftId ?? crypto.randomUUID();
    this.activeDraftId = id;
    this._persistDraft(id);
    this.toast.success('Draft saved!');
  }

  /** Auto-save on every field change — each session gets its own draft */
  onFormChange(): void {
    if (!this.form.title.trim()) return;
    if (!this.activeDraftId) this.activeDraftId = crypto.randomUUID();
    this._persistDraft(this.activeDraftId);
  }

  /** Called when files change so the draft stays in sync */
  onFilesChange(): void {
    if (this.activeDraftId) this._persistDraft(this.activeDraftId);
  }

  private _persistDraft(id: string): void {
    const files = this.selectedFiles();
    // Save actual File objects in memory
    this.draftFiles.save(id, files);
    // Save metadata + form fields to localStorage
    const draft: TicketDraft = {
      id,
      savedAt:      new Date().toISOString(),
      title:        this.form.title.trim(),
      description:  this.form.description,
      departmentId: this.form.departmentId,
      categoryId:   this.form.categoryId,
      priorityId:   this.form.priorityId,
      dueAt:        this.form.dueAt,
      files:        files.map(f => ({ name: f.name, size: f.size, type: f.type }))
    };
    const drafts = this.getDrafts();
    const idx = drafts.findIndex(d => d.id === id);
    if (idx >= 0) drafts[idx] = draft; else drafts.unshift(draft);
    this.saveDrafts(drafts);
  }

  private clearActiveDraft(): void {
    if (!this.activeDraftId) return;
    const drafts = this.getDrafts().filter(d => d.id !== this.activeDraftId);
    this.saveDrafts(drafts);
    this.draftFiles.remove(this.activeDraftId);
    this.activeDraftId = null;
  }

  // ── File handling ──────────────────────────────────────────────────

  onFileSelect(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (!input.files?.length) return;
    const newFiles = Array.from(input.files);
    this.selectedFiles.update(existing => {
      const names = existing.map(f => f.name);
      return [...existing, ...newFiles.filter(f => !names.includes(f.name))];
    });
    input.value = '';
    this.onFilesChange();
  }

  removeFile(index: number): void {
    this.selectedFiles.update(files => files.filter((_, i) => i !== index));
    this.onFilesChange();
  }

  fmtSize(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1048576) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / 1048576).toFixed(1)} MB`;
  }

  // ── Submit ─────────────────────────────────────────────────────────

  onSubmit(): void {
    if (this.loading() || !this.form.priorityId || !this.form.title.trim()) return;
    this.loading.set(true);

    this.ts.create({
      title:        this.form.title.trim(),
      description:  this.form.description.trim() || undefined,
      departmentId: this.form.departmentId,
      categoryId:   this.form.categoryId,
      priorityId:   this.form.priorityId,
      dueAt:        this.form.dueAt ? new Date(this.form.dueAt).toISOString() : undefined
    }).subscribe({
      next: ticket => {
        this.clearActiveDraft();
        this.empTs.saveTicketId(ticket.id);

        const files = this.selectedFiles();
        if (files.length === 0) {
          this.loading.set(false);
          this.toast.success('Ticket submitted successfully!');
          this.router.navigate(['/employee/tickets', ticket.id]);
          return;
        }

        this.uploadingFiles.set(true);
        let completed = 0, failed = 0;
        const uploadNext = (index: number): void => {
          if (index >= files.length) {
            this.loading.set(false);
            this.uploadingFiles.set(false);
            if (failed > 0) this.toast.warning(`Ticket created! ${completed} file(s) uploaded, ${failed} failed.`);
            else            this.toast.success(`Ticket created with ${completed} attachment(s)!`);
            this.router.navigate(['/employee/tickets', ticket.id]);
            return;
          }
          this.attachSvc.upload(ticket.id, files[index]).subscribe({
            next: () => { completed++; uploadNext(index + 1); },
            error: () => { failed++;   uploadNext(index + 1); }
          });
        };
        uploadNext(0);
      },
      error: () => this.loading.set(false)
    });
  }
}
