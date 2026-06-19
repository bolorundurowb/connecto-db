using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;

namespace Connecto.Client;

public class ConnectoClient : IAsyncDisposable
{
    private readonly string _baseUrl;
    private readonly HttpClient _http;
    private string? _token;
    private HubConnection? _dataHub;
    private HubConnection? _collectionHub;

    public bool IsConnected =>
        _dataHub?.State == HubConnectionState.Connected &&
        _collectionHub?.State == HubConnectionState.Connected;

    public ConnectoClient(string url = "http://localhost:5043")
    {
        _baseUrl = url.TrimEnd('/');
        _http = new HttpClient { BaseAddress = new Uri(_baseUrl) };
    }

    // ── Auth ────────────────────────────────────────────────────

    public async Task<AuthRes> Login(LoginReq req)
    {
        var res = await _http.PostAsJsonAsync("/api/auth/login", req);
        await ThrowIfNotOk(res);
        var auth = (await res.Content.ReadFromJsonAsync<AuthRes>())!;
        _token = auth.Token;
        return auth;
    }

    public async Task<AuthRes> Register(RegisterReq req)
    {
        var res = await _http.PostAsJsonAsync("/api/auth/register", req);
        await ThrowIfNotOk(res);
        var auth = (await res.Content.ReadFromJsonAsync<AuthRes>())!;
        _token = auth.Token;
        return auth;
    }

    // ── Connection ──────────────────────────────────────────────

    public async Task Connect()
    {
        if (_token is null) throw new InvalidOperationException("Not authenticated.");
        _dataHub = BuildHub("/data-stream");
        _collectionHub = BuildHub("/collection-stream");

        _dataHub.On<string>("HubError", err => Console.Error.WriteLine($"[connecto] {err}"));
        _collectionHub.On<string>("HubError", err => Console.Error.WriteLine($"[connecto] {err}"));

        await Task.WhenAll(_dataHub.StartAsync(), _collectionHub.StartAsync());
    }

    // ── Collections ─────────────────────────────────────────────

    public Task<List<string>> ListTables() =>
        RequireCollection().InvokeAsync<List<string>>("ListTables");

    public Task CreateTable(string name)
    {
        var tcs = new TaskCompletionSource<string>();
        RequireCollection().On<string>("TableCreated", table => tcs.TrySetResult(table));
        RequireCollection().InvokeAsync("CreateTable", name);
        return tcs.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }

    public Task DeleteTable(string name)
    {
        var tcs = new TaskCompletionSource<string>();
        RequireCollection().On<string>("TableDeleted", table => tcs.TrySetResult(table));
        RequireCollection().InvokeAsync("DeleteTable", name);
        return tcs.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }

    // ── Data ────────────────────────────────────────────────────

    public Task Subscribe(string tableName) =>
        RequireData().InvokeAsync("SubscribeToTable", tableName);

    public Task Unsubscribe(string tableName) =>
        RequireData().InvokeAsync("UnsubscribeFromTable", tableName);

    public Task<List<FlexMap>> GetAllRecords(string tableName)
    {
        var tcs = new TaskCompletionSource<List<FlexMap>>();
        RequireData().On<string, List<FlexMap>>("EntitiesRequested", (_, records) =>
            tcs.TrySetResult(records));
        RequireData().InvokeAsync("GetAllRecords", tableName);
        return tcs.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }

    public Task<FlexMap?> GetRecord(string tableName, Guid id)
    {
        var tcs = new TaskCompletionSource<FlexMap?>();
        RequireData().On<string, FlexMap?>("EntityRequested", (_, record) =>
            tcs.TrySetResult(record));
        RequireData().InvokeAsync("GetRecord", tableName, id);
        return tcs.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }

    public Task Upsert(string tableName, FlexMap data)
    {
        var tcs = new TaskCompletionSource<FlexMap>();
        if (data.ContainsKey("id"))
        {
            RequireData().On<string, FlexMap>("EntityUpdated", (_, record) =>
                tcs.TrySetResult(record));
        }
        else
        {
            RequireData().On<string, FlexMap>("EntityCreated", (_, record) =>
                tcs.TrySetResult(record));
        }
        RequireData().InvokeAsync("UpsertDataRecord", tableName, data);
        return tcs.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }

    public Task DeleteRecord(string tableName, Guid id)
    {
        var tcs = new TaskCompletionSource<string>();
        RequireData().On<string, string>("EntityDeleted", (_, deletedId) =>
            tcs.TrySetResult(deletedId));
        RequireData().InvokeAsync("DeleteRecord", tableName, id);
        return tcs.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }

    // ── Realtime Listeners ──────────────────────────────────────

    public void OnEntityCreated(Action<string, FlexMap> handler) =>
        RequireData().On("EntityCreated", handler);

    public void OnEntityUpdated(Action<string, FlexMap> handler) =>
        RequireData().On("EntityUpdated", handler);

    public void OnEntityDeleted(Action<string, string> handler) =>
        RequireData().On("EntityDeleted", handler);

    public void OnTableCreated(Action<string> handler) =>
        RequireCollection().On("TableCreated", handler);

    public void OnTableDeleted(Action<string> handler) =>
        RequireCollection().On("TableDeleted", handler);

    public void OnError(Action<string> handler) =>
        RequireData().On("HubError", handler);

    // ── IDisposable ─────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        if (_dataHub is not null) await _dataHub.DisposeAsync();
        if (_collectionHub is not null) await _collectionHub.DisposeAsync();
        _http.Dispose();
    }

    // ── Internals ───────────────────────────────────────────────

    private HubConnection RequireData() =>
        _dataHub ?? throw new InvalidOperationException("Not connected.");

    private HubConnection RequireCollection() =>
        _collectionHub ?? throw new InvalidOperationException("Not connected.");

    private HubConnection BuildHub(string path) =>
        new HubConnectionBuilder()
            .WithUrl($"{_baseUrl}{path}", opts =>
            {
                opts.AccessTokenProvider = () => Task.FromResult(_token)!;
            })
            .WithAutomaticReconnect()
            .Build();

    private static async Task ThrowIfNotOk(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
        {
            var err = await res.Content.ReadFromJsonAsync<HubError>();
            throw new HttpRequestException(err?.Message ?? res.ReasonPhrase);
        }
    }
}
