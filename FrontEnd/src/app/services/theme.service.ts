import { Injectable, signal, effect } from '@angular/core';

export type Theme = 'light' | 'dark';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly KEY = 'ticketdesk_theme';

  theme = signal<Theme>(this._load());

  constructor() {
    // Apply theme to <html> whenever signal changes
    effect(() => {
      const t = this.theme();
      document.documentElement.setAttribute('data-theme', t);
      localStorage.setItem(this.KEY, t);
    });
  }

  toggle(): void {
    this.theme.update(t => t === 'light' ? 'dark' : 'light');
  }

  private _load(): Theme {
    const saved = localStorage.getItem(this.KEY) as Theme | null;
    if (saved === 'dark' || saved === 'light') return saved;
    // Respect system preference on first load
    return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
  }
}
