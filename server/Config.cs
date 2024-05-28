namespace connecto.server;

public static class Config
{
    public const string CoreDbName = "connecto-core.db";

    public const string UserDbNameTemplate = "connecto-user-{0}.db";

    public const string EntityUpdated = nameof(EntityUpdated);

    public const string EntityCreated = nameof(EntityCreated);

    public const string TablesRequested = nameof(TablesRequested);

    public const string TableCreated = nameof(TableCreated);
}
