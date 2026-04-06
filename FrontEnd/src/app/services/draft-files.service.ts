import { Injectable } from '@angular/core';

/**
 * Holds draft ticket file attachments in memory (singleton).
 * Files cannot be serialized to localStorage, so we keep them here
 * across navigation. Keyed by draftId.
 */
@Injectable({ providedIn: 'root' })
export class DraftFilesService {
  private store = new Map<string, File[]>();

  save(draftId: string, files: File[]): void {
    this.store.set(draftId, [...files]);
  }

  get(draftId: string): File[] {
    return this.store.get(draftId) ?? [];
  }

  remove(draftId: string): void {
    this.store.delete(draftId);
  }
}
