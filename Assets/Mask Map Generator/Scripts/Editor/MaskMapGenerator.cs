using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Utils
{
    using Color = UnityEngine.Color;
    using FontStyle = UnityEngine.FontStyle;

    public class Log
    {
        public string Message;
        public Color Color;

        public void SetMessage(string message, Color color)
        {
            Message = message;
            Color = color;
        }
    }

    public class MaskMapGenerator : EditorWindow
    {
        private static bool textureInputFoldoutEnabled = true;
        private static bool metadataInputFoldoutEnabled = true;
        private static bool manualSize = false;
        private static Texture2D metallicMap;
        private static Texture2D aoMap;
        private static Texture2D detailMap;
        private static Texture2D smoothnessMap;
        private static Texture2D maskMap;
        private static int width = 0;
        private static int height = 0;
        private static string fileName = string.Empty;
        private static Log log;

        private static Object[] objectsOnStart = null;

        [MenuItem("Window/Mask Map Generator")]
        public static void ShowWindow()
        {
            log = new Log() { Color = Color.black };

            objectsOnStart = Selection.objects;
            foreach (Object o in objectsOnStart)
            {
                string nameToLower = o.name.ToLower();

                if (nameToLower.Contains("metallic"))
                {
                    metallicMap = (Texture2D) o;
                }
                else if (nameToLower.Contains("occlusion") || nameToLower.Contains("_ao") || nameToLower.Contains(" ao"))
                {
                    aoMap = (Texture2D) o;
                }
                else if (nameToLower.Contains("detail"))
                {
                    detailMap = (Texture2D) o;
                }
                else if (nameToLower.Contains("smoothness"))
                {
                    smoothnessMap = (Texture2D) o;
                }
            }

            textureInputFoldoutEnabled = true;
            metadataInputFoldoutEnabled = true;
            manualSize = true;

            if (width == 0)
                width = 1024;
            if (height == 0)
                height = 1024;



            GetWindowWithRect<MaskMapGenerator>(new Rect(0, 0, 600, 420), true, "Mask Map Generator");
            GetWindow<MaskMapGenerator>();
        }

        private void OnGUI()
        {
            #region Texture Data

            textureInputFoldoutEnabled =
                EditorGUILayout.BeginFoldoutHeaderGroup(textureInputFoldoutEnabled, "Texture Input");

            if (textureInputFoldoutEnabled)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(20f);
                metallicMap = ShowTextureField("Metallic Map", metallicMap);
                aoMap = ShowTextureField("AO Map", aoMap);
                detailMap = ShowTextureField("Detail Map", detailMap);
                smoothnessMap = ShowTextureField("Smoothness Map", smoothnessMap);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();

            #endregion Texture Data

            GUILayout.Space(5f);

            #region Metadata

            metadataInputFoldoutEnabled =
                EditorGUILayout.BeginFoldoutHeaderGroup(metadataInputFoldoutEnabled, "Metadata Input");

            if (metadataInputFoldoutEnabled)
            {
                EditorGUILayout.BeginHorizontal();

                manualSize = EditorGUILayout.BeginToggleGroup("Manual Resize", manualSize);
                if (manualSize)
                {
                    EditorGUILayout.LabelField("Width", new GUIStyle() { fontStyle = FontStyle.Bold });
                    width = EditorGUILayout.IntField(width);
                    EditorGUILayout.Space();
                    EditorGUILayout.Space();
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Height", new GUIStyle() { fontStyle = FontStyle.Bold });
                    height = EditorGUILayout.IntField(height);
                }
                EditorGUILayout.EndToggleGroup();

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                fileName = EditorGUILayout.TextField("Export filename", fileName);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();

            #endregion Metadata

            GUILayout.Space(5f);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Generate"))
            {
                if (!CanGenerate()) return;

                string path = EditorUtility.OpenFolderPanel("Save As", Application.dataPath, "");
                GenerateMaskMap(path);
                Debug.Log(path);
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(25f);

            if (!string.IsNullOrEmpty(log.Message))
            {
                EditorGUILayout.LabelField(log.Message, new GUIStyle()
                {
                    fontStyle = FontStyle.Bold,
                    normal = new GUIStyleState() { textColor = log.Color }
                });
            }
        }

        private static Texture2D ShowTextureField(string name, Texture2D texture)
        {
            EditorGUILayout.BeginVertical();

            GUIStyle style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, fixedWidth = 100 };
            GUILayout.Label(name, style);

            Texture2D tex = (Texture2D)EditorGUILayout.ObjectField(texture, typeof(Texture2D), false, GUILayout.Width(70), GUILayout.Height(70));

            EditorGUILayout.EndVertical();

            return tex;
        }

        private static void GenerateMaskMap(string path)
        {
            bool metallicPendingForReadableEdit = metallicMap != null && !metallicMap.isReadable;
            bool aoPendingForReadableEdit = aoMap != null && !aoMap.isReadable;
            bool detailPendingForReadableEdit = detailMap != null && !detailMap.isReadable;
            bool smoothnessPendingForReadableEdit = smoothnessMap != null && !smoothnessMap.isReadable;

            if (metallicPendingForReadableEdit || aoPendingForReadableEdit ||
                detailPendingForReadableEdit || smoothnessPendingForReadableEdit)
            {
                string metallicName = metallicPendingForReadableEdit ? metallicMap.name  + "\n" : "";
                string aoName = aoPendingForReadableEdit ? aoMap.name + "\n" : "";
                string detailName = detailPendingForReadableEdit ? detailMap.name + "\n" : "";
                string smoothnessName = smoothnessPendingForReadableEdit ? smoothnessMap.name : "";
                string dialogMessage = $"These textures will be set to Read/Write Enabled\n" +
                                       $"{metallicName}{aoName}{detailName}{smoothnessName}";

                if (EditorUtility.DisplayDialog("Warning", dialogMessage, "Confirm", "Cancel"))
                {
                    Texture2D[] textures = new Texture2D[]{metallicMap, aoMap, detailMap, smoothnessMap};
                    bool[] pendingForEdit = new bool[]
                    {
                        metallicPendingForReadableEdit, aoPendingForReadableEdit,
                        detailPendingForReadableEdit, smoothnessPendingForReadableEdit
                    };

                    for (int i = 0; i < pendingForEdit.Length; i++)
                    {
                        if (pendingForEdit[i])
                        {
                            string texturePath = AssetDatabase.GetAssetPath(textures[i]);
                            TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;

                            if (importer != null)
                            {
                                importer.textureType = TextureImporterType.Default;
                                importer.isReadable = true;

                                AssetDatabase.ImportAsset(texturePath);
                                AssetDatabase.Refresh();
                            }
                        }
                    }
                }
            }

            maskMap = new Texture2D(width, height/*, DefaultFormat.LDR, TextureCreationFlags.None*/);

            Color colorCache = Color.black;
            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    colorCache.r = metallicMap != null ? metallicMap.GetPixel(i, j).r : 0;
                    colorCache.g = aoMap != null ? aoMap.GetPixel(i, j).g : 0;
                    colorCache.b = detailMap != null ? detailMap.GetPixel(i, j).b : 0;
                    colorCache.a = smoothnessMap != null ? smoothnessMap.GetPixel(i, j).a : 0;
                    maskMap.SetPixel(i, j, colorCache);
                }
            }

            byte[] bytes = maskMap.EncodeToPNG();

            using (Image image = Image.FromStream(new MemoryStream(bytes)))
            {
                image.Save(path + Path.DirectorySeparatorChar + fileName + ".png", ImageFormat.Png);
                log.SetMessage($"{fileName}.png successfully generated", Color.black);
            }

            AssetDatabase.Refresh();
        }

        private static bool CanGenerate()
        {
            if (metallicMap == null && aoMap == null && detailMap == null && smoothnessMap == null)
            {
                log.SetMessage("No textures set", Color.red);
                return false;
            }

            if (width == 0 || height == 0)
            {
                log.SetMessage("Width or Height is 0", Color.red);
                return false;
            }

            if (string.IsNullOrEmpty(fileName))
            {
                log.SetMessage("Filename can't be blank", Color.red);
                return false;
            }

            return true;
        }
    }
}