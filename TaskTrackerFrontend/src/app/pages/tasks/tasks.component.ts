import {
  Component,
  inject,
  OnInit,
  ChangeDetectionStrategy,
  ChangeDetectorRef
} from '@angular/core';
import { DatePipe } from '@angular/common';

import {
  ReactiveFormsModule,
  FormsModule,
  FormBuilder,
  Validators
} from '@angular/forms';

import {
  forkJoin,
  finalize
} from 'rxjs';

import { WorkTaskService } from '../../services/worktask.service';
import { ProjectService } from '../../services/project.service';
import { UserService } from '../../services/user.service';
import { AuthService } from '../../services/auth.service';

import {
  WorkTaskWithIdDto,
  CreateUpdateWorkTaskDto,
  ProjectWithIdDto,
  UserWithIdDto,
  Priority,
  Status
} from '../../models';

@Component({
  selector: 'app-tasks',
  standalone: true,
  imports: [ReactiveFormsModule, FormsModule, DatePipe],
  templateUrl: './tasks.component.html',
  styleUrls: ['./tasks.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TasksComponent implements OnInit {

  private svc = inject(WorkTaskService);
  private projSvc = inject(ProjectService);
  private userSvc = inject(UserService);
  private auth = inject(AuthService);
  private fb = inject(FormBuilder);
  private cdr = inject(ChangeDetectorRef);

  tasks: WorkTaskWithIdDto[] = [];
  projects: ProjectWithIdDto[] = [];
  users: UserWithIdDto[] = [];

  loading = false;
  showModal = false;
  showStatusModal = false;
  submitting = false;

  editingId: string | null = null;
  statusEditingTask: WorkTaskWithIdDto | null = null;
  pendingStatus: Status = Status.InProgress;
  error = '';

  get isAdmin(): boolean { return this.auth.isAdmin(); }
  get canCreateTask(): boolean { return this.isAdmin || this.projects.length > 0; }

  viewingTask: WorkTaskWithIdDto | null = null;
  showDetailsModal = false;
  newComment = '';

  Priority = Priority;
  Status = Status;

  priorities = [
    { label: 'Low', value: Priority.Low },
    { label: 'Medium', value: Priority.Medium },
    { label: 'High', value: Priority.High },
  ];

  statuses = [
    { label: 'In Progress', value: Status.InProgress },
    { label: 'Review',      value: Status.Review },
    { label: 'Canceled',    value: Status.Canceled },
    { label: 'Done',        value: Status.Done },
  ];

  private static readonly PRIORITY_LABELS = ['Low', 'Medium', 'High'];
  private static readonly STATUS_LABELS = ['In Progress', 'Review', 'Canceled', 'Done'];

  private toStatusIndex(value: Status | string | number): number {
    return typeof value === 'number' ? value : Status[value as keyof typeof Status];
  }

  private toPriorityIndex(value: Priority | string | number): number {
    return typeof value === 'number' ? value : Priority[value as keyof typeof Priority];
  }

  statusLabel(status: Status | string | number): string {
    return TasksComponent.STATUS_LABELS[this.toStatusIndex(status)] ?? String(status);
  }

  form = this.fb.group({
    name: ['', Validators.required],
    description: ['', Validators.required],
    priority: [Priority.Low, Validators.required],
    status: [Status.InProgress, Validators.required],
    projectId: [null as string | null, Validators.required],
    assigneeId: [null as string | null],
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    if (this.loading) return;

    this.loading = true;

    forkJoin({
      tasks: this.svc.getAllWithId(),
      projects: this.projSvc.getAllWithId(),
      users: this.userSvc.getAllWithId(),
    })
    .pipe(
      finalize(() => {
        this.loading = false;
        this.cdr.markForCheck();
      })
    )
    .subscribe({
      next: (data) => {
        this.tasks = data.tasks;
        this.projects = data.projects;
        this.users = data.users;
        this.cdr.markForCheck();
      },
      error: (err) => {
        console.error('Tasks load error', err);
        this.cdr.markForCheck();
      }
    });
  }

  priorityLabel(priority: Priority | string | number): string {
    return TasksComponent.PRIORITY_LABELS[this.toPriorityIndex(priority)] ?? String(priority);
  }

  priorityClass(priority: Priority | string | number): string {
    return ['badge-low', 'badge-medium', 'badge-high'][this.toPriorityIndex(priority)] || 'badge-low';
  }

  projectName(id: string): string {
    return this.projects.find(x => x.id === id)?.name || '—';
  }

  assigneeName(id: string | null): string {
    if (!id) return '—';
    return this.users.find(x => x.id === id)?.fullName || '—';
  }

  openCreate(): void {
    this.editingId = null;
    this.form.reset({ priority: Priority.Low, status: Status.InProgress, assigneeId: null });
    this.error = '';
    this.showModal = true;
  }

  openEdit(task: WorkTaskWithIdDto): void {
    this.editingId = task.id;
    this.form.patchValue({
      name: task.name,
      description: task.description,
      priority: this.toPriorityIndex(task.priority),
      status: this.toStatusIndex(task.status),
      projectId: task.projectId,
      assigneeId: task.assigneeId
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

  openStatus(task: WorkTaskWithIdDto): void {
    this.statusEditingTask = task;
    this.pendingStatus = this.toStatusIndex(task.status);
    this.showStatusModal = true;
  }

  closeStatusModal(): void {
    this.showStatusModal = false;
    this.statusEditingTask = null;
  }

  submitStatus(): void {
    if (!this.statusEditingTask || this.submitting) return;
    if (this.pendingStatus === this.toStatusIndex(this.statusEditingTask.status)) {
      this.closeStatusModal();
      return;
    }
    const id = this.statusEditingTask.id;
    this.submitting = true;
    this.svc.updateStatus(id, this.pendingStatus)
      .pipe(finalize(() => {
        this.submitting = false;
        this.cdr.markForCheck();
      }))
      .subscribe({
        next: (updated) => {
          const idx = this.tasks.findIndex(t => t.id === id);
          if (idx >= 0) this.tasks[idx] = { ...this.tasks[idx], status: updated.status };
          this.closeStatusModal();
        },
        error: (err) => console.error(err)
      });
  }

  openDetails(task: WorkTaskWithIdDto): void {
    this.viewingTask = task;
    this.newComment = '';
    this.showDetailsModal = true;
  }

  closeDetailsModal(): void {
    this.showDetailsModal = false;
    this.viewingTask = null;
    this.newComment = '';
  }

  submitComment(): void {
    if (!this.newComment.trim() || !this.viewingTask) return;
    const taskId = this.viewingTask.id;
    const currentViewingId = taskId;
    this.svc.addComment(taskId, this.newComment.trim()).subscribe({
      next: () => {
        this.newComment = '';
        forkJoin({
          tasks: this.svc.getAllWithId(),
          projects: this.projSvc.getAllWithId(),
          users: this.userSvc.getAllWithId(),
        }).subscribe({
          next: (data) => {
            this.tasks = data.tasks;
            this.projects = data.projects;
            this.users = data.users;
            const refreshed = this.tasks.find(t => t.id === currentViewingId);
            if (refreshed) {
              this.viewingTask = refreshed;
            } else {
              this.closeDetailsModal();
            }
            this.cdr.markForCheck();
          },
          error: (err) => console.error(err)
        });
      },
      error: (err) => console.error(err)
    });
  }

  submit(): void {
    if (this.form.invalid || this.submitting) return;

    this.submitting = true;

    const dto = this.form.value as CreateUpdateWorkTaskDto;

    const request$ = this.editingId
      ? this.svc.update(this.editingId, dto)
      : this.svc.create(dto);

    request$
      .pipe(
        finalize(() => {
          this.submitting = false;
          this.cdr.markForCheck();
        })
      )
      .subscribe({
        next: () => {
          this.closeModal();
          this.load();
        },
        error: (err) => {
          console.error(err);
          this.error = err?.error?.message || 'Error saving task';
          this.cdr.markForCheck();
        }
      });
  }

  delete(id: string): void {
    if (!confirm('Delete this task?')) return;

    this.svc.delete(id).subscribe({
      next: () => {
        this.tasks = this.tasks.filter(x => x.id !== id);
        this.cdr.markForCheck();
      },
      error: (err) => {
        console.error(err);
      }
    });
  }
}
