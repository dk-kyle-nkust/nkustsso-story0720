// 依既有天方格式核對來源密碼，不記錄明文密碼。
using System.Security.Cryptography;
using System.Text;

namespace NkustSso.Server.Services;

/// <summary>相容 ForgotPassword/TienFangCrypto 的 usr_mdpwd；僅驗證，不提供加密寫回或解密輸出介面。</summary>
public static class TienFangPasswordVerifier
{
    public static bool Verify(string? encoded, string? password)
    {
        if (string.IsNullOrEmpty(encoded) || string.IsNullOrEmpty(password) ||
            encoded.Length > 257 || password.Length > 256)
        {
            return false;
        }
        // 參考演算法只正確支援 ASCII；拒絕替換字元，避免非 ASCII 字元被當成問號接受。
        if (encoded.Any(c => c < 32 || c > 126) || password.Any(c => c < 32 || c > 126))
        {
            return false;
        }

        var body = (encoded.Length == 20 ? encoded : encoded[1..]).ToCharArray();
        byte[]? expected = null;
        byte[]? supplied = null;
        byte[]? plainBytes = null;
        byte[]? inputBytes = null;
        try
        {
            if (encoded.Length != 20)
            {
                var position = encoded[0] - 'a';
                if (position < 1 || position > body.Length)
                {
                    return false;
                }

                var endpoint = position % 2 == 1 ? 0 : body.Length - 1;
                (body[endpoint], body[position - 1]) = (body[position - 1], body[endpoint]);
            }
            for (var i = 0; i < body.Length; i++)
            {
                var c = body[i];
                // 數字、大寫、小寫各自在自己的字元區間內位移；其餘字元不動。
                var lower = '\0';
                if (c >= '0' && c <= '9')
                {
                    lower = '0';
                }
                else if (c >= 'A' && c <= 'Z')
                {
                    lower = 'A';
                }
                else if (c >= 'a' && c <= 'z')
                {
                    lower = 'a';
                }

                if (lower == '\0')
                {
                    continue;
                }

                var size = lower == '0' ? 10 : 26;
                body[i] = (char)(lower + ((c - lower - (i + 1) % size + size) % size));
            }
            // 雜湊只用於固定長度的記憶體比對，不保存，也不是來源密碼的儲存格式。
            plainBytes = Encoding.ASCII.GetBytes(body);
            inputBytes = Encoding.ASCII.GetBytes(password);
            expected = SHA256.HashData(plainBytes);
            supplied = SHA256.HashData(inputBytes);
            return CryptographicOperations.FixedTimeEquals(expected, supplied);
        }
        finally
        {
            Array.Clear(body);
            if (expected is not null)
            {
                CryptographicOperations.ZeroMemory(expected);
            }

            if (supplied is not null)
            {
                CryptographicOperations.ZeroMemory(supplied);
            }

            if (plainBytes is not null)
            {
                CryptographicOperations.ZeroMemory(plainBytes);
            }

            if (inputBytes is not null)
            {
                CryptographicOperations.ZeroMemory(inputBytes);
            }
        }
    }
}
