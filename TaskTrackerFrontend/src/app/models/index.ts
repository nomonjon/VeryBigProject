// ── Enums ────────────────────────────────────────────────────────────────────
export enum Priority {
  Low = 0,
  Medium = 1,
  High = 2,
}

export enum Status {
  InProgress = 0,
  Review = 1,
  Canceled = 2,
  Done = 3,
}

// ── Auth ─────────────────────────────────────────────────────────────────────
export interface RegisterDto {
  fullName: string;
  email: string;
  password: string;
  position: string;
}

export interface LoginDto {
  email: string;
  password: string;
}

export interface AuthResponseDto {
  id: string;
  token: string;
  email: string;
  fullName: string;
  role: string;
}

// ── User ──────────────────────────────────────────────────────────────────────
export interface UserDto {
  fullName: string;
  email: string;
  position: string;
  role: string;
  workTasks: WorkTaskDto[];
}

export interface UserWithIdDto extends UserDto {
  id: string;
}

export interface CreateUpdateUserDto {
  fullName: string;
  email: string;
  position: string;
}

export interface UpdateUserRoleDto {
  role: string;
}

// ── Project ──────────────────────────────────────────────────────────────────
export interface ProjectDto {
  name: string;
  description: string;
  workTasks: WorkTaskDto[];
}

export interface ProjectWithIdDto extends ProjectDto {
  id: string;
  userIds: string[];
}

export interface CreateUpdateProjectDto {
  name: string;
  description: string;
}

// ── WorkTask ──────────────────────────────────────────────────────────────────
export interface TaskCommentDto {
  id: string;
  workTaskId: string;
  userId: string;
  content: string;
  createdAt: string;
}

export interface TaskHistoryDto {
  id: string;
  workTaskId: string;
  userId: string;
  fieldName: string;
  oldValue: string;
  newValue: string;
  changedAt: string;
}

export interface WorkTaskDto {
  id: string;
  name: string;
  description: string;
  priority: Priority;
  status: Status;
  projectId: string;
  assigneeId: string | null;
  comments?: TaskCommentDto[];
  history?: TaskHistoryDto[];
}

export interface WorkTaskWithIdDto extends WorkTaskDto {
}

export interface CreateUpdateWorkTaskDto {
  name: string;
  description: string;
  priority: Priority;
  status: Status;
  projectId: string;
  assigneeId: string | null;
}

// ── Product ──────────────────────────────────────────────────────────────────
export interface ProductDto {
  id: string;
  name: string;
  description: string;
  price: number;
}

export interface CreateUpdateProductDto {
  name: string;
  description: string;
  price: number;
}

// ── Error ─────────────────────────────────────────────────────────────────────
export interface ErrorResponse {
  code: string;
  message: string;
  status: number;
}
