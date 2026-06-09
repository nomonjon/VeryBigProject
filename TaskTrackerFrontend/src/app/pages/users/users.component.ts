import { Component, inject, OnInit, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { finalize } from 'rxjs';

import { UserService } from '../../services/user.service';
import { UserWithIdDto, CreateUpdateUserDto } from '../../models';

@Component({
  selector: 'app-users',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './users.component.html',
  styleUrls: ['./users.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class UsersComponent implements OnInit {

  private svc = inject(UserService);
  private fb = inject(FormBuilder);
  private cdr = inject(ChangeDetectorRef); // 👈

  users: UserWithIdDto[] = [];

  loading = false;
  showModal = false;
  showRoleModal = false;
  submitting = false;

  editingId: string | null = null;
  editingRoleForId: string | null = null;
  error = '';

  form = this.fb.group({
    fullName: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    position: ['', Validators.required],
  });

  roleForm = this.fb.group({
    role: ['User', Validators.required],
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    if (this.loading) return;

    this.loading = true;

    this.svc.getAllWithId()
      .pipe(
        finalize(() => {
          this.loading = false;
          this.cdr.markForCheck(); // 👈
        })
      )
      .subscribe({
        next: (data) => {
          this.users = data;
          this.cdr.markForCheck(); // 👈
        },
        error: (err) => {
          console.error('Users load error', err);
          this.cdr.markForCheck(); // 👈
        }
      });
  }

  openCreate(): void {
    this.editingId = null;
    this.form.reset({ fullName: '', email: '', position: '' });
    this.error = '';
    this.showModal = true;
  }

  openEdit(user: UserWithIdDto): void {
    this.editingId = user.id;
    this.form.patchValue({
      fullName: user.fullName,
      email: user.email,
      position: user.position
    });
    this.error = '';
    this.showModal = true;
  }

  closeModal(): void {
    this.showModal = false;
    this.form.reset();
    this.editingId = null;
    this.error = '';
  }

  openRoleModal(user: UserWithIdDto): void {
    this.editingRoleForId = user.id;
    this.roleForm.reset({ role: user.role || 'User' });
    this.showRoleModal = true;
  }

  closeRoleModal(): void {
    this.showRoleModal = false;
    this.roleForm.reset();
    this.editingRoleForId = null;
  }

  submit(): void {
    if (this.form.invalid || this.submitting) return;

    this.submitting = true;

    const dto = this.form.value as CreateUpdateUserDto;

    const request$ = this.editingId
      ? this.svc.update(this.editingId, dto)
      : this.svc.create(dto);

    request$
      .pipe(
        finalize(() => {
          this.submitting = false;
          this.cdr.markForCheck(); // 👈
        })
      )
      .subscribe({
        next: () => {
          this.closeModal();
          this.load();
        },
        error: (err) => {
          console.error(err);
          this.error = err?.error?.message || 'Error saving user';
          this.cdr.markForCheck(); // 👈
        }
      });
  }

  submitRole(): void {
    if (this.roleForm.invalid || this.submitting || !this.editingRoleForId) return;

    this.submitting = true;

    this.svc.updateRole(this.editingRoleForId, { role: this.roleForm.value.role! })
      .pipe(
        finalize(() => {
          this.submitting = false;
          this.cdr.markForCheck(); // 👈
        })
      )
      .subscribe({
        next: () => {
          this.closeRoleModal();
          this.load();
        },
        error: (err) => {
          console.error(err);
        }
      });
  }

  delete(id: string): void {
    if (!confirm('Delete this user?')) return;

    this.svc.delete(id).subscribe({
      next: () => {
        this.users = this.users.filter(x => x.id !== id);
        this.cdr.markForCheck(); // 👈
      },
      error: (err) => {
        console.error(err);
      }
    });
  }
}