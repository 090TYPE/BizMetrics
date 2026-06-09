// Thin API client: stores the token pair, attaches the bearer header, and
// transparently retries once after refreshing an expired access token.

const API_URL = import.meta.env.VITE_API_URL ?? "http://localhost:8080";

const ACCESS_KEY = "bm_access";
const REFRESH_KEY = "bm_refresh";

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  userId: string;
  organizationId: string | null;
  role: string | null;
}

export const tokens = {
  get access() {
    return localStorage.getItem(ACCESS_KEY);
  },
  get refresh() {
    return localStorage.getItem(REFRESH_KEY);
  },
  set(auth: AuthResponse) {
    localStorage.setItem(ACCESS_KEY, auth.accessToken);
    localStorage.setItem(REFRESH_KEY, auth.refreshToken);
  },
  clear() {
    localStorage.removeItem(ACCESS_KEY);
    localStorage.removeItem(REFRESH_KEY);
  },
};

async function refreshAccess(): Promise<boolean> {
  const refreshToken = tokens.refresh;
  if (!refreshToken) return false;

  const res = await fetch(`${API_URL}/api/auth/refresh`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ refreshToken }),
  });
  if (!res.ok) {
    tokens.clear();
    return false;
  }
  tokens.set((await res.json()) as AuthResponse);
  return true;
}

export async function api<T>(
  path: string,
  options: RequestInit = {},
  retry = true,
): Promise<T> {
  const headers = new Headers(options.headers);
  // Let the browser set the multipart boundary for FormData uploads.
  if (!(options.body instanceof FormData)) headers.set("Content-Type", "application/json");
  if (tokens.access) headers.set("Authorization", `Bearer ${tokens.access}`);

  const res = await fetch(`${API_URL}${path}`, { ...options, headers });

  if (res.status === 401 && retry && (await refreshAccess())) {
    return api<T>(path, options, false);
  }

  if (!res.ok) {
    const body = await res.json().catch(() => ({}));
    throw new Error(body.error ?? `Request failed (${res.status})`);
  }

  return res.status === 204 ? (undefined as T) : ((await res.json()) as T);
}

// --- JWT helpers (read claims client-side; no verification — display only) ---

export interface TokenClaims {
  sub: string;
  org_id?: string;
  org_role?: string;
}

export function decodeToken(token: string | null): TokenClaims | null {
  if (!token) return null;
  try {
    const payload = token.split(".")[1];
    const json = atob(payload.replace(/-/g, "+").replace(/_/g, "/"));
    return JSON.parse(json) as TokenClaims;
  } catch {
    return null;
  }
}

// --- Organizations ---

export interface Organization {
  id: string;
  name: string;
  slug: string;
  subscriptionStatus: string;
  trialEndsAt: string | null;
  planName: string | null;
}

export interface Member {
  userId: string;
  email: string;
  fullName: string;
  role: string;
  status: string;
  joinedAt: string;
}

export interface MyOrganization {
  id: string;
  name: string;
  slug: string;
  role: string;
}

export const orgs = {
  current: () => api<Organization>("/api/orgs/current"),
  update: (name: string) =>
    api<Organization>("/api/orgs/current", {
      method: "PATCH",
      body: JSON.stringify({ name }),
    }),
  members: () => api<Member[]>("/api/orgs/current/members"),
  changeRole: (userId: string, role: string) =>
    api<void>(`/api/orgs/current/members/${userId}/role`, {
      method: "PATCH",
      body: JSON.stringify({ role }),
    }),
  removeMember: (userId: string) =>
    api<void>(`/api/orgs/current/members/${userId}`, { method: "DELETE" }),
  mine: () => api<MyOrganization[]>("/api/orgs/mine"),
  switch: (orgId: string) =>
    api<AuthResponse>(`/api/orgs/${orgId}/switch`, { method: "POST" }),
};

// --- Datasets ---

export interface Dataset {
  id: string;
  name: string;
  status: "Pending" | "Processing" | "Ready" | "Failed";
  rowCount: number;
  columns: string[];
  errorMessage: string | null;
  createdAt: string;
  processedAt: string | null;
}

export interface DatasetRows {
  columns: string[];
  rows: Record<string, string | null>[];
  total: number;
}

export const datasets = {
  list: () => api<Dataset[]>("/api/datasets"),
  get: (id: string) => api<Dataset>(`/api/datasets/${id}`),
  upload: (file: File, name: string) => {
    const form = new FormData();
    form.append("file", file);
    form.append("name", name);
    return api<Dataset>("/api/datasets/upload", { method: "POST", body: form });
  },
  rows: (id: string, skip = 0, take = 50) =>
    api<DatasetRows>(`/api/datasets/${id}/rows?skip=${skip}&take=${take}`),
  downloadUrl: (id: string) => api<{ url: string }>(`/api/datasets/${id}/download-url`),
  remove: (id: string) => api<void>(`/api/datasets/${id}`, { method: "DELETE" }),
};

// --- Analytics & dashboards ---

export type Aggregation = "Count" | "Sum" | "Average" | "Min" | "Max";
export type TimeBucket = "None" | "Day" | "Week" | "Month";
export type ChartType = "bar" | "line" | "pie";

export interface AnalyticsQuery {
  groupBy?: string | null;
  bucket?: TimeBucket;
  measure?: string | null;
  agg: Aggregation;
  topN?: number | null;
}

export interface AnalyticsPoint {
  key: string;
  value: number;
  count: number;
}

export interface QueryResult {
  label: string;
  points: AnalyticsPoint[];
  insights: string[];
}

export interface DashboardSummary {
  id: string;
  name: string;
  widgetCount: number;
  createdAt: string;
}

export interface WidgetData {
  id: string;
  datasetId: string;
  title: string;
  chartType: ChartType;
  position: number;
  label: string;
  points: AnalyticsPoint[];
  insights: string[];
}

export interface DashboardData {
  id: string;
  name: string;
  widgets: WidgetData[];
}

export const analytics = {
  query: (datasetId: string, query: AnalyticsQuery) =>
    api<QueryResult>(`/api/datasets/${datasetId}/query`, {
      method: "POST",
      body: JSON.stringify(query),
    }),
};

export const dashboards = {
  list: () => api<DashboardSummary[]>("/api/dashboards"),
  create: (name: string) =>
    api<DashboardSummary>("/api/dashboards", { method: "POST", body: JSON.stringify({ name }) }),
  remove: (id: string) => api<void>(`/api/dashboards/${id}`, { method: "DELETE" }),
  data: (id: string) => api<DashboardData>(`/api/dashboards/${id}/data`),
  addWidget: (
    id: string,
    widget: { datasetId: string; title: string; chartType: ChartType; query: AnalyticsQuery },
  ) =>
    api<WidgetData>(`/api/dashboards/${id}/widgets`, {
      method: "POST",
      body: JSON.stringify(widget),
    }),
  removeWidget: (dashboardId: string, widgetId: string) =>
    api<void>(`/api/dashboards/${dashboardId}/widgets/${widgetId}`, { method: "DELETE" }),
};

// --- Invitations ---

export interface Invitation {
  id: string;
  email: string;
  role: string;
  status: string;
  expiresAt: string;
  createdAt: string;
}

export interface InvitationPreview {
  organizationName: string;
  email: string;
  role: string;
  redeemable: boolean;
}

export const invitations = {
  create: (email: string, role: string) =>
    api<Invitation>("/api/orgs/current/invitations", {
      method: "POST",
      body: JSON.stringify({ email, role }),
    }),
  list: () => api<Invitation[]>("/api/orgs/current/invitations"),
  revoke: (id: string) =>
    api<void>(`/api/orgs/current/invitations/${id}`, { method: "DELETE" }),
  preview: (token: string) =>
    api<InvitationPreview>(`/api/invitations/${encodeURIComponent(token)}`),
  accept: (token: string) =>
    api<void>("/api/invitations/accept", {
      method: "POST",
      body: JSON.stringify({ token }),
    }),
};

export const auth = {
  register: (body: {
    email: string;
    password: string;
    fullName: string;
    organizationName: string;
  }) =>
    api<AuthResponse>("/api/auth/register", {
      method: "POST",
      body: JSON.stringify(body),
    }),
  login: (body: { email: string; password: string }) =>
    api<AuthResponse>("/api/auth/login", {
      method: "POST",
      body: JSON.stringify(body),
    }),
};
