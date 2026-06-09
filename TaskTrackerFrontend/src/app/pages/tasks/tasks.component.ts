import {
  Component,
  inject,
  OnInit,
  ChangeDetectionStrategy,
  ChangeDetectorRef
} from '@angular/core';

import {
  ReactiveFormsModule,
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
  Priority
} from '../../models';

@Component({
  selector: 'app-tasks',
  standalone: true,
  imports: [ReactiveFormsModule],
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
  private cdr = inject(ChangeDetectorRef); // 👈

  tasks: WorkTaskWithIdDto[] = [];
  projects: ProjectWithIdDto[] = [];
  users: UserWithIdDto[] = [];

  loading = false;
  showModal = false;
  submitting = false;

  editingId: string | null = null;
  error = '';
  get currentUserRole() { return this.auth.currentUser?.role; }

  Priority = Priority;

  priorities = [
    { label: 'Low', value: Priority.Low },
    { label: 'Medium', value: Priority.Medium },
    { label: 'High', value: Priority.High },
  ];

  form = this.fb.group({
    name: ['', Validators.required],
    description: ['', Validators.required],
    priority: [Priority.Low, Validators.required],
    projectId: ['', Validators.required],
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
        this.cdr.markForCheck(); // 👈
      })
    )
    .subscribe({
      next: (data) => {
        this.tasks = data.tasks;
        this.projects = data.projects;
        this.users = data.users;
        this.cdr.markForCheck(); // 👈
      },
      error: (err) => {
        console.error('Tasks load error', err);
        this.cdr.markForCheck(); // 👈
      }
    });
  }

  priorityLabel(priority: Priority): string {
    return Priority[priority];
  }

  priorityClass(priority: Priority): string {
    return ['badge-low', 'badge-medium', 'badge-high'][priority] || 'badge-low';
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
    this.form.reset({ priority: Priority.Low, assigneeId: null });
    this.error = '';
    this.showModal = true;
  }

  openEdit(task: WorkTaskWithIdDto): void {
    this.editingId = task.id;
    this.form.patchValue({
      name: task.name,
      description: task.description,
      priority: task.priority,
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
          this.error = err?.error?.message || 'Error saving task';
          this.cdr.markForCheck(); // 👈
        }
      });
  }

  delete(id: string): void {
    if (!confirm('Delete this task?')) return;

    this.svc.delete(id).subscribe({
      next: () => {
        this.tasks = this.tasks.filter(x => x.id !== id);
        this.cdr.markForCheck(); // 👈
      },
      error: (err) => {
        console.error(err);
      }
    });
  }
}