using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace ProjectSurvive.Data.Validator
{
    /// <summary>
    /// The config hash: SHA-256 over the generated table bytes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Algorithm, fixed by tools/gen.ps1 (which writes it into
    /// <c>ConfigBuildMetadata.g.cs</c>): for each table, in ordinal order of table
    /// name, feed UTF-8(table name) + a NUL byte + the raw <c>.bytes</c> content;
    /// then hash. The hashed name is the loader key Luban generates
    /// (<c>tbitem</c> - the dictionary key here), not the file name
    /// (<c>tbitem.bytes</c>), so the hash changes whenever the balance data or
    /// the schema changes - exactly what a bug report needs to identify a build.
    /// </para>
    /// <para>
    /// This implementation and the PowerShell one must agree; a test
    /// (<c>ConfigMetadataTests</c>) recomputes the hash from
    /// <c>lib/Tests~/data</c> and compares it with the generated constant.
    /// </para>
    /// </remarks>
    public static class ConfigHash
    {
        public static string Compute(IReadOnlyDictionary<string, byte[]> tables)
        {
            if (tables is null)
            {
                throw new ArgumentNullException(nameof(tables));
            }

            var names = new List<string>(tables.Keys);
            names.Sort(StringComparer.Ordinal);

            using var sha = SHA256.Create();
            var buffer = new List<byte>(64 * 1024);

            foreach (string name in names)
            {
                buffer.AddRange(Encoding.UTF8.GetBytes(name));
                buffer.Add(0);
                buffer.AddRange(tables[name]);
            }

            byte[] digest = sha.ComputeHash(buffer.ToArray());
            var hex = new StringBuilder(digest.Length * 2);
            foreach (byte b in digest)
            {
                hex.Append(b.ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
            }

            return hex.ToString();
        }
    }
}
