#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

public class ShaderVariantStripper : IPreprocessShaders
{
    static readonly ShaderKeyword kLightCookies = new ShaderKeyword("_LIGHT_COOKIES");
    static readonly ShaderKeyword kForwardPlus = new ShaderKeyword("_FORWARD_PLUS");

    public int callbackOrder => 0;

    public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
    {
        for (int i = data.Count - 1; i >= 0; i--)
        {
            if (data[i].shaderKeywordSet.IsEnabled(kLightCookies) ||
                data[i].shaderKeywordSet.IsEnabled(kForwardPlus))
            {
                data.RemoveAt(i);
            }
        }
    }
}
#endif