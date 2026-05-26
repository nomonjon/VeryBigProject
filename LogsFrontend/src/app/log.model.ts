export interface LogEntry {
  id: string;
  timestamp: string;
  level: string;
  message: string;
  sourceContext: string;
  machineName: string;
  serviceName: string;
  exception: string | null;
  createdAt: string;
}

export interface PaginatedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}
