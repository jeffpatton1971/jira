using System.Runtime.InteropServices;
using System.Text;
using JiraCli.Credentials;

namespace JiraCli.Credentials.Mac;

public sealed class MacKeychainProvider : ICredentialStoreProvider
{
    private const int ErrSecSuccess = 0;
    private const int ErrSecNotAvailable = -25291;
    private const int ErrSecAuthFailed = -25293;
    private const int ErrSecUserCanceled = -128;
    private const int ErrSecInteractionNotAllowed = -25308;
    private const int ErrSecItemNotFound = -25300;

    public string Name => "macos-keychain";

    public async ValueTask<CredentialStoreResult> ReadAsync(
        CredentialStoreReference reference,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsMacOS())
        {
            return new CredentialStoreResult(
                CredentialStoreStatus.UnsupportedPlatform,
                SafeMessage: "macOS Keychain is supported only on macOS.");
        }

        if (string.IsNullOrWhiteSpace(reference.Service) || string.IsNullOrWhiteSpace(reference.Account))
        {
            return new CredentialStoreResult(
                CredentialStoreStatus.InvalidReference,
                SafeMessage: "A macOS Keychain reference requires 'service' and 'account'.");
        }

        try
        {
            return await Task.Run(() => ReadNative(reference.Service, reference.Account), CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
        }
        catch (TimeoutException)
        {
            return new CredentialStoreResult(
                CredentialStoreStatus.InteractiveAuthorizationRequired,
                SafeMessage: "Keychain did not respond without interaction within 15 seconds.");
        }
    }

    private static CredentialStoreResult ReadNative(string serviceName, string accountName)
    {
        var service = Encoding.UTF8.GetBytes(serviceName);
        var account = Encoding.UTF8.GetBytes(accountName);
        _ = SecKeychainGetUserInteractionAllowed(out var previousInteraction);
        _ = SecKeychainSetUserInteractionAllowed(false);
        try
        {
            var status = SecKeychainFindGenericPassword(
                IntPtr.Zero,
                (uint)service.Length,
                service,
                (uint)account.Length,
                account,
                out var passwordLength,
                out var passwordData,
                out _);
            if (status != ErrSecSuccess)
            {
                return MapStatus(status);
            }

            try
            {
                var bytes = new byte[passwordLength];
                try
                {
                    Marshal.Copy(passwordData, bytes, 0, bytes.Length);
                    return new CredentialStoreResult(
                        CredentialStoreStatus.Success,
                        SecretMaterial.FromString(Encoding.UTF8.GetString(bytes)));
                }
                finally
                {
                    System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes);
                }
            }
            finally
            {
                _ = SecKeychainItemFreeContent(IntPtr.Zero, passwordData);
            }
        }
        finally
        {
            _ = SecKeychainSetUserInteractionAllowed(previousInteraction);
        }
    }

    private static CredentialStoreResult MapStatus(int status) => status switch
    {
        ErrSecItemNotFound => new(CredentialStoreStatus.ItemNotFound, SafeMessage: "The named Keychain item was not found."),
        ErrSecNotAvailable => new(CredentialStoreStatus.StoreUnavailableOrLocked, SafeMessage: "The macOS Keychain is unavailable or locked."),
        ErrSecAuthFailed => new(CredentialStoreStatus.AccessDenied, SafeMessage: "Keychain authentication failed or access was denied."),
        ErrSecInteractionNotAllowed or ErrSecUserCanceled => new(CredentialStoreStatus.InteractiveAuthorizationRequired, SafeMessage: "Keychain access requires interactive authorization."),
        _ => new(CredentialStoreStatus.StoreUnavailableOrLocked, SafeMessage: $"Keychain lookup failed with OSStatus {status}.")
    };

    [DllImport("/System/Library/Frameworks/Security.framework/Security")]
    private static extern int SecKeychainFindGenericPassword(
        IntPtr keychainOrArray,
        uint serviceNameLength,
        byte[] serviceName,
        uint accountNameLength,
        byte[] accountName,
        out uint passwordLength,
        out IntPtr passwordData,
        out IntPtr itemRef);

    [DllImport("/System/Library/Frameworks/Security.framework/Security")]
    private static extern int SecKeychainItemFreeContent(IntPtr attributeList, IntPtr data);

    [DllImport("/System/Library/Frameworks/Security.framework/Security")]
    private static extern int SecKeychainGetUserInteractionAllowed([MarshalAs(UnmanagedType.I1)] out bool state);

    [DllImport("/System/Library/Frameworks/Security.framework/Security")]
    private static extern int SecKeychainSetUserInteractionAllowed([MarshalAs(UnmanagedType.I1)] bool state);
}
