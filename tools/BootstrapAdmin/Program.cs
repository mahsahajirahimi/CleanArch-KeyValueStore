using KeyValueStore.Infrastructure.Security;

if (args.Length != 2)
{
    Console.Error.WriteLine(
        "Usage: BootstrapAdmin <keyspace> <data-directory>");

    return 2;
}

try
{
    string keyspace = args[0].Trim();
    string dataDirectory = Path.GetFullPath(args[1]);

    Directory.CreateDirectory(dataDirectory);

    string lockPath = Path.Combine(
        dataDirectory,
        "bootstrap.lock");

    using var bootstrapLock = new FileStream(
        lockPath,
        FileMode.OpenOrCreate,
        FileAccess.ReadWrite,
        FileShare.None);

    var users = new JsonUserAccountRepository(
        Path.Combine(dataDirectory, "users.json"));

    var credentials = new JsonApiKeyCredentialRepository(
        Path.Combine(dataDirectory, "credentials.json"));

    var bootstrapper = new FirstAdminBootstrapper(
        users,
        credentials,
        new ApiKeyTokenService());

    BootstrapAdminResult result = bootstrapper.Execute(
        keyspace,
        DateTimeOffset.UtcNow);

    Console.WriteLine("Initial admin created.");
    Console.WriteLine($"User ID: {result.UserId}");
    Console.WriteLine($"Key ID: {result.KeyId}");

    Console.WriteLine();
    Console.WriteLine("API key (shown only once):");
    Console.WriteLine(result.PlaintextKey);

    Console.WriteLine();
    Console.WriteLine(
        "Store this key securely. Do not commit it to Git.");

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(
        $"Bootstrap failed: {ex.Message}");

    return 1;
}