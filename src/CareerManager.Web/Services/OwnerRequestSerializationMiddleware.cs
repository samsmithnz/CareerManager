using System.Collections.Concurrent;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CareerManager.Data;
using CareerManager.Security;
using Microsoft.EntityFrameworkCore;

namespace CareerManager.Services;

public sealed class OwnerRequestLock
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);

    public async Task<IDisposable> AcquireAsync(string ownerId, CancellationToken cancellationToken)
    {
        var semaphore = _locks.GetOrAdd(ownerId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        return new Releaser(semaphore);
    }

    private sealed class Releaser(SemaphoreSlim semaphore) : IDisposable
    {
        public void Dispose() => semaphore.Release();
    }
}

public sealed class OwnerRequestSerializationMiddleware(
    CareerManagerContext context,
    OwnerRequestLock ownerRequestLock) : IMiddleware
{
    public async Task InvokeAsync(HttpContext httpContext, RequestDelegate next)
    {
        var ownerId = httpContext.User.FindFirstValue(CareerOwnerClaim.Type);
        if (string.IsNullOrEmpty(ownerId))
        {
            await next(httpContext);
            return;
        }

        IDisposable? localLock = null;
        var connectionOpened = false;
        var sqlLockAcquired = false;
        var resource = CreateResourceName(ownerId);
        try
        {
            localLock = await ownerRequestLock.AcquireAsync(ownerId, httpContext.RequestAborted);
            if (context.Database.IsSqlServer())
            {
                await context.Database.OpenConnectionAsync(httpContext.RequestAborted);
                connectionOpened = true;
                await ExecuteLockCommandAsync(
                    context,
                    resource,
                    acquire: true,
                    httpContext.RequestAborted);
                sqlLockAcquired = true;
            }
        }
        catch (OperationCanceledException) when (httpContext.RequestAborted.IsCancellationRequested)
        {
            try
            {
                if (connectionOpened)
                {
                    await context.Database.CloseConnectionAsync();
                }
            }
            finally
            {
                localLock?.Dispose();
            }

            throw;
        }
        catch
        {
            try
            {
                if (connectionOpened)
                {
                    await context.Database.CloseConnectionAsync();
                }
            }
            finally
            {
                localLock?.Dispose();
            }

            httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await httpContext.Response.WriteAsync(
                "Owner data is temporarily unavailable. Please retry shortly.",
                CancellationToken.None);
            return;
        }

        try
        {
            await next(httpContext);
        }
        finally
        {
            if (sqlLockAcquired)
            {
                try
                {
                    await ExecuteLockCommandAsync(context, resource, acquire: false, CancellationToken.None);
                }
                catch
                {
                }
            }

            try
            {
                if (connectionOpened)
                {
                    await context.Database.CloseConnectionAsync();
                }
            }
            finally
            {
                localLock?.Dispose();
            }
        }
    }

    private static string CreateResourceName(string ownerId)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ownerId)));
        return $"CareerManager.Owner.{hash}";
    }

    private static async Task ExecuteLockCommandAsync(
        CareerManagerContext context,
        string resource,
        bool acquire,
        CancellationToken cancellationToken)
    {
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = acquire
            ? "DECLARE @result int; EXEC @result = sys.sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = -1; SELECT @result;"
            : "EXEC sys.sp_releaseapplock @Resource = @resource, @LockOwner = 'Session';";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@resource";
        parameter.Value = resource;
        command.Parameters.Add(parameter);
        if (acquire)
        {
            var result = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
            if (result < 0)
            {
                throw new InvalidOperationException("The owner request lock could not be acquired.");
            }
        }
        else
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
