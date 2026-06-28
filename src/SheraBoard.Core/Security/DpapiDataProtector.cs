using System.Security.Cryptography;
using System.Text;

namespace SheraBoard.Core.Security;

public sealed class DpapiDataProtector : IDataProtector
{
    private readonly byte[] _entropy;

    public DpapiDataProtector(string purpose)
    {
        _entropy = Encoding.UTF8.GetBytes(purpose);
    }

    public byte[] Protect(byte[] bytes)
    {
        return ProtectedData.Protect(bytes, _entropy, DataProtectionScope.CurrentUser);
    }

    public byte[] Unprotect(byte[] protectedBytes)
    {
        return ProtectedData.Unprotect(protectedBytes, _entropy, DataProtectionScope.CurrentUser);
    }
}

