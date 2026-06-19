package com.connecto.client;

import com.connecto.client.models.*;
import com.google.gson.Gson;
import com.google.gson.reflect.TypeToken;
import com.microsoft.signalr.*;
import okhttp3.*;
import java.io.IOException;
import java.lang.reflect.Type;
import java.util.*;
import java.util.concurrent.*;
import java.util.function.BiConsumer;
import java.util.function.Consumer;

public class ConnectoClient {
    private final String baseUrl;
    private final OkHttpClient http;
    private final Gson gson;
    private String token;
    private HubConnection dataHub;
    private HubConnection collectionHub;

    private final Map<String, List<BiConsumer<String, Map<String, Object>>>> entityCreatedHandlers = new ConcurrentHashMap<>();
    private final Map<String, List<BiConsumer<String, Map<String, Object>>>> entityUpdatedHandlers = new ConcurrentHashMap<>();
    private final Map<String, List<BiConsumer<String, String>>> entityDeletedHandlers = new ConcurrentHashMap<>();
    private final List<Consumer<String>> tableCreatedHandlers = new CopyOnWriteArrayList<>();
    private final List<Consumer<String>> tableDeletedHandlers = new CopyOnWriteArrayList<>();

    public ConnectoClient(String url) {
        this.baseUrl = url.replaceAll("/$", "");
        this.http = new OkHttpClient();
        this.gson = new Gson();
    }

    public boolean isConnected() {
        return dataHub != null &&
            dataHub.getConnectionState() == HubConnectionState.CONNECTED &&
            collectionHub != null &&
            collectionHub.getConnectionState() == HubConnectionState.CONNECTED;
    }

    // ── Auth ────────────────────────────────────────────────────

    public AuthRes login(String username, String password) throws Exception {
        LoginReq req = new LoginReq(username, password);
        AuthRes auth = post("/api/auth/login", req, AuthRes.class);
        this.token = auth.token;
        return auth;
    }

    public AuthRes register(String username, String password, String firstName, String lastName) throws Exception {
        RegisterReq req = new RegisterReq(username, password);
        req.firstName = firstName;
        req.lastName = lastName;
        AuthRes auth = post("/api/auth/register", req, AuthRes.class);
        this.token = auth.token;
        return auth;
    }

    // ── Connect ─────────────────────────────────────────────────

    public CompletableFuture<Void> connect() {
        if (token == null) throw new IllegalStateException("Not authenticated");

        dataHub = buildHub("/data-stream");
        collectionHub = buildHub("/collection-stream");

        setupDataListeners(dataHub);
        setupCollectionListeners(collectionHub);

        return CompletableFuture.allOf(
            dataHub.start().toCompletableFuture(),
            collectionHub.start().toCompletableFuture()
        );
    }

    public CompletableFuture<Void> disconnect() {
        CompletableFuture<Void> a = dataHub != null ? dataHub.stop().toCompletableFuture() : CompletableFuture.completedFuture(null);
        CompletableFuture<Void> b = collectionHub != null ? collectionHub.stop().toCompletableFuture() : CompletableFuture.completedFuture(null);
        return CompletableFuture.allOf(a, b);
    }

    // ── Collections ─────────────────────────────────────────────

    public CompletableFuture<List<String>> listTables() {
        return collectionHub.invoke(List.class, "ListTables")
            .toCompletableFuture()
            .thenApply(r -> (List<String>) r);
    }

    public CompletableFuture<Void> createTable(String name) {
        return collectionHub.invoke("CreateTable", name).toCompletableFuture().thenApply(r -> null);
    }

    public CompletableFuture<Void> deleteTable(String name) {
        return collectionHub.invoke("DeleteTable", name).toCompletableFuture().thenApply(r -> null);
    }

    // ── Data ────────────────────────────────────────────────────

    public CompletableFuture<Void> subscribe(String table) {
        return dataHub.invoke("SubscribeToTable", table).toCompletableFuture().thenApply(r -> null);
    }

    public CompletableFuture<Void> unsubscribe(String table) {
        return dataHub.invoke("UnsubscribeFromTable", table).toCompletableFuture().thenApply(r -> null);
    }

    public CompletableFuture<List<Map<String, Object>>> getAllRecords(String table) {
        return dataHub.invoke(List.class, "GetAllRecords", table)
            .toCompletableFuture()
            .thenApply(r -> (List<Map<String, Object>>) r);
    }

    public CompletableFuture<Map<String, Object>> getRecord(String table, String id) {
        return dataHub.invoke(Map.class, "GetRecord", table, id)
            .toCompletableFuture()
            .thenApply(r -> (Map<String, Object>) r);
    }

    public CompletableFuture<Map<String, Object>> upsert(String table, Map<String, Object> data) {
        return dataHub.invoke(Map.class, "UpsertDataRecord", table, data)
            .toCompletableFuture()
            .thenApply(r -> (Map<String, Object>) r);
    }

    public CompletableFuture<Void> deleteRecord(String table, String id) {
        return dataHub.invoke("DeleteRecord", table, id).toCompletableFuture().thenApply(r -> null);
    }

    // ── Realtime Listeners ──────────────────────────────────────

    public void onEntityCreated(BiConsumer<String, Map<String, Object>> handler) {
        dataHub.on("EntityCreated", (table, entity) ->
            handler.accept((String) table, (Map<String, Object>) entity), String.class, Map.class);
    }

    public void onEntityUpdated(BiConsumer<String, Map<String, Object>> handler) {
        dataHub.on("EntityUpdated", (table, entity) ->
            handler.accept((String) table, (Map<String, Object>) entity), String.class, Map.class);
    }

    public void onEntityDeleted(BiConsumer<String, String> handler) {
        dataHub.on("EntityDeleted", handler, String.class, String.class);
    }

    public void onTableCreated(Consumer<String> handler) {
        collectionHub.on("TableCreated", handler, String.class);
    }

    public void onTableDeleted(Consumer<String> handler) {
        collectionHub.on("TableDeleted", handler, String.class);
    }

    public void onError(Consumer<String> handler) {
        dataHub.on("HubError", handler, String.class);
        collectionHub.on("HubError", handler, String.class);
    }

    // ── Internals ───────────────────────────────────────────────

    private HubConnection buildHub(String path) {
        return HubConnectionBuilder.create(baseUrl + path)
            .withAccessTokenProvider(Single.just(token))
            .withTransport(TransportEnum.WEBSOCKETS)
            .build();
    }

    private void setupDataListeners(HubConnection hub) {
        hub.on("HubError", (msg) -> System.err.println("[connecto] " + msg), String.class);
    }

    private void setupCollectionListeners(HubConnection hub) {
        hub.on("HubError", (msg) -> System.err.println("[connecto] " + msg), String.class);
    }

    private <T> T post(String path, Object body, Class<T> clazz) throws Exception {
        String json = gson.toJson(body);
        Request request = new Request.Builder()
            .url(baseUrl + path)
            .post(RequestBody.create(json, MediaType.parse("application/json")))
            .build();

        try (Response response = http.newCall(request).execute()) {
            String respBody = response.body() != null ? response.body().string() : "";
            if (!response.isSuccessful()) {
                GenericRes err = gson.fromJson(respBody, GenericRes.class);
                throw new IOException(err.message != null ? err.message : response.message());
            }
            return gson.fromJson(respBody, clazz);
        }
    }
}
