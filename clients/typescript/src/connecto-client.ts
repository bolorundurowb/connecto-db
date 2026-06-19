import {
  HubConnectionBuilder,
  HubConnection,
  HttpTransportType,
  LogLevel,
} from "@microsoft/signalr";
import type { AuthRes, LoginReq, RegisterReq, FlexMap, UserRes, HubError } from "./types";

export type { AuthRes, LoginReq, RegisterReq, FlexMap, UserRes, HubError };

const EVENTS = {
  EntityCreated: "EntityCreated",
  EntityUpdated: "EntityUpdated",
  EntityDeleted: "EntityDeleted",
  EntityRequested: "EntityRequested",
  EntitiesRequested: "EntitiesRequested",
  TablesRequested: "TablesRequested",
  TableCreated: "TableCreated",
  TableDeleted: "TableDeleted",
  HubError: "HubError",
} as const;

export class ConnectoClient {
  private readonly baseUrl: string;
  private token: string | null = null;
  private dataHub: HubConnection | null = null;
  private collectionHub: HubConnection | null = null;

  constructor(url: string = "http://localhost:5043") {
    this.baseUrl = url.replace(/\/$/, "");
  }

  get isConnected(): boolean {
    return (
      this.dataHub?.state === "Connected" &&
      this.collectionHub?.state === "Connected"
    );
  }

  // ── Auth (REST) ──────────────────────────────────────────────

  async login(req: LoginReq): Promise<AuthRes> {
    const res = await this.post("/api/auth/login", req);
    this.token = res.token;
    return res;
  }

  async register(req: RegisterReq): Promise<AuthRes> {
    const res = await this.post("/api/auth/register", req);
    this.token = res.token;
    return res;
  }

  // ── Connection ───────────────────────────────────────────────

  async connect(): Promise<void> {
    if (!this.token) throw new Error("Not authenticated. Call login() or register() first.");
    await Promise.all([this.connectDataHub(), this.connectCollectionHub()]);
  }

  async disconnect(): Promise<void> {
    await Promise.all([
      this.dataHub?.stop(),
      this.collectionHub?.stop(),
    ]);
    this.dataHub = null;
    this.collectionHub = null;
  }

  // ── Collection Operations ────────────────────────────────────

  async listTables(): Promise<string[]> {
    const hub = this.requireCollectionHub();
    return new Promise((resolve, reject) => {
      const handler = (tables: string[]) => {
        hub.off(EVENTS.TablesRequested, handler);
        resolve(tables);
      };
      hub.on(EVENTS.TablesRequested, handler);
      hub.invoke("ListTables").catch(reject);
    });
  }

  async createTable(name: string): Promise<string> {
    const hub = this.requireCollectionHub();
    return new Promise((resolve, reject) => {
      const onCreated = (tableName: string) => {
        hub.off(EVENTS.TableCreated, onCreated);
        resolve(tableName);
      };
      hub.on(EVENTS.TableCreated, onCreated);
      hub.invoke("CreateTable", name).catch(reject);
    });
  }

  async deleteTable(name: string): Promise<string> {
    const hub = this.requireCollectionHub();
    return new Promise((resolve, reject) => {
      const onDeleted = (tableName: string) => {
        hub.off(EVENTS.TableDeleted, onDeleted);
        resolve(tableName);
      };
      hub.on(EVENTS.TableDeleted, onDeleted);
      hub.invoke("DeleteTable", name).catch(reject);
    });
  }

  // ── Data Operations ──────────────────────────────────────────

  async subscribe(tableName: string): Promise<void> {
    await this.requireDataHub().invoke("SubscribeToTable", tableName);
  }

  async unsubscribe(tableName: string): Promise<void> {
    await this.requireDataHub().invoke("UnsubscribeFromTable", tableName);
  }

  async getAllRecords(tableName: string): Promise<FlexMap[]> {
    const hub = this.requireDataHub();
    return new Promise((resolve, reject) => {
      const handler = (_table: string, records: FlexMap[]) => {
        hub.off(EVENTS.EntitiesRequested, handler);
        resolve(records);
      };
      hub.on(EVENTS.EntitiesRequested, handler);
      hub.invoke("GetAllRecords", tableName).catch(reject);
    });
  }

  async getRecord(tableName: string, id: string): Promise<FlexMap> {
    const hub = this.requireDataHub();
    return new Promise((resolve, reject) => {
      const handler = (_table: string, record: FlexMap) => {
        hub.off(EVENTS.EntityRequested, handler);
        resolve(record);
      };
      hub.on(EVENTS.EntityRequested, handler);
      hub.invoke("GetRecord", tableName, id).catch(reject);
    });
  }

  async upsert(tableName: string, data: FlexMap): Promise<FlexMap> {
    const hub = this.requireDataHub();
    if (data.id) {
      return new Promise((resolve, reject) => {
        const handler = (_table: string, record: FlexMap) => {
          hub.off(EVENTS.EntityUpdated, handler);
          resolve(record);
        };
        hub.on(EVENTS.EntityUpdated, handler);
        hub.invoke("UpsertDataRecord", tableName, data).catch(reject);
      });
    }

    return new Promise((resolve, reject) => {
      const handler = (_table: string, record: FlexMap) => {
        hub.off(EVENTS.EntityCreated, handler);
        resolve(record);
      };
      hub.on(EVENTS.EntityCreated, handler);
      hub.invoke("UpsertDataRecord", tableName, data).catch(reject);
    });
  }

  async deleteRecord(tableName: string, id: string): Promise<string> {
    const hub = this.requireDataHub();
    return new Promise((resolve, reject) => {
      const handler = (_table: string, deletedId: string) => {
        hub.off(EVENTS.EntityDeleted, handler);
        resolve(deletedId);
      };
      hub.on(EVENTS.EntityDeleted, handler);
      hub.invoke("DeleteRecord", tableName, id).catch(reject);
    });
  }

  // ── Realtime Listeners ───────────────────────────────────────

  onEntityCreated(cb: (table: string, record: FlexMap) => void): void {
    this.requireDataHub().on(EVENTS.EntityCreated, cb);
  }

  onEntityUpdated(cb: (table: string, record: FlexMap) => void): void {
    this.requireDataHub().on(EVENTS.EntityUpdated, cb);
  }

  onEntityDeleted(cb: (table: string, id: string) => void): void {
    this.requireDataHub().on(EVENTS.EntityDeleted, cb);
  }

  onTableCreated(cb: (table: string) => void): void {
    this.requireCollectionHub().on(EVENTS.TableCreated, cb);
  }

  onTableDeleted(cb: (table: string) => void): void {
    this.requireCollectionHub().on(EVENTS.TableDeleted, cb);
  }

  onError(cb: (error: HubError) => void): void {
    this.requireDataHub().on(EVENTS.HubError, cb);
    this.requireCollectionHub().on(EVENTS.HubError, cb);
  }

  // ── Internals ────────────────────────────────────────────────

  private requireDataHub(): HubConnection {
    if (!this.dataHub) throw new Error("Not connected. Call connect() first.");
    return this.dataHub;
  }

  private requireCollectionHub(): HubConnection {
    if (!this.collectionHub) throw new Error("Not connected. Call connect() first.");
    return this.collectionHub;
  }

  private async connectDataHub(): Promise<void> {
    this.dataHub = this.buildHub("/data-stream");
    this.setupHubErrorHandler(this.dataHub);
    await this.dataHub.start();
  }

  private async connectCollectionHub(): Promise<void> {
    this.collectionHub = this.buildHub("/collection-stream");
    this.setupHubErrorHandler(this.collectionHub);
    await this.collectionHub.start();
  }

  private buildHub(path: string): HubConnection {
    return new HubConnectionBuilder()
      .withUrl(`${this.baseUrl}${path}`, {
        accessTokenFactory: () => this.token!,
        transport: HttpTransportType.WebSockets,
      })
      .configureLogging(LogLevel.Warning)
      .withAutomaticReconnect()
      .build();
  }

  private setupHubErrorHandler(hub: HubConnection): void {
    hub.on(EVENTS.HubError, (err: HubError) => {
      console.error("[connecto]", err.message);
    });
  }

  private async post<T>(path: string, body: unknown): Promise<T> {
    const res = await fetch(`${this.baseUrl}${path}`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    if (!res.ok) {
      const err = await res.json().catch(() => ({ message: res.statusText }));
      throw new Error((err as { message?: string }).message ?? res.statusText);
    }
    return res.json();
  }
}
