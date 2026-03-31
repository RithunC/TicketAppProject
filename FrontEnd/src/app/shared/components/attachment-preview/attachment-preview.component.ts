import { Component, Input, Output, EventEmitter, signal, OnChanges, SimpleChanges, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AttachmentService } from '../../../services/attachment.service';
import { AttachmentResponseDto } from '../../../models';

export type PreviewState = 'loading' | 'image' | 'pdf' | 'text' | 'unsupported';

@Component({
  selector: 'app-attachment-preview',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './attachment-preview.component.html',
  styleUrls: ['./attachment-preview.component.css']
})
export class AttachmentPreviewComponent implements OnChanges {
  @Input() attachment: AttachmentResponseDto | null = null;
  @Output() close = new EventEmitter<void>();

  attachSvc = inject(AttachmentService);

  state    = signal<PreviewState>('loading');
  dataUrl  = signal<string | null>(null);
  textBody = signal<string | null>(null);
  errorMsg = signal<string | null>(null);

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['attachment'] && this.attachment) {
      this.loadPreview(this.attachment);
    }
  }

  private loadPreview(a: AttachmentResponseDto): void {
    this.state.set('loading');
    this.dataUrl.set(null);
    this.textBody.set(null);
    this.errorMsg.set(null);

    const ct   = (a.contentType ?? '').toLowerCase();
    const name = (a.fileName ?? '').toLowerCase();

    const isImage = ct.startsWith('image/') ||
      ['.jpg','.jpeg','.png','.gif','.webp','.svg','.bmp'].some(e => name.endsWith(e));
    const isPdf   = ct === 'application/pdf' || name.endsWith('.pdf');
    const isText  = ct.startsWith('text/') ||
      ['.txt','.csv','.json','.xml','.log','.md','.yaml','.yml'].some(e => name.endsWith(e));

    this.attachSvc.download(a.id).subscribe({
      next: blob => {
        const reader = new FileReader();
        if (isImage) {
          reader.onload = () => { this.dataUrl.set(reader.result as string); this.state.set('image'); };
          reader.readAsDataURL(blob);
        } else if (isPdf) {
          reader.onload = () => { this.dataUrl.set(reader.result as string); this.state.set('pdf'); };
          reader.readAsDataURL(blob);
        } else if (isText) {
          reader.onload = () => { this.textBody.set(reader.result as string); this.state.set('text'); };
          reader.readAsText(blob);
        } else {
          this.state.set('unsupported');
        }
      },
      error: () => {
        this.errorMsg.set('Failed to load file for preview.');
        this.state.set('unsupported');
      }
    });
  }

  onBackdropClick(event: MouseEvent): void {
    if ((event.target as HTMLElement).classList.contains('modal-backdrop')) {
      this.close.emit();
    }
  }
}
