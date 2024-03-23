// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.Rexl.Kernel;

/// <summary>
/// Signature handler for creating signatures.
/// REVIEW: We should also verify incoming signatures. Currently this just generates
/// outgoing signature values.
/// </summary>
public sealed class SignatureHandler
{
    private readonly Encoding _enc;
    private readonly HMAC _gen;
    private readonly object _lock;

    public SignatureHandler(string key, string algorithm)
    {
        _enc = new UTF8Encoding();

        _gen = HMAC.Create(algorithm.Replace("-", "").ToUpperInvariant());
        _gen.Key = _enc.GetBytes(key);
        _lock = new object();
    }

    public Encoding Enc { get { return _enc; } }

    public string CreateSignature(params byte[][] data)
    {
        // This is stateful so we need to lock.
        lock (_lock)
        {
            _gen.Initialize();

            foreach (byte[] item in data)
                _gen.TransformBlock(item, 0, item.Length, null, 0);

            _gen.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

            // Convert the hash, remove '-', and map to lower case.
            return BitConverter.ToString(_gen.Hash).Replace("-", "").ToLowerInvariant();
        }
    }
}
