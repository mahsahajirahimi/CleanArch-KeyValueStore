using KeyValueStore.Infrastructure.Security;

bool isProvision =
    args.Length == 3 &&
    string.Equals(
        args[0],
        "provision",
        StringComparison.OrdinalIgnoreCase);

if (args.Length != 2 && !isProvision)
{
    Console.Error.WriteLine(
        "Usage:");
    Console.Error.WriteLine(
        "  BootstrapAdmin <keyspace> <data-directory>");
    Console.Error.WriteLine(
        "  BootstrapAdmin provision <keyspace> <data-directory>");

    return 2;
}

try
{
    string keyspace = (
        isProvision ? args[1] : args[0]).Trim();

    string dataDirectory = Path.GetFullPath(
        isProvision ? args[2] : args[1]);

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

    DateTimeOffset now = DateTimeOffset.UtcNow;

    BootstrapAdminResult result = isProvision
        ? bootstrapper.ProvisionAdditionalKeyspace(
            keyspace, now)
        : bootstrapper.Execute(keyspace, now);

    Console.WriteLine(
        isProvision
            ? "Additional keyspace admin created."
            : "Initial admin created.");

    Console.WriteLine($"Keyspace: {keyspace}");
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