export interface LoginRequest {
  email: string;
  password: string;
}

export interface AuthSession {
  token: string;
  email: string;
  name: string;
  role: string;
}
