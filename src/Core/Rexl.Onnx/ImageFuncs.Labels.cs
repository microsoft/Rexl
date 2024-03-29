// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;

using Microsoft.Rexl;
using Microsoft.Rexl.Private;

namespace Microsoft.Rexl.Onnx;

public static class ImageNetLabels
{
    private const string k_pathLabels = @"Onnx/image_net_labels.txt";

    /// <summary>
    /// A tensor of text containing the labels.
    /// </summary>
    public static readonly Tensor<string> Labels = MakeLabels();

    /// <summary>
    /// Makes the labels tensor for image-net models.
    /// </summary>
    private static Tensor<string> MakeLabels()
    {
        string[] labs;
        try
        {
            string path = OnnxModel.GetFullPath(typeof(ImageNetFunc), k_pathLabels);
            labs = File.ReadAllLines(path);
        }
        catch
        {
            labs = Array.Empty<string>();
        }

        // If we don't get the expected number, fill in the extras.
        // REVIEW: Seems rather crude. Is there a better behavior?
        const int num = 1000;
        if (labs.Length < num)
        {
            int have = labs.Length;
            Array.Resize(ref labs, num);
            for (int i = have; i < labs.Length; i++)
                labs[i] = $"Label_{i:04}";
        }

        Validation.Assert(labs.Length >= num);
        return Tensor<string>.CreateFrom(labs, num);
    }
}
