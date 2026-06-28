namespace SheraBoard.Core.Security;

public interface IDataProtector
{
    byte[] Protect(byte[] bytes);

    byte[] Unprotect(byte[] protectedBytes);
}

