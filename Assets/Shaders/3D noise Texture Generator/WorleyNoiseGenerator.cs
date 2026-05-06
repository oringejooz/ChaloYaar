using UnityEngine;
using UnityEditor;

public class WorleyNoiseGenerator : MonoBehaviour
{
    [Header("Settings")]
    public ComputeShader computeShader;
    public int textureSize = 64;
    public int seed = 42;
    public string savePath = "Assets/Textures/WorleyNoise3D.asset";

    [ContextMenu("Generate")]
    public void Generate()
    {
        // Create the 3D render texture for the compute shader to write into
        RenderTexture rt = new RenderTexture(textureSize, textureSize, 0);
        rt.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
        rt.volumeDepth = textureSize;
        rt.enableRandomWrite = true;
        rt.format = RenderTextureFormat.ARGB32;
        rt.wrapMode = TextureWrapMode.Repeat;
        rt.filterMode = FilterMode.Trilinear;
        rt.Create();

        // Run the compute shader
        int kernel = computeShader.FindKernel("GenerateWorley");
        computeShader.SetTexture(kernel, "Result", rt);
        computeShader.SetInt("_Size", textureSize);
        computeShader.SetInt("_Seed", seed);

        int threadGroups = Mathf.CeilToInt(textureSize / 8.0f);
        computeShader.Dispatch(kernel, threadGroups, threadGroups, threadGroups);

        // Read back from GPU and build a Texture3D asset
        Texture3D texture = ConvertToTexture3D(rt);
        rt.Release();

        // Save as an asset
#if UNITY_EDITOR
        AssetDatabase.CreateAsset(texture, savePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Saved 3D Worley noise to {savePath}");
#endif
    }

    Texture3D ConvertToTexture3D(RenderTexture rt)
    {
        Texture3D texture = new Texture3D(textureSize, textureSize, textureSize, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Trilinear;

        // Read each Z slice from the RenderTexture
        RenderTexture slice = new RenderTexture(textureSize, textureSize, 0, RenderTextureFormat.ARGB32);
        slice.dimension = UnityEngine.Rendering.TextureDimension.Tex2D;
        slice.enableRandomWrite = true;
        slice.Create();

        Texture2D temp = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        Color[] allColors = new Color[textureSize * textureSize * textureSize];

        for (int z = 0; z < textureSize; z++)
        {
            Graphics.CopyTexture(rt, z, 0, slice, 0, 0);
            RenderTexture.active = slice;
            temp.ReadPixels(new Rect(0, 0, textureSize, textureSize), 0, 0);
            temp.Apply();

            Color[] sliceColors = temp.GetPixels();
            sliceColors.CopyTo(allColors, z * textureSize * textureSize);
        }

        texture.SetPixels(allColors);
        texture.Apply();

        RenderTexture.active = null;
        slice.Release();
        DestroyImmediate(temp);

        return texture;
    }
}