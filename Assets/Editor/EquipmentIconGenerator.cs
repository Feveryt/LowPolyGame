using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LowPolyGame.EditorTools
{
    // 装备图标与物品批量生成器：
    //   1. 为 PolygonDungeon 的武器/盾牌 prefab 渲染 256x256 图标 PNG（临时场景相机拍照）。
    //   2. 为每个装备创建 ItemDefinition 资产（含槽位、挂点参数、图标引用）。
    //   3. 把新物品追加进 ItemDefinitionCatalog。
    // 已存在的物品不会重复创建，但会刷新下表配置的物品字段。
    /// <summary>
    /// 编辑器装备生成工具：渲染图标、更新物品定义并补充目录引用。
    /// </summary>
    public static class EquipmentIconGenerator
    {
        const string IconsOutputFolder = "Assets/ArtRes/UI Res/Generated Icons";
        const string ItemsFolder = "Assets/GameData/Definitions/Items";
        const string CatalogPath = "Assets/Resources/ItemDefinitionCatalog.asset";

        // 装备清单：prefab 相对路径 → (物品ID, 中文名, 槽位, 挂点位置, 挂点欧拉, 挂点缩放)。
        // 武器参数遵循 Docs/持剑绑定规范与防回退说明.md；不同模型的刃面角度不可统一。
        static readonly (string prefab, int id, string displayName, EquipmentSlotType slot,
            Vector3 pos, Vector3 euler, float scale)[] Equipments =
        {
            ("Assets/ArtRes/PolygonDungeon/Prefabs/Weapons/SM_Wep_Crystal_Ornate_Straightsword_01.prefab",
                2000, "水晶长剑", EquipmentSlotType.Weapon, new Vector3(9f, 3.6f, 0f), new Vector3(0f, 90f, -90f), 100f),
            ("Assets/ArtRes/PolygonDungeon/Prefabs/Weapons/SM_Wep_Greatsword_Straight_01.prefab",
                2001, "巨剑", EquipmentSlotType.Weapon, new Vector3(9f, 3.6f, 0f), new Vector3(90f, 0f, 180f), 100f),
            ("Assets/ArtRes/PolygonDungeon/Prefabs/Weapons/SM_Wep_Ornate_Sword_01.prefab",
                2002, "华丽之剑", EquipmentSlotType.Weapon, new Vector3(9f, 3.6f, 0f), new Vector3(0f, 90f, -90f), 100f),
            ("Assets/ArtRes/PolygonDungeon/Prefabs/Weapons/SM_Wep_Cutlass_01.prefab",
                2003, "弯刀", EquipmentSlotType.Weapon, new Vector3(9f, 3.6f, 0f), new Vector3(0f, 90f, -90f), 100f),
            ("Assets/ArtRes/PolygonDungeon/Prefabs/Weapons/SM_Wep_HandAxe_01.prefab",
                2004, "手斧", EquipmentSlotType.Weapon, new Vector3(9f, 3.6f, 0f), new Vector3(0f, 90f, -90f), 100f),
            ("Assets/ArtRes/PolygonDungeon/Prefabs/Weapons/SM_Wep_Hammer_Small_01.prefab",
                2005, "短锤", EquipmentSlotType.Weapon, new Vector3(9f, 3.6f, 0f), new Vector3(-90f, 0f, 0f), 100f),
            ("Assets/ArtRes/PolygonDungeon/Prefabs/Weapons/SM_Wep_Spear_02.prefab",
                2006, "长矛", EquipmentSlotType.Weapon, new Vector3(9f, 3.6f, 0f), new Vector3(-90f, 0f, 0f), 100f),
            ("Assets/ArtRes/PolygonDungeon/Prefabs/Weapons/SM_Wep_Staff_Gem_01.prefab",
                2007, "宝石法杖", EquipmentSlotType.Weapon, new Vector3(9f, 3.6f, 0f), new Vector3(-90f, 0f, 0f), 100f),
            ("Assets/ArtRes/PolygonDungeon/Prefabs/Weapons/SM_Wep_Greatsword_Curved_01.prefab",
                2008, "曲刃大剑", EquipmentSlotType.Weapon, new Vector3(9f, 3.6f, 0f), new Vector3(0f, 90f, -90f), 100f),
            ("Assets/ArtRes/PolygonDungeon/Prefabs/Weapons/SM_Wep_Shield_Heater_01.prefab",
                2101, "鸢形盾", EquipmentSlotType.Shield, new Vector3(0f, 0f, 0f), new Vector3(0f, 180f, 0f), 100f),
            ("Assets/ArtRes/PolygonDungeon/Prefabs/Weapons/SM_Wep_Shield_Round_01.prefab",
                2102, "圆盾", EquipmentSlotType.Shield, new Vector3(0f, 0f, 0f), new Vector3(0f, 180f, 0f), 100f),
            ("Assets/ArtRes/PolygonDungeon/Prefabs/Weapons/SM_Wep_Shield_Ornate_01.prefab",
                2103, "华丽盾", EquipmentSlotType.Shield, new Vector3(0f, 0f, 0f), new Vector3(0f, 180f, 0f), 100f),
            ("Assets/ArtRes/PolygonDungeon/Prefabs/Weapons/SM_Wep_Shield_Skull_01.prefab",
                2104, "骷髅盾", EquipmentSlotType.Shield, new Vector3(0f, 0f, 0f), new Vector3(0f, 180f, 0f), 100f),
        };

        [MenuItem("Tools/Combat/8. Generate Equipment Icons + Items")]
        public static void GenerateAll()
        {
            if (!AssetDatabase.IsValidFolder(IconsOutputFolder))
                CreateFolderRecursive(IconsOutputFolder);
            if (!AssetDatabase.IsValidFolder(ItemsFolder))
                CreateFolderRecursive(ItemsFolder);

            int iconsCreated = 0, itemsCreated = 0;
            foreach (var entry in Equipments)
            {
                string iconPath = $"{IconsOutputFolder}/{System.IO.Path.GetFileNameWithoutExtension(entry.prefab)}.png";
                if (RenderIcon(entry.prefab, iconPath))
                    iconsCreated++;

                Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
                if (CreateOrUpdateItem(entry, icon))
                    itemsCreated++;
            }

            AssetDatabase.SaveAssets();

            // 把新物品追加进目录资产。
            int added = AppendToCatalog();
            Debug.Log($"[Icons] 生成图标 {iconsCreated} 张，新建物品 {itemsCreated} 个，目录追加 {added} 个。");
        }

        // ---- 图标渲染 ----
        static bool RenderIcon(string prefabPath, string outputPng)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[Icons] 找不到 prefab：{prefabPath}");
                return false;
            }

            GameObject sample = Object.Instantiate(prefab);
            sample.layer = LayerMask.NameToLayer("UI");

            // 计算包围盒并摆放相机。
            bool hasBounds = false;
            Bounds bounds = new Bounds();
            foreach (Renderer renderer in sample.GetComponentsInChildren<Renderer>(true))
            {
                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (!hasBounds)
            {
                Object.DestroyImmediate(sample);
                return false;
            }

            const int size = 256;
            GameObject cameraObject = new GameObject("IconCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.backgroundColor = new Color(0.13f, 0.12f, 0.11f, 1f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x) * 1.15f;
            camera.cullingMask = 1 << sample.layer;
            camera.transform.position = bounds.center + new Vector3(0f, 0f, -bounds.extents.z - 10f);
            camera.targetTexture = RenderTexture.GetTemporary(size, size, 24);

            // 打光：主光 + 补光。
            GameObject keyLightObject = new GameObject("KeyLight");
            keyLightObject.transform.position = bounds.center + new Vector3(-2f, 3f, -4f);
            keyLightObject.transform.LookAt(bounds.center);
            Light keyLight = keyLightObject.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.intensity = 1.1f;
            keyLight.cullingMask = 1 << sample.layer;

            GameObject fillLightObject = new GameObject("FillLight");
            fillLightObject.transform.position = bounds.center + new Vector3(3f, -1f, -2f);
            fillLightObject.transform.LookAt(bounds.center);
            Light fillLight = fillLightObject.AddComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.intensity = 0.4f;
            fillLight.cullingMask = 1 << sample.layer;

            camera.Render();

            RenderTexture.active = camera.targetTexture;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0f, 0f, size, size), 0, 0);
            texture.Apply();
            RenderTexture.active = null;
            camera.targetTexture.Release();

            System.IO.File.WriteAllBytes(outputPng, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(keyLightObject);
            Object.DestroyImmediate(fillLightObject);
            Object.DestroyImmediate(sample);

            AssetDatabase.ImportAsset(outputPng);
            TextureImporter importer = AssetImporter.GetAtPath(outputPng) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();
            }

            return true;
        }

        // ---- 物品资产 ----
        static bool CreateOrUpdateItem(
            (string prefab, int id, string displayName, EquipmentSlotType slot,
                Vector3 pos, Vector3 euler, float scale) entry, Sprite icon)
        {
            string assetPath = $"{ItemsFolder}/Equip_{entry.id}_{entry.slot}.asset";
            ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(assetPath);
            bool created = false;

            if (item == null)
            {
                item = ScriptableObject.CreateInstance<ItemDefinition>();
                AssetDatabase.CreateAsset(item, assetPath);
                created = true;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(entry.prefab);
            SerializedObject so = new SerializedObject(item);
            SetInt(so, "id", entry.id);
            SetString(so, "displayName", entry.displayName);
            SetObject(so, "icon", icon);
            SetInt(so, "category", (int)ItemCategory.Equipment);
            SetInt(so, "maxStack", 1);
            SetInt(so, "equipmentSlot", (int)entry.slot);
            SetObject(so, "equipmentPrefab", prefab);
            SetVector3(so, "equipmentPosition", entry.pos);
            SetVector3(so, "equipmentEuler", entry.euler);
            SetFloat(so, "equipmentScale", entry.scale);
            SetString(so, "description", $"{entry.displayName}：可装备到{(entry.slot == EquipmentSlotType.Weapon ? "主手武器" : "副手盾牌")}槽位。");
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
            return created;
        }

        // 把所有装备物品追加进目录资产（按 id 去重）。
        static int AppendToCatalog()
        {
            ItemDefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<ItemDefinitionCatalog>(CatalogPath);
            if (catalog == null)
            {
                Debug.LogError($"[Icons] 找不到物品目录：{CatalogPath}");
                return 0;
            }

            SerializedObject so = new SerializedObject(catalog);
            SerializedProperty items = so.FindProperty("items");
            if (items == null)
                return 0;

            var knownIds = new HashSet<int>();
            for (int i = 0; i < items.arraySize; i++)
            {
                var item = items.GetArrayElementAtIndex(i).objectReferenceValue as ItemDefinition;
                if (item != null)
                    knownIds.Add(item.Id);
            }

            int added = 0;
            foreach (var entry in Equipments)
            {
                if (knownIds.Contains(entry.id))
                    continue;

                string assetPath = $"{ItemsFolder}/Equip_{entry.id}_{entry.slot}.asset";
                ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(assetPath);
                if (item == null)
                    continue;

                items.arraySize++;
                items.GetArrayElementAtIndex(items.arraySize - 1).objectReferenceValue = item;
                added++;
            }

            if (added > 0)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(catalog);
            }

            return added;
        }

        // ---- 工具 ----
        static void CreateFolderRecursive(string folderPath)
        {
            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        static void SetInt(SerializedObject so, string name, int value)
        {
            SerializedProperty prop = so.FindProperty(name);
            if (prop != null)
                prop.intValue = value;
        }

        static void SetFloat(SerializedObject so, string name, float value)
        {
            SerializedProperty prop = so.FindProperty(name);
            if (prop != null)
                prop.floatValue = value;
        }

        static void SetString(SerializedObject so, string name, string value)
        {
            SerializedProperty prop = so.FindProperty(name);
            if (prop != null)
                prop.stringValue = value;
        }

        static void SetObject(SerializedObject so, string name, Object value)
        {
            SerializedProperty prop = so.FindProperty(name);
            if (prop != null)
                prop.objectReferenceValue = value;
        }

        static void SetVector3(SerializedObject so, string name, Vector3 value)
        {
            SerializedProperty prop = so.FindProperty(name);
            if (prop != null)
            {
                prop.vector3Value = value;
            }
        }
    }
}
