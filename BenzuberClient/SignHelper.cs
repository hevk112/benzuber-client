using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Benzuber.Api.Models;

namespace Benzuber
{
    internal class SignHelper
    {
        private string _publicKey;
        private byte[] _privateKey;
        private readonly string _signPath;

        public SignHelper(Configuration configuration)
        {
            _signPath = Path.GetFullPath($"bz_signature_{configuration.StationId}.sig");
        }

        public bool LoadKey()
        {
            if (!File.Exists(_signPath))
                return false;

            _privateKey = File.ReadAllBytes(_signPath);
            using (var publicPrivate = new RSACryptoServiceProvider())
            {
                publicPrivate.ImportCspBlob(_privateKey);
                _publicKey = BitConverter.ToString(publicPrivate.ExportCspBlob(false)).Replace("-", "");
            }

            return !string.IsNullOrWhiteSpace(_publicKey);
        }

        public string CreateKey()
        {
            using (var publicPrivate = new RSACryptoServiceProvider())
            {
                File.WriteAllBytes(_signPath, publicPrivate.ExportCspBlob(true));
            }

            return LoadKey()
                ? _publicKey
                : null;
        }

        internal string SignData(string data) => SignData(Encoding.ASCII.GetBytes(data));

        internal static byte[] HexStringToByteArray(string input)
        {
            return Enumerable
                .Range(0, input.Length / 2)
                .Select(i => Convert.ToByte(input.Substring(i * 2, 2), 16))
                .ToArray();
        }

        internal string SignData(byte[] data)
        {
            using (var publicPrivate = new RSACryptoServiceProvider())
            {
                publicPrivate.ImportCspBlob(_privateKey);
                return BitConverter.ToString(publicPrivate.SignData(data, SHA1.Create())).Replace("-", "");
            }
        }
    }
}
