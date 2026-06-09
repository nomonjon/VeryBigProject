import { Component, inject, OnInit, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ProjectService } from '../../services/project.service';
import { ProjectWithIdDto, CreateUpdateProjectDto } from '../../models';

@Component({
  selector: 'app-projects',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './projects.component.html',
  styleUrls: ['./projects.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush  // keep OnPush
})
export class ProjectsComponent implements OnInit {

  private projectService = inject(ProjectService);
  private fb = inject(FormBuilder);
  private cdr = inject(ChangeDetectorRef); // 👈 inject this

  projects: ProjectWithIdDto[] = [];

  loading = false;
  submitting = false;
  showModal = false;

  editingId: string | null = null;
  error = '';

  form = this.fb.group({
    name: ['', Validators.required],
    description: ['', Validators.required],
  });

  ngOnInit(): void {
    this.getProjects();
  }

  getProjects(): void {
    this.loading = true;
    this.projectService.getAllWithId().subscribe({
      next: (projects) => {
        this.projects = projects;
        this.loading = false;
        this.cdr.markForCheck(); // 👈
      },
      error: (error) => {
        console.error(error);
        this.loading = false;
        this.cdr.markForCheck(); // 👈
      }
    });
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
          this.cdr.markForCheck(); // 👈
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
        this.cdr.markForCheck(); // 👈
      }
    });
  }

  delete(id: string): void {
    if (!confirm('Delete this project?')) return;

    this.projectService.delete(id).subscribe({
      next: () => {
        this.projects = this.projects.filter(x => x.id !== id);
        this.cdr.markForCheck(); // 👈
      },
      error: (error) => {
        console.error(error);
      }
    });
  }
}