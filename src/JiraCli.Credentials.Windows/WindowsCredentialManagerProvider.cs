using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using JiraCli.Credentials;

namespace JiraCli.Credentials.Windows;

public sealed class WindowsCredentialManagerProvider : ICredentialStoreProvider
{
    private const int ErrorAccessDenied = 5;
    private const int ErrorNotFound = 1168;
    private const int ErrorNoSuchLogonSession = 1312;
    private const int ErrorCancelled = 1223;

    public string Name => "windows-credential-manager";

    public ValueTask<CredentialStoreResult> ReadAsync(
        CredentialStoreReference reference,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows())
        {
            return ValueTask.FromResult(new CredentialStoreResult(
                CredentialStoreStatus.UnsupportedPlatform,
                SafeMessage: "Windows Credential Manager is supported only on Windows."));
        }

        if (string.IsNullOrWhiteSpace(reference.Target))
        {
            return ValueTask.FromResult(new CredentialStoreResult(
                CredentialStoreStatus.InvalidReference,
                SafeMessage: "A Windows Credential Manager reference requires 'target'."));
        }

        if (!CredRead(reference.Target, 1, 0, out var credentialPointer))
        {
            var error = Marshal.GetLastWin32Error();
            return ValueTask.FromResult(MapError(error));
        }

        try
        {
            var credential = Marshal.PtrToStructure<NativeCredential>(credentialPointer);
            if (credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize == 0)
            {
                return ValueTask.FromResult(new CredentialStoreResult(
                    CredentialStoreStatus.ItemNotFound,
                    SafeMessage: $"Credential target '{reference.Target}' has no secret value."));
            }

            var bytes = new byte[credential.CredentialBlobSize];
            try
            {
                Marshal.Copy(credential.CredentialBlob, bytes, 0, bytes.Length);
                var text = LooksUtf16(bytes)
                    ? Encoding.Unicode.GetString(bytes)
                    : Encoding.UTF8.GetString(bytes);
                return ValueTask.FromResult(new CredentialStoreResult(
                    CredentialStoreStatus.Success,
                    SecretMaterial.FromString(text)));
            }
            finally
            {
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes);
            }
        }
        finally
        {
            CredFree(credentialPointer);
        }
    }

    private static bool LooksUtf16(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 2 || bytes.Length % 2 != 0)
        {
            return false;
        }

        var zeroHighBytes = 0;
        for (var index = 1; index < bytes.Length; index += 2)
        {
            if (bytes[index] == 0)
            {
                zeroHighBytes++;
            }
        }

        return zeroHighBytes >= Math.Max(1, bytes.Length / 4);
    }

    private static CredentialStoreResult MapError(int error) => error switch
    {
        ErrorNotFound => new(CredentialStoreStatus.ItemNotFound, SafeMessage: "The named credential was not found."),
        ErrorNoSuchLogonSession => new(CredentialStoreStatus.StoreUnavailableOrLocked, SafeMessage: "Credential Manager is unavailable for this logon session."),
        ErrorAccessDenied => new(CredentialStoreStatus.AccessDenied, SafeMessage: "Access to Credential Manager was denied."),
        ErrorCancelled => new(CredentialStoreStatus.InteractiveAuthorizationRequired, SafeMessage: "Credential access was cancelled or requires user authorization."),
        _ => new(CredentialStoreStatus.StoreUnavailableOrLocked, SafeMessage: $"Credential Manager failed with Windows error {error}: {new Win32Exception(error).Message}")
    };

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public uint Flags;
        public uint Type;
        public IntPtr TargetName;
        public IntPtr Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public IntPtr TargetAlias;
        public IntPtr UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr buffer);
}
