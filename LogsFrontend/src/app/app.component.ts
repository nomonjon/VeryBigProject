import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LogsService } from './logs.service';
import { LogEntry } from './log.model';
import { Subscription, timer } from 'rxjs';
import { switchMap } from 'rxjs/operators';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.css']
})
export class AppComponent implements OnInit, OnDestroy {
  logs: LogEntry[] = [];
  totalCount = 0;
  totalPages = 0;
  
  page = 1;
  pageSize = 50;
  
  filters = {
    level: '',
    serviceName: '',
    search: '',
    startDate: '',
    endDate: ''
  };

  isPolling = true;
  pollingInterval = 5000;
  private pollingSub?: Subscription;

  constructor(private logsService: LogsService) {}

  ngOnInit() {
    this.startPolling();
  }

  ngOnDestroy() {
    this.stopPolling();
  }

  fetchLogs() {
    this.logsService.getLogs(this.page, this.pageSize, this.filters).subscribe({
      next: (res) => {
        this.logs = res.items;
        this.totalCount = res.totalCount;
        this.totalPages = res.totalPages;
      },
      error: (err) => console.error('Failed to fetch logs', err)
    });
  }

  startPolling() {
    if (this.pollingSub) {
      this.pollingSub.unsubscribe();
    }
    this.isPolling = true;
    this.pollingSub = timer(0, this.pollingInterval)
      .pipe(switchMap(() => this.logsService.getLogs(this.page, this.pageSize, this.filters)))
      .subscribe({
        next: (res) => {
          this.logs = res.items;
          this.totalCount = res.totalCount;
          this.totalPages = res.totalPages;
        },
        error: (err) => console.error('Failed to fetch logs during polling', err)
      });
  }

  stopPolling() {
    this.isPolling = false;
    if (this.pollingSub) {
      this.pollingSub.unsubscribe();
    }
  }

  togglePolling() {
    if (this.isPolling) {
      this.stopPolling();
    } else {
      this.startPolling();
    }
  }

  applyFilters() {
    this.page = 1;
    this.fetchLogs();
    if (this.isPolling) {
      this.startPolling();
    }
  }

  nextPage() {
    if (this.page < this.totalPages) {
      this.page++;
      this.fetchLogs();
      if (this.isPolling) {
        this.startPolling();
      }
    }
  }

  prevPage() {
    if (this.page > 1) {
      this.page--;
      this.fetchLogs();
      if (this.isPolling) {
        this.startPolling();
      }
    }
  }

  getLevelClass(level: string): string {
    switch(level.toLowerCase()) {
      case 'error':
      case 'fatal': return 'badge-error';
      case 'warning': return 'badge-warning';
      case 'info':
      case 'information': return 'badge-info';
      case 'debug': return 'badge-debug';
      default: return 'badge-default';
    }
  }
}
