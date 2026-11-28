using System.Net;
using KeyValueStore.Infrastructure.Storage;
using Xunit;

namespace KeyValueStore.Api.Tests;

public sealed class DataDirectoryLockApiTests
{
    private const string TestUrl =
        "/api/keyspaces/team-a/entries/missing";

    [Fact]
    public void Api_ShouldHoldLockUntilDisposed()
    {
        string directory = NewDirectory();

        try
        {
            using (var factory = new TestApiFactory(directory))
            {
                using var client = factory.CreateClient();

                Assert.Throws<IOException>(() =>
                    DataDirectoryLock.Acquire(directory));
            }

            using var nextOwner =
                DataDirectoryLock.Acquire(directory);

            Assert.NotNull(nextOwner);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task SecondApi_ShouldFailOnSameDirectory()
    {
        string directory = NewDirectory();

        try
        {
            using var firstFactory =
                new TestApiFactory(directory);

            using var firstClient =
                firstFactory.CreateClient();

            using var before =
                await firstClient.GetAsync(TestUrl);

            Assert.Equal(
                HttpStatusCode.Unauthorized,
                before.StatusCode);

            using var secondFactory =
                new TestApiFactory(directory);

            Exception error = Assert.ThrowsAny<Exception>(
                () => secondFactory.CreateClient());

            Assert.Contains(
                "Cannot acquire exclusive lock",
                error.ToString());

            using var after =
                await firstClient.GetAsync(TestUrl);

            Assert.Equal(
                HttpStatusCode.Unauthorized,
                after.StatusCode);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task DifferentDirectories_ShouldAllowTwoApis()
    {
        string firstDirectory = NewDirectory();
        string secondDirectory = NewDirectory();

        try
        {
            using var firstFactory =
                new TestApiFactory(firstDirectory);

            using var secondFactory =
                new TestApiFactory(secondDirectory);

            using var firstClient =
                firstFactory.CreateClient();

            using var secondClient =
                secondFactory.CreateClient();

            using var firstResponse =
                await firstClient.GetAsync(TestUrl);

            using var secondResponse =
                await secondClient.GetAsync(TestUrl);

            Assert.Equal(
                HttpStatusCode.Unauthorized,
                firstResponse.StatusCode);

            Assert.Equal(
                HttpStatusCode.Unauthorized,
                secondResponse.StatusCode);
        }
        finally
        {
            DeleteDirectory(firstDirectory);
            DeleteDirectory(secondDirectory);
        }
    }

    private static string NewDirectory()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"KvsLockApiTests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);

        return directory;
    }

    private static void DeleteDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(
                directory,
                recursive: true);
        }
    }
}