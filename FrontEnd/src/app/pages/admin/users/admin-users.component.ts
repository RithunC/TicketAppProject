import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { LowerCasePipe } from '@angular/common';
import { NavbarComponent } from '../../../shared/components/navbar/navbar.component';
import { UserService } from '../../../services/user.service';
import { UserLiteDto } from '../../../models';

type RoleFilter = 'all' | 'agent' | 'employee' | 'admin';

@Component({
  selector: 'app-admin-users',
  standalone: true,
  imports: [FormsModule, NavbarComponent, LowerCasePipe],
  templateUrl: './admin-users.component.html',
  styleUrls: ['./admin-users.component.css']
})
export class AdminUsersComponent implements OnInit {
  userSvc    = inject(UserService);
  allUsers   = signal<UserLiteDto[]>([]);
  search     = signal('');
  roleFilter = signal<RoleFilter>('all');
  page       = signal(1);
  pageSize   = 15;
  loading    = signal(true);

  filtered = computed(() => {
    const q    = this.search().toLowerCase().trim();
    const role = this.roleFilter();
    return this.allUsers().filter(u => {
      const matchesRole   = role === 'all' || u.roleName.toLowerCase() === role;
      const matchesSearch = !q
        || u.displayName.toLowerCase().includes(q)
        || u.userName.toLowerCase().includes(q)
        || u.roleName.toLowerCase().includes(q)
        || (u.departmentName ?? '').toLowerCase().includes(q);
      return matchesRole && matchesSearch;
    });
  });

  paginated = computed(() => {
    const start = (this.page() - 1) * this.pageSize;
    return this.filtered().slice(start, start + this.pageSize);
  });

  countByRole(role: RoleFilter): number {
    if (role === 'all') return this.allUsers().length;
    return this.allUsers().filter(u => u.roleName.toLowerCase() === role).length;
  }

  get totalPages(): number { return Math.ceil(this.filtered().length / this.pageSize); }
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

  ngOnInit(): void {
    this.loading.set(true);
    // Single API call — GET /users returns all users (Admin only)
    this.userSvc.getAllUsers().subscribe({
      next: users => { this.allUsers.set(users); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }

  setRole(role: RoleFilter): void { this.roleFilter.set(role); this.page.set(1); }
  goToPage(p: number): void { this.page.set(p); }
  prevPage(): void { if (this.page() > 1) this.page.update(p => p - 1); }
  nextPage(): void { if (!this.isLastPage()) this.page.update(p => p + 1); }
  onSearch(): void { this.page.set(1); }
  min(a: number, b: number): number { return Math.min(a, b); }
}
