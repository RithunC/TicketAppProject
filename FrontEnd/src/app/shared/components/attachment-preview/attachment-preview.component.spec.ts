import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AttachmentPreviewComponent } from './attachment-preview.component';
import { HttpClientTestingModule } from '@angular/common/http/testing';
describe('AttachmentPreviewComponent', () => {
  let f: ComponentFixture<AttachmentPreviewComponent>;
  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [AttachmentPreviewComponent, HttpClientTestingModule] }).compileComponents();
    f = TestBed.createComponent(AttachmentPreviewComponent);
    f.detectChanges();
  });
  it('should create', () => expect(f.componentInstance).toBeTruthy());
  it('state starts loading', () => expect(f.componentInstance.state()).toBe('loading'));
});
