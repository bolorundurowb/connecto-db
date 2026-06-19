export interface UserRes {
  id: string;
  firstName: string | null;
  lastName: string | null;
  username: string;
  createdAt: string;
}

export interface AuthRes {
  user: UserRes;
  token: string;
  expiresAt: string;
}

export interface LoginReq {
  username: string;
  password: string;
}

export interface RegisterReq {
  username: string;
  password: string;
  firstName?: string;
  lastName?: string;
}

export type FlexMap = Record<string, unknown>;

export interface HubError {
  message: string;
}

export type EventHandler = (...args: unknown[]) => void;
