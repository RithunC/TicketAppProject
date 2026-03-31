import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AuditLogsComponent } from './audit-logs.component';
import { RouterTestingModule } from '@angular/router/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
describe('AuditLogsComponent', () => {
  let f: ComponentFixture<AuditLogsComponent>;
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AuditLogsComponent, RouterTestingModule, HttpClientTestingModule]
    }).compileComponents();
    f = TestBed.createComponent(AuditLogsComponent);
    f.detectChanges();
  });
  it('should create', () => expect(f.componentInstance).toBeTruthy());
  it('page starts at 1', () => expect(f.componentInstance.page()).toBe(1));
  it('statusClass returns code-ok for 200', () => expect(f.componentInstance.statusClass(200)).toBe('code-ok'));
  it('statusClass returns code-warn for 400', () => expect(f.componentInstance.statusClass(400)).toBe('code-warn'));
  it('statusClass returns code-err for 500', () => expect(f.componentInstance.statusClass(500)).toBe('code-err'));
  it('actionClass returns act-add for ADDED', () => expect(f.componentInstance.actionClass('ADDED')).toBe('act-add'));
  it('actionClass returns act-del for DELETED', () => expect(f.componentInstance.actionClass('DELETED')).toBe('act-del'));
});
