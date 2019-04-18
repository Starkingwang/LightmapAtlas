using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
[CustomEditor(typeof(LightmapAtlas))]
public class LightmapAtlasEditor : Editor
{
    static int s_AtlasSize = 1024;

    public LightmapAtlas LMAtlas
    {
        get => target as LightmapAtlas;
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (GUILayout.Button("保存当前场景Lightmap"))
        {
            SaveCurrentLightmap();
        }

        if (GUILayout.Button("组合Lightmap"))
        {
            SavePackedLightmap();
        }
    }

    void SaveCurrentLightmap()
    {
        var group = ExtractMapFromLightmappingSetting();

        if (group != null)
        {
            Undo.RecordObject(target, "Save Lightmap");
            EditorUtility.SetDirty(target);
            LMAtlas.lightmapGroups.Add(group);
        }
    }


    void SavePackedLightmap()
    {
        var group = PackLightmaps();

        if (group != null)
        {
            Undo.RecordObject(target, "Save Paked Lightmap");
            EditorUtility.SetDirty(target);
            LMAtlas.packedLightmap = group;
        }
    }

    //创建一个lightmap组
    LightmapGroup ExtractMapFromLightmappingSetting()
    {
        var lightmaps = LightmapSettings.lightmaps;

        if (lightmaps.Length < 1)
        {
            return null;
        }

        LightmapGroup lightmapGroup = new LightmapGroup
        {
            maps = new LightmapPack[lightmaps.Length]
        };

        for (int i = 0; i < lightmaps.Length; i++)
        {
            lightmapGroup.maps[i] = new LightmapPack
            {
                color = lightmaps[i].lightmapColor,
                dir = lightmaps[i].lightmapDir,
                mask = lightmaps[i].shadowMask
            };
        }

        if (SaveLightmapTextures(ref lightmapGroup))
        {
            return lightmapGroup;
        }

        return null;
    }

    //组合lightmap
    LightmapGroup PackLightmaps()
    {
        var folder = EditorUtility.OpenFolderPanel("Folder", "Assets", "");

        if (string.IsNullOrEmpty(folder))
        {
            return null;
        }

        folder = FileUtil.GetProjectRelativePath(folder);

        Debug.Log(folder);

        var groups = LMAtlas.lightmapGroups;

        if (groups.Count == 0)
        {
            return null;
        }
        else if (groups.Count == 1)
        {
            return groups[0];
        }

        int lmGroupCount = groups.Count;
        int mapIdCount = groups[0].maps.Length;
        var packColor = mapIdCount > 0 && groups[0].maps[0].color != null;
        var packDir = mapIdCount > 0 && groups[0].maps[0].dir != null;
        var packMask = mapIdCount > 0 && groups[0].maps[0].mask != null;

        //把多套lightmap对应idx的纹理归类, key 为对应lightmap idx，value 为 多组lightmap对应id的纹理
        Dictionary<int, Texture2D[]> colors = new Dictionary<int, Texture2D[]>();
        Dictionary<int, Texture2D[]> dirs = new Dictionary<int, Texture2D[]>();
        Dictionary<int, Texture2D[]> masks = new Dictionary<int, Texture2D[]>();

        for (int grounpID = 0; grounpID < lmGroupCount; grounpID++)
        {
            for (int lmIdx = 0; lmIdx < mapIdCount; lmIdx++)
            {
                if (packColor)
                {
                    if (!colors.ContainsKey(lmIdx))
                    {
                        colors.Add(lmIdx, new Texture2D[lmGroupCount]);
                    }
                    colors[lmIdx][grounpID] = groups[grounpID].maps[lmIdx].color;
                }

                if (packDir)
                {
                    if (!dirs.ContainsKey(lmIdx))
                    {
                        dirs.Add(lmIdx, new Texture2D[lmGroupCount]);
                    }
                    dirs[lmIdx][grounpID] = groups[grounpID].maps[lmIdx].dir;
                }

                if (packMask)
                {
                    if (!masks.ContainsKey(lmIdx))
                    {
                        masks.Add(lmIdx, new Texture2D[lmGroupCount]);
                    }
                    masks[lmIdx][grounpID] = groups[grounpID].maps[lmIdx].mask;
                }
            }
        }

        //每一个ID对应一组合并后的纹理
        Texture2D[] packedColors = new Texture2D[mapIdCount];
        Texture2D[] packedDirs = new Texture2D[mapIdCount];
        Texture2D[] packedMasks = new Texture2D[mapIdCount];

        if (packColor)
        {
            for (int i = 0; i < mapIdCount; i++)
            {
                packedColors[i] = new Texture2D(s_AtlasSize, s_AtlasSize, TextureFormat.RGBAHalf, true, true);
                packedColors[i].PackTextures(colors[i], 0, s_AtlasSize);
                var tex = new Texture2D(packedColors[i].width, packedColors[i].height, TextureFormat.RGBAHalf, true, true);
                if (PlayerSettings.colorSpace == ColorSpace.Gamma)
                {
                    for (int y = 0; y < tex.height; y++)
                    {
                        for (int x = 0; x < tex.width; x++)
                        {
                            tex.SetPixel(x, y, packedColors[i].GetPixel(x, y).linear);
                        }
                    }
                }
                else
                {
                    tex.SetPixels(packedColors[i].GetPixels());
                }
                tex.Apply();
                SaveLightmapTexture(ref tex, folder + "/Lightmap_" + i + "_color.exr");
                DestroyImmediate(packedColors[i]);
                packedColors[i] = tex;
            }
        }

        if (packDir)
        {
            for (int i = 0; i < mapIdCount; i++)
            {
                packedDirs[i] = new Texture2D(s_AtlasSize, s_AtlasSize, TextureFormat.RGBA32, true);
                packedDirs[i].PackTextures(dirs[i], 0, s_AtlasSize);
                var tex = new Texture2D(packedDirs[i].width, packedDirs[i].height, TextureFormat.RGBA32, true);
                tex.SetPixels(packedDirs[i].GetPixels());
                tex.Apply();
                SaveLightmapTexture(ref tex, folder + "/Lightmap_" + i + "_dir.png");
                DestroyImmediate(packedDirs[i]);
                packedDirs[i] = tex;
            }
        }

        if (packMask)
        {
            for (int i = 0; i < mapIdCount; i++)
            {
                packedMasks[i] = new Texture2D(s_AtlasSize, s_AtlasSize, TextureFormat.RGBA32, true);
                packedMasks[i].PackTextures(masks[i], 0, s_AtlasSize);
                var tex = new Texture2D(packedMasks[i].width, packedMasks[i].height, TextureFormat.RGBA32, true);
                tex.SetPixels(packedMasks[i].GetPixels());
                tex.Apply();
                SaveLightmapTexture(ref tex, folder + "/Lightmap_" + i + "_mask.png");
                DestroyImmediate(packedMasks[i]);
                packedMasks[i] = tex;
            }
        }

        LightmapGroup packedMap = new LightmapGroup();
        packedMap.maps = new LightmapPack[mapIdCount];
        for (int i = 0; i < mapIdCount; i++)
        {
            packedMap.maps[i] = new LightmapPack
            {
                color = packedColors[i],
                dir = packedDirs[i],
                mask = packedMasks[i]
            };
        }

        return packedMap;
    }

    //保存当前的lightmap
    bool SaveLightmapTextures(ref LightmapGroup group)
    {
        var folder = EditorUtility.OpenFolderPanel("Folder", "Assets", "");

        if (string.IsNullOrEmpty(folder))
        {
            return false;
        }

        folder = FileUtil.GetProjectRelativePath(folder);

        Debug.Log(folder);

        for (int i = 0; i < group.maps.Length; i++)
        {
            if (group.maps[i].color)
            {
                var map = CopyTexture(group.maps[i].color, LightmapType.Color);
                SaveLightmapTexture(ref map, folder + "/" + map.name + ".exr");
                SetTextureAsReadableFormat(map, LightmapType.Color);
                group.maps[i].color = map;
            }

            if (group.maps[i].dir)
            {
                var map = CopyTexture(group.maps[i].dir, LightmapType.Dir);
                SaveLightmapTexture(ref map, folder + "/" + map.name + ".png");
                SetTextureAsReadableFormat(map, LightmapType.Dir);
                group.maps[i].dir = map;
            }

            if (group.maps[i].mask)
            {
                var map = CopyTexture(group.maps[i].mask, LightmapType.Dir);
                SaveLightmapTexture(ref map, folder + "/" + map.name + ".png");
                SetTextureAsReadableFormat(map, LightmapType.Dir);
                group.maps[i].mask = map;
            }
        }

        return true;
    }

    //复制一份lightmap
    Texture2D CopyTexture(Texture2D baseTexture, LightmapType type)
    {
        SetTextureAsReadableFormat(baseTexture, type);

        var texDst = new Texture2D(
            baseTexture.width, 
            baseTexture.height,
            baseTexture.format, 
            true, type == LightmapType.Color);

        texDst.name = baseTexture.name;
        if (type == LightmapType.Color && PlayerSettings.colorSpace == ColorSpace.Gamma)
        {
            for (int y = 0; y < texDst.height; y++)
            {
                for (int x = 0; x < texDst.width; x++)
                {
                    texDst.SetPixel(x, y, baseTexture.GetPixel(x, y).linear);
                }
            }
        }
        else
        {
            texDst.SetPixels(baseTexture.GetPixels());
        }
        texDst.Apply();
        return texDst;
    }

    //保存复制的lightmap到特定位置
    void SaveLightmapTexture(ref Texture2D texture, string path)
    {
        try
        {
            System.IO.File.WriteAllBytes(path, 
                texture.format == TextureFormat.RGBAHalf ? texture.EncodeToEXR() : texture.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        catch (Exception x)
        {
            Debug.LogError(x.Message);
        }
    }

    //强制来源lightmap可读写
    void SetTextureAsReadableFormat(Texture2D texture, LightmapType type)
    {
        var targetFormat = type == LightmapType.Color ? TextureImporterFormat.RGBAHalf : TextureImporterFormat.RGBA32;

        var path = AssetDatabase.GetAssetPath(texture);

        var importer = AssetImporter.GetAtPath(path) as TextureImporter;

        bool needReimport = false;

        if (!importer.isReadable)
        {
            needReimport = true;
            importer.isReadable = true;
        }

        if (importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            needReimport = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }

        var pf = importer.GetPlatformTextureSettings(
#if UNITY_STANDALONE
            "Standalone"
#elif UNITY_IOS
            "iOS"
#elif UNITY_ANDROID
            "Android"
#else
            "Standalone"
#endif
            );

        if (pf.format != targetFormat)
        {
            needReimport = true;
            pf.overridden = true;
            pf.format = targetFormat;        
            importer.SetPlatformTextureSettings(pf);
        }

        if (importer.sRGBTexture)
        {
            needReimport = true;
            importer.sRGBTexture = false;
        }

        if (needReimport)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
    }
}

#endif

[Serializable]
public class LightmapPack
{
    public Texture2D color, dir, mask;
}

[Serializable]
public class LightmapGroup
{
    //多个id情况下会有多组
    public LightmapPack[] maps;
}

public enum LightmapType
{
    Color,
    Dir,
    ShadowMask,
}

public class LightmapAtlas : MonoBehaviour
{
    public LightmapGroup packedLightmap;
    public List<LightmapGroup> lightmapGroups = new List<LightmapGroup>();

    private void Awake()
    {
        if (packedLightmap != null)
        {
            var datas = new LightmapData[packedLightmap.maps.Length];

            for (int i = 0; i < datas.Length; i++)
            {
                datas[i] = new LightmapData()
                {
                    lightmapColor = packedLightmap.maps[i].color,
                    lightmapDir = packedLightmap.maps[i].dir,
                    shadowMask = packedLightmap.maps[i].mask
                };
            }

            LightmapSettings.lightmaps = datas;          
        }
    }

    private void OnEnable()
    {
        Shader.EnableKeyword("_LMATLAS");
    }

    private void OnDisable()
    {
        Shader.DisableKeyword("_LMATLAS");
    }
}
