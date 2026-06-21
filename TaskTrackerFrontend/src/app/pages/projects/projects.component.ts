import { Component, inject, OnInit, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { ProjectService } from '../../services/project.service';
import { UserService } from '../../services/user.service';
import { AuthService } from '../../services/auth.service';
import { ProjectWithIdDto, CreateUpdateProjectDto, UserWithIdDto, Priority, Status } from '../../models';

@Component({
  selector: 'app-projects',
  standalone: true,
  imports: [ReactiveFormsModule, FormsModule, CommonModule],
  templateUrl: './projects.component.html',
  styleUrls: ['./projects.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProjectsComponent implements OnInit {

  private projectService = inject(ProjectService);
  private userService = inject(UserService);
  private auth = inject(AuthService);
  private fb = inject(FormBuilder);
  private cdr = inject(ChangeDetectorRef);

  projects: ProjectWithIdDto[] = [];
  users: UserWithIdDto[] = [];
  userMap = new Map<string, string>();

  loading = false;
  submitting = false;
  showModal = false;
  showDetailsModal = false;
  showMembersModal = false;
  viewingProject: ProjectWithIdDto | null = null;
  managingProject: ProjectWithIdDto | null = null;
  memberToAddId: string | null = null;

  editingId: string | null = null;
  error = '';

  get isAdmin(): boolean {
    return this.auth.isAdmin();
  }

  form = this.fb.group({
    name: ['', Validators.required],
    description: ['', Validators.required],
  });

  ngOnInit(): void {
    this.getProjects();
  }

  getProjects(): void {
    this.loading = true;
    forkJoin({
      projects: this.projectService.getAllWithId(),
      users: this.userService.getAllWithId()
    }).subscribe({
      next: ({ projects, users }) => {
        this.users = users;
        this.userMap = new Map(users.map(u => [u.id, u.fullName]));
        this.projects = projects;
        this.loading = false;
        this.cdr.markForCheck();
      },
      error: (error) => {
        console.error(error);
        this.loading = false;
        this.cdr.markForCheck();
      }
    });
  }

  getMemberNames(userIds: string[]): string {
    if (!userIds?.length) return '—';
    return userIds.map(id => this.userMap.get(id) ?? id).join(', ');
  }

  openDetails(project: ProjectWithIdDto): void {
    this.viewingProject = project;
    this.showDetailsModal = true;
    this.cdr.markForCheck();
  }

  closeDetails(): void {
    this.showDetailsModal = false;
    this.viewingProject = null;
  }

  private static readonly PRIORITY_LABELS = ['Low', 'Medium', 'High'];
  private static readonly STATUS_LABELS = ['In Progress', 'Review', 'Canceled', 'Done'];

  priorityLabel(p: Priority | string | number): string {
    const idx = typeof p === 'number' ? p : Priority[p as keyof typeof Priority];
    return ProjectsComponent.PRIORITY_LABELS[idx] ?? String(p);
  }

  statusLabel(s: Status | string | number): string {
    const idx = typeof s === 'number' ? s : Status[s as keyof typeof Status];
    return ProjectsComponent.STATUS_LABELS[idx] ?? String(s);
  }

  openCreate(): void {
    this.editingId = null;
    this.form.setValue({ name: '', description: '' });
    this.error = '';
    this.showModal = true;
  }

  openEdit(project: ProjectWithIdDto): void {
    this.editingId = project.id;
    this.form.setValue({ name: project.name, description: project.description });
    this.error = '';
    this.showModal = true;
  }

  closeModal(): void {
    this.showModal = false;
    this.form.reset();
    this.editingId = null;
    this.error = '';
  }

  openMembers(project: ProjectWithIdDto): void {
    this.managingProject = project;
    this.memberToAddId = null;
    this.showMembersModal = true;
    this.cdr.markForCheck();
  }

  closeMembers(): void {
    this.showMembersModal = false;
    this.managingProject = null;
    this.memberToAddId = null;
  }

  nonMembers(): UserWithIdDto[] {
    if (!this.managingProject) return [];
    const memberIds = new Set(this.managingProject.userIds ?? []);
    return this.users.filter(u => !memberIds.has(u.id));
  }

  addMember(): void {
    if (!this.managingProject || !this.memberToAddId) return;
    const projectId = this.managingProject.id;
    const userId = this.memberToAddId;
    this.projectService.addUser(projectId, userId).subscribe({
      next: () => {
        const proj = this.projects.find(p => p.id === projectId);
        if (proj) proj.userIds = [...(proj.userIds ?? []), userId];
        if (this.managingProject?.id === projectId) {
          this.managingProject = proj ?? this.managingProject;
        }
        this.memberToAddId = null;
        this.cdr.markForCheck();
      },
      error: err => console.error(err)
    });
  }

  removeMember(userId: string): void {
    if (!this.managingProject) return;
    const projectId = this.managingProject.id;
    this.projectService.removeUser(projectId, userId).subscribe({
      next: () => {
        const proj = this.projects.find(p => p.id === projectId);
        if (proj) proj.userIds = (proj.userIds ?? []).filter(id => id !== userId);
        if (this.managingProject?.id === projectId) {
          this.managingProject = proj ?? this.managingProject;
        }
        this.cdr.markForCheck();
      },
      error: err => console.error(err)
    });
  }

  submit(): void {
    if (this.form.invalid || this.submitting) return;

    this.submitting = true;

    const dto: CreateUpdateProjectDto = {
      name: this.form.value.name || '',
      description: this.form.value.description || ''
    };

    if (this.editingId) {
      this.projectService.update(this.editingId, dto).subscribe({
        next: () => {
          this.submitting = false;
          this.closeModal();
          this.getProjects();
        },
        error: (error) => {
          console.error(error);
          this.error = 'Error updating project';
          this.submitting = false;
          this.cdr.markForCheck();
        }
      });
      return;
    }

    this.projectService.create(dto).subscribe({
      next: () => {
        this.submitting = false;
        this.closeModal();
        this.getProjects();
      },
      error: (error) => {
        console.error(error);
        this.error = 'Error creating project';
        this.submitting = false;
        this.cdr.markForCheck();
      }
    });
  }

  delete(id: string): void {
    if (!confirm('Delete this project?')) return;

    this.projectService.delete(id).subscribe({
      next: () => {
        this.projects = this.projects.filter(x => x.id !== id);
        this.cdr.markForCheck();
      },
      error: (error) => {
        console.error(error);
      }
    });
  }
}
