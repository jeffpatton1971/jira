using System.Runtime.InteropServices;
using JiraCli.Credentials;

namespace JiraCli.Credentials.Linux;

public sealed class LinuxSecretServiceProvider : ICredentialStoreProvider
{
    private const int SecretSchemaAttributeString = 0;
    private const int SecretSchemaDontMatchName = 2;
    private static readonly TimeSpan LookupTimeout = TimeSpan.FromSeconds(15);

    public string Name => "linux-secret-service";

    public async ValueTask<CredentialStoreResult> ReadAsync(
        CredentialStoreReference reference,
        CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsLinux())
        {
            return new CredentialStoreResult(
                CredentialStoreStatus.UnsupportedPlatform,
                SafeMessage: "Secret Service is supported only on Linux." );
        }

        var service = reference.Service;
        var account = reference.Account;
        if (string.IsNullOrWhiteSpace(service) || string.IsNullOrWhiteSpace(account))
        {
            return new CredentialStoreResult(
                CredentialStoreStatus.InvalidReference,
                SafeMessage: "A Linux Secret Service reference requires 'service' and 'account'.");
        }

        try
        {
            return await Task.Run(() => ReadNative(service, account), CancellationToken.None)
                .WaitAsync(LookupTimeout, cancellationToken);
        }
        catch (DllNotFoundException)
        {
            return new CredentialStoreResult(
                CredentialStoreStatus.UnsupportedPlatform,
                SafeMessage: "libsecret-1 is not installed on this Linux system.");
        }
        catch (EntryPointNotFoundException)
        {
            return new CredentialStoreResult(
                CredentialStoreStatus.UnsupportedPlatform,
                SafeMessage: "The installed libsecret does not provide the required lookup API.");
        }
        catch (TimeoutException)
        {
            return new CredentialStoreResult(
                CredentialStoreStatus.InteractiveAuthorizationRequired,
                SafeMessage: "Secret Service did not respond without interaction within 15 seconds.");
        }
    }

    private static CredentialStoreResult ReadNative(string service, string account)
    {
        var schema = SecretSchemaNew(
            "org.jiracli.ApiToken",
            SecretSchemaDontMatchName,
            "service",
            SecretSchemaAttributeString,
            "account",
            SecretSchemaAttributeString,
            IntPtr.Zero);
        if (schema == IntPtr.Zero)
        {
            return new CredentialStoreResult(CredentialStoreStatus.StoreUnavailableOrLocked, SafeMessage: "Could not initialize the Secret Service schema.");
        }

        try
        {
            var password = SecretPasswordLookupSync(
                schema,
                IntPtr.Zero,
                out var error,
                "service",
                service,
                "account",
                account,
                IntPtr.Zero);
            if (error != IntPtr.Zero)
            {
                return MapError(error);
            }

            if (password == IntPtr.Zero)
            {
                return new CredentialStoreResult(CredentialStoreStatus.ItemNotFound, SafeMessage: "The named Secret Service item was not found.");
            }

            try
            {
                var value = Marshal.PtrToStringUTF8(password);
                return value is null
                    ? new CredentialStoreResult(CredentialStoreStatus.ItemNotFound, SafeMessage: "The named Secret Service item has no secret value.")
                    : new CredentialStoreResult(CredentialStoreStatus.Success, SecretMaterial.FromString(value));
            }
            finally
            {
                SecretPasswordFree(password);
            }
        }
        finally
        {
            SecretSchemaUnref(schema);
        }
    }

    private static CredentialStoreResult MapError(IntPtr errorPointer)
    {
        try
        {
            var error = Marshal.PtrToStructure<GError>(errorPointer);
            var message = Marshal.PtrToStringUTF8(error.Message) ?? "Secret Service lookup failed.";
            var lower = message.ToLowerInvariant();
            var status = lower.Contains("denied", StringComparison.Ordinal)
                ? CredentialStoreStatus.AccessDenied
                : lower.Contains("locked", StringComparison.Ordinal)
                    ? CredentialStoreStatus.StoreUnavailableOrLocked
                    : lower.Contains("prompt", StringComparison.Ordinal) || lower.Contains("interaction", StringComparison.Ordinal)
                        ? CredentialStoreStatus.InteractiveAuthorizationRequired
                        : CredentialStoreStatus.StoreUnavailableOrLocked;
            return new CredentialStoreResult(status, SafeMessage: $"Secret Service lookup failed: {message}");
        }
        finally
        {
            GErrorFree(errorPointer);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GError
    {
        public uint Domain;
        public int Code;
        public IntPtr Message;
    }

    [DllImport("libsecret-1.so.0", EntryPoint = "secret_schema_new", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr SecretSchemaNew(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        int flags,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string attribute1,
        int type1,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string attribute2,
        int type2,
        IntPtr terminator);

    [DllImport("libsecret-1.so.0", EntryPoint = "secret_schema_unref", CallingConvention = CallingConvention.Cdecl)]
    private static extern void SecretSchemaUnref(IntPtr schema);

    [DllImport("libsecret-1.so.0", EntryPoint = "secret_password_lookup_sync", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr SecretPasswordLookupSync(
        IntPtr schema,
        IntPtr cancellable,
        out IntPtr error,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string attribute1,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string value1,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string attribute2,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string value2,
        IntPtr terminator);

    [DllImport("libsecret-1.so.0", EntryPoint = "secret_password_free", CallingConvention = CallingConvention.Cdecl)]
    private static extern void SecretPasswordFree(IntPtr password);

    [DllImport("libglib-2.0.so.0", EntryPoint = "g_error_free", CallingConvention = CallingConvention.Cdecl)]
    private static extern void GErrorFree(IntPtr error);
}
