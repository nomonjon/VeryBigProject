import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LogsService, LogEntry, PagedResult, LogQuery } from './logs.service';
import { Subject, interval, switchMap, takeUntil, tap } from 'rxjs';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
export class AppComponent implements OnInit, OnDestroy {
  logs: LogEntry[] = [];
  totalCount = 0;
  totalPages = 0;
  currentPage = 1;
  pageSize = 50;
  
  // Filters
  searchText = '';
  selectedLevel = '';
  selectedService = '';
  fromDate = '';
  toDate = '';
  
  // Dropdown options
  services: string[] = [];
  levels: string[] = [];
  
  // Polling
  pollingEnabled = true;
  pollingInterval = 5;
  private destroy$ = new Subject<void>();
  private pollSubject$ = new Subject<void>();
  
  // UI state
  loading = false;
  lastUpdated: Date | null = null;
  connectionStatus: 'connected' | 'disconnected' | 'loading' = 'loading';

  constructor(private logsService: LogsService) {}

  ngOnInit(): void {
    this.loadFilters();
    this.loadLogs();
    this.startPolling();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadFilters(): void {
    this.logsService.getServices().subscribe({
      next: (s) => this.services = s.filter(x => x),
      error: () => {}
    });
    this.logsService.getLevels().subscribe({
      next: (l) => this.levels = l.filter(x => x),
      error: () => {}
    });
  }

  loadLogs(): void {
    this.loading = true;
    const query: LogQuery = {
      page: this.currentPage,
      pageSize: this.pageSize,
      level: this.selectedLevel || undefined,
      serviceName: this.selectedService || undefined,
      search: this.searchText || undefined,
      from: this.fromDate || undefined,
      to: this.toDate || undefined,
      sortOrder: 'desc'
    };

    this.logsService.getLogs(query).subscribe({
      next: (result) => {
        this.logs = result.items;
        this.totalCount = result.totalCount;
        this.totalPages = result.totalPages;
        this.loading = false;
        this.lastUpdated = new Date();
        this.connectionStatus = 'connected';
      },
      error: () => {
        this.loading = false;
        this.connectionStatus = 'disconnected';
      }
    });
  }

  startPolling(): void {
    interval(this.pollingInterval * 1000)
      .pipe(
        takeUntil(this.destroy$),
      )
      .subscribe(() => {
        if (this.pollingEnabled) {
          this.loadLogs();
          this.loadFilters();
        }
      });
  }

  togglePolling(): void {
    this.pollingEnabled = !this.pollingEnabled;
  }

  applyFilters(): void {
    this.currentPage = 1;
    this.loadLogs();
  }

  clearFilters(): void {
    this.searchText = '';
    this.selectedLevel = '';
    this.selectedService = '';
    this.fromDate = '';
    this.toDate = '';
    this.currentPage = 1;
    this.loadLogs();
  }

  goToPage(page: number): void {
    if (page >= 1 && page <= this.totalPages) {
      this.currentPage = page;
      this.loadLogs();
    }
  }

  getVisiblePages(): number[] {
    const pages: number[] = [];
    const start = Math.max(1, this.currentPage - 2);
    const end = Math.min(this.totalPages, this.currentPage + 2);
    for (let i = start; i <= end; i++) {
      pages.push(i);
    }
    return pages;
  }

  getLevelClass(level: string): string {
    switch (level?.toLowerCase()) {
      case 'error': return 'level-error';
      case 'warning': case 'warn': return 'level-warning';
      case 'info': case 'information': return 'level-info';
      case 'debug': return 'level-debug';
      case 'critical': return 'level-critical';
      default: return 'level-default';
    }
  }

  getTimeAgo(dateStr: string): string {
    const date = new Date(dateStr);
    const now = new Date();
    const diff = Math.floor((now.getTime() - date.getTime()) / 1000);
    if (diff < 60) return `${diff}s ago`;
    if (diff < 3600) return `${Math.floor(diff / 60)}m ago`;
    if (diff < 86400) return `${Math.floor(diff / 3600)}h ago`;
    return `${Math.floor(diff / 86400)}d ago`;
  }
}
