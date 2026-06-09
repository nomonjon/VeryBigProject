import { ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, OnInit } from '@angular/core';
import { AsyncPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { AuthService } from '../../services/auth.service';
import { ProjectService } from '../../services/project.service';
import { WorkTaskService } from '../../services/worktask.service';
import { UserService } from '../../services/user.service';

@Component({
  selector: 'app-dashboard',
  imports: [AsyncPipe, RouterLink],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush 
})
export class DashboardComponent implements OnInit {
  auth = inject(AuthService);
  private projectSvc = inject(ProjectService);
  private taskSvc = inject(WorkTaskService);
  private userSvc = inject(UserService);
  private cdr = inject(ChangeDetectorRef);

  stats = { projects: 0, tasks: 0, users: 0 };
  loading = true;
  currentUser$ = this.auth.currentUser$;

  ngOnInit() {
    forkJoin({
      projects: this.projectSvc.getAllWithId(),
      tasks:    this.taskSvc.getAllWithId(),
      users:    this.userSvc.getAllWithId(),
    }).subscribe({
      next: (data) => {
        this.stats.projects = data.projects.length;
        this.stats.tasks    = data.tasks.length;
        this.stats.users    = data.users.length;
        this.loading = false;
        this.cdr.markForCheck();
      },
      error: () => { this.loading = false; this.cdr.markForCheck();},
    });
  }
}
