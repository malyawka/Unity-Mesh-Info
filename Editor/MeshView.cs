using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UObject = UnityEngine.Object;


namespace MeshExtensions.Editor
{
    public class MeshView : IDisposable
    {
        public static class Styles
        {
            public static readonly GUIContent wireframeToggle = EditorGUIUtility.TrTextContent("Wireframe", "Show wireframe");
            public static GUIContent displayModeDropdown = EditorGUIUtility.TrTextContent("", "Change display mode");
            public static GUIContent uvChannelDropdown = EditorGUIUtility.TrTextContent("", "Change active UV channel");

            public static GUIStyle preSlider = "preSlider";
            public static GUIStyle preSliderThumb = "preSliderThumb";
        }

        public class Settings : IDisposable
        {
            public HandleMode HandleMode
            {
                get => (HandleMode)EditorPrefs.GetInt("mesh-info-handle-mode", 0);
                set => EditorPrefs.SetInt("mesh-info-handle-mode", (int)value);
            }
            
            public float HandleScale
            {
                get => EditorPrefs.GetFloat("mesh-info-handle-scale", 0.5f);
                set => EditorPrefs.SetFloat("mesh-info-handle-scale", value);
            }
            
            public bool DrawWire
            {
                get => EditorPrefs.GetBool("mesh-info-draw-wire", true);
                set => EditorPrefs.SetBool("mesh-info-draw-wire", value);
            }
            
            public DisplayMode displayMode = DisplayMode.Shaded;
            public int activeUVChannel = 0;
            
            public Vector3 orthoPosition = new Vector3(0.0f, 0.0f, 0.0f);
            public Vector2 previewDir = new Vector2(0, 0);
            public Vector2 lightDir = new Vector2(0, 0);
            public Vector3 pivotPositionOffset = Vector3.zero;
            public float zoomFactor = 1.0f;
            public int checkerTextureMultiplier = 10;

            public Material shadedPreviewMaterial;
            public Material activeMaterial;
            public Material meshMultiPreviewMaterial;
            public Material wireMaterial;
            public Material lineMaterial;
            public Texture2D checkeredTexture;
            
            public bool[] availableDisplayModes = Enumerable.Repeat(true, 6).ToArray();
            public bool[] availableUVChannels = Enumerable.Repeat(true, 8).ToArray();
            public bool[] availableHandleModes = Enumerable.Repeat(true, 4).ToArray();

            public Settings()
            {
                shadedPreviewMaterial = new Material(Shader.Find("Standard"));
                wireMaterial = CreateWireframeMaterial();
                meshMultiPreviewMaterial = CreateMeshMultiPreviewMaterial();
                lineMaterial = CreateLineMaterial();
                checkeredTexture = EditorGUIUtility.LoadRequired("Previews/Textures/textureChecker.png") as Texture2D;
                activeMaterial = shadedPreviewMaterial;

                orthoPosition = new Vector3(0.5f, 0.5f, -1);
                previewDir = new Vector2(130, 0);
                lightDir = new Vector2(-40, -40);
                zoomFactor = 1.0f;
            }

            public void Dispose()
            {
                if (shadedPreviewMaterial != null)
                    UObject.DestroyImmediate(shadedPreviewMaterial);
                if (wireMaterial != null)
                    UObject.DestroyImmediate(wireMaterial);
                if (meshMultiPreviewMaterial != null)
                    UObject.DestroyImmediate(meshMultiPreviewMaterial);
                if (lineMaterial != null)
                    UObject.DestroyImmediate(lineMaterial);
            }
        }
        
        public static string[] m_DisplayModes = { "Shaded", "UV Checker", "UV Layout", "Vertex Color", "Normals", "Tangents" };
        public static string[] m_UVChannels = { "UV0", "UV1", "UV2", "UV3", "UV4", "UV5", "UV6", "UV7" };
        public static string[] m_HandleModes = { "Disabled", "Normals", "Tangents", "Binormals" };
        public enum HandleMode { Disabled = 0, Normals = 1, Tangents = 2, Binormals = 3 };
        public enum DisplayMode { Shaded = 0, UVChecker = 1, UVLayout = 2, VertexColor = 3, Normals = 4, Tangent = 5 }
        
        protected Mesh _mesh = default;
        protected PreviewRenderUtility _preview = default;
        protected Settings _settings;
        
        public MeshView(Mesh mesh)
        {
            _mesh = mesh;

            _preview = new PreviewRenderUtility();
            _preview.camera.fieldOfView = 30.0f;
            _preview.camera.transform.position = new Vector3(5, 5, 0);
            
            _settings = new Settings();
            CheckAvailableAttributes();
        }
        
        public void Dispose()
        {
            _preview.Cleanup();
            _settings.Dispose();
        }
        
        private static Material CreateWireframeMaterial()
        {
            var shader = Shader.Find("Hidden/MeshExtension/Internal-Colored");
            if (!shader)
            {
                Debug.LogWarning("Could not find the built-in Colored shader");
                return null;
            }
            var mat = new Material(shader);
            mat.hideFlags = HideFlags.HideAndDontSave;
            mat.SetColor("_Color", new Color(0, 0, 0, 0.3f));
            mat.SetFloat("_ZWrite", 0.0f);
            mat.SetFloat("_ZBias", -1.0f);
            return mat;
        }

        private static Material CreateMeshMultiPreviewMaterial()
        {
            var shader = Shader.Find("Hidden/MeshExtension/Mesh-MultiPreview");
            if (!shader)
            {
                Debug.LogWarning("Could not find the built in Mesh preview shader");
                return null;
            }
            var mat = new Material(shader);
            mat.hideFlags = HideFlags.HideAndDontSave;
            return mat;
        }

        private static Material CreateLineMaterial()
        {
            Shader shader = Shader.Find("Hidden/MeshExtension/Internal-Colored");
            if (!shader)
            {
                Debug.LogWarning("Could not find the built-in Colored shader");
                return null;
            }
            var mat = new Material(shader);
            mat.hideFlags = HideFlags.HideAndDontSave;
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_Cull", (float)CullMode.Off);
            mat.SetFloat("_ZWrite", 0.0f);
            return mat;
        }
        
        private void ResetView()
        {
            _settings.zoomFactor = 1.0f;
            _settings.orthoPosition = new Vector3(0.5f, 0.5f, -1);
            _settings.pivotPositionOffset = Vector3.zero;

            _settings.activeUVChannel = 0;

            _settings.meshMultiPreviewMaterial.SetFloat("_UVChannel", (float)_settings.activeUVChannel);
            _settings.meshMultiPreviewMaterial.SetTexture("_MainTex", null);
        }
        
        private void FrameObject()
        {
            _settings.zoomFactor = 1.0f;
            _settings.orthoPosition = new Vector3(0.5f, 0.5f, -1);
            _settings.pivotPositionOffset = Vector3.zero;
        }
        
        private void CheckAvailableAttributes()
        {
            if (!_mesh.HasVertexAttribute(VertexAttribute.Color))
                _settings.availableDisplayModes[(int)DisplayMode.VertexColor] = false;
            if (!_mesh.HasVertexAttribute(VertexAttribute.Normal))
                _settings.availableDisplayModes[(int)DisplayMode.Normals] = false;
            if (!_mesh.HasVertexAttribute(VertexAttribute.Tangent))
                _settings.availableDisplayModes[(int)DisplayMode.Tangent] = false;

            int index = 0;
            for (int i = 4; i < 12; i++)
            {
                if (!_mesh.HasVertexAttribute((VertexAttribute)i))
                    _settings.availableUVChannels[index] = false;
                index++;
            }
        }
        
        private void DoPopup(Rect popupRect, string[] elements, int selectedIndex, GenericMenu.MenuFunction2 func, bool[] disabledItems)
        {
            GenericMenu menu = new GenericMenu();
            for (int i = 0; i < elements.Length; i++)
            {
                var element = elements[i];
                if (Selection.count > 1)
                    continue;

                if (disabledItems == null || disabledItems[i])
                    menu.AddItem(new GUIContent(element), i == selectedIndex, func, i);
                else
                    menu.AddDisabledItem(new GUIContent(element));
            }
            menu.DropDown(popupRect);
        }
        
        private void SetUVChannel(object data)
        {
            int popupIndex = (int)data;
            if (popupIndex < 0 || popupIndex >= _settings.availableUVChannels.Length)
                return;

            _settings.activeUVChannel = popupIndex;

            if (_settings.displayMode == DisplayMode.UVLayout || _settings.displayMode == DisplayMode.UVChecker)
                _settings.activeMaterial.SetFloat("_UVChannel", (float)popupIndex);
        }
        
        private void SetDisplayMode(object data)
        {
            int popupIndex = (int)data;
            if (popupIndex < 0 || popupIndex >= m_DisplayModes.Length)
                return;

            _settings.displayMode = (DisplayMode)popupIndex;

            switch (_settings.displayMode)
            {
                case DisplayMode.Shaded:
                    OnDropDownAction(_settings.shadedPreviewMaterial, 0, false);
                    break;
                case DisplayMode.UVChecker:
                    OnDropDownAction(_settings.meshMultiPreviewMaterial, 4, false);
                    _settings.meshMultiPreviewMaterial.SetTexture("_MainTex", _settings.checkeredTexture);
                    _settings.meshMultiPreviewMaterial.mainTextureScale = new Vector2(_settings.checkerTextureMultiplier, _settings.checkerTextureMultiplier);
                    break;
                case DisplayMode.UVLayout:
                    OnDropDownAction(_settings.meshMultiPreviewMaterial, 0, true);
                    break;
                case DisplayMode.VertexColor:
                    OnDropDownAction(_settings.meshMultiPreviewMaterial, 1, false);
                    break;
                case DisplayMode.Normals:
                    OnDropDownAction(_settings.meshMultiPreviewMaterial, 2, false);
                    break;
                case DisplayMode.Tangent:
                    OnDropDownAction(_settings.meshMultiPreviewMaterial, 3, false);
                    break;
            }
        }
        
        private void SetHandleMode(object data)
        {
            int popupIndex = (int)data;
            if (popupIndex < 0 || popupIndex >= m_HandleModes.Length)
                return;

            _settings.HandleMode = (HandleMode)popupIndex;
        }
        
        public static void RenderMeshPreview(Mesh mesh, PreviewRenderUtility preview, Settings settings, int meshSubset)
        {
            if (mesh == null || preview == null)
                return;

            Bounds bounds = mesh.bounds;

            Transform renderCamTransform = preview.camera.GetComponent<Transform>();
            preview.camera.nearClipPlane = 0.0001f;
            preview.camera.farClipPlane = 1000f;

            if (settings.displayMode == DisplayMode.UVLayout)
            {
                preview.camera.orthographic = true;
                preview.camera.orthographicSize = settings.zoomFactor;
                renderCamTransform.position = settings.orthoPosition;
                renderCamTransform.rotation = Quaternion.identity;
                DrawUVLayout(mesh, preview, settings);
                return;
            }

            float halfSize = bounds.extents.magnitude;
            float distance = 4.0f * halfSize;

            preview.camera.orthographic = false;
            Quaternion camRotation = Quaternion.identity;
            Vector3 camPosition = camRotation * Vector3.forward * (-distance * settings.zoomFactor) + settings.pivotPositionOffset;

            renderCamTransform.position = camPosition;
            renderCamTransform.rotation = camRotation;

            preview.lights[0].intensity = 1.1f;
            preview.lights[0].transform.rotation = Quaternion.Euler(-settings.lightDir.y, -settings.lightDir.x, 0);
            preview.lights[1].intensity = 1.1f;
            preview.lights[1].transform.rotation = Quaternion.Euler(settings.lightDir.y, settings.lightDir.x, 0);

            preview.ambientColor = new Color(.1f, .1f, .1f, 0);

            RenderMeshPreviewSkipCameraAndLighting(mesh, bounds, preview, settings, null, meshSubset);
        }
        
        private static void DrawUVLayout(Mesh mesh, PreviewRenderUtility preview, Settings settings)
        {
            GL.PushMatrix();
            settings.lineMaterial.SetPass(0);

            GL.LoadProjectionMatrix(preview.camera.projectionMatrix);
            GL.MultMatrix(preview.camera.worldToCameraMatrix);

            GL.Begin(GL.LINES);
            const float step = 0.125f;
            for (var g = -2.0f; g <= 3.0f; g += step)
            {
                var majorLine = Mathf.Abs(g - Mathf.Round(g)) < 0.01f;
                if (majorLine)
                {
                    GL.Color(new Color(0.6f, 0.6f, 0.7f, 1.0f));
                    GL.Vertex3(-2, g, 0);
                    GL.Vertex3(+3, g, 0);
                    GL.Vertex3(g, -2, 0);
                    GL.Vertex3(g, +3, 0);
                }
                else if (g >= 0 && g <= 1)
                {
                    GL.Color(new Color(0.6f, 0.6f, 0.7f, 0.5f));
                    GL.Vertex3(0, g, 0);
                    GL.Vertex3(1, g, 0);
                    GL.Vertex3(g, 0, 0);
                    GL.Vertex3(g, 1, 0);
                }
            }
            GL.End();
            
            GL.LoadIdentity();
            settings.meshMultiPreviewMaterial.SetPass(0);
            GL.wireframe = true;
            Graphics.DrawMeshNow(mesh, preview.camera.worldToCameraMatrix);
            GL.wireframe = false;

            GL.PopMatrix();
        }
        
        public static Color GetSubMeshTint(int index)
        {
            // color palette generator based on "golden ratio" idea, like in
            // https://martin.ankerl.com/2009/12/09/how-to-create-random-colors-programmatically/
            var hue = Mathf.Repeat(index * 0.618f, 1);
            var sat = index == 0 ? 0f : 0.3f;
            var val = 1f;
            return Color.HSVToRGB(hue, sat, val);
        }
        
        public static void RenderMeshPreviewSkipCameraAndLighting(Mesh mesh, Bounds bounds, PreviewRenderUtility preview, Settings settings, MaterialPropertyBlock customProperties, int meshSubset)
        {
            if (mesh == null || preview == null)
                return;

            Quaternion rot = Quaternion.Euler(settings.previewDir.y, 0, 0) * Quaternion.Euler(0, settings.previewDir.x, 0);
            Vector3 pos = rot * (-bounds.center);

            bool oldFog = RenderSettings.fog;
            Unsupported.SetRenderSettingsUseFogNoDirty(false);

            int submeshes = mesh.subMeshCount;
            var tintSubmeshes = false;
            var colorPropID = 0;
            if (submeshes > 1 && settings.displayMode == DisplayMode.Shaded && customProperties == null && meshSubset == -1)
            {
                tintSubmeshes = true;
                customProperties = new MaterialPropertyBlock();
                colorPropID = Shader.PropertyToID("_Color");
            }

            if (settings.activeMaterial != null)
            {
                preview.camera.clearFlags = CameraClearFlags.Nothing;
                if (meshSubset < 0 || meshSubset >= submeshes)
                {
                    for (int i = 0; i < submeshes; ++i)
                    {
                        if (tintSubmeshes)
                            customProperties.SetColor(colorPropID, GetSubMeshTint(i));
                        preview.DrawMesh(mesh, pos, rot, settings.activeMaterial, i, customProperties);
                    }
                }
                else
                    preview.DrawMesh(mesh, pos, rot, settings.activeMaterial, meshSubset, customProperties);
                preview.Render();
            }

            if (settings.wireMaterial != null && settings.DrawWire)
            {
                preview.camera.clearFlags = CameraClearFlags.Nothing;
                GL.wireframe = true;
                if (tintSubmeshes)
                    customProperties.SetColor(colorPropID, settings.wireMaterial.color);
                if (meshSubset < 0 || meshSubset >= submeshes)
                {
                    for (int i = 0; i < submeshes; ++i)
                    {
                        var topology = mesh.GetTopology(i);
                        if (topology == MeshTopology.Lines || topology == MeshTopology.LineStrip || topology == MeshTopology.Points)
                            continue;
                        preview.DrawMesh(mesh, pos, rot, settings.wireMaterial, i, customProperties);
                    }
                }
                else
                    preview.DrawMesh(mesh, pos, rot, settings.wireMaterial, meshSubset, customProperties);
                preview.Render();

                GL.wireframe = false;
            }

            Unsupported.SetRenderSettingsUseFogNoDirty(oldFog);

            DrawHandles(mesh, preview, settings, pos, rot);
        }

        private static void DrawHandles(Mesh mesh, PreviewRenderUtility preview, Settings settings, Vector3 pos, Quaternion rot)
        {
            Handles.SetCamera(preview.camera);
            Handles.zTest = CompareFunction.LessEqual;

            float scale = settings.HandleScale * mesh.bounds.size.magnitude;

            if (MeshInfoWindow.Selected != null)
            {
                Handles.color = Color.red;
                foreach (int i in MeshInfoWindow.Selected)
                {
                    Vector3 projPos = pos + rot * mesh.vertices[i];
                    Vector3 viewVec = pos + rot * (mesh.vertices[i] + mesh.normals[i] * scale);
                    Handles.DrawSolidDisc(projPos, viewVec, 0.025f * scale);
                }
            }

            switch (settings.HandleMode)
            {
                case HandleMode.Normals:
                    Handles.color = Color.green;
                    for (int i = 0; i < mesh.vertexCount; i++)
                    {
                        Vector3 projPos = pos + rot * mesh.vertices[i];
                        Quaternion viewRot = Quaternion.LookRotation(rot * mesh.normals[i] * scale);
                        
                        Handles.ArrowHandleCap(i, projPos, viewRot, mesh.normals[i].magnitude * 0.1f * scale, EventType.Repaint);
                    }
                    break;
                case HandleMode.Tangents:
                    Handles.color = Color.magenta;
                    for (int i = 0; i < mesh.vertexCount; i++)
                    {
                        Vector3 projPos = pos + rot * mesh.vertices[i];
                        Quaternion viewRot = Quaternion.LookRotation(rot * mesh.tangents[i] * scale);
                        
                        Handles.ArrowHandleCap(i, projPos, viewRot, mesh.normals[i].magnitude * 0.1f * scale, EventType.Repaint);
                    }
                    break;
                case HandleMode.Binormals:
                    Handles.color = Color.blue;
                    for (int i = 0; i < mesh.vertexCount; i++)
                    {
                        Vector3 bin = Vector3.Cross(mesh.normals[i], mesh.tangents[i]);
                        Vector3 projPos = pos + rot * mesh.vertices[i];
                        Quaternion viewRot = Quaternion.LookRotation(rot * bin);
                        
                        Handles.ArrowHandleCap(i, projPos, viewRot, mesh.normals[i].magnitude * 0.1f * scale, EventType.Repaint);
                    }
                    break;
            }
        }

        private static Vector3 SetVectorMatrix(Matrix4x4 mat)
        {
            var pos = new Vector3(mat.m03, mat.m13, mat.m23);
            var scale = mat.lossyScale;
            
            var invScale = new Vector3(1.0f / scale.x, 1.0f / scale.y, 1.0f / scale.z);
            mat.m00 *= invScale.x; mat.m10 *= invScale.x; mat.m20 *= invScale.x;
            mat.m01 *= invScale.y; mat.m11 *= invScale.y; mat.m21 *= invScale.y;
            mat.m02 *= invScale.z; mat.m12 *= invScale.z; mat.m22 *= invScale.z;
            
            var rot = mat.rotation;
            return pos;
        }
        
        public Texture2D RenderStaticPreview(int width, int height)
        {
            if (!ShaderUtil.hardwareSupportsRectRenderTexture)
                return null;
            _preview.BeginStaticPreview(new Rect(0, 0, width, height));
            RenderMeshPreview(_mesh, _preview, _settings, -1);
            return _preview.EndStaticPreview();
        }
        
        public void OnPreviewGUI(Rect rect, GUIStyle background)
        {
            var evt = Event.current;

            if (!ShaderUtil.hardwareSupportsRectRenderTexture)
            {
                if (evt.type == EventType.Repaint)
                    EditorGUI.DropShadowLabel(new Rect(rect.x, rect.y, rect.width, 40),
                        "Mesh preview requires\nrender texture support");
                return;
            }

            if ((evt.type == EventType.ValidateCommand || evt.type == EventType.ExecuteCommand) && evt.commandName == "FrameSelected")
            {
                FrameObject();
                evt.Use();
            }

            if (evt.button <= 0 && _settings.displayMode != DisplayMode.UVLayout)
                _settings.previewDir = Drag2D(_settings.previewDir, rect);

            if (evt.button == 1 && _settings.displayMode != DisplayMode.UVLayout)
                _settings.lightDir = Drag2D(_settings.lightDir, rect);

            if (evt.type == EventType.ScrollWheel)
                MeshPreviewZoom(rect, evt);

            if (evt.type == EventType.MouseDrag && (_settings.displayMode == DisplayMode.UVLayout || evt.button == 2))
                MeshPreviewPan(rect, evt);

            if (evt.type != EventType.Repaint)
                return;

            _preview.BeginPreview(rect, background);
            RenderMeshPreview(_mesh, _preview, _settings, -1);
            _preview.EndAndDrawPreview(rect);
        }
        
        private Vector2 Drag2D(Vector2 scrollPosition, Rect position)
        {
            int controlId = GUIUtility.GetControlID("Slider".GetHashCode(), FocusType.Passive);
            Event current = Event.current;
            switch (current.GetTypeForControl(controlId))
            {
                case EventType.MouseDown:
                    if (position.Contains(current.mousePosition) && (double) position.width > 50.0)
                    {
                        GUIUtility.hotControl = controlId;
                        current.Use();
                        EditorGUIUtility.SetWantsMouseJumping(1);
                    }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlId)
                        GUIUtility.hotControl = 0;
                    EditorGUIUtility.SetWantsMouseJumping(0);
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == controlId)
                    {
                        scrollPosition -= current.delta * (current.shift ? 3f : 1f) / Mathf.Min(position.width, position.height) * 140f;
                        current.Use();
                        GUI.changed = true;
                    }
                    break;
            }
            return scrollPosition;
        }
        
        public void OnPreviewSettings(Rect r)
        {
            if (!ShaderUtil.hardwareSupportsRectRenderTexture)
                return;

            GUI.Box(r, "", EditorStyles.inspectorDefaultMargins);
            
            GUI.enabled = true;
            EditorGUILayout.BeginHorizontal();

            // calculate width based on the longest value in display modes
            float displayModeDropDownWidth = EditorStyles.toolbarDropDown.CalcSize(new GUIContent(m_DisplayModes[(int)DisplayMode.VertexColor])).x;
            Rect displayModeDropdownRect = EditorGUILayout.GetControlRect(GUILayout.Width(displayModeDropDownWidth));
            displayModeDropdownRect.y = r.y + 1;
            displayModeDropdownRect.x += 0; //2
            GUIContent displayModeDropdownContent = new GUIContent(m_DisplayModes[(int)_settings.displayMode], Styles.displayModeDropdown.tooltip);

            if (EditorGUI.DropdownButton(displayModeDropdownRect, displayModeDropdownContent, FocusType.Passive, EditorStyles.toolbarDropDown))
                DoPopup(displayModeDropdownRect, m_DisplayModes, (int)_settings.displayMode, SetDisplayMode, _settings.availableDisplayModes);
            
            if (_settings.displayMode == DisplayMode.UVLayout || _settings.displayMode == DisplayMode.UVChecker)
            {
                float channelDropDownWidth = EditorStyles.toolbarDropDown.CalcSize(new GUIContent("Channel 6")).x;
                Rect channelDropdownRect = EditorGUILayout.GetControlRect(GUILayout.Width(channelDropDownWidth));
                channelDropdownRect.y = r.y + 1;
                channelDropdownRect.x += 5;
                GUIContent channel = new GUIContent("UV " + _settings.activeUVChannel, Styles.uvChannelDropdown.tooltip);

                if (EditorGUI.DropdownButton(channelDropdownRect, channel, FocusType.Passive, EditorStyles.toolbarDropDown))
                    DoPopup(channelDropdownRect, m_UVChannels,
                        _settings.activeUVChannel, SetUVChannel, _settings.availableUVChannels);
            }

            if (_settings.displayMode == DisplayMode.UVChecker)
            {
                int oldVal = _settings.checkerTextureMultiplier;

                float sliderWidth = EditorStyles.label.CalcSize(new GUIContent("--------")).x;
                Rect sliderRect = EditorGUILayout.GetControlRect(GUILayout.Width(sliderWidth));
                sliderRect.y = r.y;
                sliderRect.x += 6;

                _settings.checkerTextureMultiplier = (int)GUI.HorizontalSlider(sliderRect, _settings.checkerTextureMultiplier, 30, 1, Styles.preSlider, Styles.preSliderThumb);
                if (oldVal != _settings.checkerTextureMultiplier)
                    _settings.activeMaterial.mainTextureScale = new Vector2(_settings.checkerTextureMultiplier, _settings.checkerTextureMultiplier);
            }

            using (new EditorGUI.DisabledScope(_settings.displayMode == DisplayMode.UVLayout))
            {
                float wireWidth = EditorStyles.toolbarDropDown.CalcSize(Styles.wireframeToggle).x;
                Rect wireRect = EditorGUILayout.GetControlRect(GUILayout.Width(wireWidth));
                wireRect.y = r.y;
                wireRect.x = r.width - wireRect.width - 0; //2
                _settings.DrawWire = GUI.Toggle(wireRect, _settings.DrawWire, Styles.wireframeToggle, EditorStyles.toolbarButton);
                
                float handleWidth = EditorStyles.toolbarDropDown.CalcSize(new GUIContent(m_DisplayModes[(int)HandleMode.Binormals])).x;
                Rect handleRect = EditorGUILayout.GetControlRect(GUILayout.Width(handleWidth));
                handleRect.y = r.y;
                handleRect.x = r.width - (wireRect.width + handleRect.width); //2
                GUIContent handleContent = new GUIContent(m_HandleModes[(int)_settings.HandleMode]);

                if (EditorGUI.DropdownButton(handleRect, handleContent, FocusType.Passive, EditorStyles.toolbarDropDown))
                    DoPopup(handleRect, m_HandleModes, (int)_settings.HandleMode, SetHandleMode, _settings.availableHandleModes);
                
                float scaleWidth = EditorStyles.label.CalcSize(new GUIContent("--------")).x;
                Rect scaleRect = EditorGUILayout.GetControlRect(GUILayout.Width(scaleWidth));
                scaleRect.y = r.y;
                scaleRect.x = r.width - (wireRect.width + handleRect.width + scaleRect.width + 6);

                _settings.HandleScale = GUI.HorizontalSlider(scaleRect, _settings.HandleScale, 0.01f, 1.0f, Styles.preSlider, Styles.preSliderThumb);
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        private void OnDropDownAction(Material mat, int mode, bool flatUVs)
        {
            ResetView();

            _settings.activeMaterial = mat;
            _settings.activeMaterial.SetFloat("_Mode", (float)mode);
            _settings.activeMaterial.SetFloat("_UVChannel", 0.0f);
            _settings.activeMaterial.SetFloat("_Cull", flatUVs ? (float)CullMode.Off : (float)CullMode.Back);
        }
        
        private void MeshPreviewZoom(Rect rect, Event evt)
        {
            float zoomDelta = -(HandleUtility.niceMouseDeltaZoom * 0.5f) * 0.05f;
            var newZoom = _settings.zoomFactor + _settings.zoomFactor * zoomDelta;
            newZoom = Mathf.Clamp(newZoom, 0.1f, 10.0f);

            // we want to zoom around current mouse position
            var mouseViewPos = new Vector2(
                evt.mousePosition.x / rect.width,
                1 - evt.mousePosition.y / rect.height);
            var mouseWorldPos = _preview.camera.ViewportToWorldPoint(mouseViewPos);
            var mouseToCamPos = _settings.orthoPosition - mouseWorldPos;
            var newCamPos = mouseWorldPos + mouseToCamPos * (newZoom / _settings.zoomFactor);

            if (_settings.displayMode != DisplayMode.UVLayout)
            {
                _preview.camera.transform.position = new Vector3(newCamPos.x, newCamPos.y, newCamPos.z);
            }
            else
            {
                _settings.orthoPosition.x = newCamPos.x;
                _settings.orthoPosition.y = newCamPos.y;
            }

            _settings.zoomFactor = newZoom;
            evt.Use();
        }
        
        private void MeshPreviewPan(Rect rect, Event evt)
        {
            var cam = _preview.camera;
            
            var delta = new Vector3(-evt.delta.x * cam.pixelWidth / rect.width, evt.delta.y * cam.pixelHeight / rect.height, 0);

            Vector3 screenPos;
            Vector3 worldPos;
            if (_settings.displayMode == DisplayMode.UVLayout)
            {
                screenPos = cam.WorldToScreenPoint(_settings.orthoPosition);
                screenPos += delta;
                worldPos = cam.ScreenToWorldPoint(screenPos);
                _settings.orthoPosition.x = worldPos.x;
                _settings.orthoPosition.y = worldPos.y;
            }
            else
            {
                screenPos = cam.WorldToScreenPoint(_settings.pivotPositionOffset);
                screenPos += delta;
                worldPos = cam.ScreenToWorldPoint(screenPos) - _settings.pivotPositionOffset;
                _settings.pivotPositionOffset += worldPos;
            }

            evt.Use();
        }
    }
}